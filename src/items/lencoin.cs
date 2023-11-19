using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace LensstoryMod
{
    public class LenCoinItem : Item
    {
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            IServerPlayer player = (byEntity.World.PlayerByUid((byEntity as EntityPlayer).PlayerUID) as IServerPlayer);
            player.SendMessage(GlobalConstants.InfoLogChatGroup, string.Format("You flip the coin, it lands on {0}",api.World.Rand.Next(2) == 1 ? "heads":"tails"), EnumChatType.Notification);
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }
    }
}