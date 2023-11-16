using System;
using System.Reflection;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

namespace LensstoryMod
{

    public class AutoPannerBlock : Block
    {
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if(world.BlockAccessor.GetBlockEntity(blockSel.Position) is AutoPannerBE entity)
            {
                return entity.OnPlayerInteract(byPlayer);
            }
            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
    }

    public class AutoPannerBE : BlockEntity
    {

        public String[] PossibleDrops = new String[] {
                        "game:amethyst",
                        "game:clearquartz",
                        "game:rosequartz",
                        "game:smokyquartz"
        };

        private bool Powered;

        private int fuel;
        public bool Working
        {
            get => this.Powered; set
            {
                if (this.Powered != value)
                {
                    if (value && !this.Powered)
                    {
                        this.MarkDirty();
                    }
                    this.Powered = value;
                };
            }
        }
        public int FuelAmnt
        {
            get => fuel; set
            {
                if (fuel != value)
                {
                    fuel = value;
                    MarkDirty();
                }
            }
        }
        private double ticker;

        private double LastTickTotalHours;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            this.RegisterGameTickListener(this.OnCommonTick, 250);
        }

        private void OnCommonTick(float dt)
        {
            if (Working)
            {
                var hourspast = Api.World.Calendar.TotalHours - LastTickTotalHours;
                if(fuel >= 1)
                {
                    ticker += hourspast * 25; 
                    if (ticker >= 1)
                    {
                        int workdone = (int)Math.Floor(ticker);
                        for (var i = 0; i < workdone; i++)
                        {
                            if (Api.World.Rand.Next(100) <= 25)
                            {
                                ItemStack Item = new ItemStack(Api.World.GetItem(AssetLocation.Create(PossibleDrops[Api.World.Rand.Next(PossibleDrops.Length)])), Api.World.Rand.Next(2) + 1);
                                if (Item != null)
                                {
                                    Api.World.SpawnItemEntity(Item, Pos.ToVec3d().Add(0.5, -1.1, 0.5));
                                }
                            }
                            if (Api.World.Rand.Next(100) <= 10)
                            {
                                fuel--;
                            }
                        }
                        ticker -= workdone;
                        MarkDirty();
                    }
                }
            }
            LastTickTotalHours = Api.World.Calendar.TotalHours;
        }

        internal bool OnPlayerInteract(IPlayer player)
        {
            var slot = player.InventoryManager.ActiveHotbarSlot;
            if (slot.Itemstack != null)
            {
                var maybeblock = slot.Itemstack.Block;
                if (maybeblock != null)
                {
                    if (player.Entity.Controls.ShiftKey && fuel < 32 && maybeblock.Attributes?.IsTrue("pannable") == true)
                    {
                        this.fuel++;

                        slot.TakeOut(1);
                        slot.MarkDirty();

                        this.MarkDirty();
                        return true;
                    }
                }
            }
            return false;
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);
            if (this.fuel >= 1)
            {
                dsc.AppendLine($"\nContents: {this.fuel}x pannable material.");
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetDouble("lastTickTotalHours", this.LastTickTotalHours);
            tree.SetInt("fuel", this.fuel);
            tree.SetDouble("ticker", this.ticker);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            this.LastTickTotalHours = tree.GetDouble("lastTickTotalHours");
            this.fuel = tree.GetInt("fuel");
            this.ticker = tree.GetDouble("ticker");
        }
    }
    public class AutoPannerBhv : BlockEntityBehavior, IManaConsumer
    {
        public AutoPannerBhv(BlockEntity blockEntity) : base(blockEntity)
        {

        }

        public int ToVoid() { return 1; }

        public void EatMana(int mana)
        {
            if (this.Blockentity is AutoPannerBE entity)
            {
                entity.Working = mana == ToVoid();
            }
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);

            dsc.AppendLine("MP: ")
                .AppendLine("Consumes: " + ToVoid());
        }
    }
}
