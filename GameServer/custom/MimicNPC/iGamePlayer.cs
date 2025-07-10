using DOL.AI.Brain;
using DOL.Database;
using DOL.Events;
using DOL.GS.Effects;
using DOL.GS.PacketHandler;
using DOL.GS.PropertyCalc;
using DOL.GS.Utils;
using System;
using System.Collections.Generic;
using static DOL.GS.GameObject;
using static DOL.GS.GamePlayer;

namespace DOL.GS.Scripts
{
    public interface IGamePlayer
    {
        public IPacketLib Out { get; }
        public GameClient Client { get; }
        public void Notify(DOLEvent e, object sender);
        public string InternalID { get; set; }
        public eObjectState ObjectState { get; set; }

        public int GetModified(eProperty property);

        public int ChangeHealth(GameObject changeSource, eHealthChangeType healthChangeType, int changeAmount);
        public int CalculateMaxHealth(int level, int constitution);
        public int CalculateMaxMana(int level, int manaStat);
        public int GetBaseStat(eStat stat);

        public List<Tuple<Skill, Skill>> GetAllUsableSkills(bool update = false);
        public List<Tuple<SpellLine, List<Skill>>> GetAllUsableListSpells(bool update = false);
        public SpellLine GetSpellLine(string keyname);
        public bool HasAbility(string keyName);
        public T GetAbility<T>() where T : Ability;
        public int GetAbilityLevel(string keyName);
        public bool HasSpecialization(string keyName);
        public int GetBaseSpecLevel(string keyName);
        public int GetWeaponStat(DbInventoryItem weapon);
        public double GetWeaponSkill(DbInventoryItem weapon);
        public int GetModifiedSpecLevel(string keyName);
        public void DisableSkill(Skill skill, int duration);

        public IPropertyIndexer AbilityBonus { get; }

        public eArmorSlot CalculateArmorHitLocation(AttackData ad);
        public double WeaponDamageWithoutQualityAndCondition(DbInventoryItem weapon);
        public double ApplyWeaponQualityAndConditionToDamage(DbInventoryItem weapon, double damage);

        public void Stealth(bool goStealth);
        public void StartStealthUncoverAction();
        public void StopStealthUncoverAction();
        public bool Sprint(bool state);
        public void StopCurrentSpellcast();
        public void StartInterruptTimer(int duration, AttackData.eAttackType attackType, GameLiving attacker);

        public void OnDuelStart(GameDuel duel);
        public void OnDuelStop();
        public GameLiving DuelPartner { get; }
        public GameDuel Duel { get; }
        public bool IsDuelPartner(GameLiving living);
        public bool IsDuelReady { get; set; }

        public void AddXPGainer(GameObject xpGainer, float damageAmount);
        public void GainRealmPoints(long amount, bool modify);
        public int GetDistanceTo(IPoint3D point);
        public int GetDistance(IPoint2D point);
        public bool CanSeeObject(GameObject obj);
        public bool IsWithinRadius(GameObject obj, int radius);
        public bool IsWithinRadius(IPoint3D point, int radius, bool ignoreZ);
        public List<GamePlayer> GetPlayersInRadius(ushort radiusToCheck);
        public List<GameNPC> GetNPCsInRadius(ushort radiusToCheck);
        public int GetConLevel(GameObject compare);

        public void PickupObject(GameObject floorObject, bool checkRange);
        public void AddMoney(long money);
        public void AddMoney(long money, string messageFormat);
        public void AddMoney(long money, string messageFormat, eChatType ct, eChatLoc cl);
        public bool RemoveMoney(long money);
        public long GetCurrentMoney();

        public bool IsControlledNPC(GameNPC npc);
        public void CommandNpcRelease();

        public RangeAttackComponent RangeAttackComponent { get; }
        public AttackComponent AttackComponent { get; }
        public StyleComponent StyleComponent { get; }
        public EffectListComponent EffectListComponent { get; }
        public GameEffectList EffectList { get; }
        public PropertyCollection TempProperties { get; }
        public IPropertyIndexer ItemBonus { get; }
        public IPropertyIndexer BaseBuffBonusCategory { get; }
        public IPropertyIndexer SpecBuffBonusCategory { get; }
        public IPropertyIndexer DebuffCategory { get; }
        public IPropertyIndexer BuffBonusCategory4 { get; }

        public GameObject TargetObject { get; set; }

        public Group Group { get; }
        public Guild Guild { get; set; }

        public IGameInventory Inventory { get; set; }
        public DbInventoryItem ActiveWeapon { get; }
        public eActiveWeaponSlot ActiveWeaponSlot { get; }

        public bool Autoloot { get; set; }
        public bool AutoSplitLoot { get; set; }
        public bool CanUseCrossRealmItems { get; }
        public int OutOfClassROGPercent { get; set; }

        public IControlledBrain ControlledBrain { get; set; }

        public PlayerDeck RandomNumberDeck { get; set; }
        public List<int> SelfBuffChargeIDs { get; }
        public int TotalConstitutionLostAtDeath { get; set; }

        public int SpellInterruptDuration { get; }

        public string GetName(int article, bool firstLetterUppercase);
        public ICharacterClass CharacterClass { get; }
        public eGender Gender { get; set; }
        public short Race { get; set; }
        public string Name { get; set; }
        public byte Level { get; set; }
        public byte MaxLevel { get; }
        public int RealmLevel { get; set; }
        public long RealmPoints { get; set; }
        public long BountyPoints { get; set; }
        public int MLLevel { get; set; }
        public eRealm Realm { get; set; }
        public bool Champion { get; set; }
        public int ChampionLevel { get; set; }

        public double Effectiveness { get; set; }
        public double SpecLock { get; set; }

        public bool IsAlive { get; }
        public bool InCombat { get; }
        public bool IsAttacking { get; }
        public bool IsCasting { get; }
        public bool IsStealthed { get; }
        public int Encumberance { get; }
        public int MaxEncumberance { get; }
        public bool IsOverencumbered { get; set; }
        public bool IsIncapacitated { get; }
        public bool IsStunned { get; set; }
        public bool IsMezzed { get; set; }
        public bool IsMoving { get; }
        public bool IsSprinting { get; }
        public bool IsSitting { get; set; }
        public bool IsStrafing { get; set; }
        public bool CanBreathUnderWater { get; set; }

        public ControlledHorse ActiveHorse { get; }
        public bool IsOnHorse { get; set; }

        public void Shade(bool state);
        public ShadeECSGameEffect ShadeEffect { get; set; }
        public bool IsShade { get; }
        public ushort ShadeModel { get; }
        public ushort Model { get; set; }
        public ushort CreationModel { get; }

        public int Health { get; set; }
        public int MaxHealth { get; }
        public byte HealthPercent { get; }
        public int Mana { get; set; }
        public int MaxMana { get; }

        public int PowerRegenStackingBonus { get; set; }

        public int Endurance { get; set; }
        public short MaxSpeedBase { get; set; }

        public int Strength { get; }
        public int Dexterity { get; }
        public int Quickness { get; }
        public int Intelligence { get; }

        public Zone CurrentZone { get; }
        public Region CurrentRegion { get; set; }
        public ushort CurrentRegionID { get; set; }
        public IList<IArea> CurrentAreas { get; set; }

        public int Z { get; }
    }
}