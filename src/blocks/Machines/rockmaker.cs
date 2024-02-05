using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace LensstoryMod
{
    public class RockmakerBlock : Block
    {
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is RockmakerBE entity)
            {
                return entity.OnPlayerInteract(world,byPlayer,blockSel);
            }
            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
    }
    public class RockmakerBE : BlockEntity
    {
        public ItemStack? contents { get; private set; }
        private bool Powered;
        public bool Working
        {
            get => Powered; set
            {
                if (Powered != value)
                {
                    if (value && !Powered)
                    {
                        MarkDirty();
                    }
                    Powered = value;
                };
            }
        }
        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            contents?.ResolveBlockOrItem(api.World);

            RegisterGameTickListener(OnCommonTick, 1000);
        }
        internal void OnCommonTick(float dt)
        {
            if(Working && contents != null)
            {
                if (Api.World.BlockAccessor.GetBlock(Pos.Copy().Add(0,1,0)).Id == 0)
                {
                    Api.World.BlockAccessor.SetBlock(contents.Id,Pos.Copy().ToVec3d().Add(0, 1, 0).AsBlockPos);
                }
            }
        }
        internal bool OnPlayerInteract(IWorldAccessor world,IPlayer player,BlockSelection blocksel) 
        {
            if (player.Entity.Controls.ShiftKey)
            {

                if (contents == null) { return false; }
                var split = contents.Clone();
                split.StackSize = 1;
                contents.StackSize--;
                if (contents.StackSize <= 0)
                {
                    contents = null;
                }
                if (!player.InventoryManager.TryGiveItemstack(split))
                {
                    world.SpawnItemEntity(split, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
                }
                MarkDirty();
                return true;

            }
            var slot = player.InventoryManager.ActiveHotbarSlot;
            if (slot.Itemstack == null)
            { return false; }
            var maybeblock = slot.Itemstack.Collectible;
            var type = maybeblock.FirstCodePart();
            if (maybeblock != null && (type == "rock" || type == "gravel" || type == "sand" || type == "soil")) 
            {
                contents = slot.Itemstack;
                slot.TakeOut(1);
                slot.MarkDirty();
                MarkDirty();
                return true;
            }
            return false;
        }
        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            if(contents!=null)
            {
                Api.World.SpawnItemEntity(contents, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
            }
            base.OnBlockBroken(byPlayer);
        }
        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);
            if (contents != null)
            {
                var nameJank = contents?.GetName();
                var pe = GetBehavior<RockmakerBhv>().ToVoid();
                dsc.AppendLine("MP:\nConsuming: " + pe);
                dsc.AppendLine($"\nContents: {nameJank}");
            }
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            contents = tree.GetItemstack("contents");
            Working = tree.GetBool("working");
            if(Api != null)
            {
                contents?.ResolveBlockOrItem(Api.World);
            }
        }
        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetItemstack("contents", contents);
            tree.SetBool("working", Powered);
        }

    }
    public class RockmakerBhv : BlockEntityBehavior, IManaConsumer
    {
        public RockmakerBhv(BlockEntity blockentity) : base(blockentity)
        {
        }
        public int ToVoid() 
        {
            if (Blockentity is RockmakerBE entity)
            {
                if (entity.contents == null)
                {
                    return 0;
                }
                switch (entity.contents.Collectible.FirstCodePart())
                {
                    case string x when x == "rock" || x == "gravel" || x == "sand":
                        {
                            return 1;
                        }
                    case "soil":
                        {
                            switch (entity.contents.Collectible.FirstCodePart(1))
                            {
                                case string x when x == "verylow" || x == "low": 
                                    {
                                        return 1;
                                    }
                                case "medium":
                                    {
                                        return 2;
                                    }
                                case "compost":
                                    {
                                        return 4;
                                    }
                                case "high":
                                    {
                                        return 8;
                                    }
                            }
                            return 1;
                        }
                }
            }
            return 0;
        }

        public void EatMana(int mana)
        {
            if (Blockentity is RockmakerBE entity) 
            {
                entity.Working = mana == ToVoid();
            }
        }
    }
}
