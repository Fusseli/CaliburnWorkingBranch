using DOL.Database.Attributes;

namespace DOL.Database;

/// <summary>
/// Saved player-owned mimic bot (PR20a). One row per stored bot; gear lives in
/// the shared Inventory table under a stable storage key (see MimicSaveManager).
/// ML/Champion columns are stored from day one so later phases need no migration.
/// </summary>
[DataTable(TableName = "MimicSave")]
public class DbMimicSave : DataObject
{
    private string m_accountName;
    private int m_realm;
    private int m_slot;
    private string m_name;
    private int m_class;
    private int m_level;
    private long m_experience;
    private string m_specs;
    private string m_realmAbilities;
    private int m_realmLevel;
    private long m_realmPoints;
    private int m_mlLine;
    private int m_mlLevel;
    private long m_mlExperience;
    private bool m_mlGranted;
    private bool m_champion;
    private int m_championLevel;
    private long m_championExperience;
    private int m_race;
    private int m_gender;
    private int m_model;
    private int m_size;
    private int m_specType;

    public DbMimicSave()
    {
    }

    [DataElement(AllowDbNull = false, Index = true, Varchar = 255)]
    public string AccountName
    {
        get { return m_accountName; }
        set { Dirty = true; m_accountName = value; }
    }

    [DataElement(AllowDbNull = false, Index = true)]
    public int Realm
    {
        get { return m_realm; }
        set { Dirty = true; m_realm = value; }
    }

    [DataElement(AllowDbNull = false)]
    public int Slot
    {
        get { return m_slot; }
        set { Dirty = true; m_slot = value; }
    }

    [DataElement(AllowDbNull = false, Varchar = 255)]
    public string Name
    {
        get { return m_name; }
        set { Dirty = true; m_name = value; }
    }

    [DataElement(AllowDbNull = false)]
    public int CharacterClass
    {
        get { return m_class; }
        set { Dirty = true; m_class = value; }
    }

    [DataElement(AllowDbNull = false)]
    public int Level
    {
        get { return m_level; }
        set { Dirty = true; m_level = value; }
    }

    [DataElement(AllowDbNull = false)]
    public long Experience
    {
        get { return m_experience; }
        set { Dirty = true; m_experience = value; }
    }

    [DataElement(AllowDbNull = true)]
    public string SerializedSpecs
    {
        get { return m_specs; }
        set { Dirty = true; m_specs = value; }
    }

    [DataElement(AllowDbNull = true)]
    public string SerializedRealmAbilities
    {
        get { return m_realmAbilities; }
        set { Dirty = true; m_realmAbilities = value; }
    }

    [DataElement(AllowDbNull = false)]
    public int RealmLevel
    {
        get { return m_realmLevel; }
        set { Dirty = true; m_realmLevel = value; }
    }

    [DataElement(AllowDbNull = false)]
    public long RealmPoints
    {
        get { return m_realmPoints; }
        set { Dirty = true; m_realmPoints = value; }
    }

    [DataElement(AllowDbNull = false)]
    public int MLLine
    {
        get { return m_mlLine; }
        set { Dirty = true; m_mlLine = value; }
    }

    [DataElement(AllowDbNull = false)]
    public int MLLevel
    {
        get { return m_mlLevel; }
        set { Dirty = true; m_mlLevel = value; }
    }

    [DataElement(AllowDbNull = false)]
    public long MLExperience
    {
        get { return m_mlExperience; }
        set { Dirty = true; m_mlExperience = value; }
    }

    [DataElement(AllowDbNull = false)]
    public bool MLGranted
    {
        get { return m_mlGranted; }
        set { Dirty = true; m_mlGranted = value; }
    }

    [DataElement(AllowDbNull = false)]
    public bool Champion
    {
        get { return m_champion; }
        set { Dirty = true; m_champion = value; }
    }

    [DataElement(AllowDbNull = false)]
    public int ChampionLevel
    {
        get { return m_championLevel; }
        set { Dirty = true; m_championLevel = value; }
    }

    [DataElement(AllowDbNull = false)]
    public long ChampionExperience
    {
        get { return m_championExperience; }
        set { Dirty = true; m_championExperience = value; }
    }

    [DataElement(AllowDbNull = false)]
    public int Race
    {
        get { return m_race; }
        set { Dirty = true; m_race = value; }
    }

    [DataElement(AllowDbNull = false)]
    public int Gender
    {
        get { return m_gender; }
        set { Dirty = true; m_gender = value; }
    }

    [DataElement(AllowDbNull = false)]
    public int Model
    {
        get { return m_model; }
        set { Dirty = true; m_model = value; }
    }

    [DataElement(AllowDbNull = false)]
    public int Size
    {
        get { return m_size; }
        set { Dirty = true; m_size = value; }
    }

    [DataElement(AllowDbNull = false)]
    public int SpecType
    {
        get { return m_specType; }
        set { Dirty = true; m_specType = value; }
    }
}
