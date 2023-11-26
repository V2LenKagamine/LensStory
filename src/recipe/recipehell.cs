using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using LensstoryMod;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using Vintagestory.ServerMods;

namespace lensstory.src.recipe
{

    //I hate everything here, not because its bad, but because it took 21ish hours to code for literally sandwiches.
    public sealed class LenRecipeRegistry
    {
        private LenRecipeRegistry() { }

        private static readonly LenRecipeRegistry registry = new LenRecipeRegistry();
        public static LenRecipeRegistry Registry { get { return registry; } }

        private List<LenShapelessRecipe> lentinknerrecipes = new List<LenShapelessRecipe>();
        public List<LenShapelessRecipe> TinkerRecipies { get { return lentinknerrecipes; }set { lentinknerrecipes = value; } }
    }
    public class LenShapelessRecipe : IByteSerializable
    {
        public string Name = "egg";
        public AssetLocation Code {  get; set; }
        public bool Enabled { get; set; } = true;

        public string? ToolType { get; set; }
        public int? ToolLoss { get; set; }

        public CraftingRecipeIngredient[] Ingredients;

        public LenSubstitutable[] Substitutes;

        public JsonItemStack Output;

        public ItemStack AttemptCraft(ICoreAPI api, ItemSlot[] inputs)
        {
            Dictionary<CraftingRecipeIngredient,int> ingToAmt;
            var matched = pairInput(inputs,out ingToAmt);

            ItemStack mixed = Output.ResolvedItemstack.Clone();
            mixed.StackSize = OutputSize(matched,ingToAmt);

            if(mixed.StackSize <=0) { return null; }

            foreach (var val in matched)
            {
                float totake = val.Value.Quantity * mixed.StackSize;
                for (int i = 0; i < Substitutes.Length; i++) 
                {
                    bool edited = false;
                    for (int j = 0; j < ingToAmt.Count; j++)
                    {
                        if (Substitutes[i].GetMatch(ingToAmt.ElementAt(j).Key, val.Key.Itemstack) != null)
                        {
                            edited = true;
                            totake /= val.Value.Quantity; break;
                        }
                    }
                    if (edited) { break; }
                }
                val.Key.TakeOut((int)Math.Floor(totake));
                val.Key.MarkDirty();
            }
            return mixed;
        }

        public bool Matches(IWorldAccessor worl, ItemSlot[] inputs)
        {
            int OutSS = 0;

            Dictionary<CraftingRecipeIngredient, int> egg;
            List<KeyValuePair<ItemSlot, CraftingRecipeIngredient>> matched = pairInput(inputs,out egg);
            if (matched == null) return false;

            OutSS = OutputSize(matched,egg);

            return OutSS >= 0;
        }

        public string OutputName()
        {
            return Lang.Get("lensstory:willcreate{0}", Output.ResolvedItemstack.GetName());
        }

        public void FromBytes(BinaryReader reader, IWorldAccessor resolver)
        {
            Name = reader.ReadString();
            if (reader.ReadBoolean()) { ToolType = reader.ReadString(); }
            if (reader.ReadBoolean()) { ToolLoss = reader.ReadInt32(); }
            Ingredients = new CraftingRecipeIngredient[reader.ReadInt32()];

            for (int i = 0; i < Ingredients.Length; i++)
            {
                Ingredients[i] = new CraftingRecipeIngredient();
                Ingredients[i].FromBytes(reader, resolver);
                Ingredients[i].Resolve(resolver, "Len Food Recipe (FromBytes)");
            }
            Substitutes = new LenSubstitutable[reader.ReadInt32()];

            for (int i = 0; i < Substitutes.Length; i++)
            {
                Substitutes[i] = new LenSubstitutable();
                Substitutes[i].FromBytes(reader, resolver);
                Substitutes[i].Resolve(resolver, "Len Food Recipe (FromBytes)");
            }

            Output = new JsonItemStack();
            Output.FromBytes(reader, resolver.ClassRegistry);
            Output.Resolve(resolver, "Len Food Recipe (FromBytes)");
        }

