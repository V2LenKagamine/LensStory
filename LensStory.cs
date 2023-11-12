using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace LensstoryMod {
    public class LensStoryMod : ModSystem
    {
        private readonly HashSet<KeyValuePair<ManaNetwork,int>> mananetworks = new();
        private readonly List<ManaConsumer> consumers = new();
        private readonly Dictionary<BlockPos,ManaPart> ManaParts = new();

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            api.RegisterItemClass("fluidskimmerclass", typeof(FluidSkimmerItem));
            api.RegisterItemClass("attunementclass",typeof(AttunementWandItem));

            api.RegisterBlockClass("lenscreativemanablock", typeof(CreativeMana));
            api.RegisterBlockEntityClass("lenscreativemana", typeof(CreativeManaBE));
            api.RegisterBlockEntityBehaviorClass("lenscreativemanabehavior",typeof(CreativeManaBhv));

            api.RegisterBlockClass("lensautopannerblock", typeof(AutoPannerBlock));
            api.RegisterBlockEntityClass("lensautopanner",typeof(AutoPannerBE));
            api.RegisterBlockEntityBehaviorClass("lensautopannerbehavior",typeof(AutoPannerBhv));

            api.RegisterBlockEntityBehaviorClass("Mana", typeof(Mana));

            api.Event.RegisterGameTickListener(this.OnGameTick, 1000);
        }

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
                this.consumers.Clear();

                var totalMana = network.Key.Makers.Sum(maker=> maker.MakeMana());

                var neededMana = 0;

                foreach (var consumer  in network.Key.Consumers.Select(theconsumer => new ManaConsumer(theconsumer)))
                {
                    neededMana += consumer.ManaNeeded;
                    this.consumers.Add(consumer);
                }
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

        public class ManaNetwork
        {
            public readonly HashSet<IManaMaker> Makers = new();
            public readonly HashSet<IManaConsumer> Consumers = new();
            public readonly HashSet<BlockPos> Positions = new();

            public int ManaID;
            public int Consumed { get => this.Consumers.Sum(consumer => consumer.ToVoid); }
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
    public class FluidSkimmerItem : Item
    {
        public override float OnBlockBreaking(IPlayer player, BlockSelection blockSel, ItemSlot itemslot, float remainingResistance, float dt, int counter)
        {
            if (blockSel.Block is BlockBarrel && api.Side == EnumAppSide.Server)
            {
                BlockEntity BarrelEntMaybe = api.World.BlockAccessor.GetBlockEntity(blockSel.Position);
                if (BarrelEntMaybe != null && BarrelEntMaybe is BlockEntityLiquidContainer BarrelEnt)
                {
                    ItemSlot theFluid = BarrelEnt.Inventory[1];
                    theFluid.TakeOut(theFluid.StackSize % 100);
                    BarrelEnt.Inventory.MarkSlotDirty(1);
                }
            }
            return base.OnBlockBreaking(player, blockSel, itemslot, remainingResistance, dt, counter);
        }
    }

    public class AttunementWandItem : Item
    {
        
        public override float OnBlockBreaking(IPlayer player, BlockSelection blockSel, ItemSlot itemslot, float remainingResistance, float dt, int counter)
        {
            if (api.Side == EnumAppSide.Server)
            {
                BlockEntity mayhapMana = api.World.BlockAccessor.GetBlockEntity(blockSel.Position);
                if (mayhapMana != null && mayhapMana.GetBehavior<Mana>() is { } behavior)
                {
                    behavior.ManaID = itemslot.StackSize; //Todo: selector for this number
                    behavior.begin(true); //Force add Connection?
                }
            }
            return base.OnBlockBreaking(player, blockSel, itemslot, remainingResistance, dt, counter);
        }
    }
}
