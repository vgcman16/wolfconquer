using System.Globalization;
using System.Net.Sockets;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedStaminaTests
{
    [Theory]
    [InlineData("owner-effect")] [InlineData("viewer-effect")] [InlineData("scalar")]
    public Task RealOutputLossAfterCommitDoesNotRefundRenewOrRepeatTheStaminaStep(string failedSurface) => Run(async f =>
    {
        await MintFan(f); var files = f.Data.Files(); int failed = 0;
        var row = f.Native.Player!.BitVector.ArrayFlags[194]; var timer = row.Timer;
        var failure = new SocketException((int)SocketError.ConnectionReset);
        f.BeforeSend = (recipient, bytes) =>
        {
            AssertCoherent(f, 15); f.Data.AssertFiles(files);
            bool selected = failedSurface == "scalar" ? bytes.SequenceEqual(StaminaGolden(15)) :
                bytes.SequenceEqual(FanEffectGolden()) && recipient == (failedSurface == "owner-effect" ? Uid : Peer);
            if (!selected) return; failed++; throw failure; // Actual connection failure, never an accepting result.
        };
        try
        {
            try { await f.TickAsync(); }
            catch (SocketException actual) { Assert.Same(failure, actual); }
        }
        finally { f.BeforeSend = null; }
        Assert.Equal(1, failed); AssertCoherent(f, 15); f.Data.AssertFiles(files);
        Assert.Same(row, f.Native.Player.BitVector.ArrayFlags[194]); Assert.Equal(timer, row.Timer);
        Assert.DoesNotContain(f.Sent, bytes => bytes.SequenceEqual(StaminaGolden(15)));
        await TickHeld(f); f.Clock.Advance(TimeSpan.FromSeconds(3)); await TickAndAssert(f, 20);
        Assert.Same(row, f.Native.Player.BitVector.ArrayFlags[194]); Assert.Equal(timer, row.Timer);
    }, new(Peer: true));

    [Fact]
    public Task RealEofAfterEffectStopsLaterScalarAndRawRestartsCannotReplayTransientGain() => Run(async f =>
    {
        await MintFan(f); var files = f.Data.Files(); var native = f.Native;
        var binding = native.OrdinarySpellBinding!; Task? close = null;
        f.BeforeSend = (recipient, bytes) =>
        {
            AssertCoherent(f, 15); f.Data.AssertFiles(files);
            if (recipient == Uid && bytes.SequenceEqual(FanEffectGolden()))
            {
                Assert.Null(close); close = f.Connection.CompleteTransportCloseAsync();
            }
        };
        try { await f.TickAsync(); } finally { f.BeforeSend = null; }
        Assert.NotNull(close); await close!; Assert.False(f.Store.TryGet(f.Connection, out _));
        Assert.Equal((ushort)15, native.Player!.Stamina); Assert.True(binding.IsRetired);
        Assert.DoesNotContain(f.Sent, bytes => bytes.SequenceEqual(StaminaGolden(15))); f.Data.AssertFiles(files);
        int count = f.Sent.Count, peers = f.PeerSent.Count;
        await f.CloseAsync();
        for (int restart = 0; restart < 2; restart++)
        {
            Assert.True(LegacyOwnedAdmissionRuntime.TryOpen(f.Data.Loader, f.Data.Options(), out var opened, out var reason), reason);
            await using var owner = Assert.IsType<LegacyOwnedAdmissionRuntime>(opened);
            f.Data.AssertFiles(files); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
            Assert.Equal(count, f.Sent.Count); Assert.Equal(peers, f.PeerSent.Count); Assert.Equal((ushort)15, native.Player.Stamina);
        }
    }, new(Peer: true));

    [Fact]
    public Task ActualDeathPublicationAndDelayedFrameReviveKeepNativeStaminaAndTheFanLifetimeCoherent() => Run(async f =>
    {
        await MintFan(f); await TickAndAssert(f, 15, true);
        var row = f.Native.Player!.BitVector.ArrayFlags[194]; var timer = row.Timer;
        var original = f.Data.Files(); var oldCharacter = File.ReadAllBytes(f.CharacterPath);
        await ActualDeathPublication(f); AssertCoherent(f, 15); Assert.Equal(0L, f.Current.Character.MinHitPoints);
        ClearOutput(f); await TickHeld(f);
        f.Clock.Advance(LegacyPlayerDeathState.DefaultReviveDelay); int first = f.Sent.Count;
        var request = new MsgActionPacket { ObjId = Uid, Timestamp = 511043495, Type = MsgActionType.Revive }.ToFrameBytes();
        await f.SendAsync(request); Assert.Equal(first, f.Sent.Count); AssertCoherent(f, 15);
        f.Clock.Advance(TimeSpan.FromTicks(1)); await f.SendAsync(request); AssertCoherent(f, 100);
        var stamina = Assert.Single(f.Sent.Skip(first), bytes =>
            LegacyMsgUpdatePacket.TryParse(Read(bytes), out var update) && update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Stamina));
        Assert.Equal(StaminaGolden(100), stamina); AssertFan(f, true);
        Assert.Same(row, f.Native.Player.BitVector.ArrayFlags[194]); Assert.Equal(timer, row.Timer);
        var expected = new Dictionary<string, byte[]>(original, StringComparer.OrdinalIgnoreCase);
        string text = CharacterSnapshotFileEncoding.Decode(oldCharacter, out var encoding);
        var hp = f.Current.Character.MinHitPoints.ToString(CultureInfo.InvariantCulture);
        string changed = text.Replace("\r\nMinHitPoints=1\r\n", "\r\nMinHitPoints=" + hp + "\r\n", StringComparison.Ordinal);
        expected[Path.GetRelativePath(f.Data.Root, f.CharacterPath)] = encoding.GetPreamble().Concat(encoding.GetBytes(changed)).ToArray();
        f.Data.AssertFiles(expected); await TickHeld(f); // Full capacity, with no revive clock reset or extra grant.
    }, new(Peer: true));

    [Fact]
    public Task ActualConfiguredWallClockRollbackDoesNotEarnACompensatingOrDuplicateStep() => Run(async f =>
    {
        await TickAndAssert(f, 5); f.Clock.RewindWallClock(TimeSpan.FromSeconds(1)); await TickHeld(f);
        f.Clock.Advance(TimeSpan.FromSeconds(3)); await TickHeld(f);
        f.Clock.Advance(TimeSpan.FromSeconds(1)); await TickAndAssert(f, 10); await TickHeld(f);
    });

    private static async Task ActualDeathPublication(StaminaFixture f)
    {
        // Existing actual death PUBLICATION phase only: this does not claim an
        // offensive spell, successful combat kill or attack authorization.
        var factory = typeof(WorldServerFrameHandler).GetMethod("CreateTradeDisconnectContext", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var context = Assert.IsType<WorldPacketHandlerContext>(factory.Invoke(f.Handler, [f.Connection]));
        var before = f.Current;
        context = context with { Session = before, WorldData = f.Catalog, PlayerSpellsRoot = f.Data.Spells,
            PlayerProficienciesRoot = f.Data.Proficiencies, PlayerQuestsRoot = f.Data.Quests,
            TeamStates = Field<TeamSessionStateStore>(f, "_teamStates"), RuneRankings = Field<LegacyRuneRankingState>(f, "_runeRankings"),
            ProtocolProfile = ConquerProtocolProfile.VisibleDx8D13, TimeProvider = f.Clock,
            LegacyClock = Field<LegacyRuntimeUpdateLoopOptions>(f, "_runtimeUpdateLoop").LegacyClock };
        Assert.Same(f.Store, context.Sessions); Assert.NotNull(context.TradeOperations);
        using var operation = await context.TradeOperations!.EnterSharedAsync(); context = context with { FrameOperationLease = operation };
        Assert.True(f.Store.TryGet(f.PeerConnection, out var peer)); Assert.NotNull(peer);
        var scope = new LegacyEquipmentQueryPublicationScope(Uid, RequiredParticipant: new(f.PeerConnection, peer!));
        var causeType = typeof(WorldPacketHandlers).GetNestedType("LegacyNormalPlayerDeathCause", BindingFlags.NonPublic)!;
        var cause = Activator.CreateInstance(causeType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, [checked((ushort)before.Character.X), checked((ushort)before.Character.Y), peer!.Character.Uid, peer.Character.Name], CultureInfo.InvariantCulture);
        var method = typeof(WorldPacketHandlers).GetMethod("ExecuteLegacyNormalPlayerDeathPhasesAsync", BindingFlags.Static | BindingFlags.NonPublic)!;
        var pending = Assert.IsType<ValueTask<LegacyEquipmentQueryPublicationResult>>(method.Invoke(null,
            [context, before, scope, f.Native.Player, cause, CancellationToken.None]));
        Assert.True((await pending).Completed);
    }
    private static T Field<T>(StaminaFixture f, string name) => Assert.IsType<T>(typeof(WorldServerFrameHandler)
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Handler));
}
