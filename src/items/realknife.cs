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
    public class RealKnife : Item
    {

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            handling = EnumHandHandling.PreventDefault;
            return;
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if(secondsUsed > 0.5f) { return false; }
            return true;
            
        }

        public override void OnHeldIdle(ItemSlot slot, EntityAgent byEntity)
        {
            if(byEntity.GetBehavior<EntityBehaviorTemporalStabilityAffected>() != null)
            {
                byEntity.GetBehavior<EntityBehaviorTemporalStabilityAffected>().OwnStability -= .00025;
            }
        }
        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (secondsUsed >= 0.5f) {
                IClientPlayer player = (byEntity.World.PlayerByUid((byEntity as EntityPlayer).PlayerUID) as IClientPlayer);
                bool exerting = slot.Itemstack.Attributes.GetBool("exertion", false);
                slot.Itemstack.Attributes.SetBool("exertion", !exerting);
                if (player == null) { return; }
                player.ShowChatNotification(exerting ? "You aim for the target." : "You aim to strike from afar.");
            }
            return;
        }

        public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            if(slot.Itemstack.Attributes.GetBool("exertion"))
            {
                handling = EnumHandHandling.Handled; return;
            }
            base.OnHeldAttackStart(slot, byEntity, blockSel, entitySel, ref handling);
        }

        public override void OnHeldAttackStop(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel) 
        {
            if(!slot.Itemstack.Attributes.GetBool("exertion")) { base.OnHeldAttackStop(secondsPassed, slot, byEntity, blockSelection, entitySel); return; }
            float damage = 0;
            if (slot.Itemstack.Collectible.Attributes != null)
            {
                damage += slot.Itemstack.Collectible.AttackPower / 3f;
            }
            damage *= byEntity.Stats.GetBlended("meleeWeaponsDamage");
            EntityProperties type = byEntity.World.GetEntityType(new AssetLocation("lensstory:windslashprojectile"));
            var projectile = byEntity.World.ClassRegistry.CreateEntity(type) as EntitySimpleProjectile;
            projectile.FiredBy = byEntity;
            projectile.Damage = damage;

            Vec3d pos = byEntity.ServerPos.XYZ.Add(0, byEntity.LocalEyePos.Y, 0);
            Vec3d aheadPos = pos.AheadCopy(1, byEntity.SidedPos.Pitch, byEntity.SidedPos.Yaw);
            Vec3d velocity = (aheadPos - pos) * 1.25f;

            projectile.ServerPos.SetPos(byEntity.SidedPos.BehindCopy(0.21).XYZ.Add(0, byEntity.LocalEyePos.Y, 0));
            projectile.ServerPos.Motion.Set(velocity);
            projectile.Pos.SetFrom(projectile.ServerPos);
            projectile.World = byEntity.World;
            projectile.SetRotation();

            byEntity.World.SpawnEntity(projectile);
            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) { byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID); }
            byEntity.World.PlaySoundAt(new AssetLocation("lensstory:sounds/slash"),byEntity,byPlayer,false,8);
        }
    }
}
