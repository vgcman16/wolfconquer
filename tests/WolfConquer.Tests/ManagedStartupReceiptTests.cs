using System.Buffers;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class ManagedStartupReceiptTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public Task ActualManagedStarterAndStatWritesKeepFullSavedMirrorsAndRealQuests(bool starter) =>
        RunAsync(starter, "success");

    [Fact]
    public Task ExternalCharacterEditAfterActualLoadIsNotAdoptedAsAStartupReceipt() =>
        RunAsync(false, "drift");

    [Fact]
    public Task ActualWindowsSharingRefusalDoesNotClaimAppliedStatsOrPublishAHolder()
    {
        if (!OperatingSystem.IsWindows())
            throw Xunit.Sdk.SkipException.ForSkip("The actual replacement-sharing refusal requires Windows.");
        return RunAsync(false, "sharing");
    }

    private static async Task RunAsync(bool starter, string scenario)
    {
        var data = new LegacyOwnedAdmissionRuntimeFixture(bootstrap: false);
        ConquerTcpConnection? connection = null;
        FileStream? sharing = null;
        var failures = new List<Exception>();
        using var log = new StringWriter();
        try
        {
            Prepare(data, starter);
            var before = data.Files();
            var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
            var ticket = handoff.Issue(LegacyOwnedAdmissionRuntimeFixture.Uid, "managed-startup-receipt", ConquerRuntimeDefaults.WorldServer);
            var catalog = Catalog();
            var handler = new WorldServerFrameHandler(data.Loader, handoff, catalog,
                LegacyRuntimeUpdateLoopOptions.Disabled, log, protocolProfile: ConquerProtocolProfile.VisibleDx8D13);
            var sent = new List<byte[]>();
            var first = true;
            byte[]? drifted = null;
            connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (memory, _) =>
            {
                // This is the actual snapshot response, after the locked loader
                // captured its receipt and before private startup begins.
                if (first)
                {
                    first = false;
                    if (scenario == "sharing") sharing = new FileStream(data.UserPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    if (scenario == "drift")
                    {
                        var text = CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(data.UserPath), out var encoding);
                        drifted = encoding.GetPreamble().Concat(encoding.GetBytes(text + "\r\n; external owned-fixture edit\r\n")).ToArray();
                        File.WriteAllBytes(data.UserPath, drifted);
                    }
                }
                sent.Add(memory.ToArray()); return ValueTask.CompletedTask;
            }, _ => ValueTask.CompletedTask);
            await handler.HandleFrameAsync(connection, ReadFrame(new InternalWorldEnterRequestPacket(ticket.Token).ToFrameBytes()));
            var store = Assert.IsType<WorldSessionStore>(typeof(WorldServerFrameHandler)
                .GetField("_sessions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(handler));
            if (scenario != "success")
            {
                Assert.False(store.TryGet(connection, out _)); Assert.Single(sent); Assert.False(first);
                if (scenario == "sharing")
                {
                    Assert.NotNull(sharing);
                    Assert.Contains("managedStartupPersistenceRefused:characterWrite:capturedImageUnchanged:", log.ToString(), StringComparison.Ordinal);
                }
                else Assert.Contains("managedStartupPersistenceRefused:beforeImageOrOwnerChanged", log.ToString(), StringComparison.Ordinal);
                Assert.DoesNotContain("applied tuned legacy first-character starter state", log.ToString(), StringComparison.Ordinal);
                Assert.DoesNotContain("repaired legacy class level stats", log.ToString(), StringComparison.Ordinal);
                var expected = before.ToDictionary(row => row.Key, row => row.Value, StringComparer.OrdinalIgnoreCase);
                if (drifted is not null) expected[Path.GetRelativePath(data.Root, data.UserPath)] = drifted;
                data.AssertFiles(expected);
            }
            else
            {
                Assert.True(store.TryGet(connection, out var current), log.ToString()); Assert.NotNull(current);
                Assert.Null(current.NativeGameClient); Assert.Null(connection.NativeGameClient);
                var disk = data.Loader.LoadByAccountId(LegacyOwnedAdmissionRuntimeFixture.Uid)!;
                var expected = starter ? (5L, 2L, 3L, 0L) : (10L, 5L, 20L, 0L);
                Assert.Equal(expected, (current.Character.Strength, current.Character.Agility, current.Character.Vitality, current.Character.Spirit));
                Assert.Equal(expected, (disk.Strength, disk.Agility, disk.Vitality, disk.Spirit));
                foreach (var pair in CharacterSnapshotScalarPersistence.BuildPersistedValues(current.Character))
                {
                    Assert.Equal(pair.Value, disk.Attributes[pair.Key]);
                    Assert.Equal(pair.Value, current.Character.Attributes[pair.Key]);
                }
                Assert.Same(StringComparer.OrdinalIgnoreCase, LegacyNativeAttributeMapProof.CaptureRaw(current.Character.Attributes).CapturedComparer);
                Assert.Equal("startup-owned-value", current.Character.Attributes["KeepBytes"]);
                Assert.Equal("retain-mixed-case", current.Character.Attributes["MixedCaseSentinel"]);
                Assert.Equal("second", current.Character.Attributes["OpaqueCase"]);
                Assert.Contains("OpaqueCase=first\r\nopaquecase=second\r\n",
                    CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(data.UserPath), out _), StringComparison.Ordinal);
                Assert.True(disk.Attributes.ContainsKey("EpicTrojan"));
                foreach (var key in new[] { "MainFlag", "StageEpicTrojanQuest", "ChangeEpicTrojan", "ChangeArrayEpicTrojan",
                    "EpicTrojanEvilArrayPoints", "EpicTrojanAbysalStage", "ChangeMr_MirrorEpicTrojan",
                    "ChangeGeneralPakEpicTrojan", "EpicTrojanMrMirrorPrograss" })
                    Assert.Equal(disk.Attributes[key], current.Character.Attributes[key]);
                Assert.Equal(uint.MaxValue.ToString(CultureInfo.InvariantCulture), disk.Attributes["Vigor"]);
                Assert.Equal("0", current.Character.Attributes["Vigor"]);
                var questKey = Path.GetRelativePath(data.Root, Path.Combine(data.Quests, LegacyOwnedAdmissionRuntimeFixture.Uid + ".bin"));
                Assert.Equal(before[questKey], PlayerQuestSnapshotLoader.Serialize(current.Character.PlayerQuests));
                Assert.Equal(before[questKey], File.ReadAllBytes(Path.Combine(data.Quests, LegacyOwnedAdmissionRuntimeFixture.Uid + ".bin")));
                if (starter)
                {
                    Assert.Equal(new uint[] { 132_009, 150_003, 561_301 }, current.PlayerItems.EquipmentItems.Select(row => row.ItemId).Order());
                    Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(current.PlayerItems),
                        File.ReadAllBytes(Path.Combine(data.Items, LegacyOwnedAdmissionRuntimeFixture.Uid + ".bin")));
                    Assert.Contains("applied tuned legacy first-character starter state", log.ToString(), StringComparison.Ordinal);
                }
                else Assert.Contains("repaired legacy class level stats", log.ToString(), StringComparison.Ordinal);
                Assert.False(LegacyTradeJournal.HasPending(data.Users));
            }
        }
        catch (Exception error) { failures.Add(error); }
        try { sharing?.Dispose(); } catch (Exception error) { failures.Add(error); }
        if (connection is not null)
            try { await connection.CompleteTransportCloseAsync(); } catch (Exception error) { failures.Add(error); }
        if (failures.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures.Count > 1) throw new AggregateException(failures);
        data.Dispose(); // Failed owned inputs are deliberately retained.
    }

    private static void Prepare(LegacyOwnedAdmissionRuntimeFixture data, bool starter)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Class"] = "2000", ["FirstClass"] = starter ? "0" : "2000", ["SecoundeClass"] = "0", ["Reborn"] = "0",
            ["Level"] = starter ? "1" : "10", ["Experience"] = starter ? "0" : "1", ["Strength"] = "0", ["Agility"] = "0",
            ["Vitaliti"] = "0", ["Spirit"] = "0", ["Atributes"] = "0", ["MinHitPoints"] = "1", ["MinMana"] = "0",
            ["Money"] = "0", ["ConquerPoints"] = "0", ["BoundConquerPoints"] = "0", ["BankCps"] = "0", ["PkPoints"] = "0",
            ["Vigor"] = uint.MaxValue.ToString(CultureInfo.InvariantCulture), ["MixedCaseSentinel"] = "retain-mixed-case",
            ["ClaimStateGift"] = "0",
        };
        Assert.True(CharacterSnapshotFileCoordinator.TryUpdateCharacterAttributes(data.UserPath, LegacyOwnedAdmissionRuntimeFixture.Uid,
            values, detailedIoFailures: true, out var reason), reason);
        new PlayerItemSnapshotLoader(data.Items).SaveByCharacterUid(LegacyOwnedAdmissionRuntimeFixture.Uid, PlayerItemSnapshot.Empty);
        new PlayerQuestSnapshotLoader(data.Quests).SaveByCharacterUid(LegacyOwnedAdmissionRuntimeFixture.Uid,
            new PlayerQuestSnapshot(new[] { new PlayerQuestSnapshotEntry(7001, PlayerQuestStatus.Accepted, 9, new uint[] { 1, 2 }) }));
        var text = CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(data.UserPath), out var encoding);
        Assert.DoesNotContain("MaxMana=", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ExpBallUsed=", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EpicTrojan=", text, StringComparison.OrdinalIgnoreCase);
        const string unrelated = "[Unrelated]";
        Assert.Contains(unrelated, text, StringComparison.Ordinal);
        text = text.Replace(unrelated, "OpaqueCase=first\r\nopaquecase=second\r\n" + unrelated, StringComparison.Ordinal);
        File.WriteAllBytes(data.UserPath, encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray());
    }

    private static WorldDataCatalog Catalog()
    {
        var items = new[] { 132_009, 150_003, 561_301 }.Select(id =>
        {
            var fields = Enumerable.Repeat("0", 64).ToArray(); fields[0] = id.ToString(CultureInfo.InvariantCulture);
            fields[1] = "ManagedStarter" + id; fields[ItemType.DurabilityFieldIndex] = "1";
            return new ItemType(id, fields[1], fields);
        }).ToArray();
        ClassLevelStat Row(int level, int strength, int agility, int vitality) => new("Warrior", level, strength, agility, vitality, 0,
            "fixture://managed-startup-stats", level, level.ToString(CultureInfo.InvariantCulture), "", "",
            new[] { strength.ToString(CultureInfo.InvariantCulture), agility.ToString(CultureInfo.InvariantCulture), vitality.ToString(CultureInfo.InvariantCulture), "0" });
        return LegacyItemWorldDataFixture.Create(items, Array.Empty<XuanbaoRandomAttribute>(), classLevelStats: new[] { Row(1, 5, 2, 3), Row(10, 10, 5, 20) });
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _)); return frame;
    }
}
