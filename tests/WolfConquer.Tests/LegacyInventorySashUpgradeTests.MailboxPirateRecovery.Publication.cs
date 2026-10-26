using System.Net;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    internal static Task RunDetachedPirateRecoveryScenarioAsync(string fault) => RunPackageJoinAsync(async (fixture, join) =>
    {
        var before = await SeedPirateRecoveryAsync(fixture, join, "");
        var intent = PreparePirateRecoveryIntent(fixture, join, before, claimAll: false, allocated: false, scenario: "");
        var sessions = join.SessionStore;
        var producer = Assert.IsType<LegacyMailboxPackageProducer>(sessions.MailboxPackages);
        Assert.True(sessions.TryRemove(join.Connection!, out var old));
        LegacySecondaryPasswordRuntime.Invalidate(join.Connection!, old!);
        var loader = new CharacterSnapshotLoader(fixture.Users, fixture.Items, null, null,
            playersCombatGearRoot: fixture.PackageGearRoot, playerQuestsRoot: null);
        var saved = Assert.IsType<CharacterSnapshot>(loader.LoadByAccountId(fixture.Account));
        var attributes = new PirateRecoveryAttributes(saved.Attributes);
        saved = saved with { Attributes = attributes }; // Same exact real saved values; callback arms only after admission.
        var client = new LegacyNativeGameClientRuntime();
        var player = client.Player;
        var connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
        {
            fixture.Sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        }, nativeGameClient: client);
        var files = fixture.PackageFiles();
        var first = fixture.Sent.Count;
        var errors = new List<Exception>();
        try
        {
            Assert.True(player.TryPrepareMailboxRecoveryLoad(client, saved, out var scalars));
            Assert.True(LegacyNativeMyCombatGear.TryPrepareMailboxRecoveryOwner(producer, client, fixture.Account,
                saved, out var prepared, out var reason), reason);
            Assert.NotNull(prepared);
            Assert.True(prepared.IsDetachedCurrent());
            Assert.Null(client.CombatGear);
            Assert.Equal(0U, player.UID); Assert.Equal(0U, player.Class); Assert.Equal(0U, player.ClassExperience);
            AssertPackageJoinFiles(fixture, files);
            var candidate = new WorldSession(fixture.Account, saved.Name, saved,
                loader.LoadPlayerItemsByCharacterUid(Fixture.Uid), loader.LoadPlayerSpellsByCharacterUid(Fixture.Uid),
                loader.LoadPlayerProficienciesByCharacterUid(Fixture.Uid),
                CombatGear: loader.LoadCombatGearByCharacterUid(Fixture.Uid), UsersRoot: fixture.Users)
            { NativeGameClient = client };
            Assert.True(sessions.TrySetMailboxRecoveryOwner(connection, candidate, producer, out var admitted, out reason), reason);
            Assert.Same(candidate, admitted);
            Assert.True(scalars!.TryPublish(sessions, connection, candidate));
            switch (fault)
            {
                case "unadmitted": Assert.True(sessions.TryRemove(connection, out _)); break;
                case "rebound-player": client.Player = new LegacyNativePlayerRuntime(client); break;
                case "rebound-owner": client.CombatGear = new LegacyNativeMyCombatGear(client); break;
                case "loaded-phase": client.FullLoading = true; break;
                case "stale-session":
                    Assert.True(sessions.TryRemove(connection, out _));
                    Assert.True(sessions.TrySetMailboxRecoveryOwner(connection, candidate with { Username = "New real owner" },
                        producer, out _, out reason), reason);
                    break;
                case "callback-store":
                    attributes.OnExperienceRead = () => Assert.True(sessions.TryRemove(connection, out _));
                    break;
                case "callback-exp": attributes.OnExperienceRead = () => client.Player.ClassExperience = 32; break;
            }
            var previousSlot = client.CombatGear;
            var nativeBefore = JsonSerializer.Serialize(new
            { client.Player.UID, client.Player.Class, client.Player.ClassExperience, client.FullLoading });
            Assert.Equal(fault == "valid", prepared.TryPublish(sessions, connection, candidate));
            if (fault == "valid")
            {
                var gear = Assert.IsType<LegacyNativeMyCombatGear>(client.CombatGear);
                Assert.True(gear.IsCombatOwnedBy(client));
                AssertPirateRecoveryNative(gear, PirateRecoveryGear());
                Assert.True(prepared.IsPublishedCurrent());
                Assert.False(prepared.TryPublish(sessions, connection, candidate));
                Assert.True(prepared.IsPublishedCurrent());
                var projection = gear.CaptureMailboxPirateReward(client, candidate.CombatGear!,
                    File.ReadAllBytes(fixture.PackageGearPath), 50, 50);
                Assert.Equal(Convert.FromHexString("1E00A009080510A0FA4E18002210080710001800200028003000380040455451536572766572"),
                    projection.CopyFrameForPoints(50));
                AssertPirateRecoveryNative(gear, PirateRecoveryGear()); // Preparation never credits the reward.
            }
            else Assert.Same(previousSlot, client.CombatGear);
            if (fault == "callback-exp")
            {
                Assert.Equal(32U, client.Player.ClassExperience);
                Assert.Equal(Fixture.Uid, client.Player.UID);
                Assert.Equal(7051U, client.Player.Class);
                Assert.False(client.FullLoading);
            }
            else Assert.Equal(nativeBefore, JsonSerializer.Serialize(new
            { client.Player.UID, client.Player.Class, client.Player.ClassExperience, client.FullLoading }));
            if (fault.StartsWith("callback-", StringComparison.Ordinal)) Assert.True(attributes.Fired);
            if (fault == "callback-store") Assert.False(sessions.TryGet(connection, out _));
            Assert.Equal(first, fixture.Sent.Count);
            AssertPackageJoinFiles(fixture, files);
            AssertNinjaRecoveryIntentRetained(join, intent);
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
        if (errors.Count > 1) throw new AggregateException("Detached Pirate publication and actual transport cleanup failed.", errors);
    }, job: 7051);
}
