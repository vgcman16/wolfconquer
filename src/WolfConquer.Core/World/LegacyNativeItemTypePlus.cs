namespace WolfConquer.Core.World;

// Native ItemType.ITPlus row storage and Parse only (ItemType.cs14..54).
// Real element type for a future native DBItem.Plus array, not a Class/job DTO.
// No ItemAdd file/table loader, ItemsBase publication, Player.Class setter or Rune route is wired.
internal class LegacyNativeItemTypePlus
{
    public uint ID;
    public byte Plus;
    public ushort ItemHP;
    public uint MinAttack;
    public uint MaxAttack;
    public ushort PhysicalDefence;
    public ushort MagicAttack;
    public ushort MagicDefence;
    public ushort Agility;
    public byte Dodge;

    public ushort Vigor => Agility;
    public ushort SpeedPlus => Dodge;

    public void Parse(string Line)
    {
        string[] array = Line.Split(new string[3] { "@@", " ", "@" }, StringSplitOptions.RemoveEmptyEntries);
        ID = uint.Parse(array[0]);
        Plus = byte.Parse(array[1]);
        ItemHP = ushort.Parse(array[2]);
        MinAttack = uint.Parse(array[3]);
        MaxAttack = uint.Parse(array[4]);
        PhysicalDefence = ushort.Parse(array[5]);
        MagicAttack = ushort.Parse(array[6]);
        MagicDefence = ushort.Parse(array[7]);
        Agility = ushort.Parse(array[8]);
        Dodge = byte.Parse(array[9]);
    }
}
