using System.Buffers;
using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed record LegacyActivenessState(
    byte ClaimRewards,
    uint ActivityPoints,
    IReadOnlyList<LegacyActivityTaskState> Tasks)
{
    public static LegacyActivenessState Empty { get; } = new(0, 0, []);

    public static LegacyActivenessState FromSnapshot(CharacterSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return snapshot.Attributes.TryGetValue("Activeness", out var rawActiveness)
            ? Parse(rawActiveness)
            : Empty;
    }

    public static LegacyActivenessState Parse(string? rawActiveness)
    {
        if (string.IsNullOrEmpty(rawActiveness) ||
            !rawActiveness.Contains('/', StringComparison.Ordinal))
        {
            return Empty;
        }

        var fields = rawActiveness.Split('/');
        var offset = 0;
        var claimRewards = ReadByte(fields, ref offset);
        var activityPoints = ReadUInt32(fields, ref offset);
        var taskCount = ReadInt32(fields, ref offset);
        if (taskCount <= 0)
        {
            return new LegacyActivenessState(claimRewards, activityPoints, []);
        }

        var tasks = new List<LegacyActivityTaskState>(Math.Min(taskCount, byte.MaxValue));
        var seenTaskIds = new HashSet<uint>();
        for (var i = 0; i < taskCount && tasks.Count < byte.MaxValue; i++)
        {
            var id = ReadUInt32(fields, ref offset);
            var progress = ReadByte(fields, ref offset);
            var completed = ReadByte(fields, ref offset);

            if (seenTaskIds.Add(id))
            {
                tasks.Add(new LegacyActivityTaskState(id, progress, completed));
            }
        }

        return new LegacyActivenessState(claimRewards, activityPoints, tasks);
    }

    private static byte ReadByte(IReadOnlyList<string> fields, ref int offset)
    {
        if (offset < fields.Count &&
            byte.TryParse(fields[offset], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            offset++;
            return value;
        }

        offset++;
        return 0;
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

    private static int ReadInt32(IReadOnlyList<string> fields, ref int offset)
    {
        if (offset < fields.Count &&
            int.TryParse(fields[offset], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            offset++;
            return value;
        }

        offset++;
        return 0;
    }
}

public readonly record struct LegacyActivityTaskState(uint Id, byte Progress, byte Completed);

public sealed record LegacyActivityTasksPacket(byte Action, IReadOnlyList<LegacyActivityTaskState> Tasks)
{
    private const int ActionOffset = 0;
    private const int CountOffset = ActionOffset + sizeof(byte);
    private const int TasksOffset = CountOffset + sizeof(byte);
    private const int TaskLength = sizeof(uint) + sizeof(byte) + sizeof(byte);

    public const ushort Opcode = WorldPacketOpcodes.LegacyActivityTasks;
    public const byte InitializeListAction = 3;

    public static LegacyActivityTasksPacket FromState(LegacyActivenessState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return new LegacyActivityTasksPacket(InitializeListAction, state.Tasks);
    }

    public static bool TryParse(PacketFrame frame, out LegacyActivityTasksPacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length < TasksOffset ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader))
        {
            return false;
        }

        var count = reader.Payload[CountOffset];
        if (frame.Payload.Length != TasksOffset + (count * TaskLength))
        {
            return false;
        }

        var tasks = new List<LegacyActivityTaskState>(count);
        for (var index = 0; index < count; index++)
        {
            var offset = TasksOffset + (index * TaskLength);
            tasks.Add(new LegacyActivityTaskState(
                reader.ReadUInt32(offset),
                Progress: reader.Payload[offset + sizeof(uint) + sizeof(byte)],
                Completed: reader.Payload[offset + sizeof(uint)]));
        }

        packet = new LegacyActivityTasksPacket(reader.Payload[ActionOffset], tasks);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        ArgumentNullException.ThrowIfNull(Tasks);

        if (Tasks.Count > byte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(Tasks), "Legacy activity task count must fit in one byte.");
        }

        var writer = new PacketWriter(Opcode, TasksOffset + (Tasks.Count * TaskLength));
        writer.Payload[ActionOffset] = Action;
        writer.Payload[CountOffset] = (byte)Tasks.Count;
        for (var index = 0; index < Tasks.Count; index++)
        {
            var task = Tasks[index];
            var offset = TasksOffset + (index * TaskLength);
            writer.WriteUInt32(offset, task.Id);
            writer.Payload[offset + sizeof(uint)] = task.Completed;
            writer.Payload[offset + sizeof(uint) + sizeof(byte)] = task.Progress;
        }

        return writer.ToArray();
    }
}

public readonly record struct LegacyActivityRewardsInfoPacket(byte Action, uint ActivityPoints)
{
    private const int ActionOffset = 0;
    private const int FixedTwoAOffset = ActionOffset + sizeof(byte);
    private const int FixedOneOffset = FixedTwoAOffset + sizeof(uint);
    private const int ActivityPointsOffset = FixedOneOffset + sizeof(uint);
    private const int FixedTwoBOffset = ActivityPointsOffset + sizeof(uint);
    private const int FixedTwoCOffset = FixedTwoBOffset + sizeof(uint);

    public const ushort Opcode = WorldPacketOpcodes.LegacyActivityRewardsInfo;
    public const byte InitializeListAction = 1;
    public const int PayloadLength = FixedTwoCOffset + sizeof(uint);

    public static LegacyActivityRewardsInfoPacket FromState(LegacyActivenessState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return new LegacyActivityRewardsInfoPacket(InitializeListAction, state.ActivityPoints);
    }

    public static bool TryParse(PacketFrame frame, out LegacyActivityRewardsInfoPacket packet)
    {
        packet = default;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length != PayloadLength ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader) ||
            reader.ReadUInt32(FixedTwoAOffset) != 2 ||
            reader.ReadUInt32(FixedOneOffset) != 1 ||
            reader.ReadUInt32(FixedTwoBOffset) != 2 ||
            reader.ReadUInt32(FixedTwoCOffset) != 2)
        {
            return false;
        }

        packet = new LegacyActivityRewardsInfoPacket(
            reader.Payload[ActionOffset],
            reader.ReadUInt32(ActivityPointsOffset));
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(Opcode, PayloadLength);
        writer.Payload[ActionOffset] = Action;
        writer.WriteUInt32(FixedTwoAOffset, 2);
        writer.WriteUInt32(FixedOneOffset, 1);
        writer.WriteUInt32(ActivityPointsOffset, ActivityPoints);
        writer.WriteUInt32(FixedTwoBOffset, 2);
        writer.WriteUInt32(FixedTwoCOffset, 2);
        return writer.ToArray();
    }
}

public readonly record struct LegacyActivityClaimsPacket(byte ClaimRewards)
{
    public const ushort Opcode = WorldPacketOpcodes.LegacyActivityClaims;
    public const int PayloadLength = sizeof(byte);

    public static LegacyActivityClaimsPacket FromState(LegacyActivenessState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return new LegacyActivityClaimsPacket(state.ClaimRewards);
    }

    public static bool TryParse(PacketFrame frame, out LegacyActivityClaimsPacket packet)
    {
        packet = default;

        if (frame.Header.Opcode != Opcode || frame.Payload.Length != PayloadLength)
        {
            return false;
        }

        packet = new LegacyActivityClaimsPacket(frame.Payload.ToArray()[0]);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(Opcode, PayloadLength);
        writer.Payload[0] = ClaimRewards;
        return writer.ToArray();
    }
}
