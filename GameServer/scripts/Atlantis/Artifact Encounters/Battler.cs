/* Author: BluRaven Date 10/7/2010
 * Notes: The following things from the database are required: nothing.
 * How it's supposed to work:  Battler is a single mob who starts small and weak but grows in size, level and ability with each player he kills.
 * Encounter credit is granted when Battler dies.
 * Adjustable settings: See Respawn mins below.
 * TODO:
*/
#region Using Statements

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using DOL.GS;
using DOL.GS.PacketHandler;
using DOL.AI.Brain;
using DOL.Events;
using DOL.GS.Effects;
using log4net;
using System.Reflection;
using DOL.GS.Atlantis;
using DOL.Database;
using DOL.Language;
using DOL.GS.Spells;

#endregion Using Statements

namespace DOL.GS.Atlantis
{


    public class GameBattler : TetheredEncounterMob
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        [ScriptLoadedEvent]
        public static void ScriptLoaded(DOLEvent e, object sender, EventArgs args)
        {
            log.Info("Spawning Artifact Encounter: Battler");
            SpawnBattler(Albion);
            SpawnBattler(Midgard);
            SpawnBattler(Hibernia);
        }

        public static int Respawnmins = 3600000; //One Hour
        public static int Albion = 73;
        public static int Midgard = 30;
        public static int Hibernia = 130;

        private bool m_hasHeals = false;

        public bool HasHeals
        {
            get { return m_hasHeals; }
            set { m_hasHeals = value; }
        }

        private bool m_hasNukes = false;

        public bool HasNukes
        {
            get { return m_hasNukes; }
            set { m_hasNukes = value; }
        }

        private bool m_hasToughness = false;

        public bool HasToughness
        {
            get { return m_hasToughness; }
            set { m_hasToughness = value; }
        }

        public override bool Interact(GamePlayer player)
        {
            return false;
        }
        public override bool WhisperReceive(GameLiving source, string str)
        {
            return false;
        }
        public override void Die(GameObject killer)
        {
            EncounterMgr.GrantEncounterCredit(killer, true, true, "Battler");
            base.Die(killer);
        }

