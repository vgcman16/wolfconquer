using System.Net;
using System.Text;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private static readonly PacketHandlerRegistry<WorldPacketHandlerContext> NativeTeamMemberPackets =
        PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

    [Fact]
    public async Task NativeTeamCreatePublicEntryEarnsOriginalMemberAndSourceIdentityShape()
    {
        using var f = new NativeTeamMemberFixture();
        Assert.False(f.Teams.TryCaptureTournamentTeam(f.Client(0), out _));
        await f.SendAsync(0, MsgTeamType.Create, 0);
        var team = f.Team(0);
        Assert.True(f.Teams.TryReadTournamentTeam(team, out var state));
        Assert.NotNull(state);
        Assert.Equal(2U, team.Uid); // Separate native Counter(1).Next shape oracle.
        Assert.Equal("NativeLeader", state.Name);
        Assert.Equal(f.Client(0).Connection, Assert.Single(state.Members).Connection);
        Assert.Equal(f.Client(0).Connection, state.Leader!.Connection);
        Assert.True(f.Teams.IsTournamentTeamMember(team, f.Client(0)));
        f.AssertCreateFrames(0);
        Assert.Empty(f.Frames[1]);
        Assert.Empty(f.Frames[2]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeTeamBothPublicAcceptEntriesCaptureActualOriginalMembers(bool leaderAccepts)
    {
        using var f = new NativeTeamMemberFixture();
        await f.CreateAsync();
        f.ClearFrames();
        await f.JoinAsync(leaderAccepts);
        var team = f.Team(0);
        Assert.True(f.Teams.TryCaptureTournamentTeam(f.Client(1), out var joined));
        Assert.True(f.Teams.IsTournamentTeamMember(team, f.Client(1)));
        Assert.True(f.Teams.IsTournamentTeamMember(joined!, f.Client(0)));
        Assert.True(f.Teams.TryReadTournamentTeam(team, out var state));
        Assert.Equal(2, state!.Members.Count);
        Assert.Equal(new long[] { NativeTeamMemberFixture.LeaderUid, NativeTeamMemberFixture.MemberUid },
            state.Members.Select(member => member.Uid));
        f.AssertJoinedFrames(1, 2);
        f.AssertSingleMemberInfo(0, 2);
        Assert.Empty(f.Frames[2]);
    }

    [Fact]
    public async Task NativeTeamAcceptInvitationMaySelectAnExistingNonleaderMember()
    {
        using var f = new NativeTeamMemberFixture();
        await f.CreateAsync();
        await f.JoinAsync(leaderAccepts: false);
        f.ClearFrames();
        Assert.False(f.Teams.IsLeader(f.Connections[1], NativeTeamMemberFixture.MemberUid));
        await f.SendAsync(2, MsgTeamType.AcceptInvitation, NativeTeamMemberFixture.MemberUid);
        var team = f.Team(0);
        Assert.True(f.Teams.IsTournamentTeamMember(team, f.Client(2)));
        Assert.True(f.Teams.TryReadTournamentTeam(team, out var state));
        Assert.Equal(3, state!.Members.Count);
        f.AssertJoinedFrames(2, 3);
        f.AssertSingleMemberInfo(0, 3);
        f.AssertSingleMemberInfo(1, 3);
    }

    [Fact]
    public async Task NativeTeamCreateNeverRecapturesSameUidReplacementAfterInitialLog()
    {
        using var f = new NativeTeamMemberFixture();
        var original = f.Client(0);
        var record = f.Owner(0);
        f.OnHandled = () => f.RebindSameRecord(0);
        await f.SendAsync(0, MsgTeamType.Create, 0);
        Assert.Same(record, f.Owner(0));
        Assert.False(f.Holders.TryRead(original, out _));
        Assert.False(f.Teams.TryCaptureTournamentTeam(f.Client(0), out _));
        f.AssertNoFrames();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeTeamAcceptNeverTransfersReplacedPeerAfterInitialLog(bool leaderAccepts)
    {
        using var f = new NativeTeamMemberFixture();
        await f.CreateAsync();
        var team = f.Team(0);
        var peer = leaderAccepts ? 1 : 0;
        var retired = f.Client(peer);
        f.ClearFrames();
        f.OnHandled = () => f.RebindSameRecord(peer);
        await f.JoinAsync(leaderAccepts);
        Assert.False(f.Holders.TryRead(retired, out _));
        Assert.False(f.Teams.TryCaptureTournamentTeam(f.Client(1), out _));
        Assert.True(f.Teams.TryReadTournamentTeam(team, out var state));
        Assert.Single(state!.Members);
        f.AssertNoFrames();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeTeamAcceptNeverTransfersReplacedActorAfterInitialLog(bool leaderAccepts)
    {
        using var f = new NativeTeamMemberFixture();
        await f.CreateAsync();
        var team = f.Team(0);
        var actor = leaderAccepts ? 0 : 1;
        var retired = f.Client(actor);
        f.ClearFrames();
        f.OnHandled = () => f.RebindSameRecord(actor);
        await f.JoinAsync(leaderAccepts);
        Assert.False(f.Holders.TryRead(retired, out _));
        Assert.False(f.Teams.TryCaptureTournamentTeam(f.Client(1), out _));
        Assert.True(f.Teams.TryReadTournamentTeam(team, out var state));
        Assert.Single(state!.Members);
        f.AssertNoFrames();
    }

    [Fact]
    public async Task NativeTeamRetiredMemberCannotInheritOldRosterAtSameConnectionAndUid()
    {
        using var f = new NativeTeamMemberFixture();
        await f.CreateAsync();
        await f.JoinAsync(leaderAccepts: false);
        var team = f.Team(0);
        var original = f.Client(1);
        f.RebindSameRecord(1);
        var replacement = f.Client(1);
        Assert.False(f.Teams.IsTournamentTeamMember(team, original));
        Assert.False(f.Teams.IsTournamentTeamMember(team, replacement));
        Assert.False(f.Teams.TryCaptureTournamentTeam(replacement, out _));
        Assert.True(f.Teams.TryReadTournamentTeam(team, out var state));
        Assert.Equal(2, state!.Members.Count); // Native actual roster still owns ORIGINAL reference.
        Assert.Contains(state.Members, member => ReferenceEquals(member.Identity, original.Identity));
        Assert.DoesNotContain(state.Members, member => ReferenceEquals(member.Identity, replacement.Identity));
        f.ClearFrames();
        await f.SendAsync(1, MsgTeamType.Create, 0); // A new explicit public Create, never inheritance.
        Assert.True(f.Teams.TryCaptureTournamentTeam(replacement, out var newTeam));
        Assert.NotEqual(team.Uid, newTeam!.Uid);
        Assert.False(f.Teams.IsTournamentTeamMember(team, replacement));
        Assert.True(f.Teams.IsTournamentTeamMember(team, f.Client(0)));
        f.AssertCreateFrames(1);
    }

    [Fact]
    public async Task NativeTeamSameClientCopyAndRealLeaderExitPreserveActualTeamIdentity()
    {
        using var f = new NativeTeamMemberFixture();
        var original = f.Client(0);
        f.OnHandled = () => f.CopyMarker(0, "copy-before-create");
        await f.CreateAsync();
        Assert.True(f.Holders.TryRead(original, out _));
        Assert.Equal("copy-before-create", f.Owner(0).Character.Attributes["NativeMemberMarker"]);
        var team = f.Team(0);
        await f.JoinAsync(leaderAccepts: false);
        var member = f.Client(1);
        f.CopyMarker(1, "same-native-member");
        // Existing actual ExitTeam owner API; this is NOT public Exit packet/wire closure.
        f.Teams.ExitTeam(f.Connections[0]);
        Assert.True(f.Teams.TryCaptureTournamentTeam(member, out var retained));
        Assert.True(f.Teams.IsTournamentTeamMember(team, member));
        Assert.False(f.Teams.IsTournamentTeamMember(team, original));
        Assert.True(f.Teams.TryReadTournamentTeam(retained!, out var state));
        Assert.Equal(team.Uid, retained!.Uid);
        Assert.Equal("NativeMember", state!.Name); // Separate native AddLider name oracle.
        Assert.Equal(member.Connection, state.Leader!.Connection);
        Assert.Single(state.Members);
        Assert.Equal("same-native-member", f.Owner(1).Character.Attributes["NativeMemberMarker"]);
    }

    [Fact]
    public async Task NativeTeamRetirementInsideFirstCreatePublicationStopsOriginalTail()
    {
        using var f = new NativeTeamMemberFixture();
        var original = f.Client(0);
        f.OnSend = index =>
        {
            if (index == 0 && f.Frames[0].Count == 1) f.RebindSameRecord(0);
        };
        await f.CreateAsync();
        Assert.False(f.Holders.TryRead(original, out _));
        Assert.False(f.Teams.TryCaptureTournamentTeam(f.Client(0), out _));
        var sent = Assert.Single(f.Frames[0]); // Already-entered send remains; no rollback claim.
        Assert.True(LegacyTeamMemberInfoPacket.TryParse(ReadFrame(sent), out var info));
        Assert.Single(info.Entries);
        Assert.Empty(f.Frames[1]);
        Assert.Empty(f.Frames[2]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeTeamCompoundApisRejectPreheldUnrelatedWorldGateWithoutRosterMutation(bool differentWorld)
    {
        using var f = new NativeTeamMemberFixture();
        await f.CreateAsync();
        var leader = f.Client(0);
        var member = f.Client(1);
        var team = f.Team(0);
        var firstRecord = f.Owner(0);
        var secondRecord = f.Owner(1);
        f.ClearFrames();
        var callbacks = 0;
        void Attempt()
        {
            callbacks++;
            Assert.True(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
            Assert.False(f.Teams.TryAddNativeMember(leader, member, requireLeader: false));
            Assert.False(f.Teams.TryCreateNativeTeam(member, "must-not-allocate"));
            Assert.False(f.Teams.TryCaptureTournamentTeam(leader, out _));
            f.Teams.ExitTeam(leader.Connection); // Bound reentrant exit/name refresh also rejects.
        }
        if (differentWorld)
        {
            var nested = new WorldSessionStore((_, _, _) => Attempt());
            var outer = new WorldSessionStore((_, _, _) =>
            {
                Assert.True(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
                nested.Set(f.Connections[2], f.Owner(2));
                Assert.True(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate); // Outer still owns C.
            });
            outer.Set(f.Connections[2], f.Owner(2));
        }
        else
        {
            f.OnStorePublished = (_, _, _) => Attempt();
            f.World.Set(f.Connections[2], f.Owner(2)); // C is neither actor A nor member B.
        }
        Assert.Equal(1, callbacks);
        Assert.False(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
        Assert.Same(firstRecord, f.Owner(0));
        Assert.Same(secondRecord, f.Owner(1));
        Assert.True(f.Teams.TryReadTournamentTeam(team, out var state));
        Assert.Single(state!.Members);
        Assert.False(f.Teams.TryCaptureTournamentTeam(member, out _));
        Assert.Equal("NativeLeader", state.Name);
        f.AssertNoFrames();
        await f.SendAsync(1, MsgTeamType.Create, 0);
        Assert.Equal(3U, f.Team(1).Uid); // Rejected nested Create consumed no native UID.
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeTeamPublicAcceptRejectsStoreCallbackBeforeAnyNewClientCapture(bool leaderAccepts)
    {
        using var f = new NativeTeamMemberFixture();
        await f.CreateAsync();
        var team = f.Team(0);
        Task? entered = null;
        f.ClearFrames();
        f.OnStorePublished = (_, _, _) =>
        {
            Assert.True(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
            entered = f.JoinAsync(leaderAccepts);
            Assert.True(entered.IsCompleted); // Synchronous fail-closed; no attempted pair wait.
        };
        f.World.Set(f.Connections[2], f.Owner(2));
        Assert.NotNull(entered);
        await entered!.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
        Assert.True(f.Teams.TryReadTournamentTeam(team, out var state));
        Assert.Single(state!.Members);
        Assert.False(f.Teams.TryCaptureTournamentTeam(f.Client(1), out _));
        f.AssertNoFrames();
    }

    [Fact]
    public async Task NativeTeamWorldGateDepthUnwindsAfterNestedCallbackException()
    {
        using var f = new NativeTeamMemberFixture();
        var nested = new WorldSessionStore((_, _, _) =>
        {
            Assert.True(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
            throw new InvalidOperationException("expected nested callback failure");
        });
        f.OnStorePublished = (_, _, _) =>
        {
            Assert.True(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
            var error = Assert.Throws<InvalidOperationException>(() =>
                nested.Set(f.Connections[1], f.Owner(1)));
            Assert.Equal("expected nested callback failure", error.Message);
            Assert.True(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
            throw new InvalidOperationException("expected outer callback failure");
        };
        var outerError = Assert.Throws<InvalidOperationException>(() =>
            f.World.Set(f.Connections[2], f.Owner(2)));
        Assert.Equal("expected outer callback failure", outerError.Message);
        Assert.False(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
        await f.CreateAsync(); // Legal ordinary entry must not be poisoned by either throw.
        f.AssertCreateFrames(0);
        Assert.True(f.Teams.TryCaptureTournamentTeam(f.Client(0), out _));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task NativeTeamAcceptRefreshesSameClientAdmissionAfterInitialAwaitedLog(int changedGate)
    {
        using var f = new NativeTeamMemberFixture();
        await f.CreateAsync();
        var actor = f.Client(1);
        var team = f.Team(0);
        f.ClearFrames();
        f.OnHandled = () => f.CopyAdmissionGate(1, changedGate);
        await f.JoinAsync(leaderAccepts: false);
        Assert.True(f.Holders.TryRead(actor, out _)); // Same client, NOT retirement.
        Assert.False(f.Teams.TryCaptureTournamentTeam(actor, out _));
        Assert.True(f.Teams.TryReadTournamentTeam(team, out var state));
        Assert.Single(state!.Members);
        f.AssertNoFrames();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task NativeTeamCreateRefreshesMapAndRealmAfterInitialAwaitedLog(int changedGate)
    {
        using var f = new NativeTeamMemberFixture();
        var actor = f.Client(0);
        f.OnHandled = () => f.CopyAdmissionGate(0, changedGate);
        await f.CreateAsync();
        Assert.True(f.Holders.TryRead(actor, out _));
        Assert.False(f.Teams.TryCaptureTournamentTeam(actor, out _));
        f.AssertNoFrames();
    }

    [Fact]
    public async Task NativeTeamCreateDoesNotInventAnAliveGateAfterInitialAwaitedLog()
    {
        using var f = new NativeTeamMemberFixture();
        f.OnHandled = () => f.CopyAdmissionGate(0, 1);
        await f.CreateAsync(); // Native Create checks map/realm, unlike the two accept modes.
        Assert.Equal(0, f.Owner(0).Character.MinHitPoints);
        Assert.True(f.Teams.TryCaptureTournamentTeam(f.Client(0), out _));
        f.AssertCreateFrames(0);
    }

    [Fact]
    public async Task NativeTeamPublicReCreateKeepsHistoricalRosterAndReplacesOnlyCurrentTeamReference()
    {
        using var f = new NativeTeamMemberFixture();
        await f.CreateAsync();
        await f.JoinAsync(leaderAccepts: false);
        var original = f.Client(0);
        var member = f.Client(1);
        var oldTeam = f.Team(0);
        var before = f.Owner(0);
        f.World.Set(f.Connections[0], before with
        { Character = before.Character with { Name = "NativeRecreated" } });
        Assert.True(f.Holders.TryRead(original, out _)); // SAME original GameClient, not replacement.
        f.ClearFrames();

        await f.CreateAsync(); // Native unconditionally assigns a NEW user.Team.

        var currentTeam = f.Team(0);
        Assert.Equal(3U, currentTeam.Uid);
        Assert.NotEqual(oldTeam.Uid, currentTeam.Uid);
        Assert.False(f.Teams.IsTournamentTeamMember(oldTeam, original)); // CURRENT user.Team changed.
        Assert.True(f.Teams.IsTournamentTeamMember(currentTeam, original));
        Assert.True(f.Teams.IsTournamentTeamMember(oldTeam, member)); // Other player's Team did not change.
        Assert.True(f.Teams.TryCaptureTournamentTeam(member, out var retained));
        Assert.Equal(oldTeam.Uid, retained!.Uid);
        Assert.True(f.Teams.TryReadTournamentTeam(oldTeam, out var oldRoster));
        Assert.Equal(2, oldRoster!.Members.Count); // Native old Team.Members still contains BOTH references.
        Assert.Contains(oldRoster.Members, client => ReferenceEquals(client.Identity, original.Identity));
        Assert.Contains(oldRoster.Members, client => ReferenceEquals(client.Identity, member.Identity));
        Assert.Equal(original.Connection, oldRoster.Leader!.Connection);
        Assert.Equal("NativeLeader", oldRoster.Name); // Old stored TeamName is not silently refreshed.
        Assert.True(f.Teams.TryReadTournamentTeam(currentTeam, out var newRoster));
        Assert.Single(newRoster!.Members);
        Assert.Equal("NativeRecreated", newRoster.Name);
        var oldInfo = f.Teams.ReadNativeTeamMemberInfo(oldTeam);
        Assert.Equal("NativeLeader", Assert.Single(oldInfo, entry => entry.Uid == original.Uid).Name);
        var newInfo = Assert.Single(f.Teams.ReadNativeTeamMemberInfo(currentTeam));
        Assert.Equal("NativeRecreated", newInfo.Name);
        f.AssertCreateFrames(0);
        Assert.Empty(f.Frames[1]); // No invented old-team Dismiss/cleanup/fanout.
        Assert.Empty(f.Frames[2]);
    }

    private sealed class NativeTeamMemberFixture : IDisposable
    {
        internal const uint LeaderUid = 430_001;
        internal const uint MemberUid = 430_002;
        private readonly NativeTeamMemberLog log;
        internal WorldSessionStore World { get; }
        internal LegacyTournamentWatcherHolders Holders { get; }
        internal TeamSessionStateStore Teams { get; }
        internal ConquerTcpConnection[] Connections { get; }
        internal List<byte[]>[] Frames { get; } = [[], [], []];
        internal Action? OnHandled { get; set; }
        internal Action<int>? OnSend { get; set; }
        internal Action<ConquerTcpConnection, WorldSession?, WorldSession>? OnStorePublished { get; set; }

        internal NativeTeamMemberFixture()
        {
            World = new((connection, previous, current) =>
            {
                var callback = OnStorePublished;
                OnStorePublished = null;
                callback?.Invoke(connection, previous, current);
            });
            Holders = new(World);
            Teams = new(World, Holders);
            log = new(() =>
            {
                var callback = OnHandled;
                OnHandled = null;
                callback?.Invoke();
            });
            Connections = Enumerable.Range(0, 3).Select(index =>
                new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9962 + index),
                    sendAsync: (frame, _) =>
                    {
                        Frames[index].Add(frame.ToArray());
                        OnSend?.Invoke(index);
                        return ValueTask.CompletedTask;
                    })).ToArray();
            string[] names = ["NativeLeader", "NativeMember", "NativeThird"];
            for (var index = 0; index < Connections.Length; index++)
            {
                var session = CreateSession(uid: LeaderUid + index, name: names[index], x: 830 + index,
                    y: 584, mapId: 1_000, minHitPoints: 5_000, spellIds: [],
                    attributes: new Dictionary<string, string>
                    { ["OnMyOwnServer"] = "true", ["NativeMemberMarker"] = "original" });
                World.Set(Connections[index], session);
                Assert.True(Holders.TryCaptureClient(Connections[index], out _));
                Assert.False(Teams.HasTeam(Connections[index]));
            }
            AssertNoFrames();
        }

        internal WorldSession Owner(int index) =>
            Assert.IsType<WorldSession>(World.TryGet(Connections[index], out var session) ? session : null);
        internal LegacyTournamentWatcherClient Client(int index)
        {
            Assert.True(Holders.TryCaptureClient(Connections[index], out var client));
            return Assert.IsType<LegacyTournamentWatcherClient>(client);
        }
        internal LegacyTournamentTeam Team(int index)
        {
            Assert.True(Teams.TryCaptureTournamentTeam(Client(index), out var team));
            return Assert.IsType<LegacyTournamentTeam>(team);
        }
        internal void RebindSameRecord(int index)
        {
            var originalRecord = Owner(index);
            Assert.True(World.TryRemove(Connections[index], out var retired));
            Assert.Same(originalRecord, retired);
            World.Set(Connections[index], originalRecord);
        }
        internal void CopyMarker(int index, string marker)
        {
            var current = Owner(index);
            var attributes = current.Character.Attributes.ToDictionary(pair => pair.Key, pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);
            attributes["NativeMemberMarker"] = marker;
            World.Set(Connections[index], current with { Character = current.Character with { Attributes = attributes } });
        }
        internal void CopyAdmissionGate(int index, int changedGate)
        {
            Assert.InRange(changedGate, 0, 2);
            var current = Owner(index);
            var attributes = current.Character.Attributes.ToDictionary(pair => pair.Key, pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);
            if (changedGate == 2) attributes["OnMyOwnServer"] = "false";
            var changed = current with
            {
                Character = current.Character with
                {
                    MapId = changedGate == 0 ? 700 : current.Character.MapId,
                    MinHitPoints = changedGate == 1 ? 0 : current.Character.MinHitPoints,
                    Attributes = attributes,
                },
            };
            World.Set(Connections[index], changed);
        }
        internal Task CreateAsync() => SendAsync(0, MsgTeamType.Create, 0);
        internal Task JoinAsync(bool leaderAccepts) => leaderAccepts
            ? SendAsync(0, MsgTeamType.AcceptJoinRequest, MemberUid)
            : SendAsync(1, MsgTeamType.AcceptInvitation, LeaderUid);
        internal Task SendAsync(int actor, MsgTeamType type, uint target) =>
            NativeTeamMemberPackets.HandleAsync(new WorldPacketHandlerContext(
                Connections[actor], log, Owner(actor), Sessions: World, TeamStates: Teams,
                TournamentWatcherHolders: Holders),
                ReadFrame(new MsgTeamPacket(type, target).ToFrameBytes())).AsTask();
        internal void ClearFrames()
        { foreach (var list in Frames) list.Clear(); }
        internal void AssertNoFrames()
        { foreach (var list in Frames) Assert.Empty(list); }
        internal void AssertCreateFrames(int index)
        {
            Assert.Equal(4, Frames[index].Count);
            AssertSingleMemberInfo(index, 1, frameIndex: 0);
            Assert.True(LegacyTeamLeadershipPacket.TryParse(ReadFrame(Frames[index][1]), out var leadership));
            Assert.Equal(LegacyTeamLeadershipMode.Leader, leadership.Mode);
            Assert.Equal(Owner(index).Character.Uid, leadership.Uid);
            Assert.True(MsgTeamPacket.TryParse(ReadFrame(Frames[index][2]), out var sent));
            Assert.Equal(MsgTeamType.Send, sent.Type);
            Assert.True(MsgTeamPacket.TryParse(ReadFrame(Frames[index][3]), out var created));
            Assert.Equal(MsgTeamType.Create, created.Type);
        }
        internal void AssertJoinedFrames(int index, int count)
        {
            Assert.Equal(5, Frames[index].Count);
            for (var frame = 0; frame < 3; frame++)
                Assert.True(LegacyTeamLeadershipPacket.TryParse(ReadFrame(Frames[index][frame]), out _));
            Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(Frames[index][3]), out var update));
            Assert.Equal(LegacyMsgUpdateDataType.Team, Assert.Single(update.Updates).Type);
            Assert.Equal(Teams.GetTeamUid(Connections[index]), Assert.Single(update.Updates).Value);
            AssertSingleMemberInfo(index, count, frameIndex: 4);
        }
        internal void AssertSingleMemberInfo(int index, int count, int? frameIndex = null)
        {
            var frame = frameIndex is { } selected ? Frames[index][selected] : Assert.Single(Frames[index]);
            Assert.True(LegacyTeamMemberInfoPacket.TryParse(ReadFrame(frame), out var info));
            Assert.Equal(count, info.Entries.Count);
        }
        public void Dispose() => log.Dispose();
    }

    private sealed class NativeTeamMemberLog(Action onHandled) : TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
        public override Task WriteLineAsync(string? value)
        {
            if (value?.StartsWith("WorldServer handled MsgTeam ", StringComparison.Ordinal) == true)
                onHandled();
            return Task.CompletedTask;
        }
    }
}
