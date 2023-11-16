using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace LensstoryMod
{
    public class ScrollItem : Item
    {
        public Dictionary<string,float> dic = new Dictionary<string, float>();

        public string scrollID;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            JsonObject scroll = Attributes?["scrollinfo"];

            if(scroll?.Exists == true)
            {
                try
                {
                    scrollID = scroll["scrollID"].AsString();
                }
                catch (Exception e)
                {
                    api.World.Logger.Error("No idea what scroll ID is for scroll {0},Ignoring. Exception: {1}",Code,e);
                    scrollID = "";
                }
            }
            JsonObject effects = Attributes?["effects"];
            if (effects?.Exists == true)
            {
                try
                {
                    dic = effects.AsObject<Dictionary<string, float>>();
                }
                catch (Exception e)
                {
                    api.World.Logger.Error("No idea what scroll {0}'s effects are,Ignoring. Exception: {1}",Code,e);
                    dic.Clear();
                }
            }
        }

        public override string GetHeldTpUseAnimation(ItemSlot activeHotbarSlot, Entity forEntity)
        {
            return "eat";
        }
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if(scrollID!= null && scrollID!= "")
            {
                handling = EnumHandHandling.PreventDefault;
                return;
            }
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }
        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if(byEntity.World is IClientWorldAccessor)
            {
                return secondsUsed <= 1.5f;
            }
            return true;
        }
        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (byEntity.World.Side == EnumAppSide.Server && secondsUsed >= 1.5f)
            {
                foreach (var stat in dic)
                {
                    if (byEntity.Stats.GetBlended(stat.Key) >= 2.5f)
                    {
                        IServerPlayer player = (byEntity.World.PlayerByUid((byEntity as EntityPlayer).PlayerUID) as IServerPlayer);
                        player.SendMessage(GlobalConstants.InfoLogChatGroup, "You feel like the " + slot.Itemstack.GetName() + " can't change you any further.", EnumChatType.Notification);
                        return;
                    }
                }
                ScrollEffect scrollboi = new ScrollEffect();
                scrollboi.ScrollStats((byEntity as EntityPlayer), dic, "lensmod", scrollID);
                if (byEntity is EntityPlayer)
                {
                    IServerPlayer player = (byEntity.World.PlayerByUid((byEntity as EntityPlayer).PlayerUID) as IServerPlayer);

                    player.SendMessage(GlobalConstants.InfoLogChatGroup, "You feel the " + slot.Itemstack.GetName() + " alter your body.", EnumChatType.Notification);
                }
                slot.TakeOut(1);
                slot.MarkDirty();

            }
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
            if(dic != null)
            {
                dsc.AppendLine(Lang.Get("\n"));

                if(dic.ContainsKey("rangedWeaponsAcc"))
                {
                    dsc.AppendLine(Lang.Get("When used, {0}% ranged accuracy till death do you part.", dic["rangedWeaponsAcc"] * 100));
                }
                if (dic.ContainsKey("animalLootDropRate"))
                {
                    dsc.AppendLine(Lang.Get("When used, {0}% bonus animal loot till death do you part.", dic["animalLootDropRate"] * 100));
                }
                if (dic.ContainsKey("animalHarvestingTime"))
                {
                    dsc.AppendLine(Lang.Get("When used, {0}% animal gather speed till death do you part.", dic["animalHarvestingTime"] * 100));
                }
                if (dic.ContainsKey("animalSeekingRange"))
                {
                    dsc.AppendLine(Lang.Get("When used, {0}% animal seek range till death do you part.", dic["animalSeekingRange"] * 100));
                }
                if (dic.ContainsKey("forageDropRate"))
                {
                    dsc.AppendLine(Lang.Get("When used, {0}% bonus forage till death do you part.", dic["forageDropRate"] * 100));
                }
                if (dic.ContainsKey("healingeffectivness"))
                {
                    dsc.AppendLine(Lang.Get("When used, {0}% healing effectiveness loot till death do you part.", dic["healingeffectivness"] * 100));
                }
                if (dic.ContainsKey("hungerrate"))
                {
                    dsc.AppendLine(Lang.Get("When used, {0}% hunger rate till death do you part.", dic["hungerrate"] * 100));
                }
                if (dic.ContainsKey("meleeWeaponsDamage"))
                {
                    dsc.AppendLine(Lang.Get("When used, {0}% melee damage till death do you part.", dic["meleeWeaponsDamage"] * 100));
                }
                if (dic.ContainsKey("miningSpeedMul"))
                {
                    dsc.AppendLine(Lang.Get("When used, {0}% mining speed till death do you part.", dic["miningSpeedMul"] * 100));
                }
                if (dic.ContainsKey("oreDropRate"))
                {
                    dsc.AppendLine(Lang.Get("When used, {0}% bonus ore till death do you part.", dic["oreDropRate"] * 100));
                }
                if (dic.ContainsKey("rangedWeaponsDamage"))
                {
                    dsc.AppendLine(Lang.Get("When used, {0}% ranged damage till death do you part.", dic["rangedWeaponsDamage"] * 100));
                }
                if (dic.ContainsKey("rangedWeaponsSpeed"))
                {
                    dsc.AppendLine(Lang.Get("When used, {0}% ranged speed till death do you part.", dic["rangedWeaponsSpeed"] * 100));
                }
                if (dic.ContainsKey("walkspeed"))
                {
                    dsc.AppendLine(Lang.Get("When used, {0}% walk speed till death do you part.", dic["walkspeed"] * 100));
                }
                if (dic.ContainsKey("wildCropDropRate"))
                {
                    dsc.AppendLine(Lang.Get("When used, {0}% bonus wild crops till death do you part.", dic["wildCropDropRate"] * 100));
                }
            }
        }

    }
}
