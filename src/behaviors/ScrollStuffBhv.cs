
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace LensstoryMod
{
    public class ScrollStuffBhv : EntityBehavior
    {
        public ScrollStuffBhv(Entity entity) : base(entity)
        {
        }

        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            IServerPlayer P = this.entity.World.PlayerByUid((this.entity as EntityPlayer).PlayerUID) as IServerPlayer;

            ScrollEffect scroll = new ScrollEffect();
            scroll.removeAll((P.Entity), "lensmod");

            base.OnEntityDeath(damageSourceForDeath);
        }

        public override string PropertyName()
        {
            return "ScrollRemoveBhv";
        }
    }

    public class ScrollEffect
    {
        EntityPlayer affected;

        Dictionary<string, float> effectPowerList;

        string effectCode;

        string effectID;

        int statDuration = 0;

        public void ScrollStats(EntityPlayer entity,Dictionary<string,float> effectlist,string code,string id,int duration = 0)
        {
            affected = entity;
            effectPowerList = effectlist;
            effectCode = code;
            effectID = id;
            if(effectlist.Count >= 1)
            {
                applyStats();
            }
            if (duration > 0)
            {
                long dissapateCallback = affected.World.RegisterCallback(DissapateEffect, duration * 1000);
                affected.WatchedAttributes.SetLong(effectID, dissapateCallback);
                statDuration = duration;
            }
        }

        public void DissapateEffect(float dt) 
        {
            Dissapate();
        }

        public void Dissapate()
        {
            foreach(KeyValuePair<string,float> stat in effectPowerList)
            {
                affected.Stats.Remove(stat.Key, effectCode);
            }
            affected.WatchedAttributes.RemoveAttribute(effectID);
            IServerPlayer player = (
               affected.World.PlayerByUid((affected).PlayerUID)
               as IServerPlayer
           );
            player.SendMessage(
                GlobalConstants.InfoLogChatGroup,
                "You feel your body shift, as a temporary effect dissapates.",
                EnumChatType.Notification
            );
        }

        public void applyStats()
        {
            foreach(KeyValuePair<string,float> stat in effectPowerList)
            {
                affected.Stats.Set(stat.Key,effectCode,stat.Value,true);
            }
        }

        public void removeAll(EntityPlayer player,string code)
        {
            foreach(var stat in player.Stats)
            {
                player.Stats.Remove(stat.Key, code);
            }
            player.GetBehavior<EntityBehaviorHealth>().MarkDirty();
        }
    }

}
