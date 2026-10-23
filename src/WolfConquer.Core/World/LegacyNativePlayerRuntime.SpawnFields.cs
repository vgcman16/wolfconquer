namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Whole native nested MainFlagType306..313 and physical spawn-input fields.
    public enum MainFlagType : uint
    {
        None = 0u, CanClaim = 1u, ShowSpecialItems = 2u, ClaimGift = 4u, OnMeleeAttack = 8u,
    }

    // Player629/701/703/777/819/857/901/903/913/939/1095/1153/1155/1183/1185/1265.
    public ushort CountryID = 0;
    public MainFlagType MainFlag = MainFlagType.None;
    public byte Away = 0;
    public uint AparenceType = 0u;
    public byte MyTitle;
    public uint ClanUID;
    public ushort ClanRank;
    internal LegacyNativeGuildMemberRank GuildRank = LegacyNativeGuildMemberRank.None;
    public uint FlowerRank;
    public string ClanName = "";
    public uint QuizPoints;
    public ushort Enilghten;
    internal LegacyNativePlayerMapFlags.ConquerAngle Angle = LegacyNativePlayerMapFlags.ConquerAngle.East;
    internal LegacyNativePlayerMapFlags.ConquerAction Action = LegacyNativePlayerMapFlags.ConquerAction.None;
    public uint RealUID = 0u;
}

// Complete native Role.Flags99..130 enums, not invented modern movement state.
internal static class LegacyNativePlayerMapFlags
{
    public enum ConquerAngle : byte
    {
        SouthWest, West, NorthWest, North, NorthEast, East, SouthEast, South,
    }

    public enum ConquerAction : uint
    {
        None = 0u, Cool = 230u, Kneel = 210u, Sad = 170u, Happy = 150u, Angry = 160u,
        Lie = 14u, Dance = 1u, Wave = 190u, Bow = 200u, Sit = 250u, Jump = 100u,
        MagicDefender = 273344u, InteractionKiss = 34466u, InteractionHold = 34468u,
        InteractionHug = 34469u, CoupleDances = 34474u,
    }
}
