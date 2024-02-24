using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using Microsoft.VisualBasic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using static System.Formats.Asn1.AsnWriter;

namespace LensstoryMod
{

    public class TemporalGiftItem : Item
    {
        public override void OnHeldUseStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumHandInteract useType, bool firstEvent, ref EnumHandHandling handling)
        {
            handling = EnumHandHandling.Handled;
            if(api.World.Side == EnumAppSide.Server)
            {
                if (!slot.Itemstack.Attributes.HasAttribute("gifts")) { return; }
                var itemattr = ((TreeArrayAttribute)slot.Itemstack.Attributes.GetTreeAttribute("gifts")["itemlist"])?.value;
                foreach (var thing in itemattr)
                {

                    var code = thing.GetString("type");
                    ItemStack yep;
                    switch (code)
                    {
                        case string x when x == "block" || x == "Block":
                            {
                                yep = new(api.World.GetBlock(new AssetLocation(thing.GetString("code"))), thing.GetAsInt("amount", 1));
                                break;
                            }
                        case string x when x == "item" || x == "Item":
                            {
                                yep = new(api.World.GetItem(new AssetLocation(thing.GetString("code"))), thing.GetAsInt("amount", 1));
                                break;
                            }

                        default:
                            { continue; }
                    }
                    var didgive = byEntity.TryGiveItemStack(yep);
                    if(!didgive)
                    {
                        api.World.SpawnItemEntity(yep,byEntity.Pos.Copy().Add(0.5f,0.5f,0.5f).AsBlockPos.ToVec3d());
                    }
                }
                slot.TakeOutWhole();
                slot.MarkDirty();
            }
        }

    }

    public class TemporalGiftBlock : Block
    {
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            TemporalGiftBe bea = world.BlockAccessor.GetBlockEntity(blockSel.Position) as TemporalGiftBe;
            if (bea != null)
            {
                return bea.OnPlayerInteract(world, byPlayer, blockSel);
            }
            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
        public override void OnBlockPlaced(IWorldAccessor world, BlockPos blockPos, ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(world, blockPos, byItemStack);
            if (byItemStack == null) { return; }

            TemporalGiftBe bea = world.BlockAccessor.GetBlockEntity<TemporalGiftBe>(blockPos);

            if (bea != null)
            {
                if (!byItemStack.Attributes.HasAttribute("gifts")) { return; }
                var itemattr = ((TreeArrayAttribute)byItemStack.Attributes.GetTreeAttribute("gifts")["itemlist"])?.value;
                foreach (var thing in itemattr)
                {

                    var code = thing.GetString("type");
                    bea.inventory.AddSlots(1);
                    ItemStack yep;
                    switch (code)
                    {
                        case string x when x == "block" || x == "Block": {
                                yep = new(world.GetBlock(new AssetLocation(thing.GetString("code"))), thing.GetAsInt("amount",1));
                                break;
                            }
                        case string x when x == "item" || x == "Item":
                            {
                                yep = new(world.GetItem(new AssetLocation(thing.GetString("code"))), thing.GetAsInt("amount",1));
                                break;
                            }
                            
                        default:
                            { continue; }
                    }
                    bea.inventory.Last().Itemstack = yep;
                    bea.inventory.Last().Itemstack.ResolveBlockOrItem(world);
                    bea.inventory.Last().MarkDirty();
                    
                }
            }
            bea.primed = true;
            bea.MarkDirty();
        }
    }

    public class TemporalGiftBe : BlockEntity
    {
        public InventoryGeneric inventory;
        public bool primed;
        public TemporalGiftBe()
        {
            inventory=new(1, "lentemporalgift-1", null, null);
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            inventory.LateInitialize("lentemporalgift-1", api);
        }

        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            foreach (var item in inventory)
            {
                if (item.Itemstack != null)
                {
                    Api.World.SpawnItemEntity(item.Itemstack, Pos.ToVec3d().Add(0.5f, 0.2f, 0.5f));
                }
            }
            base.OnBlockBroken(byPlayer);
        }

        public bool OnPlayerInteract(IWorldAccessor world, IPlayer byplayer,BlockSelection blocksel)
        {
            if (byplayer.Entity.Controls.ShiftKey && !primed)
            {
                if(byplayer.InventoryManager.ActiveHotbarSlot.Itemstack == null)
                {
                    primed = true;
                    MarkDirty();
                    return true;
                }
                var theboi = inventory.First(itemslot => itemslot.Itemstack == null || itemslot.Itemstack.Collectible == byplayer.InventoryManager.ActiveHotbarSlot.Itemstack.Collectible);
                if(theboi == null) { inventory.AddSlots(1); theboi = inventory.Last(); }
                var moved = byplayer.InventoryManager.ActiveHotbarSlot.TryPutInto(world, theboi);
                if(moved > 0)
                {
                    byplayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                    theboi.MarkDirty();
                    MarkDirty();
                }
            }
            else if (primed)
            {
                foreach (var item in inventory)
                {
                    if (item.Itemstack != null)
                    {
                        world.SpawnItemEntity(item.Itemstack, Pos.ToVec3d().Add(0.5f, 0.2f, 0.5f));
                        item.Itemstack = null;
                        item.MarkDirty();
                    }
                }
                primed = false;
                MarkDirty();
                return true;
            }
            return true;
        }
        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            inventory.FromTreeAttributes(tree);
            primed = tree.GetBool("primed");
        }
        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            inventory.ToTreeAttributes(tree);
            tree.SetBool("primed", primed);
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);
            if(primed)
            {
                dsc.AppendLine("Looks like something's inside!");
            }
            else
            {
                dsc.AppendLine("Empty. Add something by shifting, or prime with shift + empty hand");
            }
        }

    }

}
