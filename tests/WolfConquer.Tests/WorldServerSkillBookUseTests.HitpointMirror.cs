using System.Buffers;
using System.Reflection;
using System.Text;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerSkillBookUseTests
{
    [Theory]
    [InlineData(false, false, false)] [InlineData(true, false, false)]
    [InlineData(false, true, false)] [InlineData(true, true, false)]
    [InlineData(false, false, true)] [InlineData(true, false, true)]
    public async Task ActualFrameHpNormalizationRetainsSavedMirrorAndRejectsLaterExternalDrift(
        bool externalDrift, bool existingComposite, bool vigorSeed)
    {
        var users = Path.Combine(_dbRoot, "Users"); var items = Path.Combine(_dbRoot, "PlayersItems");
        var spells = Path.Combine(_dbRoot, "PlayersSpells");
        foreach (var route in new[] { users, items, spells }) Directory.CreateDirectory(route);
        WriteSnapshot(users, 1_293_600, 1_293_600);
        var characterPath = Path.Combine(users, "1293600.ini");
        var text = File.ReadAllText(characterPath).Replace("[PaidNobility]", "OpaqueHpSentinel=keep-raw-170\n[PaidNobility]", StringComparison.Ordinal);
        if (vigorSeed)
            text = text.Replace("[PaidNobility]", "Vigor=9999\n[PaidNobility]", StringComparison.Ordinal);
        if (existingComposite)
            text = text.Replace("[PaidNobility]", "EpicTrojan=4/5/6/7/1/1/8/9/10/11/12/13/14/15/1/16/17/18/1/19/\n[PaidNobility]", StringComparison.Ordinal);
        File.WriteAllText(characterPath, text);
        Assert.Contains("MinHitPoints=61258", text);
        File.WriteAllBytes(Path.Combine(items, "1293600.bin"), PlayerItemSnapshotLoaderTests.BuildPlayerItems(
            [PlayerItemSnapshotLoaderTests.DbItem(2001, 725016, 0, stackSize: 1)], []));
        var savedSpells = PlayerSpellSnapshotLoaderTests.BuildPlayerSpells([6001]);
        savedSpells[4 + 11] = 0xA5;
        File.WriteAllBytes(Path.Combine(spells, "1293600.bin"), savedSpells);
        using var log = new StringWriter(); var sent = new List<byte[]>();
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(users), handoff, log);
        var field = typeof(WorldServerFrameHandler).GetField("_sessions", BindingFlags.NonPublic | BindingFlags.Instance);
        var sessions = Assert.IsType<WorldSessionStore>(field!.GetValue(handler));
        int observedCommittedHp = 0;
        // The sender below observes the real current holder and file before an HP notification.
        ConquerTcpConnection? actualConnection = null;
        actualConnection = new(null, null, (bytes, _) =>
        {
            var payload = bytes.ToArray(); sent.Add(payload);
            if (LegacyMsgUpdatePacket.TryParse(ReadFrame(payload), out var update) &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints) &&
                sessions.TryGet(actualConnection!, out var current) && current!.Character.MinHitPoints != 61_258)
            {
                observedCommittedHp++;
                var disk = CharacterSnapshotParser.ParseText(File.ReadAllText(characterPath));
                Assert.Equal(current.Character.MinHitPoints, disk.MinHitPoints);
                Assert.Equal(disk.Attributes["MinHitPoints"], current.Character.Attributes["MinHitPoints"]);
            }
            return ValueTask.CompletedTask;
        });
        var connection = actualConnection;
        var ticket = handoff.Issue(1_293_600, "hp-mirror", ConquerRuntimeDefaults.WorldServer);
        await handler.HandleFrameAsync(connection, ReadFrame(new InternalWorldEnterRequestPacket(ticket.Token).ToFrameBytes()));
        Assert.True(sessions.TryGet(connection, out var owner), log.ToString());
        Assert.NotNull(owner); Assert.Equal(10_836, owner.Character.MinHitPoints);
        if (vigorSeed)
        {
            Assert.Equal("0", owner.Character.Attributes["Vigor"]);
            Assert.Contains("Vigor=9999", File.ReadAllText(characterPath));
        }
        Assert.Equal("10836", owner.Character.Attributes["MinHitPoints"]);
        var savedAfterHp = CharacterSnapshotParser.ParseText(File.ReadAllText(characterPath));
        foreach (var key in new[] { "StageEpicTrojanQuest", "ChangeEpicTrojan", "ChangeArrayEpicTrojan",
            "EpicTrojanEvilArrayPoints", "EpicTrojanAbysalStage", "ChangeMr_MirrorEpicTrojan",
            "ChangeGeneralPakEpicTrojan", "EpicTrojanMrMirrorPrograss" })
        {
            // Missing composite data stays missing; compare both presence and
            // value rather than inventing a zero-valued runtime mirror.
            var savedHasKey = savedAfterHp.Attributes.TryGetValue(key, out var savedValue);
            var ownerHasKey = owner.Character.Attributes.TryGetValue(key, out var ownerValue);
            Assert.Equal(savedHasKey, ownerHasKey);
            Assert.Equal(savedValue, ownerValue);
        }
        if (existingComposite)
            Assert.Equal("4/5/6/7/1/1/8/9/10/11/12/13/14/15/1/16/17/18/1/19/", savedAfterHp.Attributes["EpicTrojan"]);
        Assert.True(observedCommittedHp > 0, log.ToString());
        Assert.Contains("OpaqueHpSentinel=keep-raw-170", File.ReadAllText(characterPath));
        var committedSpellBaseline = File.ReadAllBytes(Path.Combine(spells, "1293600.bin"));
        Assert.Equal(savedSpells.AsSpan(4, 12).ToArray(), committedSpellBaseline.AsSpan(4, 12).ToArray());
        var characterBefore = File.ReadAllBytes(characterPath);
        var itemsBefore = File.ReadAllBytes(Path.Combine(items, "1293600.bin"));
        if (externalDrift)
        {
            characterBefore = PatchUniqueAscii(characterBefore,
                vigorSeed ? "Vigor=9999" : "Money=98602439956",
                vigorSeed ? "Vigor=9998" : "Money=98602439957");
            File.WriteAllBytes(characterPath, characterBefore);
        }
        var allFilesBeforeRequest = CaptureHpMirrorFiles(_dbRoot);
        var first = sent.Count;
        await handler.HandleFrameAsync(connection, ReadFrame(new MsgItemUsagePacket(2001, 0, MsgItemUsageAction.Equip, 1, 0, 0, 0).ToFrameBytes()));
        Assert.True(sessions.TryGet(connection, out var final));
        if (externalDrift)
        {
            Assert.Same(owner, final); Assert.Equal(first, sent.Count);
            Assert.Same(owner.PlayerItems, final!.PlayerItems);
            Assert.Same(owner.PlayerSpells, final.PlayerSpells);
            Assert.Equal(itemsBefore, File.ReadAllBytes(Path.Combine(items, "1293600.bin")));
            Assert.Equal(committedSpellBaseline, File.ReadAllBytes(Path.Combine(spells, "1293600.bin")));
            Assert.Contains("itemSpellCharacterBaselineStale", log.ToString());
            AssertHpMirrorFiles(allFilesBeforeRequest, CaptureHpMirrorFiles(_dbRoot));
        }
        else
        {
            if (sent.Count != first + 5)
            {
                var disk = CharacterSnapshotParser.ParseText(File.ReadAllText(characterPath));
                var excluded = new HashSet<string>(StringComparer.Ordinal)
                    { "SourceFile", "Attributes", "PaidNobility", "PlayerQuests" };
                var scalars = typeof(CharacterSnapshot).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.CanRead && p.GetIndexParameters().Length == 0 && !excluded.Contains(p.Name) &&
                        !Equals(p.GetValue(disk), p.GetValue(owner!.Character)))
                    .Select(p => p.Name).Order().ToArray();
                var attributes = disk.Attributes.Where(pair => !owner!.Character.Attributes.TryGetValue(pair.Key, out var value) || value != pair.Value)
                    .Select(pair => pair.Key).Order().ToArray();
                var nobility = disk.PaidNobility.Where(pair => !owner!.Character.PaidNobility.TryGetValue(pair.Key, out var value) || value != pair.Value)
                    .Select(pair => pair.Key).Order().ToArray();
                Assert.Fail("Book emitted no expected success frames. Character mismatch names: scalar=" +
                    string.Join(",", scalars) + "; attributes=" + string.Join(",", attributes) +
                    "; nobility=" + string.Join(",", nobility) + "; log=" + log);
            }
            Assert.Equal(first + 5, sent.Count);
            AssertItemUsage(sent[first], new MsgItemUsagePacket(2001, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0));
            Assert.True(LegacySpellPacket.TryParse(ReadFrame(sent[first + 1]), out var learned));
            Assert.Equal((ushort)1360, learned.Id); Assert.Equal((ushort)0, learned.Level); Assert.Equal(0, learned.Experience);
            AssertNpcReply(sent[first + 2], LegacyNpcReplyInteractType.Dialog, "You~have~learned~1360.");
            AssertNpcReply(sent[first + 3], LegacyNpcReplyInteractType.Option, "I~see.");
            AssertNpcReply(sent[first + 4], LegacyNpcReplyInteractType.Finish, "");
            Assert.True(final!.PlayerSpells.ContainsSpell(1360));
            var retained = File.ReadAllBytes(Path.Combine(spells, "1293600.bin"));
            Assert.Equal(savedSpells.AsSpan(4, 12).ToArray(), retained.AsSpan(4, 12).ToArray());
        }
        Assert.Equal(characterBefore, File.ReadAllBytes(characterPath));
    }

    private static Dictionary<string, byte[]> CaptureHpMirrorFiles(string root) =>
        Directory.GetFiles(root, "*", SearchOption.AllDirectories).ToDictionary(
            path => Path.GetRelativePath(root, path), File.ReadAllBytes, StringComparer.OrdinalIgnoreCase);

    private static void AssertHpMirrorFiles(Dictionary<string, byte[]> expected, Dictionary<string, byte[]> actual)
    {
        Assert.Equal(expected.Keys.Order(StringComparer.OrdinalIgnoreCase), actual.Keys.Order(StringComparer.OrdinalIgnoreCase));
        foreach (var (path, bytes) in expected) Assert.Equal(bytes, actual[path]);
    }

    private static byte[] PatchUniqueAscii(byte[] source, string before, string after)
    {
        var needle = Encoding.ASCII.GetBytes(before); var replacement = Encoding.ASCII.GetBytes(after);
        Assert.Equal(needle.Length, replacement.Length);
        int index = source.AsSpan().IndexOf(needle); Assert.True(index >= 0);
        Assert.Equal(-1, source.AsSpan(index + needle.Length).IndexOf(needle));
        var result = source.ToArray(); replacement.CopyTo(result, index); return result;
    }
}
