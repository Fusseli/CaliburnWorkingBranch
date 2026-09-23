using DOL.AI.Brain;
using DOL.Database;
using DOL.GS;
using DOL.GS.RealmAbilities;
using DOL.GS.ServerProperties;
using System;

namespace DOL.GS.Scripts
{
    public class ArcherBrain : MimicBrain
    {
        public ArcherBrain()
        { }

        /// <summary>Last seen archery mode. Flips re-run the spec refresh so a live
        /// admin flag change grants (or stops granting) bow abilities like a relog.</summary>
        private bool _lastArcheryFlag = true;

        public override void OnLeaderAggro()
        {
            Body.Stealth(true);
        }

        public override void OnRefreshSpecDependantSkills()
        {
            GrantBowAbilities();
        }

        public override void SelectGoapAttackTarget()
        {
            base.SelectGoapAttackTarget();
            UpdateRangedShotType();
        }

        /// <summary>Mirror the player bow-ability grants (OnSkillTrained) for old archery:
        /// Critical Shot, Rapid Fire, SureShot and Penetrating Arrow by bow spec.
        /// Gated on the old flag exactly like players, who learn nothing new under
        /// new archery. Penetrating Arrow is passive once granted; SureShot is granted
        /// for completeness but not triggered (its accuracy half lives in player code).</summary>
        public void GrantBowAbilities()
        {
            if (Body == null || ServerProperties.Properties.ALLOW_OLD_ARCHERY != true)
                return;
            int bow = Math.Max(Body.GetModifiedSpecLevel(Specs.Longbow),
                Math.Max(Body.GetModifiedSpecLevel(Specs.CompositeBow),
                    Body.GetModifiedSpecLevel(Specs.RecurveBow)));
            if (bow < 3)
                return;
            Grant(Abilities.Critical_Shot, bow >= 27 ? 9 : Math.Max(1, bow / 3));
            if (bow >= 35)
                Grant(Abilities.RapidFire, bow >= 45 ? 2 : 1);
            if (bow >= 45)
                Grant(Abilities.SureShot);
            if (bow >= 30)
                Grant(Abilities.PenetratingArrow, bow >= 50 ? 3 : bow >= 40 ? 2 : 1);
        }

        private void Grant(string key, int level = 0)
        {
            var ab = level > 0 ? SkillBase.GetAbility(key, level) : SkillBase.GetAbility(key);
            if (ab == null)
                return;
            var existing = Body.GetAbility(key);
            if (existing == null || existing.Level < ab.Level)
                Body.AddAbility(ab, false);
        }

        /// <summary>Pick the shot type before firing. The engine consumes it per shot
        /// (back to Normal), so this runs every decision: stealthed opener goes Critical,
        /// Rapid Fire pressures enemy casters at range. New-archery mode always fires
        /// Normal. Also watches the admin flag and refreshes grants shortly after a flip.</summary>
        public void UpdateRangedShotType()
        {
            if (Body == null)
                return;
            bool oldArchery = ServerProperties.Properties.ALLOW_OLD_ARCHERY;
            if (oldArchery != _lastArcheryFlag)
            {
                _lastArcheryFlag = oldArchery;
                MimicBody.RefreshSpecDependantSkills(false);
            }
            if (!oldArchery)
                return;
            var rac = Body.rangeAttackComponent;
            if (rac == null)
                return;
            var target = Body.TargetObject as GameLiving;
            if (Body.ActiveWeaponSlot != eActiveWeaponSlot.Distance || target == null || !target.IsAlive)
                return;
            if (Body.IsStealthed && Body.HasAbility(Abilities.Critical_Shot))
                rac.RangedAttackType = eRangedAttackType.Critical;
            else if (!Body.IsStealthed && Body.HasAbility(Abilities.RapidFire) && target.IsCasting)
                rac.RangedAttackType = eRangedAttackType.RapidFire;
        }

        public override bool CheckSpells(eCheckSpellType type)
        {
            if (type == eCheckSpellType.Defensive)
            {
                UpdateRangedShotType();
                if (base.CheckSpells(type))
                    return true;

                if (Body.Group == null || Body.Group.MimicGroup.CampPoint != null && !MimicBody.MimicBrain.IsMainPuller)
                    Body.Stealth(true);
                else
                    Body.Stealth(false);

                if (Body.ControlledBrain != null && PvPMode)
                    MimicBody.CommandNpcRelease();

                return false;
            }

            return base.CheckSpells(type);
        }

        protected override bool CheckInstantOffensiveSpells(Spell spell)
        {
            if (Body.IsStealthed)
                return false;

            return base.CheckInstantOffensiveSpells(spell);
        }
    }
}
