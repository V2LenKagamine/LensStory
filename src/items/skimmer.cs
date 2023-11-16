using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace LensstoryMod
{
    public class FluidSkimmerItem : Item
    {
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (byEntity.Controls.ShiftKey)
            {
                handling = EnumHandHandling.PreventDefault;
                return;
            }
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling );
        }
        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (byEntity.World is IClientWorldAccessor)
            {
                return secondsUsed <= 0.5f;
            } else
            {
                if (blockSel != null)
                {
                    BlockEntity BarrelEntMaybe = api.World.BlockAccessor.GetBlockEntity(blockSel.Position);
                    if (BarrelEntMaybe != null && BarrelEntMaybe is BlockEntityLiquidContainer BarrelEnt && secondsUsed >= 0.5f)
                    {
                        ItemSlot theFluid = BarrelEnt.Inventory[1];
                        theFluid.TakeOut(theFluid.StackSize % 100);
                        BarrelEnt.Inventory.MarkSlotDirty(1);
                        return secondsUsed <= 0.5f;
                    }
                }
            }
            return true;
        }
        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        { 
            if (blockSel != null)
            {
                BlockEntity BarrelEntMaybe = api.World.BlockAccessor.GetBlockEntity(blockSel.Position);
                if (BarrelEntMaybe != null && BarrelEntMaybe is BlockEntityLiquidContainer BarrelEnt && secondsUsed >= 0.5f)
                {
                    ItemSlot theFluid = BarrelEnt.Inventory[1];
                    theFluid.TakeOut(theFluid.StackSize % 100);
                    BarrelEnt.Inventory.MarkSlotDirty(1);
                }
            }
        }
    }
}
