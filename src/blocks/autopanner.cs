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
            get => this.fuel; set
            {
                if (this.fuel != value)
                {
                    this.fuel = value;
                    this.MarkDirty();
                }
            }
        }
        private double ticker;

        private double LastTickTotalHours;
        private Mana ManaStore => this.GetBehavior<Mana>();

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            this.RegisterGameTickListener(this.OnCommonTick, 250);
        }

        private void OnCommonTick(float dt)
        {
            if (this.Working)
            {
                var hourspast = this.Api.World.Calendar.TotalHours - this.LastTickTotalHours;
                if(this.fuel >= 1)
                {
                    ticker += hourspast;
                    if (ticker >= 0.05)
                    {
                        if (Api.World.Rand.Next(100) <= 25)
                        {
                            ItemStack Item = new ItemStack(Api.World.GetItem(AssetLocation.Create(this.PossibleDrops[Api.World.Rand.Next(PossibleDrops.Length)])),Api.World.Rand.Next(2)+1);
                            if (Item != null) 
                            {
                                this.Api.World.SpawnItemEntity(Item, this.Pos.ToVec3d().Add(0.5, -1.5, 0.5));
                            }
                            
                        }
                        if (Api.World.Rand.Next(100) <= 10)
                        {
                            this.fuel--;
                        }
                        ticker -= 0.05;
                        this.MarkDirty();
                    }
                }
            }
            this.LastTickTotalHours = this.Api.World.Calendar.TotalHours;
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

        public int ToVoid => 1;

        public void EatMana(int mana)
        {
            if (this.Blockentity is AutoPannerBE entity)
            {
                entity.Working = mana == ToVoid;
            }
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);

            dsc.AppendLine("Mana: ")
                .AppendLine("Consuming: " + ToVoid);
        }
    }
}
