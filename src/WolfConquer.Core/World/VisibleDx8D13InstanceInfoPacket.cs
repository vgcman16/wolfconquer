using System.Buffers;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public enum VisibleDx8D13InstanceInfoType : uint
{
    UserInstances = 0,
    AvailableAttempts = 1,
    KillBossTimes = 2,
}

public readonly record struct VisibleDx8D13UserInstanceInfo(
    uint ChallengeId,
    uint? Field2 = null,
    uint? CompletionFlag = null,
    uint? Field4 = null,
    uint? Field5 = null,
    uint? Field6 = null);

public readonly record struct VisibleDx8D13CompleteInstanceInfo(
    uint ChallengeId,
    uint AvailableAttempts,
    bool HasAvailableAttempts = true);

public sealed record VisibleDx8D13InstanceInfoPacket(
    uint Type,
    IReadOnlyList<VisibleDx8D13UserInstanceInfo> UserInstances,
    IReadOnlyList<VisibleDx8D13CompleteInstanceInfo> Completions)
{
    private const int TypeField = 1;
    private const int UserInstancesField = 4;
    private const int CompletionsField = 5;
    private const int ChallengeIdField = 1;
    private const int AvailableAttemptsField = 2;

    public const ushort Opcode = 2_382;

    public VisibleDx8D13InstanceInfoPacket(
        uint type,
        IReadOnlyList<VisibleDx8D13CompleteInstanceInfo> completions)
        : this(type, [], completions)
    {
    }

    public static VisibleDx8D13InstanceInfoPacket CreateUserInstanceProgress(
        uint challengeId,
        bool completed)
    {
        return new VisibleDx8D13InstanceInfoPacket(
            (uint)VisibleDx8D13InstanceInfoType.UserInstances,
            [new VisibleDx8D13UserInstanceInfo(challengeId, CompletionFlag: completed ? 1U : 0U)],
            []);
    }

    public static VisibleDx8D13InstanceInfoPacket CreateAvailableAttempts(
        uint challengeId,
        uint availableAttempts)
    {
        return new VisibleDx8D13InstanceInfoPacket(
            (uint)VisibleDx8D13InstanceInfoType.AvailableAttempts,
            [],
            [new VisibleDx8D13CompleteInstanceInfo(challengeId, availableAttempts)]);
    }

    public static bool TryParse(PacketFrame frame, out VisibleDx8D13InstanceInfoPacket packet)
    {
        packet = default!;
        if (frame.Header.Opcode != Opcode)
        {
            return false;
        }

        var reader = new LegacyProtoWire.Reader(frame.Payload.ToArray());
        uint type = 0;
        var hasType = false;
        var userInstances = new List<VisibleDx8D13UserInstanceInfo>();
        var completions = new List<VisibleDx8D13CompleteInstanceInfo>();

        while (!reader.End)
        {
            if (!reader.TryReadTag(out var fieldNumber, out var wireType))
            {
                return false;
            }

            if (fieldNumber == TypeField)
            {
                if (wireType != LegacyProtoWire.VarintWireType ||
                    !reader.TryReadUInt32(out type))
                {
                    return false;
                }

                hasType = true;
                continue;
            }

            if (fieldNumber == UserInstancesField)
            {
                if (wireType != LegacyProtoWire.LengthDelimitedWireType ||
                    !reader.TryReadLengthDelimited(out var userInstancePayload) ||
                    !TryParseUserInstance(userInstancePayload, out var userInstance))
                {
                    return false;
                }

                userInstances.Add(userInstance);
                continue;
            }

            if (fieldNumber == CompletionsField)
            {
                if (wireType != LegacyProtoWire.LengthDelimitedWireType ||
                    !reader.TryReadLengthDelimited(out var completionPayload) ||
                    !TryParseCompletion(completionPayload, out var completion))
                {
                    return false;
                }

                completions.Add(completion);
                continue;
            }

            if (!reader.TrySkipField(wireType))
            {
                return false;
            }
        }

        if (!hasType)
        {
            return false;
        }

        packet = new VisibleDx8D13InstanceInfoPacket(type, userInstances, completions);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var payload = new List<byte>();
        LegacyProtoWire.WriteVarintField(payload, TypeField, Type);
        foreach (var userInstance in UserInstances)
        {
            var userInstancePayload = new List<byte>();
            LegacyProtoWire.WriteVarintField(
                userInstancePayload,
                ChallengeIdField,
                userInstance.ChallengeId);
            WriteOptionalUInt32(userInstancePayload, 2, userInstance.Field2);
            WriteOptionalUInt32(userInstancePayload, 3, userInstance.CompletionFlag);
            WriteOptionalUInt32(userInstancePayload, 4, userInstance.Field4);
            WriteOptionalUInt32(userInstancePayload, 5, userInstance.Field5);
            WriteOptionalUInt32(userInstancePayload, 6, userInstance.Field6);
            LegacyProtoWire.WriteBytesField(payload, UserInstancesField, userInstancePayload);
        }

        foreach (var completion in Completions)
        {
            var completionPayload = new List<byte>();
            LegacyProtoWire.WriteVarintField(
                completionPayload,
                ChallengeIdField,
                completion.ChallengeId);
            if (completion.HasAvailableAttempts)
            {
                LegacyProtoWire.WriteVarintField(
                    completionPayload,
                    AvailableAttemptsField,
                    completion.AvailableAttempts);
            }

            LegacyProtoWire.WriteBytesField(payload, CompletionsField, completionPayload);
        }

        var writer = new PacketWriter(Opcode, payload.Count);
        payload.CopyTo(writer.Payload);
        return writer.ToArray();
    }

    private static bool TryParseUserInstance(
        ReadOnlySpan<byte> payload,
        out VisibleDx8D13UserInstanceInfo userInstance)
    {
        userInstance = default;
        var reader = new LegacyProtoWire.Reader(payload);
        uint challengeId = 0;
        var hasChallengeId = false;
        uint? field2 = null;
        uint? completionFlag = null;
        uint? field4 = null;
        uint? field5 = null;
        uint? field6 = null;

        while (!reader.End)
        {
            if (!reader.TryReadTag(out var fieldNumber, out var wireType))
            {
                return false;
            }

            if (fieldNumber is >= 1 and <= 6)
            {
                if (wireType != LegacyProtoWire.VarintWireType ||
                    !reader.TryReadUInt32(out var value))
                {
                    return false;
                }

                switch (fieldNumber)
                {
                    case 1:
                        challengeId = value;
                        hasChallengeId = true;
                        break;
                    case 2:
                        field2 = value;
                        break;
                    case 3:
                        completionFlag = value;
                        break;
                    case 4:
                        field4 = value;
                        break;
                    case 5:
                        field5 = value;
                        break;
                    case 6:
                        field6 = value;
                        break;
                }

                continue;
            }

            if (!reader.TrySkipField(wireType))
            {
                return false;
            }
        }

        if (!hasChallengeId)
        {
            return false;
        }

        userInstance = new VisibleDx8D13UserInstanceInfo(
            challengeId,
            field2,
            completionFlag,
            field4,
            field5,
            field6);
        return true;
    }

    private static bool TryParseCompletion(
        ReadOnlySpan<byte> payload,
        out VisibleDx8D13CompleteInstanceInfo completion)
    {
        completion = default;
        var reader = new LegacyProtoWire.Reader(payload);
        uint challengeId = 0;
        uint availableAttempts = 0;
        var hasChallengeId = false;
        var hasAvailableAttempts = false;

        while (!reader.End)
        {
            if (!reader.TryReadTag(out var fieldNumber, out var wireType))
            {
                return false;
            }

            if (fieldNumber == ChallengeIdField)
            {
                if (wireType != LegacyProtoWire.VarintWireType ||
                    !reader.TryReadUInt32(out challengeId))
                {
                    return false;
                }

                hasChallengeId = true;
                continue;
            }

            if (fieldNumber == AvailableAttemptsField)
            {
                if (wireType != LegacyProtoWire.VarintWireType ||
                    !reader.TryReadUInt32(out availableAttempts))
                {
                    return false;
                }

                hasAvailableAttempts = true;
                continue;
            }

            if (!reader.TrySkipField(wireType))
            {
                return false;
            }
        }

        if (!hasChallengeId)
        {
            return false;
        }

        completion = new VisibleDx8D13CompleteInstanceInfo(
            challengeId,
            availableAttempts,
            hasAvailableAttempts);
        return true;
    }

    private static void WriteOptionalUInt32(
        List<byte> payload,
        int fieldNumber,
        uint? value)
    {
        if (value is { } present)
        {
            LegacyProtoWire.WriteVarintField(payload, fieldNumber, present);
        }
    }
}
