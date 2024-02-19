using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace LensstoryMod
{
    public class SturdyBucketBlock : BlockLiquidContainerTopOpened
    {
        public override float CapacityLitres => 10;
        protected new WorldInteraction[] interactions;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (api.Side != EnumAppSide.Client)
            { return; }
            var capi = api as ICoreClientAPI;

            interactions = ObjectCacheUtil.GetOrCreate(api, "sturdybucketfilled", () =>
            {
                var liquidContainerStacks = new List<ItemStack>();
                foreach (var obj in api.World.Collectibles)
                {
                    if (obj is ILiquidSource || obj is ILiquidSink || obj is BlockWateringCan)
                    {
                        var stacks = obj.GetHandBookStacks(capi);
                        if (stacks == null)
                        { continue; }

                        foreach (var stack in stacks)
                        {
                            stack.StackSize = 1;
                            liquidContainerStacks.Add(stack);
                        }
                    }
                }
                var lcstacks = liquidContainerStacks.ToArray();
                return new WorldInteraction[] {
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-behavior-rightclickpickup",
                        MouseButton = EnumMouseButton.Right,
                        RequireFreeHand = true
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-bucket-rightclick",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = lcstacks
                    }
                };
            });
        }


        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            if (byPlayer.Entity.Controls.Sneak)
            { return base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode); }
            return false;
        }


        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {
            if (blockSel == null || byEntity.Controls.Sneak)
            { return; }
            var bucketPath = slot.Itemstack.Block.Code.Path;
            var pos = blockSel.Position;
            var block = byEntity.World.BlockAccessor.GetBlock(pos, BlockLayersAccess.Default);

            var contentStack = GetContent(slot.Itemstack);

            if (block.Code.Path.Contains("lava-") && bucketPath.Contains("-empty") && contentStack == null)
            {
                if (block.Code.Path.Contains("-7"))
                {
                    if (api.World.Side == EnumAppSide.Server)
                    {
                        var newblock = byEntity.World.GetBlock(new AssetLocation("lensstory:" + bucketPath.Replace("-empty", "-filled")));
                        var newStack = new ItemStack(newblock);
                        slot.TakeOut(1);
                        slot.MarkDirty();
                        if (!byEntity.TryGiveItemStack(newStack))
                        { api.World.SpawnItemEntity(newStack, byEntity.Pos.XYZ.AddCopy(0, 0.5, 0)); }
                        newblock = byEntity.World.GetBlock(new AssetLocation("lava-still-3"));

                        api.World.BlockAccessor.SetBlock(newblock.BlockId, pos);
                        newblock.OnNeighbourBlockChange(byEntity.World, pos, pos.NorthCopy());
                        api.World.BlockAccessor.TriggerNeighbourBlockUpdate(pos);
                        api.World.BlockAccessor.MarkBlockDirty(pos); 
                    }
                    handHandling = EnumHandHandling.PreventDefault;
                    if ((byEntity as EntityPlayer) != null)  
                    {
                        if (((byEntity as EntityPlayer).Player as IClientPlayer) != null)
                        { ((byEntity as EntityPlayer).Player as IClientPlayer).TriggerFpAnimation(EnumHandInteract.HeldItemInteract); }
                    }
                    return;
                }
            }
            else
            {
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
            }
        }


        public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
        {
            var val = base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack);
            if (val)
            {
                if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is SturdyBucketBE bect)
                {
                    var targetPos = blockSel.DidOffset ? blockSel.Position.AddCopy(blockSel.Face.Opposite) : blockSel.Position;
                    var dx = byPlayer.Entity.Pos.X - (targetPos.X + blockSel.HitPosition.X);
                    var dz = byPlayer.Entity.Pos.Z - (targetPos.Z + blockSel.HitPosition.Z);
                    var angleHor = (float)Math.Atan2(dx, dz);
                    var deg22dot5rad = GameMath.PIHALF / 4;
                    var roundRad = ((int)Math.Round(angleHor / deg22dot5rad)) * deg22dot5rad;
                    bect.MeshAngle = roundRad;
                }
            }
            return val;
        }


        public MeshData GenMesh(ICoreClientAPI capi, string shapePath, ITexPositionSource texture)
        {
            var tesselator = capi.Tesselator;
            var shape = capi.Assets.TryGet(shapePath + ".json").ToObject<Shape>();
            tesselator.TesselateShape(shapePath, shape, out var mesh, texture, new Vec3f(0, 0, 0));
            return mesh;

        }


        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return interactions;
        }


        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return new WorldInteraction[]
            {
                new WorldInteraction()
                {
                    ActionLangCode = "heldhelp-fill",
                    MouseButton = EnumMouseButton.Right,
                    ShouldApply = (wi, bs, es) => GetCurrentLitres(inSlot.Itemstack) < CapacityLitres
                },
                new WorldInteraction()
                {
                    ActionLangCode = "heldhelp-place",
                    HotKeyCode = "sneak",
                    MouseButton = EnumMouseButton.Right,
                    ShouldApply = (wi, bs, es) => true
                }
            };
        }
    }
    public class SturdyBucketBE : BlockEntityLiquidContainer
    {
        public override string InventoryClassName => "metalbucket";
        private MeshData currentMesh;
        private SturdyBucketBlock ownBlock;
        public float MeshAngle;


        public SturdyBucketBE()
        {
            inventory = new InventoryGeneric(1, null, null);
        }


        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            ownBlock = Block as SturdyBucketBlock;
            if (Api.Side == EnumAppSide.Client)
            {
                currentMesh = GenMesh();
                MarkDirty(true);
            }
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);
            if (Api.Side == EnumAppSide.Client)
            {
                currentMesh = GenMesh();
                MarkDirty(true);
            }
        }


        internal MeshData GenMesh()
        {
            if (ownBlock == null)
            { return null; }
            var mesh = ownBlock.GenMesh(Api as ICoreClientAPI, GetContent(), Pos);
            if (mesh.CustomInts != null)
            {
                for (var i = 0; i < mesh.CustomInts.Count; i++)
                {
                    mesh.CustomInts.Values[i] |= 1 << 27; // Disable water wavy
                    mesh.CustomInts.Values[i] |= 1 << 26; // Enabled weak foam
                }
            }
            return mesh;
        }


        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            ITexPositionSource tmpTextureSource;
            MeshData mesh;
            var shapePath = "lensstory:shapes/block/sturdybucket/empty";
            var block = Api.World.BlockAccessor.GetBlock(Pos, BlockLayersAccess.Default) as SturdyBucketBlock;
            if (block != null)
            {
                tmpTextureSource = tesselator.GetTextureSource(block);
                mesh = block.GenMesh(Api as ICoreClientAPI, shapePath, tmpTextureSource);
            }
            else
            {
                tmpTextureSource = tesselator.GetTextureSource(ownBlock);
                mesh = ownBlock.GenMesh(Api as ICoreClientAPI, shapePath, tmpTextureSource);
            }
            mesher.AddMeshData(mesh.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, MeshAngle, 0));

            if (GetContent() != null)
            {
                shapePath = "game:shapes/block/wood/bucket/contents";
                if (GetContent().Block != null)
                {
                    tmpTextureSource = ((ICoreClientAPI)Api).Tesselator.GetTextureSource(GetContent().Block);
                    try
                    {
                        mesh = ownBlock.GenMesh(Api as ICoreClientAPI, GetContent(), Pos);
                        mesher.AddMeshData(mesh.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, MeshAngle, 0));
                    }
                    catch
                    { }
                }
                else if (GetContent().Item != null)
                {
                    tmpTextureSource = ((ICoreClientAPI)Api).Tesselator.GetTextureSource(GetContent().Item);
                    try
                    {
                        mesh = ownBlock.GenMesh(Api as ICoreClientAPI, GetContent(), Pos);
                        mesher.AddMeshData(mesh.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, MeshAngle, 0));
                    }
                    catch { }
                }

            }
            return true;
        }


        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            MeshAngle = tree.GetFloat("meshAngle", MeshAngle);
            if (Api != null)
            {
                if (Api.Side == EnumAppSide.Client)
                {
                    currentMesh = GenMesh();
                    MarkDirty(true);
                }
            }
        }


        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetFloat("meshAngle", MeshAngle);
        }


        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
        {
            var slot = inventory[0];
            if (slot.Empty)
            { sb.AppendLine(Lang.Get("Empty")); }
            else
            { sb.AppendLine(Lang.Get("Contents: {0}x{1}", slot.Itemstack.StackSize, slot.Itemstack.GetName())); }
        }
    }
    public class SturdyBucketFilledBlock : Block
    {
        protected WorldInteraction[] interactions;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (api.Side != EnumAppSide.Client)
            { return; }
            var capi = api as ICoreClientAPI;

            interactions = ObjectCacheUtil.GetOrCreate(api, "sturdybucketfilled", () =>
            {
                var liquidContainerStacks = new List<ItemStack>();
                foreach (var obj in api.World.Collectibles)
                {
                    if (obj is ILiquidSource || obj is ILiquidSink || obj is BlockWateringCan)
                    {
                        var stacks = obj.GetHandBookStacks(capi);
                        if (stacks == null)
                        { continue; }

                        foreach (var stack in stacks)
                        {
                            stack.StackSize = 1;
                            liquidContainerStacks.Add(stack);
                        }
                    }
                }
                var lcstacks = liquidContainerStacks.ToArray();
                return new WorldInteraction[] {
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-behavior-rightclickpickup",
                        MouseButton = EnumMouseButton.Right,
                        RequireFreeHand = true
                    }
                };
            });
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            if (byPlayer.Entity.Controls.Sneak)
            {
                return base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode);
            }
            return false;
        }


        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {
            if (blockSel == null)
            { return; }
            if (byEntity.Controls.Sneak)
            { return; }
            var bucketPath = slot.Itemstack.Block.Code.Path;
            var pos = blockSel.Position;
            var block = byEntity.World.BlockAccessor.GetBlock(pos, BlockLayersAccess.Default);

            if (byEntity.Controls.Sprint && (api.World.Side == EnumAppSide.Server))
            {
                var newblock = byEntity.World.GetBlock(new AssetLocation("lensstory:" + bucketPath.Replace("-filled", "-empty")));
                var newStack = new ItemStack(newblock);
                slot.TakeOut(1);
                slot.MarkDirty();
                if (!byEntity.TryGiveItemStack(newStack))
                {
                    api.World.SpawnItemEntity(newStack, byEntity.Pos.XYZ.AddCopy(0, 0.5, 0));
                }
                newblock = byEntity.World.GetBlock(new AssetLocation("lava-still-7"));
                BlockPos targetPos;
                if (block.IsLiquid())
                { targetPos = pos; }
                else
                { targetPos = blockSel.Position.AddCopy(blockSel.Face); }
                api.World.BlockAccessor.SetBlock(newblock.BlockId, targetPos);
                newblock.OnNeighbourBlockChange(byEntity.World, targetPos, targetPos.NorthCopy());
                api.World.BlockAccessor.TriggerNeighbourBlockUpdate(targetPos);
                api.World.BlockAccessor.MarkBlockDirty(targetPos);
            }

            if ((byEntity as EntityPlayer) != null)
            {
                if (((byEntity as EntityPlayer).Player as IClientPlayer) != null)
                { ((byEntity as EntityPlayer).Player as IClientPlayer).TriggerFpAnimation(EnumHandInteract.HeldItemInteract); }
            }
            handHandling = EnumHandHandling.PreventDefault;
            return;
        }


        public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
        {
            var val = base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack);
            if (val)
            {
                if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is SturdyBucketFilledBE bect)
                {
                    var targetPos = blockSel.DidOffset ? blockSel.Position.AddCopy(blockSel.Face.Opposite) : blockSel.Position;
                    var dx = byPlayer.Entity.Pos.X - (targetPos.X + blockSel.HitPosition.X);
                    var dz = byPlayer.Entity.Pos.Z - (targetPos.Z + blockSel.HitPosition.Z);
                    var angleHor = (float)Math.Atan2(dx, dz);
                    var deg22dot5rad = GameMath.PIHALF / 4;
                    var roundRad = ((int)Math.Round(angleHor / deg22dot5rad)) * deg22dot5rad;
                    bect.MeshAngle = roundRad;
                }
            }
            return val;
        }


        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            Dictionary<int, MeshRef> meshrefs = null;
            if (capi.ObjectCache.TryGetValue("bucketMeshRefs", out var obj))
            { meshrefs = obj as Dictionary<int, MeshRef>; }
            else
            { capi.ObjectCache["bucketMeshRefs"] = meshrefs = new Dictionary<int, MeshRef>(); }
        }

        public override void OnUnloaded(ICoreAPI api)
        {
            if (!(api is ICoreClientAPI capi))
            { return; }
            if (capi.ObjectCache.TryGetValue("bucketMeshRefs", out var obj))
            {
                var meshrefs = obj as Dictionary<int, MeshRef>;
                foreach (var val in meshrefs)
                {
                    val.Value.Dispose();
                }
                capi.ObjectCache.Remove("bucketMeshRefs");
            }
        }


        public MeshData GenMesh(ICoreClientAPI capi)
        {
            var shape = capi.Assets.TryGet("lensstory:shapes/block/sturdybucket/filled.json").ToObject<Shape>();
            capi.Tesselator.TesselateShape(this, shape, out var bucketmesh);
            return bucketmesh;
        }


        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return new WorldInteraction[]
            {
                new WorldInteraction()
                {
                    ActionLangCode = "heldhelp-empty",
                    HotKeyCode = "sprint",
                    MouseButton = EnumMouseButton.Right,
                    ShouldApply = (wi, bs, es) => true
                },
                new WorldInteraction()
                {
                    ActionLangCode = "heldhelp-place",
                    HotKeyCode = "sneak",
                    MouseButton = EnumMouseButton.Right,
                    ShouldApply = (wi, bs, es) => true
                }
            };
        }
    }
    public class SturdyBucketFilledBE : BlockEntity
    {
        internal InventoryGeneric inventory;
        private MeshData currentMesh;
        private SturdyBucketFilledBlock ownBlock;
        public float MeshAngle;

        public SturdyBucketFilledBE()
        {
            inventory = new InventoryGeneric(1, null, null);
        }


        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            ownBlock = Block as SturdyBucketFilledBlock;
            if (Api.Side == EnumAppSide.Client)
            {
                currentMesh = GenMesh();
                MarkDirty(true);
            }
        }
        public override void OnBlockBroken(IPlayer forPlayer){}
        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);
            if (Api.Side == EnumAppSide.Client)
            {
                currentMesh = GenMesh();
                MarkDirty(true);
            }
        }


        public ItemStack GetContent()
        {
            return inventory[0].Itemstack;
        }


        internal void SetContent(ItemStack stack)
        {
            inventory[0].Itemstack = stack;
            MarkDirty(true);
        }


        internal MeshData GenMesh()
        {
            if (ownBlock == null)
            { return null; }
            var mesh = ownBlock.GenMesh(Api as ICoreClientAPI);
            if (mesh.CustomInts != null)
            {
                for (var i = 0; i < mesh.CustomInts.Count; i++)
                {
                    mesh.CustomInts.Values[i] |= 1 << 27;
                    mesh.CustomInts.Values[i] |= 1 << 26;
                }
            }
            return mesh;
        }


        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            if (currentMesh != null)
            {
                mesher.AddMeshData(currentMesh.Clone().Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, MeshAngle, 0));
            }
            return true;
        }


        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            MeshAngle = tree.GetFloat("meshAngle", MeshAngle);
            if (Api != null)
            {
                if (Api.Side == EnumAppSide.Client)
                {
                    currentMesh = GenMesh();
                    MarkDirty(true);
                }
            }
        }
        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetFloat("meshAngle", MeshAngle);
        }
    }
}
