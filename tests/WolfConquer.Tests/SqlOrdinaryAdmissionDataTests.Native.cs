using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class SqlOrdinaryAdmissionDataTests
{
    [Fact]
    public Task CompleteNativePreparationOwnsQuestAndModelExposuresWithoutAnyPublication() => AssertCompleteNativePlanAsync(Plan(CompleteBundle()));

    internal static Task AssertCompleteNativePlanAsync(SqlOrdinaryAdmissionImagePlan plan) => RunNativeAsync(async (fixture, handler, log) =>
    {
        MutateCallerData(plan.ReadPreparedData()); // A prior caller's writable models cannot poison the sealed producer.
        var wire = await fixture.ConnectGameAsync(false); var birth = await fixture.NextAsync();
        Assert.Equal(NativePlayerLoopback.ProbeOpcode, birth.Opcode); Assert.Null(birth.Before); Assert.Null(birth.After);
        var client = Assert.IsType<LegacyNativeGameClientRuntime>(birth.Connection.NativeGameClient); var player = client.Player;
        player.Online = 9;
        var before = LegacyNativePlayerRuntime.ExistingPrefixLoad.Read(player); var flags = player.BitVector; var random = player.MyRandom;
        var raw = RawImages(plan.After);
        Assert.True(LegacyNativeCapturedCharacterPrefix.TryPrepareFromSqlPlan(birth.Connection, plan, out var prepared, out var reason), reason);
        var ready = Assert.IsType<LegacyNativeCapturedCharacterPrefix>(prepared);
        Assert.True(ready.IsCurrent()); Assert.Same(plan.After.Core, ready.Image);
        Assert.Equal(9001U, ready.PreparedPlayerValues.Uid); Assert.Equal(10U, ready.PreparedPlayerValues.Online);
        AssertQuests(ready.PreparedSnapshot.PlayerQuests); Assert.Equal(1L, ready.PreparedSnapshot.Online);
        var data = Assert.IsType<SqlOrdinaryAdmissionData>(ready.PreparedData);
        Assert.Same(plan.After, data.Source); AssertCompleteData(data);
        MutateCallerData(data);
        var snapshot = ready.PreparedSnapshot;
        Assert.IsAssignableFrom<IList<uint>>(snapshot.PlayerQuests.Quests[0].Intentions)[0] = 999;
        Assert.IsAssignableFrom<IList<PlayerQuestSnapshotEntry>>(snapshot.PlayerQuests.Quests)[1] = new(999, PlayerQuestStatus.Available, 9, [999U]);
        Assert.IsType<Dictionary<string, string>>(snapshot.Attributes)["Money"] = "999999";
        AssertCompleteData(Assert.IsType<SqlOrdinaryAdmissionData>(ready.PreparedData));
        AssertQuests(ready.PreparedSnapshot.PlayerQuests); Assert.Equal("1000", ready.PreparedSnapshot.Attributes["Money"]);
        Assert.Equal(plan.After.Core.CopyCharacterBytes(), ready.CopyOnlineCharacterBytes()); AssertRawUnchanged(raw, plan.After);
        Assert.Equal(before, LegacyNativePlayerRuntime.ExistingPrefixLoad.Read(player));
        Assert.Same(client, player.Owner); Assert.Equal(0U, player.UID); Assert.Equal(0U, player.Class); Assert.Equal((ushort)0, player.Level);
        Assert.Same(flags, player.BitVector); Assert.Same(random, player.MyRandom);
        Assert.Null(client.Beasts); Assert.Null(client.HairfaceStorage); Assert.Null(client.MySpells); Assert.Null(client.MyProfs);
        Assert.Null(client.Equipment); Assert.Null(client.OrdinarySpellBinding); Assert.False(client.FullLoading);
        Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection)); Assert.DoesNotContain("legacy MsgLoginGame Success", log.Text);
        Assert.True(ready.IsCurrent()); GC.KeepAlive(wire);
    });

    [Fact]
    public Task ExistingCoreOnlyPreparationKeepsNullCompleteDataAndDoesNotInventQuestHydration() => RunNativeAsync(async (fixture, _, _) =>
    {
        var plan = Plan(CompleteBundle()); var wire = await fixture.ConnectGameAsync(false); var birth = await fixture.NextAsync();
        Assert.True(LegacyNativeCapturedCharacterPrefix.TryPrepare(birth.Connection, plan.PreparedPrefix, out var prepared, out var reason), reason);
        var coreOnly = Assert.IsType<LegacyNativeCapturedCharacterPrefix>(prepared);
        Assert.Null(coreOnly.PreparedData); Assert.Empty(coreOnly.PreparedSnapshot.PlayerQuests.Quests);
        Assert.Equal(0U, birth.Connection.NativeGameClient!.Player.UID); GC.KeepAlive(wire);
    });

    [Fact]
    public Task GenuinePeerEofExpiresCompleteReadinessAndCannotReprepareTheSamePlan() => RunNativeAsync(async (fixture, handler, _) =>
    {
        var plan = Plan(CompleteBundle()); var wire = await fixture.ConnectGameAsync(false); var birth = await fixture.NextAsync();
        var client = birth.Connection.NativeGameClient!; var before = LegacyNativePlayerRuntime.ExistingPrefixLoad.Read(client.Player);
        Assert.True(LegacyNativeCapturedCharacterPrefix.TryPrepareFromSqlPlan(birth.Connection, plan, out var prepared, out var reason), reason);
        var ready = Assert.IsType<LegacyNativeCapturedCharacterPrefix>(prepared); Assert.True(ready.IsCurrent());
        await fixture.CloseClientTransportAsync(); Assert.True(birth.Connection.HasObservedTransportClose);
        Assert.False(ready.IsCurrent()); Assert.Same(client, birth.Connection.NativeGameClient);
        Assert.False(LegacyNativeCapturedCharacterPrefix.TryPrepareFromSqlPlan(birth.Connection, plan, out var later, out reason));
        Assert.Null(later); Assert.Equal("capturedNativePrefixOwnerUnavailable", reason);
        Assert.Equal(before, LegacyNativePlayerRuntime.ExistingPrefixLoad.Read(client.Player));
        Assert.Null(client.Beasts); Assert.Null(client.HairfaceStorage); Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection));
        GC.KeepAlive(wire);
    });

    private static async Task RunNativeAsync(Func<NativePlayerLoopback, WorldServerFrameHandler, NativePlayerDiagnosticLog, Task> body)
    {
        var prior = LegacyNativeHairfaceStorageTable.Hairfaces;
        using var beasts = new NativeLoginBeastMetadataScope(1000);
        try
        {
            LegacyNativeHairfaceStorageTable.Hairfaces = [new() { ID = 4201, Type = LegacyHairFaceStorageType.Avatar,
                Name = "Owned complete-data face", Classes = [7], Colors = [1, 0, 0, 0, 0, 0, 0] }];
            using var log = new NativePlayerDiagnosticLog();
            var handler = new WorldServerFrameHandler(snapshotLoader: null, protocolProfile: ConquerProtocolProfile.VisibleDx8D13, log: log);
            await NativePlayerLoopback.RunAsync(ConquerTcpWireMode.LegacyGameServer, fixture => body(fixture, handler, log),
                handler.HandleFrameAsync, handler.ReadCurrentSessionForNativeOwnerObservation);
            log.AssertNoWorldRuntimeDiagnostics();
        }
        finally { LegacyNativeHairfaceStorageTable.Hairfaces = prior; }
    }
}
