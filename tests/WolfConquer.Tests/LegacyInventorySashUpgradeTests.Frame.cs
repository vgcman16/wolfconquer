using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Theory]
    [InlineData(ConquerProtocolProfile.RootD11_7899)]
    [InlineData(ConquerProtocolProfile.VisibleDx8D13)]
    public Task ActualFrameSashUpgradeUsesItsExclusiveOwnerWithoutNestedSharedUpgrade(ConquerProtocolProfile profile) => RunAsync(async fixture =>
    {
        await fixture.EnterFrameAsync(profile);
        var before = fixture.Files();
        var frameCount = fixture.Sent.Count;
        var observed = 0;
        fixture.SendObserver = _ =>
        {
            Assert.Equal(ProjectSashFile(before[Path.GetRelativePath(fixture.Root, fixture.CharacterPath)], 60, 1),
                File.ReadAllBytes(fixture.CharacterPath));
            observed++;
        };

        await fixture.FrameAsync(Fixture.Request());

        Assert.Equal(3, observed);
        AssertFrameSashCommit(fixture, before, frameCount);
    });

    [WindowsTradeJournalFailureFact]
    public Task ActualD13FrameSashCharacterSharingFailureHasNoSuccessAndSameSessionRetriesAfterRealRelease() => RunAsync(async fixture =>
    {
        await fixture.EnterFrameAsync(ConquerProtocolProfile.VisibleDx8D13);
        var before = fixture.Files();
        var frameCount = fixture.Sent.Count;
        fixture.HoldCharacterFile();

        await fixture.FrameAsync(Fixture.Request());

        fixture.ReleaseFiles(); // Actual Frame returned before its owned leaf is released.
        fixture.AssertFiles(before);
        Assert.Equal(frameCount, fixture.Sent.Count);
        await fixture.FrameAsync(Fixture.Request());
        AssertFrameSashCommit(fixture, before, frameCount);
    });

    private static void AssertFrameSashCommit(Fixture fixture, Dictionary<string, byte[]> before, int frameCount)
    {
        var expected = new Dictionary<string, byte[]>(before, StringComparer.OrdinalIgnoreCase);
        var path = Path.GetRelativePath(fixture.Root, fixture.CharacterPath);
        expected[path] = ProjectSashFile(before[path], 60, 1);
        fixture.AssertFiles(expected);
        var saved = CharacterSnapshotParser.ParseText(File.ReadAllText(fixture.CharacterPath), fixture.CharacterPath);
        Assert.Equal(60, saved.ConquerPoints);
        Assert.Equal("1", saved.Attributes["InventorySashCount"]);
        var frames = fixture.Sent.Skip(frameCount).ToArray();
        Assert.Equal(3, frames.Length);
        Assert.Equal(new LegacyMsgUpdatePacket(Fixture.Uid, [new(LegacyMsgUpdateDataType.ConquerPoints, 60)]).ToFrameBytes(), frames[0]);
        Assert.Equal(new LegacyMsgUpdatePacket(Fixture.Uid, [new(LegacyMsgUpdateDataType.InventorySashMax, 300)]).ToFrameBytes(), frames[1]);
        Assert.Equal(new LegacyMsgUpdatePacket(Fixture.Uid, [new(LegacyMsgUpdateDataType.InventorySash, 1)]).ToFrameBytes(), frames[2]);
    }

    private sealed partial class Fixture
    {
        private WorldServerFrameHandler? frameOwner;

        internal async Task EnterFrameAsync(ConquerProtocolProfile profile)
        {
            Seed(frameLogin: true);
            var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
            var loader = new CharacterSnapshotLoader(Users, Items, playerSpellsRoot: null,
                playerProficienciesRoot: null, playersCombatGearRoot: null, playerQuestsRoot: null);
            frameOwner = new WorldServerFrameHandler(loader, handoff, worldData: null,
                runtimeUpdateLoop: LegacyRuntimeUpdateLoopOptions.Disabled, log: Log, protocolProfile: profile);
            var ticket = handoff.Issue(Account, "sash-fixture", ConquerRuntimeDefaults.WorldServer);
            await FrameAsync(new InternalWorldEnterRequestPacket(ticket.Token).ToFrameBytes());
            var parsed = CharacterSnapshotParser.ParseText(File.ReadAllText(CharacterPath), CharacterPath);
            Assert.Equal(WorldSnapshotResponsePacket.Build(WorldSnapshotWireStatus.Success, Account, parsed), Sent[0]);
            await FrameAsync(new MsgActionPacket { ObjId = Uid, Type = MsgActionType.SetLocation }.ToFrameBytes());
            Assert.Equal(150, parsed.ConquerPoints);
            Assert.Equal("0", parsed.Attributes["InventorySashCount"]);
        }

        internal async Task FrameAsync(byte[] bytes)
        {
            // Real public login/dispatch initializes and supplies its own Recovery,
            // Trades and barrier. No reflected Store/owners or accepting callback.
            // None avoids manufacturing detached connection-cancel cleanup.
            var actual = frameOwner!.HandleFrameAsync(Connection, ReadFrame(bytes), CancellationToken.None).AsTask();
            Calls.Add(actual);
            await ObserveAsync(actual);
        }
    }
}
