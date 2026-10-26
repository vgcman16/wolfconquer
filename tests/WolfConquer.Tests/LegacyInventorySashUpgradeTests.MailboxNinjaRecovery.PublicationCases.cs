using System.Net;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Theory]
    [InlineData("valid")]
    [InlineData("unadmitted")]
    [InlineData("rebound-player")]
    [InlineData("rebound-owner")]
    [InlineData("loaded-phase")]
    [InlineData("stale-session")]
    public Task ColdNinjaDetachedPublicationRequiresCurrentStorePlayerPhaseAndUnchangedEmptyOwnerSlot(string fault) =>
        RunAsync(async fixture =>
        {
            fixture.SeedMailboxPackageJoin(job: 5051);
            fixture.SeedPackageJoinNinjaLine();
            var loader = new CharacterSnapshotLoader(fixture.Users, fixture.Items, null, null,
                playersCombatGearRoot: fixture.PackageGearRoot, playerQuestsRoot: null);
            var saved = Assert.IsType<CharacterSnapshot>(loader.LoadByAccountId(fixture.Account));
            var client = new LegacyNativeGameClientRuntime();
            var player = client.Player;
            var files = fixture.Files();
            var first = fixture.Sent.Count;
            var connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
            {
                fixture.Sent.Add(frame.ToArray());
                return ValueTask.CompletedTask;
            }, nativeGameClient: client);
            var sessions = new WorldSessionStore();
            var errors = new List<Exception>();
            try
            {
                Assert.True(player.TryPrepareMailboxRecoveryLoad(client, saved, out var scalars));
                Assert.True(LegacyNativeNinja.TryPrepareMailboxRecoveryOwner(client, saved, out var prepared));
                Assert.NotNull(prepared);
                Assert.Null(client.MyNinja);
                Assert.Equal(0U, player.UID);
                Assert.Equal(0U, player.Class);
                Assert.Equal(0U, player.ClassExperience);
                Assert.True(prepared.IsDetachedCurrent());
                fixture.AssertFiles(files);
                var admitted = new WorldSession(fixture.Account, saved.Name, saved,
                    loader.LoadPlayerItemsByCharacterUid(Fixture.Uid), loader.LoadPlayerSpellsByCharacterUid(Fixture.Uid),
                    loader.LoadPlayerProficienciesByCharacterUid(Fixture.Uid),
                    CombatGear: loader.LoadCombatGearByCharacterUid(Fixture.Uid), UsersRoot: fixture.Users)
                { NativeGameClient = client };
                sessions.Set(connection, admitted);
                Assert.True(scalars!.TryPublish(sessions, connection, admitted));
                switch (fault)
                {
                    case "unadmitted": Assert.True(sessions.TryRemove(connection, out _)); break;
                    case "rebound-player": client.Player = new LegacyNativePlayerRuntime(client); break;
                    case "rebound-owner": client.MyNinja = new LegacyNativeNinja(client); break;
                    case "loaded-phase": client.FullLoading = true; break;
                    case "stale-session": sessions.Set(connection, admitted with { Username = "Actual newer owner" }); break;
                }
                var previousSlot = client.MyNinja;
                var nativeBefore = JsonSerializer.Serialize(new
                { client.Player.UID, client.Player.Class, client.Player.ClassExperience, client.FullLoading });
                Assert.Equal(fault == "valid", prepared.TryPublish(sessions, connection, admitted));
                if (fault == "valid")
                {
                    var ninja = Assert.IsType<LegacyNativeNinja>(client.MyNinja);
                    Assert.True(ninja.IsCombatOwnedBy(client));
                    Assert.Equal(25U, ninja.Exp);
                    Assert.Equal(2, ninja.Items.Count);
                    Assert.Equal(41U, ninja.Items[100U].UK);
                    Assert.Equal(42U, ninja.Items[404U].UK);
                    Assert.True(prepared.IsPublishedCurrent());
                    Assert.False(prepared.TryPublish(sessions, connection, admitted));
                    Assert.True(prepared.IsPublishedCurrent());
                    Assert.True(ninja.TryPrepareMailboxPoints(client, PackageJoinNinjaLine, 50, out var reward));
                    Assert.Equal(PackageJoinNinjaPointsFrame(), reward!.CopyFrame());
                    Assert.Equal(25U, ninja.Exp); // Pure preparation has not granted the reward.
                }
                else Assert.Same(previousSlot, client.MyNinja);
                Assert.Equal(nativeBefore, JsonSerializer.Serialize(new
                { client.Player.UID, client.Player.Class, client.Player.ClassExperience, client.FullLoading }));
                Assert.Equal(first, fixture.Sent.Count);
                fixture.AssertFiles(files);
                Assert.Null(client.Socket);
            }
            catch (Exception error) { errors.Add(error); }
            try
            {
                sessions.TryRemove(connection, out _);
                var close = connection.CompleteTransportCloseAsync();
                fixture.Calls.Add(close);
                await fixture.ObserveAsync(close);
            }
            catch (Exception error) { errors.Add(error); }
            if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
            if (errors.Count > 1) throw new AggregateException("Detached Ninja recovery and transport cleanup failed.", errors);
        });
}
