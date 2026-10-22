using System.Globalization;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    [Fact]
    public async Task HandlerRuneProgressAdjacentIdRetainsRawProgress()
    {
        await AssertRuneProgressIdentityHandlerAsync(4_010_002, "NeutralRune", "0", 1);
    }

    [Fact]
    public async Task HandlerRuneProgressOtherIdWithComprehensionNameRetainsRawProgress()
    {
        await AssertRuneProgressIdentityHandlerAsync(4_010_501, "Comprehension label", "0", 1);
    }

    [Fact]
    public async Task HandlerRuneProgressOtherIdWithComprehensionFieldRetainsRawProgress()
    {
        await AssertRuneProgressIdentityHandlerAsync(4_010_501, "NeutralRune", "Comprehension marker", 1);
    }

    [Fact]
    public async Task HandlerRuneProgressExactComprehensionIdIgnoresNeutralMetadata()
    {
        await AssertRuneProgressIdentityHandlerAsync(4_010_001, "NeutralRune", "0", 5_030);
    }

    private async Task AssertRuneProgressIdentityHandlerAsync(
        uint runeItemId,
        string name,
        string finalMetadataField,
        uint expectedProgress)
    {
        const long accountId = 1_293_600;
        const uint runeUid = 5_337_600;
        var itemsRoot = Path.Combine(Path.GetTempPath(), "wolf-rune-progress-identity-" + Guid.NewGuid());
        try
        {
            Directory.CreateDirectory(_usersRoot);
            Directory.CreateDirectory(itemsRoot);
            WriteSnapshot(accountId, uid: accountId, mapId: 1_015, x: 826, y: 565);

            // Use an actual parser-accepted, present catalog row, not an absent-row shortcut.
            var fields = Enumerable.Repeat("0", 66).ToArray();
            fields[0] = runeItemId.ToString(CultureInfo.InvariantCulture);
            fields[1] = name;
            fields[ItemType.DurabilityFieldIndex] = "1";
            fields[ItemType.RuneSpellIdFieldIndex] = fields[0];
            fields[^1] = finalMetadataField;
            var itemType = ItemTypeParser.ParseLine(string.Join("@@", fields), expectedFieldCount: fields.Length);
            var worldData = CreateStaticVisibilityCatalog(items: [itemType]);
            var presentRow = Assert.Single(worldData.ItemsById[checked((int)runeItemId)]);
            Assert.Equal(checked((int)runeItemId), presentRow.Id);
            Assert.Equal(name, presentRow.Name);
            Assert.Equal(runeItemId, presentRow.RuneSpellId);
            Assert.Equal(finalMetadataField, presentRow.Fields[^1]);

            var initialRune = new PlayerItemSnapshotItem
            {
                Uid = runeUid,
                ItemId = runeItemId,
                Position = 101,
                Durability = 1,
                MaximumDurability = 1,
                StackSize = 1,
                SocketProgress = 17,
            };
            new PlayerItemSnapshotLoader(itemsRoot).SaveByCharacterUid(
                accountId,
                new PlayerItemSnapshot([], [], [], [], [initialRune], []));
            using var log = new StringWriter();
            var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
            var ticket = handoff.Issue(accountId, "rune-progress-identity", ConquerRuntimeDefaults.WorldServer);
            var handler = new WorldServerFrameHandler(
                new CharacterSnapshotLoader(_usersRoot, itemsRoot), handoff, worldData, log);
            var sent = new List<byte[]>();
            var connection = CreateSendingConnection(sent);
            await handler.HandleFrameAsync(
                connection,
                ReadFrame(new InternalWorldEnterRequestPacket(ticket.Token).ToFrameBytes()));
            var enteredRune = Assert.Single(
                GetSession(handler, connection).PlayerItems.RuneItems,
                item => item.Uid == runeUid);
            Assert.Equal(runeItemId, enteredRune.ItemId);
            Assert.Equal(17U, enteredRune.SocketProgress);
            var countBeforeRequest = sent.Count;

            await handler.HandleFrameAsync(
                connection,
                ReadFrame(new MsgItemUsagePacket(
                    Id: runeUid,
                    DwParam: 1,
                    Action: MsgItemUsageAction.RedRuneProgressControl,
                    Timestamp: 1246,
                    DwParam2: 0,
                    DwParam3: 0,
                    DwParam4: 0).ToFrameBytes()));

            Assert.Equal(countBeforeRequest + 1, sent.Count);
            var update = ParseLegacyGameItem(sent[countBeforeRequest]);
            Assert.Equal(runeUid, update.Uid);
            Assert.Equal(runeItemId, update.ItemId);
            Assert.Equal(expectedProgress, update.SocketProgress);
            Assert.Equal(LegacyGameItemPacket.UpdateItemMode, update.Mode);

            var live = Assert.Single(
                GetSession(handler, connection).PlayerItems.RuneItems,
                item => item.Uid == runeUid);
            Assert.Equal(runeItemId, live.ItemId);
            Assert.Equal(expectedProgress, live.SocketProgress);
            var reloaded = Assert.Single(
                new PlayerItemSnapshotLoader(itemsRoot).LoadByCharacterUid(accountId).RuneItems,
                item => item.Uid == runeUid);
            Assert.Equal(runeItemId, reloaded.ItemId);
            Assert.Equal(expectedProgress, reloaded.SocketProgress);
            Assert.Equal(enteredRune.Position, live.Position);
            Assert.Equal(live.Position, reloaded.Position);
            Assert.Equal(17U, enteredRune.SocketProgress);
            Assert.Contains("persisted MsgItemUsage red rune progress items", log.ToString());
        }
        finally
        {
            if (Directory.Exists(itemsRoot))
            {
                Directory.Delete(itemsRoot, recursive: true);
            }
        }
    }
}
