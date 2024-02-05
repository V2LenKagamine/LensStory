using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace LensstoryMod
{
    public class KingsSword : ItemSword
    {
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
            if(handling == EnumHandHandling.PreventDefault) { return; }
            handling = EnumHandHandling.PreventDefault;
        }
        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (byEntity.World is IClientWorldAccessor)
            {
                return secondsUsed <= 5.1f;
            }
            return true;
        }
        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (secondsUsed < 4) return;
            float damage = 0;
            if(slot.Itemstack.Collectible.Attributes != null)
            {
                damage += slot.Itemstack.Collectible.AttackPower * 3;
            }

            EntityProperties type = byEntity.World.GetEntityType(new AssetLocation("lensstory:kingsswordprojectile"));
            var projectile = byEntity.World.ClassRegistry.CreateEntity(type) as EntitySimpleProjectile;
            projectile.FiredBy = byEntity;
            projectile.Damage = damage;

            Vec3d pos = byEntity.ServerPos.XYZ.Add(0, byEntity.LocalEyePos.Y, 0);
            Vec3d aheadPos = pos.AheadCopy(1, byEntity.SidedPos.Pitch, byEntity.SidedPos.Yaw);
            Vec3d velocity = (aheadPos - pos) * .5;

            projectile.ServerPos.SetPos(byEntity.SidedPos.BehindCopy(0.21).XYZ.Add(0, byEntity.LocalEyePos.Y, 0));
            projectile.ServerPos.Motion.Set(velocity);
            projectile.Pos.SetFrom(projectile.ServerPos);
            projectile.World = byEntity.World;

            byEntity.World.SpawnEntity(projectile);

            slot.Itemstack.Collectible.DamageItem(byEntity.World, byEntity, slot,5);
        }
    }
}
