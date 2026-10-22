#nullable disable

namespace WolfConquer.Core.World;

// Complete native MsgStringPacket mutable state and identifiers. The private Handler is absent.
internal class LegacyNativeMsgStringPacket
{
    public enum StringID : byte
    {
        Fireworks = 1,
        GuildName = 3,
        Spouse = 6,
        LocationEffect = 9,
        Effect = 10,
        GuildList = 11,
        Unknown = 13,
        ViewEquipSpouse = 16,
        StartGamble = 17,
        EndGamble = 18,
        Sound = 20,
        GuildAllies = 21,
        GuildEnemies = 22,
        WhisperDetails = 26,
        ServerName = 61
    }

    public string[] Strings;

    public uint UID;

    public ushort X;

    public ushort Y;

    public byte StringsLength;

    public StringID ID;
}
