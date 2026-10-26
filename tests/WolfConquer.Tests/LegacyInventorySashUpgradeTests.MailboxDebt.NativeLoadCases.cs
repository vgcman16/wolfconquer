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
    [InlineData("loaded-phase")]
    [InlineData("changed-cold-state")]
    [InlineData("different-saved-owner")]
    public Task ActualColdNativeRecoveryLoadRequiresTheSameRawSavedAndCurrentStoreOwner(string scenario) =>
        RunAsync(async fixture =>
        {
            fixture.SeedMailboxPackageJoin();
            var loader = new CharacterSnapshotLoader(fixture.Users, fixture.Items, null, null,
                playersCombatGearRoot: fixture.PackageGearRoot, playerQuestsRoot: null);
            var saved = Assert.IsType<CharacterSnapshot>(loader.LoadByAccountId(fixture.Account));
            var raw = new WorldSession(fixture.Account, saved.Name, saved,
                loader.LoadPlayerItemsByCharacterUid(Fixture.Uid), loader.LoadPlayerSpellsByCharacterUid(Fixture.Uid),
                loader.LoadPlayerProficienciesByCharacterUid(Fixture.Uid),
                CombatGear: loader.LoadCombatGearByCharacterUid(Fixture.Uid), UsersRoot: fixture.Users);
            var files = fixture.Files();
            var native = new LegacyNativeGameClientRuntime();
            var player = native.Player;
            var first = fixture.Sent.Count;
            var connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
            {
                fixture.Sent.Add(frame.ToArray());
                return ValueTask.CompletedTask;
            }, nativeGameClient: native);
            var errors = new List<Exception>();
            try
            {
                Assert.True(player.TryPrepareMailboxRecoveryLoad(native, saved, out var projection));
                Assert.NotNull(projection);
                Assert.Equal(0U, player.UID);
                Assert.Equal(0U, player.Class);
                Assert.Equal(0U, player.ClassExperience);
                Assert.Equal(first, fixture.Sent.Count);
                fixture.AssertFiles(files);
                var admitted = raw with { NativeGameClient = native };
                var sessions = new WorldSessionStore();
                if (scenario != "unadmitted") sessions.Set(connection, admitted);
                switch (scenario)
                {
                    case "rebound-player": native.Player = new LegacyNativePlayerRuntime(native); break;
                    case "loaded-phase": native.FullLoading = true; break;
                    case "changed-cold-state": player.Name = "Changed after preparation"; break;
                    case "different-saved-owner":
                        admitted = admitted with { Character = admitted.Character with { Name = "Different saved owner" } };
                        sessions.Set(connection, admitted);
                        break;
                }
                var before = new { player.UID, player.Class, player.ClassExperience, player.Body, player.Face, player.Name, player.Spouse };
                var applied = projection!.TryPublish(sessions, connection, admitted);
                Assert.Equal(scenario == "valid", applied);
                if (applied)
                {
                    Assert.Equal(Fixture.Uid, player.UID);
                    Assert.Equal(10U, player.Class);
                    Assert.Equal(PackageClassExperience, player.ClassExperience);
                    Assert.Equal(saved.Name, player.Name);
                    Assert.Equal(saved.Body, player.Body);
                    Assert.Equal(saved.Face, player.Face);
                    Assert.Equal("None", player.Spouse);
                    Assert.True(projection.IsPublishedCurrent());
                    Assert.False(projection.TryPublish(sessions, connection, admitted));
                    Assert.True(projection.IsPublishedCurrent());
                }
                else
                    Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(new
                    { player.UID, player.Class, player.ClassExperience, player.Body, player.Face, player.Name, player.Spouse }));
                Assert.Equal(first, fixture.Sent.Count);
                fixture.AssertFiles(files);
                Assert.Null(native.MyNinja);
                Assert.Null(native.CombatGear);
            }
            catch (Exception error) { errors.Add(error); }
            try
            {
                var close = connection.CompleteTransportCloseAsync();
                fixture.Calls.Add(close);
                await fixture.ObserveAsync(close);
            }
            catch (Exception error) { errors.Add(error); }
            if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
            if (errors.Count > 1) throw new AggregateException("Native cold recovery load and owned transport cleanup failed.", errors);
        });
}
