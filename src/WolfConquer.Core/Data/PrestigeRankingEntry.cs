namespace WolfConquer.Core.Data;

/// <summary>A validated, immutable saved prestige row used only for read-only queries.</summary>
public sealed class PrestigeRankingEntry
{
    internal PrestigeRankingEntry(
        uint uid,
        string name,
        PrestigeRankCategory category,
        uint characterClass,
        byte level,
        uint totalPoints,
        PrestigeRankingAppearance appearance,
        uint[] points,
        string sourceFile,
        int lineNumber)
    {
        UID = uid;
        Name = name;
        Category = category;
        Class = characterClass;
        Level = level;
        TotalPoints = totalPoints;
        Appearance = appearance;
        Points = Array.AsReadOnly((uint[])points.Clone());
        SourceFile = sourceFile;
        LineNumber = lineNumber;
    }

    public uint UID { get; }
    public string Name { get; }
    public PrestigeRankCategory Category { get; }
    public uint Class { get; }
    public byte Level { get; }
    public uint TotalPoints { get; }
    public PrestigeRankingAppearance Appearance { get; }
    public IReadOnlyList<uint> Points { get; }
    public string SourceFile { get; }
    public int LineNumber { get; }

    public uint Mesh => Appearance.Mesh;
    public uint HairStyle => Appearance.HairStyle;
    public uint Head => Appearance.Head;
    public uint Garment => Appearance.Garment;
    public uint LeftWeapon => Appearance.LeftWeapon;
    public uint LeftWeaponAccessory => Appearance.LeftWeaponAccessory;
    public uint RightWeapon => Appearance.RightWeapon;
    public uint RightWeaponAccessory => Appearance.RightWeaponAccessory;
    public uint MountArmor => Appearance.MountArmor;
    public uint Armor => Appearance.Armor;
    public uint Wing => Appearance.Wing;
    public uint WingPlus => Appearance.WingPlus;
    public uint Title => Appearance.Title;
    public uint Flag => Appearance.Flag;
    public string GuildName => Appearance.GuildName;
}