        public void LevelUp()
        {
            if (this.IsAlive)
            {
                if (this.Level < 76)
                {
                    this.Level++;
                    if (this.Size <= 252)
                    {
                        this.Size = (byte)(this.Size + 3);
                    }
                    this.Health = this.MaxHealth;

                }
                foreach (GamePlayer player in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                {
                    if (player == null) continue;
                    player.Out.SendEmoteAnimation(this, eEmote.LvlUp);
                    player.Out.SendSpellEffectAnimation(this, this, 6152, 0, false, 1);
                }
            }
            return;
        }

        //Gain an ability from the player battler killed.
        public void GainAbility(GamePlayer player)
        {
            int cl = player.CharacterClass.ID;
            switch (cl)
            { 
                case ((int)eCharacterClass.Cabalist):
                case ((int)eCharacterClass.Heretic):
                case ((int)eCharacterClass.Necromancer):
                case ((int)eCharacterClass.Sorcerer):
                case ((int)eCharacterClass.Theurgist):
                case ((int)eCharacterClass.Wizard):
                case ((int)eCharacterClass.Animist):
                case ((int)eCharacterClass.Bainshee):
                case ((int)eCharacterClass.Eldritch):
                case ((int)eCharacterClass.Enchanter):
                case ((int)eCharacterClass.Mentalist):
                case ((int)eCharacterClass.Valewalker):
                case ((int)eCharacterClass.Bonedancer):
                case ((int)eCharacterClass.Runemaster):
                case ((int)eCharacterClass.Spiritmaster):
                case ((int)eCharacterClass.Warlock):
                    {
                        if (!this.HasNukes)
                        {
                            //give battler his nukes!
                            this.HasNukes = true;
                        }
                        break;
                    }
                case ((int)eCharacterClass.Cleric):
                case ((int)eCharacterClass.Friar):
                case ((int)eCharacterClass.Druid):
                case ((int)eCharacterClass.Warden):
                case ((int)eCharacterClass.Healer):
                case ((int)eCharacterClass.Shaman):
                    {
                        if (!this.HasHeals)
                        {
                            //give battler his heals!
                            this.HasHeals = true;
                        }
                        break;
                    }
                case ((int)eCharacterClass.Armsman):
                case ((int)eCharacterClass.MaulerAlb):
                case ((int)eCharacterClass.MaulerHib):
                case ((int)eCharacterClass.MaulerMid):
                case ((int)eCharacterClass.Mercenary):
                case ((int)eCharacterClass.Minstrel):
                case ((int)eCharacterClass.Paladin):
                case ((int)eCharacterClass.Reaver):
                case ((int)eCharacterClass.Bard):
                case ((int)eCharacterClass.Blademaster):
                case ((int)eCharacterClass.Champion):
                case ((int)eCharacterClass.Hero):
                case ((int)eCharacterClass.Vampiir):
                case ((int)eCharacterClass.Berserker):
                case ((int)eCharacterClass.Savage):
                case ((int)eCharacterClass.Skald):
                case ((int)eCharacterClass.Thane):
                case ((int)eCharacterClass.Valkyrie):
                case ((int)eCharacterClass.Warrior):
                case ((int)eCharacterClass.Hunter):
                case ((int)eCharacterClass.Infiltrator):
                case ((int)eCharacterClass.Nightshade):
                case ((int)eCharacterClass.Ranger):
                case ((int)eCharacterClass.Scout):
                case ((int)eCharacterClass.Shadowblade):
                    {
                        if (!this.HasToughness)
                        {
                            //give battler his extra toughness!
                            this.MaxHealth = (this.MaxHealth + this.MaxHealth / 3);
                            this.Constitution = (short)(this.Constitution + this.Constitution / 3);
                            this.Health = this.MaxHealth;
                            this.Strength = (short)(this.Strength + this.Strength / 3);
                            this.HasToughness = true;
                        }
                        break;
                    }
                default:
                    {
                        break;
                    }
            }
            return;
        }

        public override bool AddToWorld()
        {
            Level = 65;
            Size = 37;
            HasHeals = false;
            HasNukes = false;
            HasToughness = false;
            AutoSetStats();
            return base.AddToWorld();
        }

        public static void SpawnBattler(int region)
        {
            //Create and spawn Battler
            GameBattler battler = new GameBattler();
            battler.Model = 1192;
            battler.Size = 37;
            battler.Level = 65;
            battler.Name = "Battler";
            battler.CurrentRegionID = (ushort)region;
            battler.Heading = 3783;
            battler.Realm = 0;
            battler.CurrentSpeed = 0;
            battler.MaxSpeedBase = 200;
            battler.GuildName = "";
            battler.X = 535188;
            battler.Y = 548389;
            battler.Z = 10104;
            battler.RoamingRange = 1000;
            battler.RespawnInterval = Respawnmins;
            battler.TetherRange = 1000;
            battler.BodyType = 0;
            BattlerBrain brain = new BattlerBrain();
            brain.AggroLevel = 100;
            brain.AggroRange = 700;
            battler.SetOwnBrain(brain);
            battler.IsImmuneToMagic = true;
            battler.AddToWorld();
            return;
        }

        private static Spell m_battlerHealSpell = null;
        public static SpellLine BattlerLine = new SpellLine("Battlerspells", "battlerspells", "nospec", true);

        public static Spell BattlerHealSpell
        {
            get
            {
                if (m_battlerHealSpell == null)
                {
                    DbSpell spell = new DbSpell();
                    spell.AllowAdd = false;
                    spell.CastTime = 2;
                    spell.ClientEffect = 1340;
                    spell.Value = 225;
                    spell.Name = "Battler Heal";
                    spell.Range = WorldMgr.VISIBILITY_DISTANCE;
                    spell.SpellID = 90001;
                    spell.Target = "Self";
                    spell.Type = "Heal";
                    spell.Uninterruptible = true;
                    m_battlerHealSpell = new Spell(spell, 50);
                }
                return m_battlerHealSpell;
            }
        }
    }
}

namespace DOL.AI.Brain
{

    public class BattlerBrain : StandardMobBrain
    {
        public BattlerBrain()
            : base()
        {
        }

