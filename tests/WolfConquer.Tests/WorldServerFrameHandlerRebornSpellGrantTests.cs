using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    [Fact]
    public async Task HandlerLegacyWorldLoginDoesNotSynthesizeFrostGazeFromThunderStrikerFirstClass()
    {
        var usersRoot = Path.Combine(_usersRoot, "Users");
        Directory.CreateDirectory(usersRoot);
        WriteSnapshot(
            usersRoot,
            1293600,
            uid: 1293600,
            classCode: 9_005,
            level: 70,
            reborn: 1,
            secondClass: 0);
        var snapshotPath = Path.Combine(usersRoot, "1293600.ini");
        File.WriteAllText(
            snapshotPath,
            File.ReadAllText(snapshotPath).Replace("FirstClass=0", "FirstClass=9005", StringComparison.Ordinal));
        var loader = new CharacterSnapshotLoader(usersRoot);
        var playerSpellsRoot = Path.Combine(_usersRoot, "PlayersSpells");
        Directory.CreateDirectory(playerSpellsRoot);
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(loader, log);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyMsgLoginGamePacket(1293600).ToFrameBytes()));

        var persistedSpells = loader.LoadPlayerSpellsByCharacterUid(1293600);
        Assert.DoesNotContain(12_830U, persistedSpells.SpellIds);
        Assert.DoesNotContain(13_070U, persistedSpells.SpellIds);
        Assert.Equal(persistedSpells.SpellIds.ToArray(), GetSession(handler, connection).PlayerSpells.SpellIds.ToArray());
        Assert.DoesNotContain("legacy reborn spells", log.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandlerLegacyWorldLoginPreservesAlreadyLearnedFrostGazeSpells()
    {
        var usersRoot = Path.Combine(_usersRoot, "Users");
        Directory.CreateDirectory(usersRoot);
        WriteSnapshot(
            usersRoot,
            1293600,
            uid: 1293600,
            classCode: 16_005,
            level: 70,
            reborn: 1,
            secondClass: 0);
        var snapshotPath = Path.Combine(usersRoot, "1293600.ini");
        File.WriteAllText(
            snapshotPath,
            File.ReadAllText(snapshotPath).Replace("FirstClass=0", "FirstClass=9005", StringComparison.Ordinal));
        var loader = new CharacterSnapshotLoader(usersRoot);
        var playerSpellsRoot = Path.Combine(_usersRoot, "PlayersSpells");
        var learnedSpells = new PlayerSpellSnapshot(
        [
            new PlayerSpellSnapshotEntry(12_830, Level: 2, Experience: 123, PreviousLevel: 1, SoulLevel: 3, UseSpellSoul: 1),
            new PlayerSpellSnapshotEntry(13_070, Level: 1, Experience: 456, PreviousLevel: 0, SoulLevel: 2, UseSpellSoul: 1),
        ]);
        new PlayerSpellSnapshotLoader(playerSpellsRoot).SaveByCharacterUid(1293600, learnedSpells);
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(loader, log);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyMsgLoginGamePacket(1293600).ToFrameBytes()));

        var persistedSpells = loader.LoadPlayerSpellsByCharacterUid(1293600);
        Assert.Equal(
            learnedSpells.Spells.Single(spell => spell.Id == 12_830),
            persistedSpells.Spells.Single(spell => spell.Id == 12_830));
        Assert.Equal(
            learnedSpells.Spells.Single(spell => spell.Id == 13_070),
            persistedSpells.Spells.Single(spell => spell.Id == 13_070));
        Assert.Equal(persistedSpells.SpellIds.ToArray(), GetSession(handler, connection).PlayerSpells.SpellIds.ToArray());
        Assert.DoesNotContain("legacy reborn spells", log.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