        public void ToBytes(BinaryWriter writer)
        {
            writer.Write(Name);
            writer.Write((ToolType != null ? ToolType : null)!=null);
            if((ToolType != null ? ToolType : null) != null) { writer.Write(ToolType); }
            writer.Write((ToolLoss != null ? ToolLoss : null) != null);
            if ((ToolLoss != null ? ToolLoss : null) != null) { writer.Write((int)ToolLoss); }
            writer.Write(Ingredients.Length);
            for (int i = 0; i < Ingredients.Length; i++)
            {
                Ingredients[i].ToBytes(writer);
            }
            writer.Write(Substitutes.Length);
            for (int i = 0; i < Substitutes.Length; i++)
            {
                Substitutes[i].ToBytes(writer);
            }
            Output.ToBytes(writer);
        }

        private List<KeyValuePair<ItemSlot, CraftingRecipeIngredient>> pairInput(ItemSlot[] inputStacks,out Dictionary<CraftingRecipeIngredient,int> ingToAmt)
        {
            Stack<ItemSlot> inputSlotsList = new Stack<ItemSlot>();
            foreach (var val in inputStacks) if (!val.Empty) inputSlotsList.Push(val);

            List<KeyValuePair<ItemSlot, CraftingRecipeIngredient>> matched = new List<KeyValuePair<ItemSlot, CraftingRecipeIngredient>>();
            ingToAmt = new();

            while (inputSlotsList.Count > 0)
            {
                ItemSlot inputSlot = inputSlotsList.Pop();
                for (int i = 0; i < Ingredients.Length; i++)
                {
                    bool foundsub = false;
                    if (Ingredients[i].SatisfiesAsIngredient(inputSlot.Itemstack, false))
                    {
                        KeyValuePair<ItemSlot, CraftingRecipeIngredient> toadd = new(inputSlot, Ingredients[i]);
                        try { ingToAmt[Ingredients[i]] += inputSlot.StackSize; }
                        catch(KeyNotFoundException)
                        {
                            ingToAmt.Add(Ingredients[i],inputSlot.StackSize);
                        }
                        matched.Add(toadd);
                        break;
                    }
                    for (int i2 = 0; i2 < Substitutes.Length; i2++)
                    {
                        if (Substitutes[i2].GetMatch(Ingredients[i],inputSlot.Itemstack) != null)
                        {
                            KeyValuePair<ItemSlot, CraftingRecipeIngredient> toadd = new(inputSlot, Ingredients[i]);
                            try { ingToAmt[Ingredients[i]] += inputSlot.StackSize; }
                            catch (KeyNotFoundException)
                            {
                                ingToAmt.Add(Ingredients[i], inputSlot.StackSize);
                            }
                            if (matched.Contains(toadd)) { break; }
                            matched.Add(toadd);
                            foundsub = true;
                            break;
                        }
                    }
                    if (foundsub) { break; }
                }
            }
            for (int i = 0; i < ingToAmt.Count; i++)
            {
                try
                {
                    if (ingToAmt[Ingredients[i]] < Ingredients[i].Quantity) { return null; }
                }
                catch (KeyNotFoundException) { return null; }
            }
            return matched;
        }
        public bool Resolve(IWorldAccessor world, string sourceForErrorLogging)
        {
            bool ok = true;

            for (int i = 0; i < Ingredients.Length; i++)
            {
                ok &= Ingredients[i].Resolve(world, sourceForErrorLogging);
            }
            for(int i =0; i < Substitutes.Length; i++)
            {
                ok &= Substitutes[i].Resolve(world, sourceForErrorLogging);
            }

            ok &= Output.Resolve(world, sourceForErrorLogging);


            return ok;
        }

        private int OutputSize(List<KeyValuePair<ItemSlot, CraftingRecipeIngredient>> matched,Dictionary<CraftingRecipeIngredient,int> dic)
        {
            int outQuantityMul = -1;

            foreach (var val in dic)
            {
                CraftingRecipeIngredient type = val.Key;
                int amt = val.Value;
                int posChange = amt / type.Quantity;
                if (posChange < outQuantityMul || outQuantityMul == -1) outQuantityMul = posChange;
            }
            if (outQuantityMul == -1)
            {
                return -1;
            }
            return Output.StackSize * outQuantityMul;
        }

       
    }

