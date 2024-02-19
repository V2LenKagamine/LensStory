using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using Vintagestory.ServerMods;

namespace LensstoryMod
{
    public class RockmakerBlock : Block
    {
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is RockmakerBE entity)
            {
                return entity.OnPlayerInteract(world,byPlayer,blockSel);
            }
            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
    }
    public class RockmakerBE : BlockEntity
    {
        public ItemStack? contents { get; private set; }
        private bool Powered;
        private BlockPos[][] TowerPos;
        public bool Working
        {
            get => Powered; set
            {
                if (Powered != value)
                {
                    if (value && !Powered)
                    {
                        MarkDirty();
                    }
                    Powered = value;
                };
            }
        }
        public int oreCycles;
        private bool oremode;
        public bool OreGenMode
        {
            get
            {
                if(oremode != true) 
                {
                    oremode = Api.World.BlockAccessor.GetBlock(Pos.DownCopy()).FirstCodePart() == "mantle";
                    return oremode;
                }
                return oremode;
            }
            set
            {
                oremode = value;
            }
        }
        private float workdone;
        public double LastTickTotalHours;
        ProPickWorkSpace ppws;
        PropickReading theOreReading;
        int boostTowers;
        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            contents?.ResolveBlockOrItem(api.World);

            TowerPos = new BlockPos[][]
            {
                new BlockPos[]
                {
                    Pos.NorthCopy().West(),
                    Pos.NorthCopy().East(),
                    Pos.SouthCopy().West(),
                    Pos.SouthCopy().East()
                },
                new BlockPos[]
                {
                    Pos.UpCopy().North().West(),
                    Pos.UpCopy().North().East(),
                    Pos.UpCopy().South().West(),
                    Pos.UpCopy().South().East()
                },
                new BlockPos[]
                {
                    Pos.UpCopy(2).North().West(),
                    Pos.UpCopy(2).North().East(),
                    Pos.UpCopy(2).South().West(),
                    Pos.UpCopy(2).South().East()
                }
            };

            if (api.Side == EnumAppSide.Server)
            {
                ppws = ObjectCacheUtil.GetOrCreate(api, "propickworkspace", () =>
                {
                    ProPickWorkSpace ppws = new ProPickWorkSpace();
                    ppws.OnLoaded(api);
                    return ppws;
                });
            }

            RegisterGameTickListener(OnCommonTick, 1000);

            GetBehavior<Mana>().begin(true);
        }
        internal void OnCommonTick(float dt)
        {
            if (OreGenMode && Working && Api.World.BlockAccessor.GetBlock(Pos.UpCopy()).Id == 0)
            {
                DoOreGeneration();
            }
            else if (Working && contents != null)
            {
                if (Api.World.BlockAccessor.GetBlock(Pos.Copy().Add(0, 1, 0)).Id == 0)
                {
                    Api.World.BlockAccessor.SetBlock(contents.Id, Pos.Copy().ToVec3d().Add(0, 1, 0).AsBlockPos);
                }
            }
        }

        internal void DoOreGeneration()
        {
            workdone = (float)((Api.World.Calendar.TotalHours - LastTickTotalHours) * 100);
            if (workdone > 1)
            {
                if(oreCycles >= 4)
                {
                    boostTowers = CheckBoosts();
                    oreCycles = 0;
                }
                
                if(theOreReading == null)
                {
                    theOreReading = GetTheReading();
                }

                KeyValuePair<string,OreReading> chosen = theOreReading.OreReadings.ElementAt(Api.World.Rand.Next(0, theOreReading.OreReadings.Count));

                if(Api.World.Rand.Next(100) <= chosen.Value.PartsPerThousand * ((boostTowers / 3f) + 1f ))
                {
                    Api.World.BlockAccessor.SetBlock(Api.World.GetBlock(new AssetLocation(chosen.Key)).Id,Pos.UpCopy());
                }

                oreCycles++;
            }
            LastTickTotalHours = Api.World.Calendar.TotalHours;
        }

        internal PropickReading GetTheReading()
        {
            DepositVariant[] deposits = Api.ModLoader.GetModSystem<GenDeposits>()?.Deposits;
            if (deposits == null) return null;

            IBlockAccessor blockAccess = Api.World.BlockAccessor;
            int regsize = blockAccess.RegionSize;
            IMapRegion reg = Api.World.BlockAccessor.GetMapRegion(Pos.X / regsize, Pos.Z / regsize);
            int lx = Pos.X % regsize;
            int lz = Pos.Z % regsize;

            Pos = Pos.Copy();
            Pos.Y = Api.World.BlockAccessor.GetTerrainMapheightAt(Pos);
            int[] blockColumn = ppws.GetRockColumn(Pos.X, Pos.Z);

            PropickReading readings = new PropickReading();
            readings.Position = new Vec3d(Pos.X, Pos.Y, Pos.Z);

            foreach (var val in reg.OreMaps)
            {
                IntDataMap2D map = val.Value;
                int noiseSize = map.InnerSize;

                float posXInRegionOre = (float)lx / regsize * noiseSize;
                float posZInRegionOre = (float)lz / regsize * noiseSize;

                int oreDist = map.GetUnpaddedColorLerped(posXInRegionOre, posZInRegionOre);

                double ppt;
                double totalFactor;

                if (!ppws.depositsByCode.ContainsKey(val.Key))
                {
                    continue;
                }

                ppws.depositsByCode[val.Key].GetPropickReading(Pos, oreDist, blockColumn, out ppt, out totalFactor);

                if (totalFactor > 0)
                {
                    var reading = new OreReading();
                    reading.TotalFactor = totalFactor;
                    reading.PartsPerThousand = ppt;
                    readings.OreReadings[val.Key] = reading;
                }
            }

            return readings;
        }
    

        internal int CheckBoosts()
        {
            var boost = 0;
            for (int i = 0; i < TowerPos.Length; i++)
            {
                foreach (var block in TowerPos[i])
                {
                    if (Api.World.BlockAccessor.GetBlock(block).FirstCodePart() != "metalblock") { return boost; }
                }
                boost++;
            }
            return boost;
        }
        internal bool OnPlayerInteract(IWorldAccessor world,IPlayer player,BlockSelection blocksel) 
        {
            if (player.Entity.Controls.ShiftKey)
            {

                if (contents == null) { return false; }
                var split = contents.Clone();
                split.StackSize = 1;
                contents.StackSize--;
                if (contents.StackSize <= 0)
                {
                    contents = null;
                }
                if (!player.InventoryManager.TryGiveItemstack(split))
                {
                    world.SpawnItemEntity(split, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
                }
                MarkDirty();
                return true;

            }
            var slot = player.InventoryManager.ActiveHotbarSlot;
            if (slot.Itemstack == null)
            { return false; }
            var maybeblock = slot.Itemstack.Collectible;
            var type = maybeblock.FirstCodePart();
            if (maybeblock != null && (type == "rock" || type == "gravel" || type == "sand" || type == "soil" || type == "cobblestone" || type == "rockpolished")) 
            {
                contents = slot.Itemstack;
                slot.TakeOut(1);
                slot.MarkDirty();
                MarkDirty();
                return true;
            }
            return false;
        }
        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            if(contents!=null)
            {
                Api.World.SpawnItemEntity(contents, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
            }
            base.OnBlockBroken(byPlayer);
        }
        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);
            if (contents != null)
            {
                var nameJank = contents?.GetName();
                var pe = GetBehavior<RockmakerBhv>().ToVoid();
                dsc.AppendLine("MP:\nConsuming: " + pe);
                dsc.AppendLine($"\nContents: {nameJank}");
            }
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            contents = tree.GetItemstack("contents");
            Working = tree.GetBool("working");
            LastTickTotalHours = tree.GetFloat("lasttick");
            workdone = tree.GetFloat("workdone");
            contents?.ResolveBlockOrItem(worldAccessForResolve);
        }
        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetItemstack("contents", contents);
            tree.SetBool("working", Powered);
            tree.SetDouble("lasttick",LastTickTotalHours);
            tree.SetFloat("workdone",workdone);
        }

    }
    public class RockmakerBhv : BlockEntityBehavior, IManaConsumer
    {
        public RockmakerBhv(BlockEntity blockentity) : base(blockentity)
        {
        }
        public int ToVoid() 
        {
            if (Blockentity is RockmakerBE entity)
            {
                if(entity.OreGenMode)
                {
                    return 10;
                }
                if (entity.contents == null)
                {
                    return 0;
                }
                switch (entity.contents.Collectible.FirstCodePart())
                {
                    case string x when x == "cobblestone" || x == "gravel" || x == "sand":
                        {
                            return 1;
                        }
                    case "rock":
                        {
                            return 2;
                        }
                    case "rockpolished":
                        {
                            return 3;
                        }
                    case "soil":
                        {
                            switch (entity.contents.Collectible.FirstCodePart(1))
                            {
                                case string x when x == "verylow" || x == "low": 
                                    {
                                        return 1;
                                    }
                                case "medium":
                                    {
                                        return 2;
                                    }
                                case "compost":
                                    {
                                        return 4;
                                    }
                                case "high":
                                    {
                                        return 8;
                                    }
                            }
                            return 1;
                        }
                }
            }
            return 0;
        }

        public void EatMana(int mana)
        {
            if (Blockentity is RockmakerBE entity) 
            {
                entity.Working = mana >= ToVoid();
            }
        }
    }
}
