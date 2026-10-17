using System.Buffers;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class VisibleDx8D13InstanceInfoPacketTests
{
    [Fact]
    public void ParsesObservedFloorOneCompletionQuery()
    {
        byte[] bytes =
        [
            0x0B, 0x00, 0x4E, 0x09,
            0x08, 0x01, 0x2A, 0x03, 0x08, 0xDC, 0x03,
        ];

        Assert.True(VisibleDx8D13InstanceInfoPacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal((uint)VisibleDx8D13InstanceInfoType.AvailableAttempts, packet.Type);
        var completion = Assert.Single(packet.Completions);
        Assert.Equal(476U, completion.ChallengeId);
        Assert.False(completion.HasAvailableAttempts);
        Assert.Equal(0U, completion.AvailableAttempts);
    }

    [Fact]
    public void AvailableAttemptsResponseMatchesExactD13Wire()
    {
        var bytes = VisibleDx8D13InstanceInfoPacket.CreateAvailableAttempts(476, 10).ToFrameBytes();

        Assert.Equal(
            new byte[]
            {
                0x0D, 0x00, 0x4E, 0x09,
                0x08, 0x01, 0x2A, 0x05, 0x08, 0xDC, 0x03, 0x10, 0x0A,
            },
            bytes);
        Assert.True(VisibleDx8D13InstanceInfoPacket.TryParse(ReadFrame(bytes), out var packet));
        var completion = Assert.Single(packet.Completions);
        Assert.True(completion.HasAvailableAttempts);
        Assert.Equal(476U, completion.ChallengeId);
        Assert.Equal(10U, completion.AvailableAttempts);
    }

    [Fact]
    public void CompletedUserInstanceMatchesExactD13ProgressionWire()
    {
        var bytes = VisibleDx8D13InstanceInfoPacket.CreateUserInstanceProgress(
                challengeId: 476,
                completed: true)
            .ToFrameBytes();

        Assert.Equal(
            new byte[]
            {
                0x0D, 0x00, 0x4E, 0x09,
                0x08, 0x00, 0x22, 0x05, 0x08, 0xDC, 0x03, 0x18, 0x01,
            },
            bytes);
        Assert.True(VisibleDx8D13InstanceInfoPacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal((uint)VisibleDx8D13InstanceInfoType.UserInstances, packet.Type);
        Assert.Empty(packet.Completions);
        var userInstance = Assert.Single(packet.UserInstances);
        Assert.Equal(476U, userInstance.ChallengeId);
        Assert.Null(userInstance.Field2);
        Assert.Equal(1U, userInstance.CompletionFlag);
        Assert.Null(userInstance.Field4);
        Assert.Null(userInstance.Field5);
        Assert.Null(userInstance.Field6);
    }

    [Fact]
    public void SkyTowerCatalogModelsAllD13Floors()
    {
        var challenges = Enumerable.Range(1, 160)
            .Select(floor =>
            {
                Assert.True(LegacySkyTowerCatalog.TryResolveByFloor(floor, out var challenge));
                return challenge;
            })
            .ToArray();

        Assert.Equal(160, challenges.Length);
        Assert.Equal(new LegacySkyTowerChallenge(476, 1, 6072, 1, 1068, 50, 50), challenges[0]);
        Assert.Equal(new LegacySkyTowerChallenge(477, 2, 6073, 2, 1068, 50, 50), challenges[1]);
        Assert.Equal(new LegacySkyTowerChallenge(610, 135, 6206, 5, 1068, 50, 50), challenges[134]);
        Assert.Equal(new LegacySkyTowerChallenge(764, 136, 6279, 5, 1068, 50, 50), challenges[135]);
        Assert.Equal(new LegacySkyTowerChallenge(788, 160, 6291, 25, 1068, 50, 50), challenges[159]);
        Assert.Equal(160, challenges.Select(static challenge => challenge.ChallengeId).Distinct().Count());
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
