using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using lensstory.src.recipe;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace LensstoryMod
{

    /*
     * Most of this is taken from https://github.com/l33tmaan/ACulinaryArtillery---.NET7
     * I would've loved to just use as a dependency/do support for it but I needed
     * some edge cases and like the flexibility of my own class.
     */
    public class SimpleFoodItem : Item
    {
        public float SatMult
        {
            get { return Attributes?["satMult"].AsFloat(1f) ?? 1f; }
        }
        public override string GetHeldTpUseAnimation(ItemSlot activeHotbarSlot, Entity forEntity)
        {
            return "eat";
        }
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            handling = EnumHandHandling.PreventDefaultAction;
            return;
        }
        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (byEntity.World is IClientWorldAccessor)
            {
                return secondsUsed < 0.95f;
            }
            return true;
        }
        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            FoodNutritionProperties nutriProps = GetNutritionProperties(byEntity.World, slot.Itemstack, byEntity);
            FoodNutritionProperties[] addProps = GetPropsFromArray((slot.Itemstack.Attributes["bonusSats"] as FloatArrayAttribute)?.value);
            if (api.Side == EnumAppSide.Server && nutriProps != null && addProps?.Length > 0 && secondsUsed >= 0.95f)
            {
                TransitionState state = UpdateAndGetTransitionState(api.World, slot, EnumTransitionType.Perish);
                float spoilState = state != null ? state.TransitionLevel : 0;

                float satLossMul = GlobalConstants.FoodSpoilageSatLossMul(spoilState, slot.Itemstack, byEntity);
                float healthLossMul = GlobalConstants.FoodSpoilageHealthLossMul(spoilState, slot.Itemstack, byEntity);

                foreach (FoodNutritionProperties prop in addProps)
                {
                    float sat = prop.Satiety * satLossMul;
                    float satLossDelay = Math.Min(1.3f, satLossMul * 3) * 10 + sat / 70f * 60f;
                    byEntity.ReceiveSaturation(sat, prop.FoodCategory,satLossDelay);

                    float healthChange = prop.Health * healthLossMul;

                    if (healthChange != 0)
                    {
                        byEntity.ReceiveDamage(new DamageSource() { Source = EnumDamageSource.Internal, Type = healthChange > 0 ? EnumDamageType.Heal : EnumDamageType.Poison }, Math.Abs(healthChange));
                    }
                }
            }
            if (api.Side == EnumAppSide.Server && Attributes?["effects"] != null && Attributes?["scrollinfo"]["scrollID"].Exists == true)
            {
                Dictionary<string, float> dic = Attributes["effects"].AsObject<Dictionary<string, float>>();
                string effectID = Attributes["scrollinfo"]["scrollID"].AsString();
                ScrollEffect scrollboi = new ScrollEffect();
                scrollboi.ScrollStats((byEntity as EntityPlayer), dic, "lensmod", effectID);
            }

            base.OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel);
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
            ListIngredients(inSlot, dsc, world);
            ItemStack stack = inSlot.Itemstack;

            int durability = GetMaxDurability(stack);
            
            if (durability > 1)
            {
                dsc.AppendLine(Lang.Get("Durability: {0} / {1}", stack.Attributes.GetInt("durability", durability), durability));
            }
            EntityPlayer entity = world.Side == EnumAppSide.Client ? (world as IClientWorldAccessor).Player.Entity : null;

            StringBuilder dummy = new();

            float spoilState = AppendPerishableInfoText(inSlot, dummy, world);

            FoodNutritionProperties nutriProps = GetNutritionProperties(world, stack, entity);
            FoodNutritionProperties[] addProps = GetPropsFromArray((inSlot.Itemstack.Attributes["bonusSats"] as FloatArrayAttribute)?.value);

            if (nutriProps != null && addProps?.Length > 0)
            {
                dsc.AppendLine(Lang.Get("Nutrients Info:"));

                foreach (FoodNutritionProperties props in addProps)
                {
                    float satLossMul = GlobalConstants.FoodSpoilageSatLossMul(spoilState, stack, entity);
                    float healthLossMul = GlobalConstants.FoodSpoilageHealthLossMul(spoilState, stack, entity);

                    if (stack.Collectible.MatterState == EnumMatterState.Liquid)
                    {
                        float liquidVolume = stack.StackSize;
                        if (Math.Abs(props.Health * healthLossMul) > 0.001f)
                        {
                            dsc.AppendLine(Lang.Get(" ~{0} {2} sat, {1} hp", Math.Round((props.Satiety * satLossMul) * (liquidVolume / 10)), ((props.Health * healthLossMul) * (liquidVolume / 10)), props.FoodCategory.ToString()));
                        }
                        else
                        {
                            dsc.AppendLine(Lang.Get("  ~{0} {1} sat", Math.Round((props.Satiety * satLossMul) * (liquidVolume / 10)), props.FoodCategory.ToString()));
                        }
                    }
                    else
                    {
                        if (Math.Abs(props.Health * healthLossMul) > 0.001f)
                        {
                            dsc.AppendLine(Lang.Get(" ~{0} {2} sat, {1} hp", Math.Round(props.Satiety * satLossMul), props.Health * healthLossMul, props.FoodCategory.ToString()));
                        }
                        else
                        {
                            dsc.AppendLine(Lang.Get(" ~{0} {1} sat", Math.Round(props.Satiety * satLossMul), props.FoodCategory.ToString()));
                        }
                    }
                }
            }
        }
    public override void OnCreatedByCrafting(ItemSlot[] allInputslots, ItemSlot outputSlot, GridRecipe byRecipe)
        {
            base.OnCreatedByCrafting(allInputslots, outputSlot, byRecipe);
            ItemStack output = outputSlot.Itemstack;

            List<string> ingredients = new List<string>();
            float[] sat = new float[6];

            foreach (ItemSlot slot in allInputslots)
            {
                if (slot.Itemstack == null) { continue; }
                CraftingRecipeIngredient possible = null;
                if (byRecipe?.Ingredients != null)
                {
                    foreach (var value in byRecipe.Ingredients) { if (value.Value.SatisfiesAsIngredient(slot.Itemstack)) { possible = value.Value; break; } }
                }
                if (slot.Itemstack.Collectible is SimpleFoodItem)
                {
                    string[] addIngs = (slot.Itemstack.Attributes["madeWith"] as StringArrayAttribute)?.value;
                    float[] addSat = (slot.Itemstack.Attributes["bonusSats"] as FloatArrayAttribute)?.value;

                    if (addSat != null && addSat.Length == 6)
                        sat = sat.Zip(addSat, (x, y) => x + (y * possible?.Quantity ?? 1)).ToArray();

                    if (addIngs != null && addIngs.Length > 0)
                    {
                        foreach (string aL in addIngs)
                        {
                            if (ingredients.Contains(aL))
                                continue;

                            ingredients.Add(aL);
                        }
                    }
                }
                else
                {
                    GetNutrientsFromIngredient(ref sat, slot.Itemstack.Collectible, possible?.Quantity ?? 1);
                    string aL = slot.Itemstack.Collectible.Code.Domain + ":" + slot.Itemstack.Collectible.Code.Path;
                    if (ingredients.Contains(aL))
                        continue;
                    ingredients.Add(aL);
                }
            }
            if (byRecipe.Output.Quantity > 1)
            {
                for (var i = 0; i < sat.Length; i++)
                {
                    sat[i] = (float)Math.Floor(sat[i] * (1f / (float)byRecipe.Output.Quantity));
                }
            }
            ingredients.Sort();

            output.Attributes["madeWith"] = new StringArrayAttribute(ingredients.ToArray());
            output.Attributes["bonusSats"] = new FloatArrayAttribute(sat.ToArray());
        }

        public void OnCreatedTinker(ItemStack[] allInputSlots,LenShapelessRecipe byRecipe,ref EntityItem thePopper)
        {
            List<string> ingredients = new List<string>();
            float[] sat = new float[6];

            foreach (ItemStack slot in allInputSlots)
            {
                if (slot == null) { continue; }
                CraftingRecipeIngredient possible = null;
                if (byRecipe?.Ingredients != null)
                {
                    foreach (var value in byRecipe.Ingredients) { if (value!= null) { possible = value; break; } }
                }
                if (slot.Collectible is SimpleFoodItem)
                {
                    string[] addIngs = (slot.Attributes["madeWith"] as StringArrayAttribute)?.value;
                    float[] addSat = (slot.Attributes["bonusSats"] as FloatArrayAttribute)?.value;

                    if (addSat != null && addSat.Length == 6)
                        sat = sat.Zip(addSat, (x, y) => x + (y * possible?.Quantity ?? 1)).ToArray();

                    if (addIngs != null && addIngs.Length > 0)
                    {
                        foreach (string aL in addIngs)
                        {
                            if (ingredients.Contains(aL))
                                continue;

                            ingredients.Add(aL);
                        }
                    }
                }
                else
                {
                    GetNutrientsFromIngredient(ref sat, slot.Collectible, possible?.Quantity ?? 1);
                    string aL = slot.Collectible.Code.Domain + ":" + slot.Collectible.Code.Path;
                    if (ingredients.Contains(aL))
                        continue;
                    ingredients.Add(aL);
                }
            }
            if (byRecipe.Output.Quantity > 1)
            {
                for (var i = 0; i < sat.Length; i++)
                {
                    sat[i] = (float)Math.Floor(sat[i] * (1f / (float)byRecipe.Output.Quantity));
                }
            }
            ingredients.Sort();
            thePopper.Itemstack.Attributes["madeWith"] = new StringArrayAttribute(ingredients.ToArray());
            thePopper.Itemstack.Attributes["bonusSats"] = new FloatArrayAttribute(sat.ToArray());
            thePopper.Slot.MarkDirty();
        }

        public void GetNutrientsFromIngredient(ref float[] satHolder, CollectibleObject ing, int mult)
        {
            TreeAttribute check = Attributes?["expandedNutritionProps"].ToAttribute() as TreeAttribute;
            List<string> chk = new List<string>();
            if (check != null)
                foreach (var val in check)
                    chk.Add(val.Key);

            FoodNutritionProperties ingProps = null;
            if (chk.Count > 0)
                ingProps = Attributes["bonusNutrition"][Lentills.FindInArray(ing.Code.Domain + ":" + ing.Code.Path, chk.ToArray())].AsObject<FoodNutritionProperties>();
            if (ingProps == null)
                ingProps = ing.Attributes?["bonusInMeal"].AsObject<FoodNutritionProperties>();
            if (ingProps == null)
                ingProps = ing.NutritionProps;
            if (ingProps == null)
                return;

            if (ingProps.Health != 0)
                satHolder[(int)EnumNutritionType.Hp] += ingProps.Health * mult;

            switch (ingProps.FoodCategory)
            {
                case EnumFoodCategory.Fruit:
                    satHolder[(int)EnumNutritionType.Fruit] += ingProps.Satiety * mult;
                    break;

                case EnumFoodCategory.Grain:
                    satHolder[(int)EnumNutritionType.Grain] += ingProps.Satiety * mult;
                    break;

                case EnumFoodCategory.Vegetable:
                    satHolder[(int)EnumNutritionType.Vegetable] += ingProps.Satiety * mult;
                    break;

                case EnumFoodCategory.Protein:
                    satHolder[(int)EnumNutritionType.Protein] += ingProps.Satiety * mult;
                    break;

                case EnumFoodCategory.Dairy:
                    satHolder[(int)EnumNutritionType.Dairy] += ingProps.Satiety * mult;
                    break;
            }
        }

        public override bool CanSmelt(IWorldAccessor world, ISlotProvider cookingSlotsProvider, ItemStack inputStack, ItemStack outputStack)
        {
            if (inputStack.Collectible.CombustibleProps == null || outputStack == null) { return false; }

            if (!inputStack.Collectible.CombustibleProps.SmeltedStack.ResolvedItemstack.Equals(world, outputStack, GlobalConstants.IgnoredStackAttributes.Concat(new string[] { "madeWith", "bonusSats" }).ToArray()))
                return false;
            if (outputStack.StackSize >= outputStack.Collectible.MaxStackSize)
                return false;

            if (outputStack.Attributes["madeWith"] == null || !inputStack.Attributes["madeWith"].Equals(world, outputStack.Attributes["madeWith"]))
                return false;
            if (outputStack.Attributes["bonusSats"] == null || !inputStack.Attributes["bonusSats"].Equals(world, outputStack.Attributes["bonusSats"]))
                return false;


            return true;
        }

        public override void DoSmelt(IWorldAccessor world, ISlotProvider cookingSlotsProvider, ItemSlot inputSlot, ItemSlot outputSlot)
        {
            if (!CanSmelt(world, cookingSlotsProvider, inputSlot.Itemstack, outputSlot.Itemstack))
                return;

            ItemStack smeltedStack = inputSlot.Itemstack.Collectible.CombustibleProps.SmeltedStack.ResolvedItemstack.Clone();
            TransitionState state = UpdateAndGetTransitionState(world, new DummySlot(inputSlot.Itemstack), EnumTransitionType.Perish);

            if (state != null)
            {
                TransitionState smeltedState = smeltedStack.Collectible.UpdateAndGetTransitionState(world, new DummySlot(smeltedStack), EnumTransitionType.Perish);

                float nowTransitionedHours = (state.TransitionedHours / (state.TransitionHours + state.FreshHours)) * 0.8f * (smeltedState.TransitionHours + smeltedState.FreshHours) - 1;

                smeltedStack.Collectible.SetTransitionState(smeltedStack, EnumTransitionType.Perish, Math.Max(0, nowTransitionedHours));
            }

            int batchSize = 1;

            string[] ingredients = (inputSlot.Itemstack.Attributes["madeWith"] as StringArrayAttribute)?.value;
            float[] satieties = (inputSlot.Itemstack.Attributes["bonusSats"] as FloatArrayAttribute)?.value;


            if (ingredients != null)
                smeltedStack.Attributes["madeWith"] = new StringArrayAttribute(ingredients);
            if (satieties != null)
                smeltedStack.Attributes["bonusSats"] = new FloatArrayAttribute(satieties);

            if (outputSlot.Itemstack == null)
            {
                outputSlot.Itemstack = smeltedStack;
                outputSlot.Itemstack.StackSize = batchSize * smeltedStack.StackSize;
            }
            else
            {
                smeltedStack.StackSize = batchSize * smeltedStack.StackSize;
                ItemStackMergeOperation op = new ItemStackMergeOperation(world, EnumMouseButton.Left, 0, EnumMergePriority.ConfirmedMerge, batchSize * smeltedStack.StackSize);
                op.SourceSlot = new DummySlot(smeltedStack);
                op.SinkSlot = new DummySlot(outputSlot.Itemstack);
                outputSlot.Itemstack.Collectible.TryMergeStacks(op);
                outputSlot.Itemstack = op.SinkSlot.Itemstack;
            }

            inputSlot.Itemstack.StackSize -= batchSize * CombustibleProps.SmeltedRatio;

            if (inputSlot.Itemstack.StackSize <= 0)
            {
                inputSlot.Itemstack = null;
            }

            outputSlot.MarkDirty();
        }
        public override ItemStack OnTransitionNow(ItemSlot slot, TransitionableProperties props)
        {
            string[] ings = (slot.Itemstack.Attributes["madeWith"] as StringArrayAttribute)?.value;
            float[] xNutr = (slot.Itemstack.Attributes["bonusSats"] as FloatArrayAttribute)?.value;

            ItemStack org = base.OnTransitionNow(slot, props);
            if (org == null || !(org.Collectible is SimpleFoodItem))
                return org;
            if (ings != null)
                org.Attributes["madeWith"] = new StringArrayAttribute(ings);
            if (xNutr != null && xNutr.Length > 0)
                org.Attributes["bonusSats"] = new FloatArrayAttribute(xNutr);
            return org;
        }

        public void ListIngredients(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world)
        {
            string desc = "Made with:";
            string[] ings = (inSlot.Itemstack.Attributes["madeWith"] as StringArrayAttribute)?.value;

            if (ings == null || ings.Length < 1)
            {
                return;
            }

            List<string> readable = new List<string>();
            for (int i = 0; i < ings.Length; i++)
            {
                AssetLocation obj = new AssetLocation(ings[i]);
                Block block = world.GetBlock(obj);
                string ingInfo = Lang.GetIfExists("recipeingredient-" + (block != null ? "block-" : "item-") + obj.Path);
                if (ingInfo != null && !readable.Contains(ingInfo))
                    readable.Add(ingInfo);
            }

            ings = readable.ToArray();

            if (ings == null || ings.Length < 1)
            {
                return;
            }
            if (ings.Length < 2)
            {
                desc += ings[0];

                dsc.AppendLine(desc);
                return;
            }
            for (int i = 0; i < ings.Length; i++)
            {
                if (i + 1 == ings.Length)
                {
                    desc += "and " + ings[i];
                }
                else
                {
                    desc += ings[i] + ", ";
                }
            }

            dsc.AppendLine(desc);
        }

        public FoodNutritionProperties[] GetPropsFromArray(float[] satieties)
        {
            if (satieties == null || satieties.Length < 6)
                return null;

            List<FoodNutritionProperties> props = new List<FoodNutritionProperties>();

            if (satieties[(int)EnumNutritionType.Fruit] != 0)
                props.Add(new FoodNutritionProperties() { FoodCategory = EnumFoodCategory.Fruit, Satiety = satieties[(int)EnumNutritionType.Fruit] * SatMult });
            if (satieties[(int)EnumNutritionType.Grain] != 0)
                props.Add(new FoodNutritionProperties() { FoodCategory = EnumFoodCategory.Grain, Satiety = satieties[(int)EnumNutritionType.Grain] * SatMult });
            if (satieties[(int)EnumNutritionType.Vegetable] != 0)
                props.Add(new FoodNutritionProperties() { FoodCategory = EnumFoodCategory.Vegetable, Satiety = satieties[(int)EnumNutritionType.Vegetable] * SatMult });
            if (satieties[(int)EnumNutritionType.Protein] != 0)
                props.Add(new FoodNutritionProperties() { FoodCategory = EnumFoodCategory.Protein, Satiety = satieties[(int)EnumNutritionType.Protein] * SatMult });
            if (satieties[(int)EnumNutritionType.Dairy] != 0)
                props.Add(new FoodNutritionProperties() { FoodCategory = EnumFoodCategory.Dairy, Satiety = satieties[(int)EnumNutritionType.Dairy] * SatMult });

            if (satieties[0] != 0 && props.Count > 0)
                props[0].Health = satieties[0] * SatMult;

            return props.ToArray();
        }
    }

    public enum EnumNutritionType
    {
        Hp,
        Fruit,
        Grain,
        Vegetable,
        Protein,
        Dairy
    }

}
