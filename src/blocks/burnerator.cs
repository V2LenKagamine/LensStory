
using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace LensstoryMod
{
    public class Burnerator : Block
    {
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is BurneratorBE entity)
            {
                return entity.OnPlayerInteract(byPlayer);
            }
            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
        public Mana? GetMana(IWorldAccessor world, BlockPos pos)
        {
            if (world.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BlockEntityBehavior>() is Mana device)
            {
                return device;
            }
                return null;
        }
    }
    public class BurneratorBE : BlockEntity
    {
        private int ManaNumber;

        public int fuel;

        private double ticker;

        private double LastTickTotalHours;

        private Mana theMana => GetBehavior<Mana>();
        public int ManaID
        {
            get => ManaNumber; set
            {
                if (value != ManaNumber) { }
                theMana.ManaID = ManaNumber = value;
                MarkDirty();
            }
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            RegisterGameTickListener(OnCommonTick, 1000);
        }

        private void OnCommonTick(float dt)
        {
            if(fuel > 0)
            {
                var hourspast = Api.World.Calendar.TotalHours - LastTickTotalHours;
                ticker += hourspast * 100;
                if(ticker >= 1)
                {
                    var workdone = (int)Math.Floor(ticker);
                    fuel-= workdone;
                    ticker -= workdone;
                    MarkDirty();
                }
            }
        }

        internal bool OnPlayerInteract(IPlayer player)
        {
            var slot = player.InventoryManager.ActiveHotbarSlot;
            if (slot.Itemstack != null)
            {
                if(slot.Itemstack.Collectible.CombustibleProps != null)
                {
                    var combustprops = slot.Itemstack.Collectible.CombustibleProps;
                    if(combustprops.BurnDuration + fuel <= 48000)
                    {
                        slot.TakeOut(1);
                        slot.MarkDirty();
                        fuel += (int)combustprops.BurnDuration;
                        MarkDirty();

                        return true;
                    }
                }
            }
            return false;
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetInt("manaid", ManaNumber);
            tree.SetInt("burntime", fuel);
            tree.SetDouble("lasttick", LastTickTotalHours);
            tree.SetDouble("ticker", ticker);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            ManaNumber = tree.GetAsInt("manaid");
            fuel = tree.GetAsInt("burntime");
            LastTickTotalHours = tree.GetDouble("lasttick");
            ticker = tree.GetDouble("ticker");
        }

    }
    public class BurneratorBhv : BlockEntityBehavior, IManaMaker
    {

        private int fuelAmt;
        public BurneratorBhv(BlockEntity blockentity) : base(blockentity)
        {
        }

        public int MakeMana()
        {
            BlockEntity here = Api.World.BlockAccessor.GetBlockEntity(this.Pos);
            if(here is BurneratorBE)
            {
                BurneratorBE yep = here as BurneratorBE;
                fuelAmt = yep.fuel;
                if(yep.fuel >= 1) { return (yep.fuel / 600) + 1; }
            }
            return 0;
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);

            dsc.AppendLine("MP:")
                .AppendLine("Producing: " + MakeMana())
                .AppendLine("Fuel: " + fuelAmt) ;
        }

    }

}
