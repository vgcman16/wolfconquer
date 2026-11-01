using System.Globalization;
using System.Net.Sockets;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedActionTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task ActualSkinTransportFailureCannotUndoCommittedFullActionOrEmitItsStaleEcho(bool viewer) => Run(async f =>
    {
        var files = f.Data.Files(); var failure = new SocketException((int)SocketError.ConnectionReset); int attempted = 0;
        f.BeforeSend = (recipient, bytes) =>
        {
            AssertFilesAndOwner(f, files, 65786);
            if (recipient == (viewer ? Peer : Uid) && bytes.SequenceEqual(SkinGolden())) { attempted++; throw failure; }
        };
        try { try { await f.StanceAsync(65786); } catch (SocketException error) { Assert.Same(failure, error); } }
        finally { f.BeforeSend = null; }
        Assert.Equal(1, attempted); AssertFilesAndOwner(f, files, 65786);
        Assert.Equal(viewer ? 1 : 0, f.Sent.Count); Assert.Empty(f.PeerSent);
        Assert.DoesNotContain(f.Sent.Concat(f.PeerSent), bytes => MsgActionPacket.TryParse(Read(bytes), out var action) && action.Type == MsgActionType.ChangeStance);
        ClearOutput(f); await Tick(f, 5); // Real next runtime event, never an automatic stance replay or a spent stamina stamp.
    });

    [Fact]
    public Task ActualEofAfterOwnerSkinStopsAllLaterOutputAndNeverRestoresAnUnboundActionSetter() => Run(async f =>
    {
        var files = f.Data.Files(); var native = f.Native; var player = native.Player!; var binding = native.OrdinarySpellBinding!; Task? close = null;
        f.BeforeSend = (recipient, bytes) =>
        {
            AssertFilesAndOwner(f, files, 65786);
            if (recipient == Uid && bytes.SequenceEqual(SkinGolden())) { Assert.Null(close); close = f.Connection.CompleteTransportCloseAsync(); }
        };
        try { await f.StanceAsync(65786); } finally { f.BeforeSend = null; }
        Assert.NotNull(close); await close!;
        Assert.Equal(SkinGolden(), Assert.Single(f.Sent)); Assert.Empty(f.PeerSent);
        Assert.False(f.Store.TryGet(f.Connection, out _)); Assert.True(binding.IsRetired); Assert.Equal(65786U, (uint)player.Action);
        Assert.Throws<InvalidOperationException>(() => player.Action = LegacyNativePlayerMapFlags.ConquerAction.None);
        f.Data.AssertFiles(files); int actor = f.Sent.Count, peer = f.PeerSent.Count;
        await f.CloseAsync();
        for (int restart = 0; restart < 2; restart++)
        {
            Assert.True(LegacyOwnedAdmissionRuntime.TryOpen(f.Data.Loader, f.Data.Options(), out var opened, out var reason), reason);
            await using var owner = Assert.IsType<LegacyOwnedAdmissionRuntime>(opened);
            f.Data.AssertFiles(files); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
            Assert.Equal(actor, f.Sent.Count); Assert.Equal(peer, f.PeerSent.Count); Assert.Equal(65786U, (uint)player.Action);
        }
    });

    [Fact]
    public Task ASecondRealStanceDuringSkinPublicationRetiresTheFirstOutcomeRatherThanSendingItsOldEcho() => Run(async f =>
    {
        var files = f.Data.Files(); bool nested = false;
        f.BeforeSend = (recipient, bytes) =>
        {
            if (nested || recipient != Uid || !bytes.SequenceEqual(SkinGolden())) return;
            nested = true; AssertFilesAndOwner(f, files, 65786);
            // Actual second Frame and Shared operation, not an injected owner,
            // epoch, accepted result or native Action assignment.
            f.StanceAsync(190).GetAwaiter().GetResult();
        };
        try { await f.StanceAsync(65786); } finally { f.BeforeSend = null; }
        Assert.True(nested); AssertFilesAndOwner(f, files, 190);
        Assert.Equal(StanceGolden(190), Assert.Single(f.Sent, bytes => MsgActionPacket.TryParse(Read(bytes), out var action) && action.Type == MsgActionType.ChangeStance));
        Assert.Equal(StanceGolden(190), Assert.Single(f.PeerSent, bytes => MsgActionPacket.TryParse(Read(bytes), out var action) && action.Type == MsgActionType.ChangeStance));
        Assert.DoesNotContain(f.Sent.Concat(f.PeerSent), bytes => bytes.SequenceEqual(StanceGolden(65786)));
        ClearOutput(f); await Tick(f, 5);
    });

    [Theory]
    [InlineData(250U)] [InlineData(65786U)]
    public Task ActualDeathPublicationAndFrameRevivePreserveFullActionAndThePreviouslyCommittedStaminaClock(uint action) => Run(async f =>
    {
        await f.StanceAsync(action); ClearOutput(f); await Tick(f, action == 250 ? (ushort)15 : (ushort)5);
        var before = f.Data.Files(); int deathFirst = f.Sent.Count;
        await ActualActionDeathPublication(f); AssertCoherent(f, action); Assert.Equal(0L, f.Current.Character.MinHitPoints);
        // Actual managed query status is the proven maximum owner. The raw
        // native Status.MaxHitpoints field is not joined by this slice.
        var status = Assert.Single(f.Sent.Skip(deathFirst).Select(bytes => LegacyPlayerStatusPacket.TryParse(
            Read(bytes), out var parsed) ? parsed : null).OfType<LegacyPlayerStatusPacket>());
        uint maximum = status.MaxHP; Assert.True(maximum > 0);
        ClearOutput(f); await Tick(f, action == 250 ? (ushort)15 : (ushort)5, held: true);
        f.Clock.Advance(LegacyPlayerDeathState.DefaultReviveDelay); await f.SendAsync(new MsgActionPacket
        { ObjId = Uid, Timestamp = 123456, Type = MsgActionType.Revive }.ToFrameBytes());
        Assert.Equal(0L, f.Current.Character.MinHitPoints); AssertCoherent(f, action); Assert.Empty(f.Sent);
        f.Clock.Advance(TimeSpan.FromTicks(1)); await f.SendAsync(new MsgActionPacket
        { ObjId = Uid, Timestamp = 123456, Type = MsgActionType.Revive }.ToFrameBytes());
        AssertCoherent(f, action); Assert.Equal((long)maximum, f.Current.Character.MinHitPoints); Assert.Equal((ushort)100, Stamina(f));
        var expected = before.ToDictionary(row => row.Key, row => row.Value, StringComparer.OrdinalIgnoreCase);
        string path = Path.GetRelativePath(f.Data.Root, f.CharacterPath());
        string text = CharacterSnapshotFileEncoding.Decode(expected[path], out var encoding);
        text = ReplaceLine(text, "MinHitPoints", 1, maximum); expected[path] = encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray();
        f.Data.AssertFiles(expected); ClearOutput(f);
        // Declared saved blessing gives real cap150. Revive100 is below cap,
        // so an immediate source step proves it did not reset the old clock.
        await Tick(f, action == 250 ? (ushort)115 : (ushort)105);
    }, new(Blessed: true));

    private static async Task ActualActionDeathPublication(ActionFixture f)
    {
        // Existing ordinary death PUBLICATION phase only, not an invented
        // offensive attack/kill authorizer or fake persistence callback.
        var factory = typeof(WorldServerFrameHandler).GetMethod("CreateTradeDisconnectContext", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var context = Assert.IsType<WorldPacketHandlerContext>(factory.Invoke(f.Handler, [f.Connection]));
        var before = f.Current;
        context = context with { Session = before, WorldData = f.Catalog, PlayerSpellsRoot = f.Data.Spells,
            PlayerProficienciesRoot = f.Data.Proficiencies, PlayerQuestsRoot = f.Data.Quests,
            TeamStates = ActionField<TeamSessionStateStore>(f, "_teamStates"), RuneRankings = ActionField<LegacyRuneRankingState>(f, "_runeRankings"),
            ProtocolProfile = ConquerProtocolProfile.VisibleDx8D13, TimeProvider = f.Clock,
            LegacyClock = ActionField<LegacyRuntimeUpdateLoopOptions>(f, "_runtimeUpdateLoop").LegacyClock };
        Assert.Same(f.Store, context.Sessions); Assert.NotNull(context.TradeOperations);
        using var operation = await context.TradeOperations!.EnterSharedAsync(); context = context with { FrameOperationLease = operation };
        var peer = f.Get(Peer); var scope = new LegacyEquipmentQueryPublicationScope(Uid, RequiredParticipant: new(f.ConnectionFor(Peer), peer));
        var causeType = typeof(WorldPacketHandlers).GetNestedType("LegacyNormalPlayerDeathCause", BindingFlags.NonPublic)!;
        var cause = Activator.CreateInstance(causeType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
            [checked((ushort)before.Character.X), checked((ushort)before.Character.Y), peer.Character.Uid, peer.Character.Name], CultureInfo.InvariantCulture);
        var method = typeof(WorldPacketHandlers).GetMethod("ExecuteLegacyNormalPlayerDeathPhasesAsync", BindingFlags.Static | BindingFlags.NonPublic)!;
        var pending = Assert.IsType<ValueTask<LegacyEquipmentQueryPublicationResult>>(method.Invoke(null,
            [context, before, scope, f.Native.Player, cause, CancellationToken.None]));
        Assert.True((await pending).Completed);
    }
    private static T ActionField<T>(ActionFixture f, string name) => Assert.IsType<T>(typeof(WorldServerFrameHandler)
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Handler));
}
