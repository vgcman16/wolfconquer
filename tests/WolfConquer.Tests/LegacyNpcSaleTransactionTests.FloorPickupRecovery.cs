using System.Globalization;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNpcSaleTransactionTests
{
    [Theory]
    [InlineData(0, 0)] // Item: published WAL, both preimages still present.
    [InlineData(0, 2)] // Item: both postimages, pending record still present.
    [InlineData(2, 1)] // CP: changed Character replaced, unchanged Items not yet replaced.
    public async Task PendingPickupJournalFencesLiveOwnerAndFreshLoginRecoversAssetsOnceWithoutRecreatingGround(int disposition, int replacements)
    {
        const uint uid = 1_300_011;
        Seed(uid);
        var owner = NewOwner(identity: new(42));
        var player = await Login(owner, uid);
        var original = Current(owner, player);
        var entry = SeedPersistencePickupFloor(owner, player, disposition);
        var wallet = NormalPersistencePickupWallet(original, disposition, PersistencePickupAmount);
        var updated = RecoveryPickupResult(original, entry, wallet);
        var beforeCharacter = File.ReadAllBytes(UserPath(uid));
        var beforeItems = File.ReadAllBytes(ItemPath(uid));
        var afterCharacter = ExpectedPersistencePickupCharacterBytes(beforeCharacter,
            PersistencePickupWalletValues(original, entry.Kind, wallet));
        var afterItems = PlayerItemSnapshotLoader.ToLegacyBytes(updated.PlayerItems);

        // This is the real v2 writer's managed interruption seam, not a killed
        // process, runtime injection option, power loss, or durable floor proof.
        Assert.False(LegacyNpcSalePersistence.TryPersistFloorPickup(original, updated, ItemsRoot,
            entry, entry.Item, LegacyJournalInterruption.StopAfter(replacements), out var reason));
        Assert.StartsWith(LegacyTradeJournal.RecoveryRequired, reason, StringComparison.Ordinal);
        Assert.True(LegacyTradeJournal.HasPending(UsersRoot));
        Assert.Equal(replacements == 0 ? beforeCharacter : afterCharacter, File.ReadAllBytes(UserPath(uid)));
        Assert.Equal(replacements < 2 ? beforeItems : afterItems, File.ReadAllBytes(ItemPath(uid)));
        var pendingCharacter = File.ReadAllBytes(UserPath(uid));
        var pendingItems = File.ReadAllBytes(ItemPath(uid));
        var journalBytes = File.ReadAllBytes(PendingPath);

        await AssertPendingPickupFencesCurrentOwner(owner, player, original, entry,
            pendingCharacter, pendingItems, journalBytes);

        var restarted = NewOwner(identity: new(42));
        var reopened = await Login(restarted, uid);

        AssertRecoveredPickupAssets(restarted, reopened, updated, wallet, afterCharacter, afterItems);
        await AssertPersistencePickupReplayIsSilent(restarted, reopened);
        // A valid independent owner recovering the record cannot unfence this
        // old world's stale wallet/inventory or consume its retained floor token.
        await AssertPendingPickupPingIsFenced(owner, player);
        Assert.Same(original, Current(owner, player));
        Assert.Same(entry, Assert.Single(DropFloor(owner).SnapshotVisible(DateTimeOffset.UtcNow)));
        Assert.Equal(afterCharacter, File.ReadAllBytes(UserPath(uid)));
        Assert.Equal(afterItems, File.ReadAllBytes(ItemPath(uid)));

        var again = NewOwner(identity: new(42));
        var reloaded = await Login(again, uid);
        AssertRecoveredPickupAssets(again, reloaded, updated, wallet, afterCharacter, afterItems);
        await AssertPersistencePickupReplayIsSilent(again, reloaded);
        Assert.True(LegacyTradeJournal.TryRecover(UsersRoot, ItemsRoot, out var replayed, out reason), reason);
        Assert.Equal(0, replayed);
        Assert.Equal(afterCharacter, File.ReadAllBytes(UserPath(uid)));
        Assert.Equal(afterItems, File.ReadAllBytes(ItemPath(uid)));
    }

    [Fact]
    public async Task CorruptPickupJournalRetainsFloorAndEvidenceWhileBlockingLivePickupAndFreshAdmission()
    {
        const uint uid = 1_300_021;
        Seed(uid);
        var owner = NewOwner(identity: new(42));
        var player = await Login(owner, uid);
        var original = Current(owner, player);
        var entry = SeedPersistencePickupFloor(owner, player, disposition: 0);
        var wallet = NormalPersistencePickupWallet(original, disposition: 0, amount: PersistencePickupAmount);
        var updated = RecoveryPickupResult(original, entry, wallet);
        Assert.False(LegacyNpcSalePersistence.TryPersistFloorPickup(original, updated, ItemsRoot,
            entry, entry.Item, LegacyJournalInterruption.StopAfter(0), out var reason));
        Assert.StartsWith(LegacyTradeJournal.RecoveryRequired, reason, StringComparison.Ordinal);
        File.WriteAllText(PendingPath, "{\"Version\":2,\"Changes\":[");
        var journalBytes = File.ReadAllBytes(PendingPath);
        var characterBytes = File.ReadAllBytes(UserPath(uid));
        var itemBytes = File.ReadAllBytes(ItemPath(uid));

        await AssertPendingPickupFencesCurrentOwner(owner, player, original, entry,
            characterBytes, itemBytes, journalBytes);
        var fresh = NewOwner(identity: new(42));
        var attempted = CreateConnection(uid);

        await SendLogin(fresh, attempted);

        Assert.False(fresh.Sessions.TryGet(attempted.Connection, out _));
        Assert.Empty(attempted.Sent); // No admission, currency, item or floor reply.
        Assert.Contains("trade recovery", fresh.Log.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Empty(DropFloor(fresh).SnapshotVisible(DateTimeOffset.UtcNow));
        Assert.False(LegacyTradeJournal.TryRecover(UsersRoot, ItemsRoot, out var replayed, out reason));
        Assert.Equal(0, replayed);
        Assert.StartsWith(LegacyTradeJournal.RecoveryRequired, reason, StringComparison.Ordinal);
        Assert.Same(original, Current(owner, player));
        Assert.Same(entry, Assert.Single(DropFloor(owner).SnapshotVisible(DateTimeOffset.UtcNow)));
        Assert.Equal(characterBytes, File.ReadAllBytes(UserPath(uid)));
        Assert.Equal(itemBytes, File.ReadAllBytes(ItemPath(uid)));
        Assert.Equal(journalBytes, File.ReadAllBytes(PendingPath));
        Assert.True(LegacyTradeJournal.HasPending(UsersRoot));
    }

    private static WorldSession RecoveryPickupResult(WorldSession original, FloorItemRuntimeEntry entry, PersistencePickupWallet wallet)
    {
        var attributes = new Dictionary<string, string>(original.Character.Attributes, StringComparer.OrdinalIgnoreCase);
        foreach (var value in PersistencePickupWalletValues(original, entry.Kind, wallet)) attributes[value.Key] = value.Value;
        return original with
        {
            Character = original.Character with
            {
                Money = wallet.Money, ConquerPoints = wallet.ConquerPoints,
                BankConquerPoints = wallet.BankCps, Attributes = attributes,
            },
            PlayerItems = PersistencePickupItems(original, entry),
        };
    }

    private async Task AssertPendingPickupFencesCurrentOwner(Owner owner, Player player, WorldSession original,
        FloorItemRuntimeEntry entry, byte[] characterBytes, byte[] itemBytes, byte[] journalBytes)
    {
        var first = player.Sent.Count;
        var logStart = owner.Log.GetStringBuilder().Length;

        await PickupAdmissionFloor(owner, player, PersistencePickupFloorUid);

        Assert.Same(original, Current(owner, player));
        Assert.Empty(player.Sent.Skip(first));
        Assert.Same(entry, Assert.Single(DropFloor(owner).SnapshotVisible(DateTimeOffset.UtcNow)));
        Assert.Equal(characterBytes, File.ReadAllBytes(UserPath(player.Uid)));
        Assert.Equal(itemBytes, File.ReadAllBytes(ItemPath(player.Uid)));
        Assert.Equal(journalBytes, File.ReadAllBytes(PendingPath));
        Assert.Contains("tradeJournalRecoveryRequired:pending", owner.Log.ToString()[logStart..], StringComparison.Ordinal);
        await AssertPendingPickupPingIsFenced(owner, player);
        Assert.Same(original, Current(owner, player));
        Assert.Same(entry, Assert.Single(DropFloor(owner).SnapshotVisible(DateTimeOffset.UtcNow)));
        Assert.Equal(characterBytes, File.ReadAllBytes(UserPath(player.Uid)));
        Assert.Equal(itemBytes, File.ReadAllBytes(ItemPath(player.Uid)));
        Assert.Equal(journalBytes, File.ReadAllBytes(PendingPath));
    }

    private async Task AssertPendingPickupPingIsFenced(Owner owner, Player player)
    {
        var first = player.Sent.Count;
        var logStart = owner.Log.GetStringBuilder().Length;
        var ping = new MsgItemUsagePacket(0, 0, MsgItemUsageAction.Ping, 456, 0, 0, 0).ToFrameBytes();

        await owner.Handler.HandleFrameAsync(player.Connection, ReadFrame(ping), player.Lifetime.Token);

        Assert.Empty(player.Sent.Skip(first));
        Assert.Contains("trade recovery", owner.Log.ToString()[logStart..], StringComparison.OrdinalIgnoreCase);
    }

    private void AssertRecoveredPickupAssets(Owner owner, Player player, WorldSession expected,
        PersistencePickupWallet wallet, byte[] characterBytes, byte[] itemBytes)
    {
        var current = Current(owner, player);
        Assert.False(LegacyTradeJournal.HasPending(UsersRoot));
        Assert.Equal(characterBytes, File.ReadAllBytes(UserPath(player.Uid)));
        Assert.Equal(itemBytes, File.ReadAllBytes(ItemPath(player.Uid)));
        Assert.Equal(itemBytes, PlayerItemSnapshotLoader.ToLegacyBytes(current.PlayerItems));
        Assert.Equal(wallet.Money, current.Character.Money);
        Assert.Equal(wallet.ConquerPoints, current.Character.ConquerPoints);
        Assert.Equal(wallet.BankCps, current.Character.BankConquerPoints);
        Assert.Equal(wallet.WarehouseMoney.ToString(CultureInfo.InvariantCulture), current.Character.Attributes["WHMoney"]);
        // Ordinary login saving materializes fields absent from the old live
        // dictionary. The exact accepted postimage, not that pre-save holder,
        // defines the complete fresh-loader attribute projection.
        var savedCharacter = CharacterSnapshotParser.ParseText(Encoding.UTF8.GetString(characterBytes), expected.Character.SourceFile);
        // Startup's equipment query always runs Prefix.RunVigorAndCreateSpawnPrefix
        // and Appearance commits Vigor even without equipment. The empty fixture
        // has maximum0 and absent current Vigor0, so the exact runtime value is0.
        // Vigor is absent from ScalarPersistence.PersistedAttributeKeys: it is a
        // runtime projection, not a field this asset WAL is allowed to add to INI.
        Assert.Empty(expected.PlayerItems.EquipmentItems);
        Assert.False(savedCharacter.Attributes.ContainsKey("Vigor"));
        var expectedAttributes = new Dictionary<string, string>(savedCharacter.Attributes, StringComparer.OrdinalIgnoreCase)
        { ["Vigor"] = "0" };
        var extraAttributes = current.Character.Attributes.Where(field => !expectedAttributes.ContainsKey(field.Key))
            .Select(field => field.Key.Equals("SecurityPass", StringComparison.OrdinalIgnoreCase) ? "SecurityPass=[redacted extra]" :
                field.Key + "=" + field.Value).ToArray();
        var missingAttributes = expectedAttributes.Keys.Where(key => !current.Character.Attributes.ContainsKey(key)).ToArray();
        var changedAttributes = expectedAttributes.Where(field => current.Character.Attributes.TryGetValue(field.Key, out var value) && value != field.Value)
            .Select(field => field.Key.Equals("SecurityPass", StringComparison.OrdinalIgnoreCase) ? "SecurityPass=[redacted mismatch]" :
                field.Key + ":saved=" + field.Value + ",current=" + current.Character.Attributes[field.Key]).ToArray();
        Assert.True(expectedAttributes.Count == current.Character.Attributes.Count,
            $"Complete fresh attribute projection differs: extra=[{string.Join(";", extraAttributes)}] " +
            $"missing=[{string.Join(";", missingAttributes)}] changed=[{string.Join(";", changedAttributes)}].");
        foreach (var field in expectedAttributes)
            Assert.Equal(field.Value, current.Character.Attributes[field.Key]);
        Assert.Equal(expected.Character.PaidNobility.OrderBy(field => field.Key), current.Character.PaidNobility.OrderBy(field => field.Key));
        Assert.Equal(PlayerQuestSnapshotLoader.Serialize(expected.Character.PlayerQuests),
            PlayerQuestSnapshotLoader.Serialize(current.Character.PlayerQuests));
        Assert.Equal(expected.Character with
        {
            Attributes = current.Character.Attributes, PaidNobility = current.Character.PaidNobility,
            PlayerQuests = current.Character.PlayerQuests,
        }, current.Character);
        Assert.Empty(DropFloor(owner).SnapshotVisible(DateTimeOffset.UtcNow));
        // No old-world floor object is copied into the independent handler. The
        // WAL recovers assets only, not floor lifetime/map/ground restart state.
        Assert.Equal(expected.PlayerItems.InventoryItems.Count(item => item.Uid == AdmissionPickedItemUid),
            current.PlayerItems.InventoryItems.Count(item => item.Uid == AdmissionPickedItemUid));
    }
}
