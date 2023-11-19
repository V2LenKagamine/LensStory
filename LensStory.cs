using System;
using System.Collections.Generic;
using System.Linq;
using ProtoBuf;
using Vintagestory;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace LensstoryMod {

    /* Quick reference to all attributes that change the characters Stats:
   healingeffectivness, maxhealthExtraPoints, walkSpeed, hungerrate, rangedWeaponsAcc, rangedWeaponsSpeed
   rangedWeaponsDamage, meleeWeaponsDamage, mechanicalsDamage, animalLootDropRate, forageDropRate, wildCropDropRate
   vesselContentsDropRate, oreDropRate, rustyGearDropRate, miningSpeedMul, animalSeekingRange, armorDurabilityLoss,
   bowDrawingStrength, wholeVesselLootChance, temporalGearTLRepairCost, animalHarvestingTime*/
    //A reminder that "mana" is now "Mechanus Potentia"
    public class LensStoryMod : ModSystem
    {
        private readonly HashSet<KeyValuePair<ManaNetwork,int>> mananetworks = new();
        private readonly List<ManaConsumer> consumers = new();
        private readonly Dictionary<BlockPos,ManaPart> ManaParts = new();

        public IServerNetworkChannel servermanachannel;
        public IClientNetworkChannel clientmanachannel;
        ICoreClientAPI storedcapi;
        ICoreServerAPI storedsapi;
        public AttunementWandGui manathing;

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            api.RegisterItemClass("fluidskimmerclass", typeof(FluidSkimmerItem));
            api.RegisterItemClass("attunementclass",typeof(AttunementWandItem));
            api.RegisterItemClass("scrollitem",typeof(ScrollItem));

            api.RegisterBlockClass("frameblock", typeof(WoodFrame));
            api.RegisterBlockClass("liquidconcreteblock", typeof(LiquidConcreteBlock));

            api.RegisterItemClass("lenssimpleore",typeof(SimpleItemOre));
            api.RegisterItemClass("lenssimplenugget", typeof(SimpleItemNugget));
            api.RegisterItemClass("lenssimplebloom", typeof(GenericOreBloom));

            api.RegisterBlockClass("lenssturdybucketblock", typeof(SturdyBucketBlock));
            api.RegisterBlockEntityClass("lenssturdybucket", typeof(SturdyBucketBE));
            api.RegisterBlockClass("lenssturdybucketfilledblock", typeof(SturdyBucketFilledBlock));
            api.RegisterBlockEntityClass("lenssturdybucketfilled", typeof(SturdyBucketFilledBE));

            api.RegisterBlockClass("lensreinforcedbloomeryblock", typeof(ReinforcedBloomery));
            api.RegisterBlockEntityClass("lensreinforcedbloomery", typeof(ReinforcedBloomeryBE));

            RegisterTrio(api,"creativemana",typeof(CreativeMana),typeof(CreativeManaBE),typeof(CreativeManaBhv));

            RegisterTrio(api, "furnacegen", typeof(Burnerator), typeof(BurneratorBE), typeof(BurneratorBhv));

            RegisterTrio(api, "autopanner", typeof(AutoPannerBlock), typeof(AutoPannerBE), typeof(AutoPannerBhv));

            RegisterTrio(api, "manarepair", typeof(ManaRepairBlock), typeof(ManaRepairBE), typeof(ManaRepairBhv));

            RegisterTrio(api, "rockmaker", typeof(RockmakerBlock), typeof(RockmakerBE), typeof(RockmakerBhv));

            api.RegisterBlockEntityBehaviorClass("Mana", typeof(Mana));

            api.Event.RegisterGameTickListener(this.OnGameTick, 1000);
        }

        private static void RegisterTrio(ICoreAPI api,string basename,Type block,Type blockEntity,Type entBehavior)
        {
            api.RegisterBlockClass("lens" + basename + "block", block);
            api.RegisterBlockEntityClass("lens" + basename, blockEntity);
            api.RegisterBlockEntityBehaviorClass("lens" + basename + "behavior", entBehavior);
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            api.Event.PlayerNowPlaying += (IServerPlayer player) =>
            {
                if(player.Entity is EntityPlayer)
                {
                    Entity e = player.Entity;
                    e.AddBehavior(new ScrollStuffBhv(e));
                }
            };

            storedsapi = api;

            servermanachannel = api.Network.RegisterChannel("manamessages")
                .RegisterMessageType(typeof(ManaWandMessage))
                .RegisterMessageType(typeof(ManaWandResponce))
                .SetMessageHandler<ManaWandMessage>(OnManaMessageS);
        }
        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);

            storedcapi = api;

            clientmanachannel = api.Network.RegisterChannel("manamessages")
                .RegisterMessageType(typeof(ManaWandMessage))
                .RegisterMessageType(typeof(ManaWandResponce))
                .SetMessageHandler<ManaWandResponce>(OnManaMessageC);

            manathing = new AttunementWandGui(api);
        }


        #region NetworkingBullshit

        private void OnManaMessageS(IPlayer from, ManaWandMessage packet) 
        {
            if (from.InventoryManager.ActiveHotbarSlot.Itemstack.Item.Code == AssetLocation.Create("lensstory:attunementwand"))
            {
                from.InventoryManager.ActiveHotbarSlot.Itemstack.Attributes.SetInt("channel", packet.message);
                from.InventoryManager.ActiveHotbarSlot.MarkDirty();
            }
        }

        private void OnManaMessageC(ManaWandResponce packet)
        {
            storedcapi.ShowChatMessage("You attune the wand to channel " + packet.message);
        }

        [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
        public class ManaWandMessage
        {
            public int message;
        }
        [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
        public class ManaWandResponce
        {
            public int message;
        }

        #endregion

        //Mana stuff start
        #region Manastuff
        public bool DoUpdate(BlockPos pos,int code,bool forced = false)
        {
            
            if(!this.ManaParts.TryGetValue(pos,out var part)) {
                if (code == 0) return false;
                part = this.ManaParts[pos] = new ManaPart(pos);
            }
            if (code == part.ManaCode && !forced) { return false; }

            part.ManaCode = code;

            this.AddManaConnection(ref part,code);
            
            if (part.ManaCode == 0)
            {
                this.ManaParts.Remove(pos);
            }

            return true;
        }
        private void OnGameTick(float _)
        {
            foreach (var network in mananetworks)
            {
                consumers.Clear();

                var totalMana = network.Key.Makers.Sum(maker=> maker.MakeMana());

                var neededMana = 0;

                foreach (var consumer in network.Key.Consumers.Select(theconsumer => new ManaConsumer(theconsumer)))
                {
                    neededMana += consumer.ManaNeeded;
                    consumers.Add(consumer);
                }
                if (totalMana >= 1)
                {
                    do
                    {
                        foreach (var customer in this.consumers)
                        {
                            customer.ManaEater.EatMana(customer.ManaNeeded);
                            neededMana -= customer.ManaNeeded;
                        }
                    }
                    while (totalMana >= neededMana && neededMana > 0);
                }
            }
        }

        public class ManaNetwork
        {
            public readonly HashSet<IManaMaker> Makers = new();
            public readonly HashSet<IManaConsumer> Consumers = new();
            public readonly HashSet<BlockPos> Positions = new();

            public int ManaID;
            public int Consumed { get => this.Consumers.Sum(consumer => consumer.ToVoid()); }
            public int Produced { get => this.Makers.Sum(producer => producer.MakeMana()); }

            public ManaNetwork(int ManaID)
            {
                this.ManaID = ManaID;
            }

        }
        public ManaNetwork CreateManaNetwork(int ManaID)
        {
            var manaboi = new ManaNetwork(ManaID);
            var compressedboi = new KeyValuePair<ManaNetwork, int>(manaboi, ManaID);
            this.mananetworks.Add(compressedboi);
            return manaboi;
        }
        public void Remove(BlockPos pos)
        {
            if(this.ManaParts.TryGetValue(pos,out var part))
            {
                this.ManaParts.Remove(pos);
                this.RemoveManaConnection(ref part, part.ManaCode);
            }
        }
        private void RemoveManaConnection(ref ManaPart part, int manaID)
        {
            if (manaID == 0)
            {
                return;
            }
        }

        public void AddManaConnection(ref ManaPart part,int manaID)
        {
            if (manaID == 0 )
            {
                return;
            }
            if (mananetworks.Count >= 1)
            {
                if (mananetworks.Where(output => output.Value == manaID).Count() >= 1) 
                {
                    part.ManaNet = mananetworks.Where(output => output.Value == manaID).First().Key;
                }
                else
                {
                    part.ManaNet = CreateManaNetwork(manaID);
                }
            }
            else
            {
                part.ManaNet = CreateManaNetwork(manaID);
            }
        }


        public void SetManaConsumer(BlockPos pos, IManaConsumer? eater)
        {
            if (!this.ManaParts.TryGetValue(pos,out var part))
            {
                if (eater == null)
                {
                    return;
                }
                part = this.ManaParts[pos] = new ManaPart(pos);
            }
            if(part.Consumer != eater)
            {
                if (part.Consumer is not null) {
                    part.ManaNet?.Consumers.Remove(part.Consumer);
                }
            }

            if (eater is not null)
            {
                part.ManaNet?.Consumers.Add(eater);
            }
            part.Consumer = eater;
        }
        public void SetManaProducer(BlockPos pos, IManaMaker? maker)
        {
            if (!this.ManaParts.TryGetValue(pos, out var part))
            {
                if (maker == null)
                {
                    return;
                }
                part = this.ManaParts[pos] = new ManaPart(pos);
            }
            if (part.Maker != maker)
            {
                if (part.Maker is not null)
                {
                    part.ManaNet?.Makers.Remove(part.Maker);
                }
            }
            if (maker is not null)
            {
                part.ManaNet?.Makers.Add(maker);
            }
            part.Maker = maker;
        }

        public ManaNetInfo GetManaNetInfo(BlockPos pos)
        {
            var result = new ManaNetInfo();

            if(this.ManaParts.TryGetValue(pos,out var part))
            {
                if(part.ManaNet is { } net)
                {
                    result.TotalBlocks = net.Positions.Count;
                    result.TotalMakers = net.Makers.Count;
                    result.TotalConsumers = net.Consumers.Count;
                    result.ManaProduced = net.Produced;
                    result.ManaConsumned = net.Consumed;
                    result.ManaID = net.ManaID;
                }
            }
            return result;
        }

        public class ManaPart
        {
            public ManaNetwork? ManaNet = null;
            public readonly BlockPos Position;
            public int ManaCode = 0;
            public IManaConsumer? Consumer;
            public IManaMaker? Maker;

            public ManaPart(BlockPos pos)
            {
                this.Position = pos;
            }
        }

        public class ManaNetInfo
        {
            public int ManaConsumned;
            public int TotalBlocks;
            public int TotalConsumers;
            public int TotalMakers;
            public int ManaProduced;
            public int ManaID;
        }

        #endregion
    }
}
