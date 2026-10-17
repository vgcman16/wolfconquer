using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class WorldServerSkillBookUseTests : IDisposable
{
    private readonly string _dbRoot = Path.Combine(Path.GetTempPath(), "wolf-world-skill-book-" + Guid.NewGuid());

    [Fact]
    public async Task HandlerItemUsageSkillBookTeachesSpellConsumesItemAndPersists()
    {
        var usersRoot = Path.Combine(_dbRoot, "Users");
        var itemsRoot = Path.Combine(_dbRoot, "PlayersItems");
        var spellsRoot = Path.Combine(_dbRoot, "PlayersSpells");
        Directory.CreateDirectory(usersRoot);
        Directory.CreateDirectory(itemsRoot);
        Directory.CreateDirectory(spellsRoot);
        WriteSnapshot(usersRoot, accountId: 1_293_600, uid: 1_293_600);
        File.WriteAllBytes(
            Path.Combine(itemsRoot, "1293600.bin"),
            PlayerItemSnapshotLoaderTests.BuildPlayerItems(
            [
                PlayerItemSnapshotLoaderTests.DbItem(uid: 2001, itemId: 725016, position: 0, stackSize: 1),
            ],
            []));
        File.WriteAllBytes(
            Path.Combine(spellsRoot, "1293600.bin"),
            PlayerSpellSnapshotLoaderTests.BuildPlayerSpells([]));
        using var log = new StringWriter();
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var ticket = handoff.Issue(1_293_600, "user02", ConquerRuntimeDefaults.WorldServer);
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(usersRoot), handoff, log);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new InternalWorldEnterRequestPacket(ticket.Token).ToFrameBytes()));

        var countBeforeRequest = sent.Count;
        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new MsgItemUsagePacket(
                Id: 2001,
                DwParam: 0,
                Action: MsgItemUsageAction.Equip,
                Timestamp: 1234,
                DwParam2: 0,
                DwParam3: 0,
                DwParam4: 0).ToFrameBytes()));

        Assert.Equal(countBeforeRequest + 5, sent.Count);
        AssertItemUsage(
            sent[countBeforeRequest],
            new MsgItemUsagePacket(
                Id: 2001,
                DwParam: 0,
                Action: MsgItemUsageAction.RemoveInventory,
                Timestamp: 0,
                DwParam2: 0,
                DwParam3: 0,
                DwParam4: 0));
        Assert.True(LegacySpellPacket.TryParse(ReadFrame(sent[countBeforeRequest + 1]), out var learnedSpell));
        Assert.Equal((ushort)1360, learnedSpell.Id);
        Assert.Equal((ushort)0, learnedSpell.Level);
        Assert.Equal(0, learnedSpell.Experience);
        AssertNpcReply(sent[countBeforeRequest + 2], LegacyNpcReplyInteractType.Dialog, "You~have~learned~1360.");
        AssertNpcReply(sent[countBeforeRequest + 3], LegacyNpcReplyInteractType.Option, "I~see.");
        AssertNpcReply(sent[countBeforeRequest + 4], LegacyNpcReplyInteractType.Finish, "");

        var persistedSpells = new PlayerSpellSnapshotLoader(spellsRoot).LoadByCharacterUid(1_293_600);
        Assert.Contains(persistedSpells.Spells, spell => spell.Id == 1360);
        var persistedItems = new PlayerItemSnapshotLoader(itemsRoot).LoadByCharacterUid(1_293_600);
        Assert.DoesNotContain(persistedItems.InventoryItems, item => item.Uid == 2001);
        Assert.Contains("handled MsgItemUsage skill book", log.ToString());
    }

    [Fact]
    public async Task HandlerItemUsageSkillBookAlreadyLearnedDoesNotConsumeItem()
    {
        var usersRoot = Path.Combine(_dbRoot, "Users");
        var itemsRoot = Path.Combine(_dbRoot, "PlayersItems");
        var spellsRoot = Path.Combine(_dbRoot, "PlayersSpells");
        Directory.CreateDirectory(usersRoot);
        Directory.CreateDirectory(itemsRoot);
        Directory.CreateDirectory(spellsRoot);
        WriteSnapshot(usersRoot, accountId: 1_293_600, uid: 1_293_600);
        File.WriteAllBytes(
            Path.Combine(itemsRoot, "1293600.bin"),
            PlayerItemSnapshotLoaderTests.BuildPlayerItems(
            [
                PlayerItemSnapshotLoaderTests.DbItem(uid: 2001, itemId: 725016, position: 0, stackSize: 1),
            ],
            []));
        File.WriteAllBytes(
            Path.Combine(spellsRoot, "1293600.bin"),
            PlayerSpellSnapshotLoaderTests.BuildPlayerSpells([1360]));
        using var log = new StringWriter();
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var ticket = handoff.Issue(1_293_600, "user02", ConquerRuntimeDefaults.WorldServer);
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(usersRoot), handoff, log);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new InternalWorldEnterRequestPacket(ticket.Token).ToFrameBytes()));

        var countBeforeRequest = sent.Count;
        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new MsgItemUsagePacket(
                Id: 2001,
                DwParam: 0,
                Action: MsgItemUsageAction.Equip,
                Timestamp: 1234,
                DwParam2: 0,
                DwParam3: 0,
                DwParam4: 0).ToFrameBytes()));

        Assert.Equal(countBeforeRequest + 3, sent.Count);
        AssertNpcReply(sent[countBeforeRequest], LegacyNpcReplyInteractType.Dialog, "You already have this spell.");
        AssertNpcReply(sent[countBeforeRequest + 1], LegacyNpcReplyInteractType.Option, "I~see.");
        AssertNpcReply(sent[countBeforeRequest + 2], LegacyNpcReplyInteractType.Finish, "");
        var persistedItems = new PlayerItemSnapshotLoader(itemsRoot).LoadByCharacterUid(1_293_600);
        Assert.Contains(persistedItems.InventoryItems, item => item.Uid == 2001);
        Assert.Contains("already learned", log.ToString());
    }

    [Fact]
    public async Task HandlerItemUsageSkillBookClassGateRejectsWrongClassBeforeLearningOrConsuming()
    {
        var usersRoot = Path.Combine(_dbRoot, "Users");
        var itemsRoot = Path.Combine(_dbRoot, "PlayersItems");
        var spellsRoot = Path.Combine(_dbRoot, "PlayersSpells");
        Directory.CreateDirectory(usersRoot);
        Directory.CreateDirectory(itemsRoot);
        Directory.CreateDirectory(spellsRoot);
        WriteSnapshot(usersRoot, accountId: 1_293_600, uid: 1_293_600);
        File.WriteAllBytes(
            Path.Combine(itemsRoot, "1293600.bin"),
            PlayerItemSnapshotLoaderTests.BuildPlayerItems(
            [
                PlayerItemSnapshotLoaderTests.DbItem(uid: 2001, itemId: 725000, position: 0, stackSize: 1),
            ],
            []));
        File.WriteAllBytes(
            Path.Combine(spellsRoot, "1293600.bin"),
            PlayerSpellSnapshotLoaderTests.BuildPlayerSpells([]));
        var worldData = CreateCatalog(
            magic: [CreateMagic(1_000)],
            CreateOperation(id: 29, mode: 1, group: 13, 1_000),
            CreateOperation(id: 37, mode: 1, group: 14, 1_000));
        using var log = new StringWriter();
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var ticket = handoff.Issue(1_293_600, "user02", ConquerRuntimeDefaults.WorldServer);
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(usersRoot), handoff, worldData, log);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new InternalWorldEnterRequestPacket(ticket.Token).ToFrameBytes()));

        var countBeforeRequest = sent.Count;
        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new MsgItemUsagePacket(
                Id: 2001,
                DwParam: 0,
                Action: MsgItemUsageAction.Equip,
                Timestamp: 1234,
                DwParam2: 0,
                DwParam3: 0,
                DwParam4: 0).ToFrameBytes()));

        Assert.Equal(countBeforeRequest, sent.Count);
        var persistedSpells = new PlayerSpellSnapshotLoader(spellsRoot).LoadByCharacterUid(1_293_600);
        Assert.DoesNotContain(persistedSpells.Spells, spell => spell.Id == 1_000);
        var persistedItems = new PlayerItemSnapshotLoader(itemsRoot).LoadByCharacterUid(1_293_600);
        Assert.Contains(persistedItems.InventoryItems, item => item.Uid == 2001);
        var text = log.ToString();
        Assert.Contains("source magictypeop profession gate rejected", text);
        Assert.Contains("classFamilies=7", text);
        Assert.Contains("requiredClassFamilies=13,14", text);
    }

    [Fact]
    public async Task HandlerItemUsageSkillBookKeepsExistingBehaviorWhenClassGateDataIsMissing()
    {
        var usersRoot = Path.Combine(_dbRoot, "Users");
        var itemsRoot = Path.Combine(_dbRoot, "PlayersItems");
        var spellsRoot = Path.Combine(_dbRoot, "PlayersSpells");
        Directory.CreateDirectory(usersRoot);
        Directory.CreateDirectory(itemsRoot);
        Directory.CreateDirectory(spellsRoot);
        WriteSnapshot(usersRoot, accountId: 1_293_600, uid: 1_293_600);
        File.WriteAllBytes(
            Path.Combine(itemsRoot, "1293600.bin"),
            PlayerItemSnapshotLoaderTests.BuildPlayerItems(
            [
                PlayerItemSnapshotLoaderTests.DbItem(uid: 2001, itemId: 725000, position: 0, stackSize: 1),
            ],
            []));
        File.WriteAllBytes(
            Path.Combine(spellsRoot, "1293600.bin"),
            PlayerSpellSnapshotLoaderTests.BuildPlayerSpells([]));
        var worldData = CreateCatalog(magic: [CreateMagic(1_000)]);
        using var log = new StringWriter();
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var ticket = handoff.Issue(1_293_600, "user02", ConquerRuntimeDefaults.WorldServer);
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(usersRoot), handoff, worldData, log);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new InternalWorldEnterRequestPacket(ticket.Token).ToFrameBytes()));

        var countBeforeRequest = sent.Count;
        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new MsgItemUsagePacket(
                Id: 2001,
                DwParam: 0,
                Action: MsgItemUsageAction.Equip,
                Timestamp: 1234,
                DwParam2: 0,
                DwParam3: 0,
                DwParam4: 0).ToFrameBytes()));

        Assert.Equal(countBeforeRequest + 5, sent.Count);
        AssertItemUsage(
            sent[countBeforeRequest],
            new MsgItemUsagePacket(
                Id: 2001,
                DwParam: 0,
                Action: MsgItemUsageAction.RemoveInventory,
                Timestamp: 0,
                DwParam2: 0,
                DwParam3: 0,
                DwParam4: 0));
        Assert.True(LegacySpellPacket.TryParse(ReadFrame(sent[countBeforeRequest + 1]), out var learnedSpell));
        Assert.Equal((ushort)1_000, learnedSpell.Id);
        var persistedSpells = new PlayerSpellSnapshotLoader(spellsRoot).LoadByCharacterUid(1_293_600);
        Assert.Contains(persistedSpells.Spells, spell => spell.Id == 1_000);
        var persistedItems = new PlayerItemSnapshotLoader(itemsRoot).LoadByCharacterUid(1_293_600);
        Assert.DoesNotContain(persistedItems.InventoryItems, item => item.Uid == 2001);
        Assert.DoesNotContain("profession gate rejected", log.ToString());
    }

    [Theory]
    [InlineData(83, 14_002, "Sorry,~you~cannot~learn~this~spell~before~your~Level~is~84.~Please~train~harder.", "level=83 requiredLevel=84")]
    [InlineData(84, 13_002, "Sorry,this spell is just for Fire Taoist`s.", "class=13002 requiredClassFamily=14")]
    public async Task HandlerItemUsageFireOfHellBookRejectsSourceRequirementsBeforeLearningOrConsuming(
        long level,
        long characterClass,
        string expectedDialog,
        string expectedLogDetail)
    {
        var usersRoot = Path.Combine(_dbRoot, "Users");
        var itemsRoot = Path.Combine(_dbRoot, "PlayersItems");
        var spellsRoot = Path.Combine(_dbRoot, "PlayersSpells");
        Directory.CreateDirectory(usersRoot);
        Directory.CreateDirectory(itemsRoot);
        Directory.CreateDirectory(spellsRoot);
        WriteSnapshot(
            usersRoot,
            accountId: 1_293_600,
            uid: 1_293_600,
            characterClass: characterClass,
            level: level);
        File.WriteAllBytes(
            Path.Combine(itemsRoot, "1293600.bin"),
            PlayerItemSnapshotLoaderTests.BuildPlayerItems(
            [
                PlayerItemSnapshotLoaderTests.DbItem(uid: 2001, itemId: 1_060_101, position: 0, stackSize: 1),
            ],
            []));
        File.WriteAllBytes(
            Path.Combine(spellsRoot, "1293600.bin"),
            PlayerSpellSnapshotLoaderTests.BuildPlayerSpells([]));
        var worldData = CreateCatalog(magic: [CreateMagic(1_165)]);
        using var log = new StringWriter();
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var ticket = handoff.Issue(1_293_600, "user02", ConquerRuntimeDefaults.WorldServer);
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(usersRoot), handoff, worldData, log);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new InternalWorldEnterRequestPacket(ticket.Token).ToFrameBytes()));

        var countBeforeRequest = sent.Count;
        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new MsgItemUsagePacket(
                Id: 2001,
                DwParam: 0,
                Action: MsgItemUsageAction.Equip,
                Timestamp: 1234,
                DwParam2: 0,
                DwParam3: 0,
                DwParam4: 0).ToFrameBytes()));

        Assert.Equal(countBeforeRequest + 3, sent.Count);
        AssertNpcReply(sent[countBeforeRequest], LegacyNpcReplyInteractType.Dialog, expectedDialog);
        var persistedSpells = new PlayerSpellSnapshotLoader(spellsRoot).LoadByCharacterUid(1_293_600);
        Assert.DoesNotContain(persistedSpells.Spells, spell => spell.Id == 1_165);
        var persistedItems = new PlayerItemSnapshotLoader(itemsRoot).LoadByCharacterUid(1_293_600);
        Assert.Contains(persistedItems.InventoryItems, item => item.Uid == 2001);
        var text = log.ToString();
        Assert.Contains("source skill-book requirement gate rejected", text);
        Assert.Contains(expectedLogDetail, text);
    }

    [Fact]
    public async Task HandlerItemUsageFireOfHellBookLearnsWhenLevelAndClassMeetSourceRequirements()
    {
        var usersRoot = Path.Combine(_dbRoot, "Users");
        var itemsRoot = Path.Combine(_dbRoot, "PlayersItems");
        var spellsRoot = Path.Combine(_dbRoot, "PlayersSpells");
        Directory.CreateDirectory(usersRoot);
        Directory.CreateDirectory(itemsRoot);
        Directory.CreateDirectory(spellsRoot);
        WriteSnapshot(
            usersRoot,
            accountId: 1_293_600,
            uid: 1_293_600,
            characterClass: 14_002,
            level: 84);
        File.WriteAllBytes(
            Path.Combine(itemsRoot, "1293600.bin"),
            PlayerItemSnapshotLoaderTests.BuildPlayerItems(
            [
                PlayerItemSnapshotLoaderTests.DbItem(uid: 2001, itemId: 1_060_101, position: 0, stackSize: 1),
            ],
            []));
        File.WriteAllBytes(
            Path.Combine(spellsRoot, "1293600.bin"),
            PlayerSpellSnapshotLoaderTests.BuildPlayerSpells([]));
        var worldData = CreateCatalog(magic: [CreateMagic(1_165)]);
        using var log = new StringWriter();
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var ticket = handoff.Issue(1_293_600, "user02", ConquerRuntimeDefaults.WorldServer);
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(usersRoot), handoff, worldData, log);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new InternalWorldEnterRequestPacket(ticket.Token).ToFrameBytes()));

        var countBeforeRequest = sent.Count;
        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new MsgItemUsagePacket(
                Id: 2001,
                DwParam: 0,
                Action: MsgItemUsageAction.Equip,
                Timestamp: 1234,
                DwParam2: 0,
                DwParam3: 0,
                DwParam4: 0).ToFrameBytes()));

        Assert.Equal(countBeforeRequest + 5, sent.Count);
        AssertItemUsage(
            sent[countBeforeRequest],
            new MsgItemUsagePacket(
                Id: 2001,
                DwParam: 0,
                Action: MsgItemUsageAction.RemoveInventory,
                Timestamp: 0,
                DwParam2: 0,
                DwParam3: 0,
                DwParam4: 0));
        Assert.True(LegacySpellPacket.TryParse(ReadFrame(sent[countBeforeRequest + 1]), out var learnedSpell));
        Assert.Equal((ushort)1_165, learnedSpell.Id);
        var persistedSpells = new PlayerSpellSnapshotLoader(spellsRoot).LoadByCharacterUid(1_293_600);
        Assert.Contains(persistedSpells.Spells, spell => spell.Id == 1_165);
        var persistedItems = new PlayerItemSnapshotLoader(itemsRoot).LoadByCharacterUid(1_293_600);
        Assert.DoesNotContain(persistedItems.InventoryItems, item => item.Uid == 2001);
        Assert.Contains("handled MsgItemUsage skill book", log.ToString());
    }

    [Theory]
    [InlineData(721157, 11_000)]
    [InlineData(721158, 11_005)]
    public async Task HandlerItemUsageTrojanLineSkillBookRejectsUnderLevelBeforeLearningOrConsuming(
        uint itemId,
        ushort spellId)
    {
        var usersRoot = Path.Combine(_dbRoot, "Users");
        var itemsRoot = Path.Combine(_dbRoot, "PlayersItems");
        var spellsRoot = Path.Combine(_dbRoot, "PlayersSpells");
        Directory.CreateDirectory(usersRoot);
        Directory.CreateDirectory(itemsRoot);
        Directory.CreateDirectory(spellsRoot);
        WriteSnapshot(usersRoot, accountId: 1_293_600, uid: 1_293_600, characterClass: 1_000, level: 39);
        File.WriteAllBytes(
            Path.Combine(itemsRoot, "1293600.bin"),
            PlayerItemSnapshotLoaderTests.BuildPlayerItems(
            [
                PlayerItemSnapshotLoaderTests.DbItem(uid: 2001, itemId: itemId, position: 0, stackSize: 1),
            ],
            []));
        File.WriteAllBytes(
            Path.Combine(spellsRoot, "1293600.bin"),
            PlayerSpellSnapshotLoaderTests.BuildPlayerSpells([]));
        var worldData = CreateCatalog(
            magic: [CreateMagic(spellId)],
            CreateOperation(id: 1, mode: 1, group: 1, spellId));
        using var log = new StringWriter();
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var ticket = handoff.Issue(1_293_600, "trojan", ConquerRuntimeDefaults.WorldServer);
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(usersRoot), handoff, worldData, log);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new InternalWorldEnterRequestPacket(ticket.Token).ToFrameBytes()));

        var countBeforeRequest = sent.Count;
        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new MsgItemUsagePacket(
                Id: 2001,
                DwParam: 0,
                Action: MsgItemUsageAction.Equip,
                Timestamp: 1234,
                DwParam2: 0,
                DwParam3: 0,
                DwParam4: 0).ToFrameBytes()));

        Assert.Equal(countBeforeRequest + 3, sent.Count);
        AssertNpcReply(
            sent[countBeforeRequest],
            LegacyNpcReplyInteractType.Dialog,
            "Sorry,~you~cannot~learn~this~spell~before~your~Level~is~40.~Please~train~harder.");
        var persistedSpells = new PlayerSpellSnapshotLoader(spellsRoot).LoadByCharacterUid(1_293_600);
        Assert.DoesNotContain(persistedSpells.Spells, spell => spell.Id == spellId);
        var persistedItems = new PlayerItemSnapshotLoader(itemsRoot).LoadByCharacterUid(1_293_600);
        Assert.Contains(persistedItems.InventoryItems, item => item.Uid == 2001);
        var text = log.ToString();
        Assert.Contains("source skill-book requirement gate rejected", text);
        Assert.Contains("level=39 requiredLevel=40", text);
    }

    [Fact]
    public async Task HandlerItemUsageFastBladeBookRejectsUnderLevelBeforeLearningOrConsuming()
    {
        var usersRoot = Path.Combine(_dbRoot, "Users");
        var itemsRoot = Path.Combine(_dbRoot, "PlayersItems");
        var spellsRoot = Path.Combine(_dbRoot, "PlayersSpells");
        var profsRoot = Path.Combine(_dbRoot, "PlayersProfs");
        Directory.CreateDirectory(usersRoot);
        Directory.CreateDirectory(itemsRoot);
        Directory.CreateDirectory(spellsRoot);
        Directory.CreateDirectory(profsRoot);
        WriteSnapshot(usersRoot, accountId: 1_293_600, uid: 1_293_600, characterClass: 1_000, level: 39);
        File.WriteAllBytes(
            Path.Combine(itemsRoot, "1293600.bin"),
            PlayerItemSnapshotLoaderTests.BuildPlayerItems(
            [
                PlayerItemSnapshotLoaderTests.DbItem(uid: 2001, itemId: 725005, position: 0, stackSize: 1),
            ],
            []));
        File.WriteAllBytes(
            Path.Combine(spellsRoot, "1293600.bin"),
            PlayerSpellSnapshotLoaderTests.BuildPlayerSpells([]));
        File.WriteAllBytes(
            Path.Combine(profsRoot, "1293600.bin"),
            PlayerProficiencySnapshotLoaderTests.BuildPlayerProficiencies(
            [
                PlayerProficiencySnapshotLoaderTests.Entry(410, level: 5),
            ]));
        var worldData = CreateCatalog(
            magic: [CreateMagic(1_045)],
            CreateOperation(id: 1, mode: 1, group: 1, 1_045));
        using var log = new StringWriter();
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var ticket = handoff.Issue(1_293_600, "trojan", ConquerRuntimeDefaults.WorldServer);
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(usersRoot), handoff, worldData, log);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new InternalWorldEnterRequestPacket(ticket.Token).ToFrameBytes()));

        var countBeforeRequest = sent.Count;
        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new MsgItemUsagePacket(
                Id: 2001,
                DwParam: 0,
                Action: MsgItemUsageAction.Equip,
                Timestamp: 1234,
                DwParam2: 0,
                DwParam3: 0,
                DwParam4: 0).ToFrameBytes()));

        Assert.Equal(countBeforeRequest + 3, sent.Count);
        AssertNpcReply(
            sent[countBeforeRequest],
            LegacyNpcReplyInteractType.Dialog,
            "Sorry,~you~cannot~learn~this~spell~before~your~Level~is~40.~Please~train~harder.");
        var persistedSpells = new PlayerSpellSnapshotLoader(spellsRoot).LoadByCharacterUid(1_293_600);
        Assert.DoesNotContain(persistedSpells.Spells, spell => spell.Id == 1_045);
        var persistedItems = new PlayerItemSnapshotLoader(itemsRoot).LoadByCharacterUid(1_293_600);
        Assert.Contains(persistedItems.InventoryItems, item => item.Uid == 2001);
        var text = log.ToString();
        Assert.Contains("source skill-book requirement gate rejected", text);
        Assert.Contains("level=39 requiredLevel=40", text);
    }

    [Theory]
    [InlineData(725005, 1_045, 410, "blade")]
    [InlineData(725010, 1_046, 420, "sword")]
    public async Task HandlerItemUsageTrojanWeaponSkillBookRejectsMissingWeaponProficiencyBeforeLearningOrConsuming(
        uint itemId,
        ushort spellId,
        uint proficiencyId,
        string weaponName)
    {
        var usersRoot = Path.Combine(_dbRoot, "Users");
        var itemsRoot = Path.Combine(_dbRoot, "PlayersItems");
        var spellsRoot = Path.Combine(_dbRoot, "PlayersSpells");
        var profsRoot = Path.Combine(_dbRoot, "PlayersProfs");
        Directory.CreateDirectory(usersRoot);
        Directory.CreateDirectory(itemsRoot);
        Directory.CreateDirectory(spellsRoot);
        Directory.CreateDirectory(profsRoot);
        WriteSnapshot(usersRoot, accountId: 1_293_600, uid: 1_293_600, characterClass: 1_000, level: 40);
        File.WriteAllBytes(
            Path.Combine(itemsRoot, "1293600.bin"),
            PlayerItemSnapshotLoaderTests.BuildPlayerItems(
            [
                PlayerItemSnapshotLoaderTests.DbItem(uid: 2001, itemId: itemId, position: 0, stackSize: 1),
            ],
            []));
        File.WriteAllBytes(
            Path.Combine(spellsRoot, "1293600.bin"),
            PlayerSpellSnapshotLoaderTests.BuildPlayerSpells([]));
        File.WriteAllBytes(
            Path.Combine(profsRoot, "1293600.bin"),
            PlayerProficiencySnapshotLoaderTests.BuildPlayerProficiencies(
            [
                PlayerProficiencySnapshotLoaderTests.Entry(proficiencyId, level: 4),
            ]));
        var worldData = CreateCatalog(
            magic: [CreateMagic(spellId)],
            CreateOperation(id: 1, mode: 1, group: 1, spellId));
        using var log = new StringWriter();
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var ticket = handoff.Issue(1_293_600, "trojan", ConquerRuntimeDefaults.WorldServer);
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(usersRoot), handoff, worldData, log);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new InternalWorldEnterRequestPacket(ticket.Token).ToFrameBytes()));

        var countBeforeRequest = sent.Count;
        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new MsgItemUsagePacket(
                Id: 2001,
                DwParam: 0,
                Action: MsgItemUsageAction.Equip,
                Timestamp: 1234,
                DwParam2: 0,
                DwParam3: 0,
                DwParam4: 0).ToFrameBytes()));

        Assert.Equal(countBeforeRequest + 3, sent.Count);
        AssertNpcReply(
            sent[countBeforeRequest],
            LegacyNpcReplyInteractType.Dialog,
            $"Sorry,~you~cannot~learn~this~skill~before~you~practice~your~{weaponName}~to~level~5.~Please~train~harder.");
        var persistedSpells = new PlayerSpellSnapshotLoader(spellsRoot).LoadByCharacterUid(1_293_600);
        Assert.DoesNotContain(persistedSpells.Spells, spell => spell.Id == spellId);
        var persistedItems = new PlayerItemSnapshotLoader(itemsRoot).LoadByCharacterUid(1_293_600);
        Assert.Contains(persistedItems.InventoryItems, item => item.Uid == 2001);
        var text = log.ToString();
        Assert.Contains("source skill-book requirement gate rejected", text);
        Assert.Contains($"proficiencyId={proficiencyId} proficiencyLevel=4 requiredProficiencyLevel=5", text);
    }

    [Theory]
    [InlineData(725005, 1_045, 410)]
    [InlineData(725010, 1_046, 420)]
    public async Task HandlerItemUsageTrojanWeaponSkillBookLearnsWhenLevelAndWeaponProficiencyMeetSourceRequirements(
        uint itemId,
        ushort spellId,
        uint proficiencyId)
    {
        var usersRoot = Path.Combine(_dbRoot, "Users");
        var itemsRoot = Path.Combine(_dbRoot, "PlayersItems");
        var spellsRoot = Path.Combine(_dbRoot, "PlayersSpells");
        var profsRoot = Path.Combine(_dbRoot, "PlayersProfs");
        Directory.CreateDirectory(usersRoot);
        Directory.CreateDirectory(itemsRoot);
        Directory.CreateDirectory(spellsRoot);
        Directory.CreateDirectory(profsRoot);
        WriteSnapshot(usersRoot, accountId: 1_293_600, uid: 1_293_600, characterClass: 1_000, level: 40);
        File.WriteAllBytes(
            Path.Combine(itemsRoot, "1293600.bin"),
            PlayerItemSnapshotLoaderTests.BuildPlayerItems(
            [
                PlayerItemSnapshotLoaderTests.DbItem(uid: 2001, itemId: itemId, position: 0, stackSize: 1),
            ],
            []));
        File.WriteAllBytes(
            Path.Combine(spellsRoot, "1293600.bin"),
            PlayerSpellSnapshotLoaderTests.BuildPlayerSpells([]));
        File.WriteAllBytes(
            Path.Combine(profsRoot, "1293600.bin"),
            PlayerProficiencySnapshotLoaderTests.BuildPlayerProficiencies(
            [
                PlayerProficiencySnapshotLoaderTests.Entry(proficiencyId, level: 5),
            ]));
        var worldData = CreateCatalog(
            magic: [CreateMagic(spellId)],
            CreateOperation(id: 1, mode: 1, group: 1, spellId));
        using var log = new StringWriter();
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var ticket = handoff.Issue(1_293_600, "trojan", ConquerRuntimeDefaults.WorldServer);
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(usersRoot), handoff, worldData, log);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new InternalWorldEnterRequestPacket(ticket.Token).ToFrameBytes()));

        var countBeforeRequest = sent.Count;
        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new MsgItemUsagePacket(
                Id: 2001,
                DwParam: 0,
                Action: MsgItemUsageAction.Equip,
                Timestamp: 1234,
                DwParam2: 0,
                DwParam3: 0,
                DwParam4: 0).ToFrameBytes()));

        Assert.Equal(countBeforeRequest + 5, sent.Count);
        AssertItemUsage(
            sent[countBeforeRequest],
            new MsgItemUsagePacket(
                Id: 2001,
                DwParam: 0,
                Action: MsgItemUsageAction.RemoveInventory,
                Timestamp: 0,
                DwParam2: 0,
                DwParam3: 0,
                DwParam4: 0));
        Assert.True(LegacySpellPacket.TryParse(ReadFrame(sent[countBeforeRequest + 1]), out var learnedSpell));
        Assert.Equal(spellId, learnedSpell.Id);
        var persistedSpells = new PlayerSpellSnapshotLoader(spellsRoot).LoadByCharacterUid(1_293_600);
        Assert.Contains(persistedSpells.Spells, spell => spell.Id == spellId);
        var persistedItems = new PlayerItemSnapshotLoader(itemsRoot).LoadByCharacterUid(1_293_600);
        Assert.DoesNotContain(persistedItems.InventoryItems, item => item.Uid == 2001);
        Assert.Contains("handled MsgItemUsage skill book", log.ToString());
    }

    public void Dispose()
    {
        if (Directory.Exists(_dbRoot))
        {
            Directory.Delete(_dbRoot, recursive: true);
        }
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }

    private static void AssertItemUsage(byte[] bytes, MsgItemUsagePacket expected)
    {
        Assert.True(MsgItemUsagePacket.TryParse(ReadFrame(bytes), out var actual));
        Assert.Equal(expected, actual);
    }

    private static void AssertNpcReply(
        byte[] bytes,
        LegacyNpcReplyInteractType interactType,
        string text)
    {
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(interactType, packet.InteractType);
        Assert.Equal(text, packet.Text);
    }

    private static void WriteSnapshot(
        string usersRoot,
        long accountId,
        long uid,
        long characterClass = 7_051,
        long level = 140)
    {
        File.WriteAllText(
            Path.Combine(usersRoot, accountId + ".ini"),
            $$"""
            [Character]
            UID={{uid}}
            Body=1008
            Face=154
            Name=HeroOne
            Class={{characterClass}}
            Map=1015
            X=830
            Y=584
            FirstClass=0
            SecoundeClass=7050
            Reborn=2
            Level={{level}}
            Haire=27
            Experience=0
            PMap=1000
            PMapX=500
            PMapY=650
            Agility=36
            Strength=0
            Vitaliti=447
            Spirit=0
            Atributes=12
            MinHitPoints=61258
            MinMana=5000
            ConquerPoints=639778904
            BankCps=123
            BoundConquerPoints=456
            Money=98602439956
            PkPoints=0
            Online=7

            [PaidNobility]
            Rank=King
            Identifier=1293600
            """);
    }

    private static MagicType CreateMagic(int spellId)
    {
        return new MagicType(spellId, spellId, $"Spell{spellId}", []);
    }

    private static MagicTypeOperationEntry CreateOperation(
        int id,
        int mode,
        int group,
        params int[] referencedMagicTypeIds)
    {
        var values = new int[65];
        values[0] = id;
        values[1] = mode;
        values[2] = group;
        values[3] = group;
        for (var index = 0; index < referencedMagicTypeIds.Length; index++)
        {
            values[5 + index] = referencedMagicTypeIds[index];
        }

        var fields = values.Select(value => value.ToString(CultureInfo.InvariantCulture)).ToArray();
        return new MagicTypeOperationEntry(
            id,
            mode,
            group,
            Level: group,
            Flag: 0,
            referencedMagicTypeIds,
            values,
            SourceFile: "magictypeop.txt",
            LineNumber: id,
            RawLine: string.Join(",", fields),
            fields);
    }

    private static WorldDataCatalog CreateCatalog(
        IReadOnlyList<MagicType> magic,
        params MagicTypeOperationEntry[] magicTypeOperations)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-06-20T00:00:00Z", CultureInfo.InvariantCulture),
                "fixture://legacy-skill-book-class-gate",
                new Dictionary<string, int>()),
            clientConfigs: [],
            items: [],
            itemAddBonuses: [],
            levelExperience: [],
            runeLevelExperience: [],
            runeStorageAttributes: [],
            itemRefineEffects: [],
            itemRefineEffectEx: [],
            itemRefineUpgrades: [],
            transformInfo: [],
            furniture: [],
            hairColorTypes: [],
            hairFaceStorageTypes: [],
            coatStorageTypes: [],
            coatStorageAttributes: [],
            coatColorRules: [],
            xuanbaoAdditionAttributes: [],
            xuanbaoComposeAttributeLimits: [],
            xuanbaoRandomAttributes: [],
            christmasNpcs: [],
            tutorTypes: [],
            tutorBattleLimitTypes: [],
            beastAttributes: [],
            fateExperience: [],
            spiritRates: [],
            hundredWeapons: [],
            gouyuTypes: [],
            gouyuImmortals: [],
            specialEffectAttributes: [],
            daoqiDictTypes: [],
            syndicateFormationTypes: [],
            syndicateSkillTypes: [],
            disdainRules: [],
            jiangHuAttributes: [],
            jiangHuCultivateStatuses: [],
            refineryBoxes: [],
            refinerySouls: [],
            refineryMaterials: [],
            stateCountFiles: [],
            binarySeedFiles: [],
            emptySeedFiles: [],
            backupSeedFiles: [],
            pokerTables: [],
            rouletteTables: [],
            npcTimeEntries: [],
            activityTasks: new ActivityTaskCatalog(new ActivityTaskMetadata(0, "", 0, []), [], "fixture://activity"),
            meltingTypeRules: [],
            lotteryPrizes: [],
            arenaRecords: [],
            elitePkRecords: [],
            teamElitePkRecords: [],
            skillTeamPkRecords: [],
            classPkWarRecords: [],
            combatGearRanks: [],
            prestigeRanks: [],
            innerPowerRanks: [],
            runeRankings: [],
            staticStatues: [],
            newPlayerRewards: [],
            votes: [],
            couplesPkRecords: [],
            socialAssociations: [],
            guildWarInfo: EmptyGuildWarData("guildwar"),
            superGuildWarInfo: EmptyGuildWarData("superguildwar"),
            classLevelStats: [],
            professionLevelUps: [],
            professionLevelBenefits: [],
            professionTitleBenefits: [],
            questInfo: new QuestInfoMetadata(ActionBase: null, RecommendInterval: null, TotalMission: null),
            questTaskTypes: [],
            questMissions: [],
            quizQuestions: [],
            magic: magic,
            magicEffects: [],
            magicTypeOperations: magicTypeOperations,
            titleTypes: [],
            combatGear: [],
            traps: [],
            npcs: [],
            sobNpcs: [],
            portals: [],
            maps: [],
            mapNames: [],
            monsters: [],
            monsterSpawns: [],
            lotteryDictionary: [],
            collectionRewardTiers: [],
            taskRewardTypes: [],
            taskRewardTypeIniEntries: [],
            neiGongHeader: new NeiGongHeader(0, [], "fixture://neigong", 0, "", new Dictionary<string, string>()),
            neiGongSecrets: [],
            neiGongTypes: [],
            subProfessions: [],
            syndicateLevels: [],
            stageGoalInfo: new StageGoalInfo(0),
            stageGoalStages: [],
            stageGoalTasks: [],
            shops: [],
            shopItems: [],
            exchangeShopGoods: [],
            exchangeShopGoodsEx: [],
            exchangeShopLevels: [],
            honorShopItems: [],
            honorShopRecommendations: [],
            racePointShopItems: [],
            racePointShopRecommendations: [],
            goldenLeagueShopItems: [],
            goldenLeagueShopRecommendations: [],
            goldenLeagueShopConfig: new Dictionary<string, string>(),
            booths: [],
            boothItems: []);
    }

    private static GuildWarData EmptyGuildWarData(string name)
    {
        return new GuildWarData($"fixture://{name}", [], [], [], info: null, pole: null, conductors: []);
    }
}
