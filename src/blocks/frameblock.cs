using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace LensstoryMod
{
    public class WoodFrame : Block
    {
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {

            if(world.Side == EnumAppSide.Client) { return true; }
            ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if(slot.Itemstack != null && slot.Itemstack.Collectible is BlockLiquidContainerBase container)
            {
                ItemStack fluid = container.GetContent(slot.Itemstack);
                if (fluid!=null && fluid.Collectible?.Code == AssetLocation.Create("lensstory:concreteportion"))
                {
                    if (fluid.StackSize >= 200)
                    {
                        container.TryTakeLiquid(slot.Itemstack, 0.2f);
                        world.BlockAccessor.SetBlock(api.World.GetBlock(AssetLocation.Create("lensstory:concretefull")).Id,blockSel.Position);
                        slot.MarkDirty();
                    }
                }
            }
            return true;
        }
    }
}
