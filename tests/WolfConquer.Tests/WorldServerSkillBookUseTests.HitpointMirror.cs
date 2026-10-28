using System.Buffers;
using System.Reflection;
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
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public async Task ActualFrameHpNormalizationRetainsSavedMirrorAndRejectsLaterExternalDrift(bool externalDrift, bool existingComposite)
    {
        var users = Path.Combine(_dbRoot, "Users"); var items = Path.Combine(_dbRoot, "PlayersItems");
        var spells = Path.Combine(_dbRoot, "PlayersSpells");
        foreach (var route in new[] { users, items, spells }) Directory.CreateDirectory(route);
        WriteSnapshot(users, 1_293_600, 1_293_600);
        var characterPath = Path.Combine(users, "1293600.ini");
        var text = File.ReadAllText(characterPath).Replace("[PaidNobility]", "OpaqueHpSentinel=keep-raw-170\n[PaidNobility]", StringComparison.Ordinal);
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
        Assert.Equal("10836", owner.Character.Attributes["MinHitPoints"]);
        var savedAfterHp = CharacterSnapshotParser.ParseText(File.ReadAllText(characterPath));
        foreach (var key in new[] { "StageEpicTrojanQuest", "ChangeEpicTrojan", "ChangeArrayEpicTrojan",
            "EpicTrojanEvilArrayPoints", "EpicTrojanAbysalStage", "ChangeMr_MirrorEpicTrojan",
            "ChangeGeneralPakEpicTrojan", "EpicTrojanMrMirrorPrograss" })
            Assert.Equal(savedAfterHp.Attributes[key], owner.Character.Attributes[key]);
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
            text = File.ReadAllText(characterPath).Replace("Money=98602439956", "Money=98602439957", StringComparison.Ordinal);
            File.WriteAllText(characterPath, text); characterBefore = File.ReadAllBytes(characterPath);
        }
        var first = sent.Count;
        await handler.HandleFrameAsync(connection, ReadFrame(new MsgItemUsagePacket(2001, 0, MsgItemUsageAction.Equip, 1, 0, 0, 0).ToFrameBytes()));
        Assert.True(sessions.TryGet(connection, out var final));
        if (externalDrift)
        {
            Assert.Same(owner, final); Assert.Equal(first, sent.Count);
            Assert.Equal(itemsBefore, File.ReadAllBytes(Path.Combine(items, "1293600.bin")));
            Assert.Equal(committedSpellBaseline, File.ReadAllBytes(Path.Combine(spells, "1293600.bin")));
            Assert.Contains("itemSpellCharacterBaselineStale", log.ToString());
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
            Assert.True(final!.PlayerSpells.ContainsSpell(1360));
            var retained = File.ReadAllBytes(Path.Combine(spells, "1293600.bin"));
            Assert.Equal(savedSpells.AsSpan(4, 12).ToArray(), retained.AsSpan(4, 12).ToArray());
        }
        Assert.Equal(characterBefore, File.ReadAllBytes(characterPath));
    }
}
