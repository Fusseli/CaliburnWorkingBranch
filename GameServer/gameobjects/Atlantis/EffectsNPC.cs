using System;
using System.Collections;
using System.Collections.Generic;
using DOL.GS;
using DOL.GS.PacketHandler;
using DOL.AI.Brain;
using DOL.GS.Effects;
using DOL.Events;
using log4net;
using System.Reflection;
using DOL.Database;

namespace DOL.GS
{
    public class EffectsNPC : GameNPC
    {
        public bool Effect_Enable = true;
        public int Effect_DelayMs = 1000;
        public int Effect_CastTimeSec = 1;
        public int Effect_ID = 1;

        public override bool AddToWorld()
        {
            new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(Effect), 30 * 1000);
            return base.AddToWorld();
        }
        public override void SaveIntoDatabase()
        {
        }

        public int Effect(ECSGameTimer timer)
        {
            DbSpell spell = new DbSpell();
            spell.AllowAdd = false;
            spell.CastTime = Effect_CastTimeSec;
            spell.ClientEffect = Effect_ID;
            spell.Icon = Effect_ID;
            spell.Duration = 0;
            spell.Value = 0;
            spell.Name = "Dexterity Buff";
            spell.Description = "test";
            spell.Range = WorldMgr.VISIBILITY_DISTANCE;
            spell.Target = "Self";
            spell.Type = "DexterityBuff";
            spell.Message1 = null;
            spell.Message2 = null;
            spell.Message3 = null;
            spell.Message4 = null;
            this.CastSpell(new Spell(spell, 0), new SpellLine("NPCSpell", "NPC Spell", "none", false), false);

            new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(Effect), Effect_DelayMs);
            return 0;
        }
    }
}
