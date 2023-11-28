﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using Vintagestory.ServerMods.NoObf;

namespace LensstoryMod
{

    public class AutoPannerBlock : Block
    {
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if(world.BlockAccessor.GetBlockEntity(blockSel.Position) is AutoPannerBE entity)
            {
                return entity.OnPlayerInteract(byPlayer);
            }
            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
    }

    public class AutoPannerBE : BlockEntity
    {

        Dictionary<string, PanningDrop[]> dropsbymat;
        private bool Powered;

        public ItemStack? contents;
        public bool Working
        {
            get => this.Powered; set
            {
                if (this.Powered != value)
                {
                    if (value && !this.Powered)
                    {
                        this.MarkDirty();
                    }
                    this.Powered = value;
                };
            }
        }
        private double ticker;

        private double LastTickTotalHours;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            Block pan = api.World.GetBlock(AssetLocation.Create("game:pan-wooden"));

            if (pan?.Attributes?["panningDrops"]?.Exists == true)
            {
                dropsbymat = pan.Attributes["panningDrops"].AsObject<Dictionary<string, PanningDrop[]>>();

                foreach (var drops in dropsbymat.Values)
                {
                    for (int i = 0; i < drops.Length; i++)
                    {
                        if (drops[i].Code.Path.Contains("{rocktype}")) continue;
                        drops[i].Resolve(api.World, "panningdrop");
                    }
                }

            }

            RegisterGameTickListener(OnCommonTick, 1000);
        }

        private void OnCommonTick(float dt)
        {
            if (Working && Api.World.Side == EnumAppSide.Server)
            {
                var hourspast = Api.World.Calendar.TotalHours - LastTickTotalHours;
                if(contents != null)
                {
                    ticker += hourspast * 25; 
                    if (ticker >= 1)
                    {
                        int workdone = (int)Math.Floor(ticker);
                        for (var i = 0; i < workdone; i++)
                        {
                            if (Api.World.Rand.Next(100) <= 25)
                            {
                                PanningDrop[] drops = null;
                                foreach (var val in dropsbymat.Keys) //TODO, ensure this works.
                                {
                                    if (WildcardUtil.Match(val, contents.Collectible.Code.ToString()))
                                    {
                                        drops = dropsbymat[val];
                                    }
                                }
                                if(drops == null)
                                {
                                    throw new InvalidOperationException("Coding error, no drops defined for source mat " + contents.Collectible.Code.ToString());
                                }
                                string rocktype = Api.World.GetBlock(new AssetLocation(contents.Block.Code.ToString()))?.Variant["rock"];
                                for (int f = 0; f < drops.Length; f++)
                                {
                                    PanningDrop drop = drops[f];

                                    double rnd = Api.World.Rand.NextDouble();

                                    float extraMul = 1f;

                                    float val = drop.Chance.nextFloat() * extraMul;


                                    ItemStack stack = drop.ResolvedItemstack;

                                    if (drops[f].Code.Path.Contains("{rocktype}"))
                                    {
                                        stack = Resolve(drops[i].Type, drops[f].Code.Path.Replace("{rocktype}", rocktype));
                                    }

                                    if (rnd < val && stack != null)
                                    {
                                        stack = stack.Clone();
                                        Api.World.SpawnItemEntity(stack, Pos.ToVec3d().Add(0.5,-1.1,0.5));
                                        break;
                                    }
                                }
                            }
                            if (Api.World.Rand.Next(100) <= 10)
                            {
                                contents.StackSize--;
                                if(contents.StackSize <=0)
                                {
                                    contents = null;
                                }
                            }
                        }
                        ticker -= workdone;
                        MarkDirty();
                    }
                }
            }
            LastTickTotalHours = Api.World.Calendar.TotalHours;
        }

        private ItemStack Resolve(EnumItemClass type, string code)
        {
            if (type == EnumItemClass.Block)
            {
                Block block = Api.World.GetBlock(new AssetLocation(code));
                if (block == null)
                {
                    Api.World.Logger.Error("Failed resolving panning block drop with code {0}. Will skip.", code);
                    return null;
                }
                return new ItemStack(block);

            }
            else
            {
                Item item = Api.World.GetItem(new AssetLocation(code));
                if (item == null)
                {
                    Api.World.Logger.Error("Failed resolving panning item drop with code {0}. Will skip.", code);
                    return null;
                }
                return new ItemStack(item);
            }
        }

        internal bool OnPlayerInteract(IPlayer player)
        {
            var slot = player.InventoryManager.ActiveHotbarSlot;
            var yea = slot.Itemstack?.Block?.Attributes?.IsTrue("pannable");
            if (contents == null && yea != null && (bool)yea)
            {
                contents = slot.Itemstack.Clone();
                contents.StackSize = 1;

                slot.TakeOut(1);
                slot.MarkDirty();
                MarkDirty();
                return true;
            }
            else if(contents != null && yea != null && (bool)yea)
            {
                if(contents?.Collectible == slot.Itemstack?.Collectible)
                {
                    slot.TakeOut(1);
                    contents.StackSize++;
                    MarkDirty();
                    slot.MarkDirty();
                }
            }
            else if (player.Entity.Controls.ShiftKey && contents != null)
            {
                if(!player.InventoryManager.TryGiveItemstack(contents))
                {
                    Api.World.SpawnItemEntity(contents, Pos.ToVec3d().Add(0.5, -1.1, 0.5));
                }
                contents = null;
                MarkDirty();
                return true;
            }
            return false;
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);
            if (contents != null)
            {
                dsc.AppendLine($"\nContents: {contents?.StackSize}x {contents?.GetName()}");
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetDouble("lastTickTotalHours", this.LastTickTotalHours);
            tree.SetBool("hasitem", contents != null);
            if (contents != null)
            {
                tree.SetItemstack("contents", contents);
            }
            tree.SetDouble("ticker", this.ticker);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            LastTickTotalHours = tree.GetDouble("lastTickTotalHours");
            var hasitem = tree.GetBool("hasitem");
            if (hasitem)
            {
                contents = tree.GetItemstack("contents");
                contents.ResolveBlockOrItem(worldAccessForResolve);
            }
            ticker = tree.GetDouble("ticker");
        }
    }
    public class AutoPannerBhv : BlockEntityBehavior, IManaConsumer
    {
        public AutoPannerBhv(BlockEntity blockEntity) : base(blockEntity)
        {

        }

        public int ToVoid() { if(Blockentity is AutoPannerBE yep) {
                return yep.contents != null ? 1 : 0;
            }return 0; }

        public void EatMana(int mana)
        {
            if (this.Blockentity is AutoPannerBE entity)
            {
                entity.Working = mana == ToVoid();
            }
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);

            dsc.AppendLine("MP: ")
                .AppendLine("Consumes: " + ToVoid());
        }
    }
}
