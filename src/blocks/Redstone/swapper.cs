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
    public class SwapperBE : BlockEntity
    {
        public bool toggle = false;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            GetBehavior<Redstone>().begin(true);
        }
        public void SwapBlocks(bool activate)
        {
            if (activate != toggle) { return; }
            var Ori = Block.Variant["orientation"] is string v ? string.Intern(v) : null;
            switch (Ori)
            {
                case "ud":
                    {
                        DoTheSwap(Pos.UpCopy(),Pos.DownCopy());
                        break;
                    }
                case "we":
                    {
                        DoTheSwap(Pos.EastCopy(),Pos.WestCopy());
                        break;
                    }
                case "ns":
                    {
                        DoTheSwap(Pos.NorthCopy(),Pos.SouthCopy());
                        break;
                    }
                default:
                    {
                        break;
                    }
            }
        }
        public void DoTheSwap(BlockPos one, BlockPos two)
        {
            if(Api.World.BlockAccessor.GetBlock(Pos).FirstCodePart() != "blockswapper") { return; }
            if (Api.World.BlockAccessor.GetBlockEntity(one) != null || Api.World.BlockAccessor.GetBlockEntity(two) != null)
            { return; }

            Block alpha = Api.World.BlockAccessor.GetBlock(one);
            Block omega = Api.World.BlockAccessor.GetBlock(two);

            Api.World.BlockAccessor.SetBlock(alpha.BlockId, two);
            Api.World.BlockAccessor.SetBlock(omega.BlockId, one);

            toggle = !toggle;
        }
        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetBool("wason", toggle);
        }
        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            toggle = tree.GetBool("wason");
        }
    }
    public class SwapperBhv : BlockEntityBehavior, IRedstoneTaker
    {
        public SwapperBhv(BlockEntity block) : base(block)
        {
        }

        public void OnSignal(bool Activated)
        {
            if(Blockentity is SwapperBE swapper && Api.Side == EnumAppSide.Server)
            {
                swapper.SwapBlocks(Activated);
            }
        }
    }

    //Diag Variant
    public class SwapperDiagBE : BlockEntity
    {
        public bool toggle = false;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            GetBehavior<Redstone>().begin(true);
        }
        public void SwapBlocks(bool activate)
        {
            if (activate != toggle) { return; }
            var Ori = Block.Variant["orientation"] is string v ? string.Intern(v) : null;
            var side = Block.Variant["side"] is string x ? string.Intern(x) : null;
            switch (Ori) //NIGHTMARE NIGHTMARE NIGHTMARE
            {
                case "north":
                    {
                        switch (side)
                        {
                            case "east": 
                                {
                                    DoTheSwap(Pos.EastCopy(), Pos.NorthCopy());
                                    break;
                                }
                            case "west":
                                {
                                    DoTheSwap(Pos.WestCopy(), Pos.NorthCopy());
                                    break;
                                }
                            case "up":
                                {
                                    DoTheSwap(Pos.UpCopy(), Pos.NorthCopy());
                                    break;
                                }
                            case "down":
                                {
                                    DoTheSwap(Pos.DownCopy(), Pos.NorthCopy());
                                    break;
                                }
                        }
                        break;
                        
                    }
                case "east":
                    {
                        switch (side)
                        {
                            case "south":
                                {
                                    DoTheSwap(Pos.SouthCopy(), Pos.EastCopy());
                                    break;
                                }
                            case "north":
                                {
                                    DoTheSwap(Pos.EastCopy(), Pos.NorthCopy());
                                    break;
                                }
                            case "up":
                                {
                                    DoTheSwap(Pos.UpCopy(), Pos.EastCopy());
                                    break;
                                }
                            case "down":
                                {
                                    DoTheSwap(Pos.DownCopy(), Pos.EastCopy());
                                    break;
                                }
                        }
                        break;
                    }
                case "south":
                    {
                        switch (side)
                        {
                            case "east":
                                {
                                    DoTheSwap(Pos.EastCopy(), Pos.SouthCopy());
                                    break;
                                }
                            case "west":
                                {
                                    DoTheSwap(Pos.WestCopy(), Pos.SouthCopy());
                                    break;
                                }
                            case "up":
                                {
                                    DoTheSwap(Pos.UpCopy(), Pos.SouthCopy());
                                    break;
                                }
                            case "down":
                                {
                                    DoTheSwap(Pos.DownCopy(), Pos.SouthCopy());
                                    break;
                                }
                        }
                        break;
                    }
                case "west":
                    {
                        switch (side)
                        {
                            case "south":
                                {
                                    DoTheSwap(Pos.WestCopy(), Pos.SouthCopy());
                                    break;
                                }
                            case "north":
                                {
                                    DoTheSwap(Pos.WestCopy(), Pos.NorthCopy());
                                    break;
                                }
                            case "up":
                                {
                                    DoTheSwap(Pos.UpCopy(), Pos.WestCopy());
                                    break;
                                }
                            case "down":
                                {
                                    DoTheSwap(Pos.DownCopy(), Pos.WestCopy());
                                    break;
                                }
                        }
                        break;
                    }
                case "up":
                    {
                        switch (side)
                        {
                            case "north":
                                {
                                    DoTheSwap(Pos.UpCopy(), Pos.NorthCopy());
                                    break;
                                }
                            case "east":
                                {
                                    DoTheSwap(Pos.NorthCopy(), Pos.UpCopy());
                                    break;
                                }
                            case "south":
                                {
                                    DoTheSwap(Pos.UpCopy(), Pos.SouthCopy());
                                    break;
                                }
                            case "west":
                                {
                                    DoTheSwap(Pos.UpCopy(), Pos.WestCopy());
                                    break;
                                }
                        }
                        break;
                    }
                case "down": {
                        switch (side)
                        {
                            case "north":
                                {
                                    DoTheSwap(Pos.DownCopy(), Pos.NorthCopy());
                                    break;
                                }
                            case "east":
                                {
                                    DoTheSwap(Pos.NorthCopy(), Pos.DownCopy());
                                    break;
                                }
                            case "south":
                                {
                                    DoTheSwap(Pos.DownCopy(), Pos.SouthCopy());
                                    break;
                                }
                            case "west":
                                {
                                    DoTheSwap(Pos.DownCopy(), Pos.WestCopy());
                                    break;
                                }
                        }
                        break;
                    }
                default:
                    {
                        break;
                    }
            }
        }
        public void DoTheSwap(BlockPos one, BlockPos two)
        {
            if (Api.World.BlockAccessor.GetBlockEntity(one) != null || Api.World.BlockAccessor.GetBlockEntity(two) != null)
            { return; }

            Block alpha = Api.World.BlockAccessor.GetBlock(one);
            Block omega = Api.World.BlockAccessor.GetBlock(two);

            Api.World.BlockAccessor.SetBlock(alpha.BlockId, two);
            Api.World.BlockAccessor.SetBlock(omega.BlockId, one);

            toggle = !toggle;
        }
        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetBool("wason", toggle);
        }
        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            toggle = tree.GetBool("wason");
        }
    }
    public class SwapperDiagBhv : BlockEntityBehavior, IRedstoneTaker
    {
        public SwapperDiagBhv(BlockEntity block) : base(block)
        {
        }

        public void OnSignal(bool Activated)
        {
            if (Blockentity is SwapperDiagBE swapper && Api.Side == EnumAppSide.Server)
            {
                swapper.SwapBlocks(Activated);
            }
        }
    }
}