        public override void Notify(DOLEvent e, object sender, EventArgs args)
        {
            base.Notify(e, sender, args);

            if (e == GameLivingEvent.EnemyKilled)
            {
                EnemyKilledEventArgs eArgs = args as EnemyKilledEventArgs;
                if (eArgs != null)
                {
                    if (eArgs.Target is GameNPC)
                    {
                        return;
                    }
                    GameBattler body = Body as GameBattler;
                    GamePlayer p = eArgs.Target as GamePlayer;
                    body.LevelUp();
                    body.GainAbility(p);
                }
                return;
            }

        }


        public override void Think()
        {
            GameBattler body = Body as GameBattler;

            //// Check if Battler is dead, returning home, stunned, or mezzed, if yes then don't think.
            if (!body.IsAlive) { return; }
            if (body.IsReturningToSpawnPoint) { return; }
            if ((body.IsStunned) || (body.IsMezzed)) { return; }

            //Check for healing
            if (body.HasHeals && body.HealthPercent < 65 && !body.IsCasting)
            {
                CastHealSpell(body);

            }
            //Check for nuke
            if (body.HasNukes && !body.IsCasting && body.HealthPercent > 40 && body.TargetObject != null && body.IsWithinRadius(body.TargetObject, 1500))
            {
                if (GameServer.ServerRules.IsAllowedToAttack(body as GameLiving, body.TargetObject as GameLiving, true))
                {
                    CastNukeSpell(body, body.TargetObject as GameLiving);
                }
            }
            base.Think();
        }

        public static void CastHealSpell(GameBattler battler)
        {

            if (!battler.IsStunned && !battler.IsMezzed)
            {
                battler.StopAttack();
                if (battler.IsMoving)
                {
                    battler.StopFollowing();
                }
                battler.TargetObject = battler;
                battler.CastSpell(GameBattler.BattlerHealSpell, GameBattler.BattlerLine);
            }
        }

        public static void CastNukeSpell(GameBattler battler, GameLiving target)
        {
            battler.TargetObject = target;
            switch (battler.Realm)
            {
                case eRealm.None:
                case eRealm.Albion: LaunchSpell(47, "Pyromancy", battler); break;
                case eRealm.Midgard: LaunchSpell(48, "Runecarving", battler); break;
                case eRealm.Hibernia: LaunchSpell(47, "Way of the Eclipse", battler); break;
            }
        }

        public static void LaunchSpell(int spellLevel, string spellLineName, GameBattler battler)
        {
            if (battler.TargetObject == null)
                return;

            Spell castSpell = null;
            SpellLine castLine = null;

            castLine = SkillBase.GetSpellLine(spellLineName);
            List<Spell> spells = SkillBase.GetSpellList(castLine.KeyName);

            foreach (Spell spell in spells)
            {
                if (spell.Level == spellLevel)
                {
                    castSpell = spell;
                    break;
                }
            }
            if (battler.AttackState)
                battler.StopAttack();
            if (battler.IsMoving)
                battler.StopFollowing();
            battler.TurnTo(battler.TargetObject);
            battler.CastSpell(castSpell, castLine);
        }

