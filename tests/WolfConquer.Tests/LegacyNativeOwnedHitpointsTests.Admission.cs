using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeOwnedHitpointsTests
{
    [Fact]
    public Task GenuineAdmissionHydratesActualPlayerBeforeFirstSuccessAndEquipmentClamp() => Run(async f =>
    {
        var player = f.Player;
        Assert.Equal(0, player.HitPoints); Assert.False(player.Alive); Assert.True(player.NotLife);
        Assert.Null(f.Native.OrdinarySpellBinding);
        int firstSuccess = 0;
        var answer = LegacyMsgMessagePacket.AnswerOk().ToFrameBytes();
        f.ObserveSend = (bytes, _) =>
        {
            if (bytes.AsSpan().SequenceEqual(answer))
            {
                firstSuccess++;
                Assert.Same(player, f.Current.NativePlayer);
                AssertHp(f, 1000); // Actual saved input, before any derived-HP query.
                Assert.False(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
            }
            f.Sent.Add(bytes); return ValueTask.CompletedTask;
        };

        await f.LoginAsync(); f.RequireCommitObserved();

        Assert.Equal(1, firstSuccess); Assert.Same(player, f.Player);
        Assert.Equal(answer, f.Sent[0]); AssertHp(f, 1000);
        Assert.False(typeof(LegacyNativePlayerRuntime).GetProperty(nameof(LegacyNativePlayerRuntime.HitPoints))!.CanWrite);
        var current = f.Current; var count = f.Sent.Count;
        await f.LoginAsync();
        Assert.Same(current, f.Current); Assert.Equal(count, f.Sent.Count); AssertHp(f, 1000);
    });

    [Theory]
    [InlineData(-1L)]
    [InlineData(2_147_483_648L)]
    public Task OrdinaryAdmissionRefusesInvalidPhysicalRangeBeforeAnySavedOrNativePrefix(long hp) => Run(async f =>
    {
        Assert.True(CharacterSnapshotFileCoordinator.TryUpdateCharacterAttributes(f.CharacterPath, FixtureUid,
            new Dictionary<string, string> { ["MinHitPoints"] = hp.ToString(System.Globalization.CultureInfo.InvariantCulture) },
            detailedIoFailures: true, out var reason), reason);
        var files = f.Files(); var player = f.Player;
        Assert.Equal(0, player.HitPoints);

        await f.LoginAsync();

        Assert.Null(f.CurrentOrNull); Assert.Same(player, f.Player);
        Assert.Equal(0, player.HitPoints); Assert.False(player.Alive); Assert.True(player.NotLife);
        Assert.Equal(0u, player.UID); Assert.False(player.CompleteLogin);
        Assert.Null(f.Native.OrdinarySpellBinding); Assert.Empty(f.Sent);
        f.RequireFiles(files); Assert.False(LegacyTradeJournal.HasPending(f.Users));
    });

    private const uint FixtureUid = WorldServerFrameHandlerTests.NativeSpellJoinFixture.Uid;
}
