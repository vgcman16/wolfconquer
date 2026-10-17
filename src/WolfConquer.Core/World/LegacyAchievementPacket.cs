using System.Buffers;
using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed record LegacyAchievementState(IReadOnlyList<uint> Flags)
{
    public const int FlagWordCount = 14;

    public static LegacyAchievementState Empty { get; } = new(CreateZeroFlags());

    public static LegacyAchievementState FromSnapshot(CharacterSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return snapshot.Attributes.TryGetValue("Achivement", out var rawAchievement)
            ? Parse(rawAchievement)
            : Empty;
    }

    public static LegacyAchievementState Parse(string? rawAchievement)
    {
        if (string.IsNullOrEmpty(rawAchievement) ||
            !rawAchievement.Contains('/', StringComparison.Ordinal))
        {
            return Empty;
        }

        var fields = rawAchievement.Split('/');
        var flags = new uint[FlagWordCount];
        var offset = 1; // Legacy field 0 is AchievementCollection.Key, not part of the outbound flag vector.
        for (var index = 0; index < flags.Length; index++)
        {
            flags[index] = ReadUInt32(fields, ref offset);
        }

        return new LegacyAchievementState(flags);
    }

    private static uint ReadUInt32(IReadOnlyList<string> fields, ref int offset)
    {
        if (offset < fields.Count &&
            uint.TryParse(fields[offset], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            offset++;
            return value;
        }

        offset++;
        return 0;
    }

    private static uint[] CreateZeroFlags()
    {
        return new uint[FlagWordCount];
    }
}

public sealed record LegacyAchievementPacket(uint Action, uint Uid, IReadOnlyList<uint> Flags)
{
    private const int ActionOffset = 0;
    private const int UidOffset = ActionOffset + sizeof(uint);
    private const int ReservedOffset = UidOffset + sizeof(uint);
    private const int CountOffset = ReservedOffset + sizeof(uint);
    private const int FlagsOffset = CountOffset + sizeof(uint);

    public const ushort Opcode = WorldPacketOpcodes.LegacyAchievement;
    public const uint BigHashAction = 0;
    public const int BigHashPayloadLength = FlagsOffset + (LegacyAchievementState.FlagWordCount * sizeof(uint));

    public static LegacyAchievementPacket Startup(long uid, LegacyAchievementState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return new LegacyAchievementPacket(BigHashAction, CheckedUInt32(uid, nameof(uid)), state.Flags);
    }

    public static LegacyAchievementPacket FromSnapshot(CharacterSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return Startup(snapshot.Uid, LegacyAchievementState.FromSnapshot(snapshot));
    }

    public static bool TryParse(PacketFrame frame, out LegacyAchievementPacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length != BigHashPayloadLength ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader) ||
            reader.ReadUInt32(ReservedOffset) != 0 ||
            reader.ReadUInt32(ActionOffset) != BigHashAction ||
            reader.ReadUInt32(CountOffset) != LegacyAchievementState.FlagWordCount)
        {
            return false;
        }

        var flags = new uint[LegacyAchievementState.FlagWordCount];
        for (var index = 0; index < flags.Length; index++)
        {
            flags[index] = reader.ReadUInt32(FlagsOffset + (index * sizeof(uint)));
        }

        packet = new LegacyAchievementPacket(BigHashAction, reader.ReadUInt32(UidOffset), flags);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        ArgumentNullException.ThrowIfNull(Flags);

        if (Flags.Count != LegacyAchievementState.FlagWordCount)
        {
            throw new ArgumentOutOfRangeException(nameof(Flags), "Legacy achievement startup requires exactly 14 flag words.");
        }

        var writer = new PacketWriter(Opcode, BigHashPayloadLength);
        writer.WriteUInt32(ActionOffset, Action);
        writer.WriteUInt32(UidOffset, Uid);
        writer.WriteUInt32(ReservedOffset, 0);
        writer.WriteUInt32(CountOffset, LegacyAchievementState.FlagWordCount);
        for (var index = 0; index < Flags.Count; index++)
        {
            writer.WriteUInt32(FlagsOffset + (index * sizeof(uint)), Flags[index]);
        }

        return writer.ToArray();
    }

    private static uint CheckedUInt32(long value, string name)
    {
        if (value < 0 || value > uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(name, value, "Legacy achievement UID must fit in uint.");
        }

        return (uint)value;
    }
}