        protected override GameLiving CalculateNextAttackTarget()
        {
            GameBattler body = Body as GameBattler;
            GameLiving maxAggroObject = null;
            if (body.HasHeals && body.HasNukes && body.HasToughness)
            {
                return base.CalculateNextAttackTarget();
            }
            bool foundHealer = false;
            bool foundCaster = false;
            bool foundTank = false;
            List<GameLiving> removable = new List<GameLiving>();

            foreach (var pair in AggroList)
            {
                GameLiving living = pair.Key;

                if (!living.IsAlive ||
                    living.ObjectState != GameObject.eObjectState.Active ||
                    living.IsStealthed ||
                    Body.GetDistanceTo(living, 0) > MAX_AGGRO_LIST_DISTANCE)
                {
                    removable.Add(living);
                    continue;
                }

                if (living.EffectList.GetOfType<NecromancerShadeEffect>() != null)
                    continue;

                if (living.CurrentRegion == Body.CurrentRegion)
                {
                    if (!body.HasHeals && living is GamePlayer && !foundHealer)
                    {
                        GamePlayer player = living as GamePlayer;
                        if (player.CharacterClass.ID == (int)eCharacterClass.Cleric || player.CharacterClass.ID == (int)eCharacterClass.Friar
                            || player.CharacterClass.ID == (int)eCharacterClass.Druid || player.CharacterClass.ID == (int)eCharacterClass.Warden
                            || player.CharacterClass.ID == (int)eCharacterClass.Healer || player.CharacterClass.ID == (int)eCharacterClass.Shaman)
                        {
                            maxAggroObject = player;
                            foundHealer = true;
                        }
                    }

                    if (!body.HasNukes && living is GamePlayer && !foundHealer && !foundCaster)
                    {
                        GamePlayer player = living as GamePlayer;
                        if (player.CharacterClass.ID == (int)eCharacterClass.Cabalist || player.CharacterClass.ID == (int)eCharacterClass.Heretic
                            || player.CharacterClass.ID == (int)eCharacterClass.Necromancer || player.CharacterClass.ID == (int)eCharacterClass.Sorcerer
                            || player.CharacterClass.ID == (int)eCharacterClass.Theurgist || player.CharacterClass.ID == (int)eCharacterClass.Wizard
                            || player.CharacterClass.ID == (int)eCharacterClass.Animist || player.CharacterClass.ID == (int)eCharacterClass.Bainshee
                            || player.CharacterClass.ID == (int)eCharacterClass.Eldritch || player.CharacterClass.ID == (int)eCharacterClass.Enchanter
                            || player.CharacterClass.ID == (int)eCharacterClass.Mentalist || player.CharacterClass.ID == (int)eCharacterClass.Valewalker
                            || player.CharacterClass.ID == (int)eCharacterClass.Bonedancer || player.CharacterClass.ID == (int)eCharacterClass.Runemaster
                            || player.CharacterClass.ID == (int)eCharacterClass.Spiritmaster || player.CharacterClass.ID == (int)eCharacterClass.Warlock)
                        {
                            maxAggroObject = player;
                            foundCaster = true;
                        }
                    }

                    if (!body.HasToughness && living is GamePlayer && !foundHealer && !foundCaster && !foundTank)
                    {
                        GamePlayer player = living as GamePlayer;
                        if (player.CharacterClass.ID == (int)eCharacterClass.Armsman || player.CharacterClass.ID == (int)eCharacterClass.MaulerAlb
                            || player.CharacterClass.ID == (int)eCharacterClass.MaulerHib || player.CharacterClass.ID == (int)eCharacterClass.MaulerMid
                            || player.CharacterClass.ID == (int)eCharacterClass.Mercenary || player.CharacterClass.ID == (int)eCharacterClass.Minstrel
                            || player.CharacterClass.ID == (int)eCharacterClass.Paladin || player.CharacterClass.ID == (int)eCharacterClass.Reaver
                            || player.CharacterClass.ID == (int)eCharacterClass.Bard || player.CharacterClass.ID == (int)eCharacterClass.Blademaster
                            || player.CharacterClass.ID == (int)eCharacterClass.Champion || player.CharacterClass.ID == (int)eCharacterClass.Hero
                            || player.CharacterClass.ID == (int)eCharacterClass.Vampiir || player.CharacterClass.ID == (int)eCharacterClass.Berserker
                            || player.CharacterClass.ID == (int)eCharacterClass.Savage || player.CharacterClass.ID == (int)eCharacterClass.Skald
                            || player.CharacterClass.ID == (int)eCharacterClass.Thane || player.CharacterClass.ID == (int)eCharacterClass.Valkyrie
                            || player.CharacterClass.ID == (int)eCharacterClass.Warrior || player.CharacterClass.ID == (int)eCharacterClass.Hunter
                            || player.CharacterClass.ID == (int)eCharacterClass.Infiltrator || player.CharacterClass.ID == (int)eCharacterClass.Nightshade
                            || player.CharacterClass.ID == (int)eCharacterClass.Ranger || player.CharacterClass.ID == (int)eCharacterClass.Scout
                            || player.CharacterClass.ID == (int)eCharacterClass.Shadowblade)
                        {
                            maxAggroObject = player;
                            foundTank = true;
                        }
                    }

                    if (maxAggroObject == null)
                    {
                        maxAggroObject = living;
                    }
                }
            }

            foreach (GameLiving l in removable)
            {
                RemoveFromAggroList(l);
            }

            if (maxAggroObject == null)
            {
                ClearAggroList();
            }

            return maxAggroObject;
        }
    }
}
