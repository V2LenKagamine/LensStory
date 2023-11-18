using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace LensstoryMod
{
    //Simple version of ItemOre.cs, but tooled for my mod.
    public class SimpleItemOre : Item
    {
        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            if (CombustibleProps?.SmeltedStack?.ResolvedItemstack == null)
            {
                if (Attributes?["metalUnits"].Exists == true)
                {
                    float units = Attributes["metalUnits"].AsInt();

                    string orename = LastCodePart(1);
                    AssetLocation loc = new AssetLocation("lensstory:nugget-" + orename);
                    Item item = api.World.GetItem(loc);

                    if (item?.CombustibleProps?.SmeltedStack?.ResolvedItemstack != null)
                    {
                        string metalname = item.CombustibleProps.SmeltedStack.ResolvedItemstack.GetName().Replace(" ingot", "");
                        dsc.AppendLine(Lang.Get("{0} units of {1}", units.ToString("0.#"), metalname));
                    }

                    dsc.AppendLine(Lang.Get("Parent Material: {0}", Lang.Get("rock-" + LastCodePart())));
                    dsc.AppendLine();
                    dsc.AppendLine(Lang.Get("Crush with hammer to extract nuggets"));
                }
            }
            else
            {

                base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

                if (CombustibleProps.SmeltedStack.ResolvedItemstack.GetName().Contains("ingot"))
                {
                    string smelttype = CombustibleProps.SmeltingType.ToString().ToLowerInvariant();
                    int instacksize = CombustibleProps.SmeltedRatio;
                    int outstacksize = CombustibleProps.SmeltedStack.ResolvedItemstack.StackSize;
                    float units = outstacksize * 100f / instacksize;

                    string metalname = CombustibleProps.SmeltedStack.ResolvedItemstack.GetName().Replace(" ingot", "");

                    string str = Lang.Get("lensstory:smeltdesc-" + smelttype + "ore-plural", units.ToString("0.#"), metalname);
                    dsc.AppendLine(str);
                }

                return;
            }
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
        }

        public override string GetHeldItemName(ItemStack itemStack)
        {
            if (Attributes?["metalUnits"].Exists == true)
            {
                string orename = LastCodePart(1);
                string rockname = LastCodePart(0);

                if (FirstCodePart() == "lencrystalore")
                {
                    return Lang.Get(LastCodePart(2) + "-crystalore-chunk", Lang.Get("ore-" + orename));

                }
                return Lang.Get(LastCodePart(2) + "-ore-chunk", Lang.Get("ore-" + orename));

            }

            return base.GetHeldItemName(itemStack);
        }
    }
    //once again we retool the games ItemNugget.cs
    public class SimpleItemNugget : Item
    {
        public override void OnCreatedByCrafting(ItemSlot[] allInputslots, ItemSlot outputSlot, GridRecipe byRecipe)
        {
            ItemSlot oreSlot = allInputslots.FirstOrDefault(slot => slot.Itemstack?.Collectible is ItemOre);
            if (oreSlot != null)
            {
                int units = oreSlot.Itemstack.ItemAttributes["metalUnits"].AsInt(5);

                Item item = api.World.GetItem(new AssetLocation("lensstory:nugget-" + oreSlot.Itemstack.Collectible.Variant["ore"]));
                ItemStack outStack = new ItemStack(item);
                outStack.StackSize = Math.Max(1, (int)Math.Floor(units * (item.CombustibleProps.SmeltedRatio/100f)));
                outputSlot.Itemstack = outStack;
            }

            base.OnCreatedByCrafting(allInputslots, outputSlot, byRecipe);
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {

            if (CombustibleProps?.SmeltedStack == null)
            {
                base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
                return;
            }

            CombustibleProperties props = CombustibleProps;

            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            string smelttype = CombustibleProps.SmeltingType.ToString().ToLowerInvariant();
            int instacksize = CombustibleProps.SmeltedRatio;
            int outstacksize = CombustibleProps.SmeltedStack.ResolvedItemstack.StackSize;
            float units = outstacksize * 100f / instacksize;

            string metalname = CombustibleProps.SmeltedStack.ResolvedItemstack.GetName().Replace(" ingot", "");

            string str = Lang.Get("game:smeltdesc-" + smelttype + "ore-plural", units.ToString("0.#"), metalname);
            dsc.AppendLine(str);
        }
    }
}
