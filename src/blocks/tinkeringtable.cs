using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using lensstory.src.recipe;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.Common;
using Vintagestory.GameContent;

namespace LensstoryMod
{
    public class TinkeringTable : Block
    {
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is TinkeringTableBe entity)
            {
                return entity.OnPlayerInteract(world, byPlayer, blockSel);
            }
            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
        public override bool DoParticalSelection(IWorldAccessor world, BlockPos pos)
        {
            return true;
        }
    }
    public class TinkeringTableBe : BlockEntityDisplay
    {
        internal InventoryGeneric contents;

        ItemSlot IngASlot { get { return contents[0]; } }
        ItemSlot IngBSlot { get { return contents[1]; } }
        ItemSlot IngCSlot { get { return contents[2]; } }
        ItemSlot IngDSlot { get { return contents[3]; } }

        ItemSlot[] itemSlots
        {
            get
            {
                List<ItemSlot> staccs = new List<ItemSlot>(4);
                for (int i = 0; i < contents.Count; i++)
                {
                    ItemSlot stacc = contents[i];
                    if(stacc == null) { continue; }
                    staccs.Add(stacc);
                }
                return staccs.ToArray();
            }
        }

        public override InventoryBase Inventory => contents;

        public override string InventoryClassName => "TinkeringTable-0";

        public TinkeringTableBe()
        {
            contents = new InventoryDisplayed(this,4, InventoryClassName, null, null);
        }
        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            contents.LateInitialize(InventoryClassName, api);
        }
        public bool OnPlayerInteract(IWorldAccessor world, IPlayer player, BlockSelection blockSel)
        {
            if(world.Side == EnumAppSide.Client) { return true; }
            ItemSlot slot = player.InventoryManager.ActiveHotbarSlot;
            LenShapelessRecipe recipe = FindMatchingRecipe(Api.World, itemSlots);
            if (recipe != null)
            {
                if ((slot.Itemstack?.Collectible?.FirstCodePart() == recipe.ToolType || recipe.ToolType == null) && player.Entity.Controls.Sneak)
                {
                    if(recipe.ToolLoss!=null && recipe.ToolType != null)
                    {
                        int? olddura = slot.Itemstack.Attributes.GetInt("durability",-1);
                        if(olddura == -1)
                        {
                            olddura = slot.Itemstack.Collectible.Durability;
                        }
                        slot.Itemstack.Attributes?.SetInt("durability", (int)olddura - ((int)recipe.ToolLoss*recipe.Output.StackSize));
                        if(slot.Itemstack.Attributes?.GetInt("durability")<=0)
                        {
                            slot.TakeOutWhole();
                        }
                        slot.MarkDirty();
                    }
                    ItemStack?[] bruhmoment = new ItemStack[itemSlots.Length];
                    for(int i = 0; i< itemSlots.Length; i++)
                    {
                        bruhmoment[i] = itemSlots[i].Itemstack?.Clone();
                    }
                    EntityItem? mayhap= (EntityItem)Api.World.SpawnItemEntity(recipe.AttemptCraft(Api, itemSlots), Pos.ToVec3d().Add(0.5, 1.1, 0.5));
                    if(mayhap !=null && mayhap.Itemstack.Collectible is SimpleFoodItem food)
                    {
                        food.OnCreatedTinker(bruhmoment, recipe, ref mayhap);
                    }
                    MarkDirty();
                    return true;
                }
            }
            if (slot != null && !player.Entity.Controls.Sneak)
            {
                if (TryPut(slot, blockSel, player))
                {
                    return true;
                }
            }
            else if (player.Entity.Controls.Sneak)
            {
                return TryTake(player, blockSel);
            }
            return false;
        }

        private bool TryPut(ItemSlot slot, BlockSelection blockSel, IPlayer player)
        {
            int index = blockSel.SelectionBoxIndex;
            bool? maybefit = slot.Itemstack?.Collectible?.CanBePlacedInto(contents[index].Itemstack, contents[index]);
            bool doesfit = maybefit != null ? (bool)maybefit : false;
            if (contents[index].Empty || doesfit)
            {
                int moved = slot.TryPutInto(Api.World, contents[index]);

                if (moved > 0)
                { 
                    updateMeshes();
                    MarkDirty(true);
                }
                return moved > 0;
            }
            return false;
        }

        private bool TryTake(IPlayer player,BlockSelection blockSel)
        {
            int index = blockSel.SelectionBoxIndex;

            if (!contents[index].Empty)
            {
                ItemStack stack = contents[index].TakeOutWhole();
                player.InventoryManager.TryGiveItemstack(stack);
                if (stack.StackSize > 0)
                {
                    Api.World.SpawnItemEntity(stack, Pos.ToVec3d().Add(0.5, 1.5, 0.5));
                }
                updateMesh(index);
                MarkDirty(true);
                return true;
            }

            return false;
        }

        public LenShapelessRecipe FindMatchingRecipe(IWorldAccessor world, ItemSlot[] staccs)
        {
            var recipes = LenRecipeRegistry.Registry.TinkerRecipies;
            if (recipes == null) { return null; }
            for (int i = 0; i < recipes.Count; i++)
            {
                if (recipes[i].Matches(world, staccs)) 
                {
                    return recipes[i];
                }
            }
            return null;
        }

        protected override float[][] genTransformationMatrices()
        {
            float[][] tfMatrices = new float[4][];

            for (int index = 0; index < 4; index++)
            {
                float x = (index % 2 == 0) ? 5 / 16f : 11 / 16f;
                float y = 0.69f; //Nice
                float z = (index > 1) ? 11 / 16f : 5 / 16f;

                tfMatrices[index] =
                    new Matrixf()
                    .Translate(0.5f, 0, 0.5f)
                    .Translate(x - 0.5f, y, z - 0.5f)
                    .Scale(0.75f, 0.75f, 0.75f)
                    .Translate(-0.5f, 0, -0.5f)
                    .Values
                ;
            }

            return tfMatrices;
        }
    }
}
