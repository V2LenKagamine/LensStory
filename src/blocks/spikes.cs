using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace LensstoryMod
{
    public class SpikesBlock : Block
    {
        public override void OnEntityCollide(IWorldAccessor world, Entity entity, BlockPos pos, BlockFacing facing, Vec3d collideSpeed, bool isImpact)
        {
            if (world.Side == EnumAppSide.Server)
            {
                base.OnEntityCollide(world, entity, pos, facing, collideSpeed, isImpact);

                float pain = Attributes?["pain"].AsFloat() != null? Attributes["pain"].AsFloat(1) : 1;
                if (isImpact && facing.Axis == EnumAxis.Y)
                {
                    if (entity.Alive)
                    {
                        var dmg = (float)Math.Abs((collideSpeed.Y * pain) + pain);
                        entity.ReceiveDamage(new DamageSource() { Source = EnumDamageSource.Block, SourceBlock = this, Type = EnumDamageType.PiercingAttack, SourcePos = pos.ToVec3d() }, dmg);
                    }
                }
                else if (entity.Alive)
                {
                    pain /= 5;
                    entity.ReceiveDamage(new DamageSource() { Source = EnumDamageSource.Block, SourceBlock = this, Type = EnumDamageType.PiercingAttack, SourcePos = pos.ToVec3d() }, pain);;
                }
            }
        }
        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
            float pain = Attributes?["pain"].AsFloat() != null ? Attributes["pain"].AsFloat(1) : 1;
            dsc.AppendLine(string.Format("Does {0} + entity fall speed times {0}; or {1} on touch.",pain,pain*(1f/5f)));
        }
    }
}