    public class LenSubstitutable : IByteSerializable
    {
        public CraftingRecipeIngredient FromIngredient;
        public CraftingRecipeIngredient[] ToIngredients;
        public CraftingRecipeIngredient GetMatch(CraftingRecipeIngredient from,ItemStack stacc)
        {
            if (stacc == null) { return null; }
            if(from.Code != FromIngredient.Code) { return null; }
            if(from.SatisfiesAsIngredient(stacc,false)) { return from; }
            for (int i = 0; i < ToIngredients.Length; i++)
            {
                if (ToIngredients[i].SatisfiesAsIngredient(stacc,false)) return ToIngredients[i];
            }
            return null;
        }
        public bool Resolve(IWorldAccessor world, string debug)
        {
            bool ok = true;
            ok &= FromIngredient.Resolve(world, debug);
            for (int i = 0; i < ToIngredients.Length; i++)
            {
                ok &= ToIngredients[i].Resolve(world, debug);
            }
            return ok;
        }

        public void FromBytes(BinaryReader reader, IWorldAccessor resolver)
        {
            CraftingRecipeIngredient From = new();
            From.FromBytes(reader, resolver);
            From.Resolve(resolver, "Len Substitutable (FromBytes)");
            FromIngredient = From;
            CraftingRecipeIngredient[] ToArr = new CraftingRecipeIngredient[reader.ReadInt32()];
            for (int i = 0; i < ToArr.Length; i++)
            {
                ToArr[i] = new CraftingRecipeIngredient();
                ToArr[i].FromBytes(reader, resolver);
                ToArr[i].Resolve(resolver, "Len Substitutable (FromBytes)");
            }
            ToIngredients = ToArr;
        }
        public void ToBytes(BinaryWriter writer)
        {
            FromIngredient.ToBytes(writer);
            writer.Write(ToIngredients.Length);
            for (int i = 0; i < ToIngredients.Length; i++)
            {
                ToIngredients[i].ToBytes(writer);
            }
        }

        public LenSubstitutable Clone()
        {
            CraftingRecipeIngredient[] ToIng = new CraftingRecipeIngredient[ToIngredients.Length];
            CraftingRecipeIngredient FromIng = new();
            for (int i = 0; i < ToIngredients.Length; i++)
            {
                ToIng[i] = ToIngredients[i].Clone();
            }
            FromIng = FromIngredient.Clone();
            return new LenSubstitutable()
            {
                FromIngredient = FromIng,
                ToIngredients = ToIng
            };
        }
    }
    public class LenRecipeLoader : RecipeLoader
    {
        public ICoreServerAPI api;
        public override double ExecuteOrder()
        {
            return 100;
        }

        public override bool ShouldLoad(EnumAppSide side)
        {
            return side == EnumAppSide.Server;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.api = api;
        }
        public override void AssetsLoaded(ICoreAPI api)
        {
            if (!(api is ICoreServerAPI sapi)) return;
            this.api = sapi;
        }
        public override void AssetsFinalize(ICoreAPI api)
        {
            LoadAll();
        }
        public void LoadAll()
        {
            LoadFoods();
        }
        public void LoadFoods()
        {
            Dictionary<AssetLocation, JToken> files = api.Assets.GetMany<JToken>(api.Server.Logger, "recipes/lenfood");
            int recipeQuantity = 0;

            foreach (var val in files)
            {
                if (val.Value is JObject)
                {
                    LenShapelessRecipe rec = val.Value.ToObject<LenShapelessRecipe>();
                    if (!rec.Enabled) continue;

                    rec.Resolve(api.World, "Len Food recipe " + val.Key);
                    LenRecipeRegistry.Registry.TinkerRecipies.Add(rec);
                    recipeQuantity++;
                }
                if (val.Value is JArray)
                {
                    foreach (var token in (val.Value as JArray))
                    {
                        LenShapelessRecipe rec = token.ToObject<LenShapelessRecipe>();
                        if (!rec.Enabled) continue;

                        rec.Resolve(api.World, "Len Food recipe " + val.Key);
                        LenRecipeRegistry.Registry.TinkerRecipies.Add(rec);

                        recipeQuantity++;
                    }
                }
            }

            api.World.Logger.Event("{0} food recipes from LenStory loaded", recipeQuantity);
            api.World.Logger.StoryEvent("The aroma of sustenance...");
        }
    }
}
