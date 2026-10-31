using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyAuthenticatedOrdinaryCreationTests
{
    private sealed record CreationBefore(Dictionary<string, byte[]> Files, uint HighWater, ulong Generation);
    private static CreationBefore BeforeCreation(CreationFixture f)
    {
        var state = JsonNode.Parse(File.ReadAllBytes(f.Data.State))!;
        foreach (var kind in LegacyAuthenticatedOrdinaryCreationRecipe.TargetKinds) Assert.False(File.Exists(f.DataPath(kind)));
        Assert.False(File.Exists(Path.Combine(f.Data.Mailbox, CreationFixture.Account + ".bin")));
        Assert.False(f.TryCurrent(out _)); Assert.Equal(0U, f.Native.Player.UID);
        return new(f.Data.Files(), state["HighWater"]!.GetValue<uint>(), state["Generation"]!.GetValue<ulong>());
    }

    private static LegacyItemUidProducerIntentSnapshot CompletedIntent(CreationFixture f)
    {
        var registry = f.Runtime.Ordinary.Registry;
        Assert.True(registry.TryReadProducerIntents(f.Data.ExpectedDomain, out var all, out var reason), reason);
        var intent = Assert.Single(all, row => row.Request.Writer == LegacyOrdinaryAdmissionUidOwner.Writer);
        Assert.True(registry.TryReadActiveProducerIntents(f.Data.ExpectedDomain, out var active, out reason), reason);
        Assert.DoesNotContain(active, row => row.Request.OperationId == intent.Request.OperationId);
        Assert.NotNull(intent.Allocation); Assert.Equal(CreationFixture.Account, intent.Request.AccountId);
        Assert.Equal(CreationFixture.Account, intent.Request.CharacterUid);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(intent.CopyPayload())).ToLowerInvariant(), intent.Request.PayloadSha256);
        Assert.False(File.Exists(f.PendingPath)); return intent;
    }

    private static LegacyAuthenticatedOrdinaryCreationRecipe Recipe(CreationFixture f, LegacyItemUidProducerIntentSnapshot intent) =>
        LegacyAuthenticatedOrdinaryCreationRecipe.Decode(intent.CopyPayload(), f.Data.Options().Roots, f.Data.Mailbox);

    private static byte[] ExpectedStarterItems(LegacyAuthenticatedCharacterCreationProjection projection, LegacyItemUidReservationReceipt allocation)
    {
        var character = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(projection.CopyCharacterImage(), out _));
        Assert.True(LegacyFirstCharacterStarterProfiles.TryGet(character.Class, out var profile));
        var roots = new (uint Id, ushort Position)[] { (profile.ArmorItemId, 3), (profile.RightWeaponItemId, 4),
            (profile.LeftWeaponItemId, 5), (profile.RingItemId, 6) }.Where(row => row.Id != 0).ToArray();
        Assert.Equal(roots.Length, allocation.Count);
        return PlayerItemSnapshotLoader.ToLegacyBytes(new([], [], [], roots.Select((row, index) =>
        {
            uint uid = checked(allocation.First + (uint)index);
            return new PlayerItemSnapshotItem { Uid = uid, ItemId = row.Id, Position = row.Position, Durability = 321,
                MaximumDurability = 321, Color = 3 + unchecked(uid + row.Id) % 6 };
        }).ToArray(), []));
    }

    private static void AssertCreated(CreationFixture f, CreationBefore before)
    {
        var intent = CompletedIntent(f); var allocation = intent.Allocation!; var recipe = Recipe(f, intent);
        Assert.Equal(before.HighWater + 1, allocation.First); Assert.Equal("creation-owner", recipe.Username);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(f.Ticket))).ToLowerInvariant(), recipe.TicketDigest);
        Assert.Equal(("NewOwner", f.Selection), (recipe.Projection.Name, recipe.Projection.ClassSelection));
        var expectedItems = ExpectedStarterItems(recipe.Projection, allocation);
        Assert.Equal(expectedItems, File.ReadAllBytes(f.ItemsPath));
        Assert.Equal(recipe.Projection.CopySpellsImage(), File.ReadAllBytes(f.SpellsPath));
        Assert.Equal(recipe.Projection.CopyProficienciesImage(), File.ReadAllBytes(f.DataPath(LegacyTradeJournalFileKind.Proficiencies)));
        Assert.Equal(recipe.Projection.CopyQuestsImage(), File.ReadAllBytes(f.DataPath(LegacyTradeJournalFileKind.Quests)));
        Assert.Equal(recipe.Projection.CopyCombatGearImage(), File.ReadAllBytes(f.DataPath(LegacyTradeJournalFileKind.CombatGear)));
        Assert.Equal(LegacyOrdinaryAdmissionImages.ProjectCharacter(recipe.Projection.CopyCharacterImage(), recipe.Projection.PreparedAt), File.ReadAllBytes(f.CharacterPath));
        Assert.False(File.Exists(Path.Combine(f.Data.Mailbox, CreationFixture.Account + ".bin")));
        var state = JsonNode.Parse(File.ReadAllBytes(f.Data.State))!;
        Assert.Equal(before.HighWater + (uint)allocation.Count, state["HighWater"]!.GetValue<uint>());
        Assert.Equal(before.Generation + 3, state["Generation"]!.GetValue<ulong>());
        Assert.Equal(1UL, state["ArchiveCount"]!.GetValue<ulong>());
        foreach (var pair in before.Files)
            if (!pair.Key.StartsWith(Path.GetRelativePath(f.Data.Root, f.Data.Registry) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                Assert.Equal(pair.Value, f.Data.Files()[pair.Key]);
        AssertCurrentCreated(f, expectedItems);
    }

    private static void AssertCurrentCreated(CreationFixture f, byte[] expectedItems)
    {
        var current = f.Current; var native = f.Native;
        Assert.Equal((long)CreationFixture.Account, current.AccountId); Assert.Equal((long)CreationFixture.Account, current.Character.Uid);
        Assert.Equal(expectedItems, PlayerItemSnapshotLoader.ToLegacyBytes(current.PlayerItems));
        Assert.Equal(expectedItems, File.ReadAllBytes(f.ItemsPath));
        var binding = native.OrdinarySpellBinding!; Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding)); Assert.True(binding.Items.Image.IsCurrentPlain());
        Assert.Equal(current.PlayerItems.EquipmentItems.Select(row => row.Uid).Order(), native.Equipment!.ClientItems.Keys.Order());
        foreach (var row in current.PlayerItems.EquipmentItems)
        {
            var actual = native.Equipment.ClientItems[row.Uid]; Assert.Equal(row.ItemId, actual.ITEM_ID);
            Assert.Equal(row.Position, actual.Position); Assert.Equal(row.StackSize, actual.StackSize); Assert.Equal(row.Durability, actual.Durability);
        }
        Assert.Empty(native.Inventory!.ClientItems); Assert.Empty(native.Rune!.Objects);
        Assert.Equal(current.PlayerSpells.Spells.Select(row => row.Id).Order(), native.MySpells!.ClientSpells.Keys.Order());
        foreach (var row in current.PlayerSpells.Spells)
        {
            var actual = native.MySpells.ClientSpells[row.Id]; Assert.Equal(row.Level, actual.Level); Assert.Equal(row.Experience, actual.Experience);
            Assert.Equal(row.PreviousLevel, actual.PreviousLevel); Assert.Equal(row.SoulLevel, actual.SoulLevel); Assert.Equal(row.UseSpellSoul, actual.UseSpellSoul);
        }
        Assert.Equal(current.Character.Money, native.Player.Money); Assert.Equal(current.Character.MinHitPoints, (long)native.Player.HitPoints);
        Assert.Equal((1L, 0L), (current.Character.Level, current.Character.Reborn)); Assert.False(f.Runtime.Recovery.IsBlocked);
    }

    private static async Task<CreationBefore> CreateAndAssert(CreationFixture f)
    {
        var before = BeforeCreation(f); int observed = 0;
        f.BeforeSend = (uid, _) => { Assert.Equal(CreationFixture.Account, uid); AssertCreated(f, before); observed++; };
        try { await f.CreateAsync(); } finally { f.BeforeSend = null; }
        Assert.True(observed > 0); AssertCreated(f, before);
        Assert.Equal(LegacyMsgMessagePacket.AnswerOk().ToFrameBytes(), f.Sent[0]); return before;
    }

    private static async Task DepositNewbornMoney(CreationFixture f)
    {
        var items = File.ReadAllBytes(f.ItemsPath); var state = File.ReadAllBytes(f.Data.State); var archive = CompletedIntent(f).CopyPayload();
        var before = f.Current; int first = f.Sent.Count;
        var request = new MsgItemUsagePacket(12345, 700, MsgItemUsageAction.DepositWarehouse, 112233, 44, 55, 66);
        f.BeforeSend = (uid, frame) =>
        {
            Assert.Equal(CreationFixture.Account, uid); Assert.Equal(99_300, f.Current.Character.Money); Assert.Equal("700", f.Current.Character.Attributes["WHMoney"]);
            Assert.Equal(99_300, f.Native.Player.Money); Assert.Equal(700, f.Native.Player.WHMoney);
            var saved = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(f.CharacterPath), out _));
            Assert.Equal(99_300, saved.Money); Assert.Equal(items, File.ReadAllBytes(f.ItemsPath)); Assert.Equal(state, File.ReadAllBytes(f.Data.State));
            Assert.Equal(archive, CompletedIntent(f).CopyPayload()); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        };
        try { await f.SendAsync(request.ToFrameBytes()); } finally { f.BeforeSend = null; }
        Assert.Equal(first + 3, f.Sent.Count); Assert.Equal(request.ToFrameBytes(), f.Sent[^1]);
        Assert.Equal(before.Character.MinHitPoints, f.Current.Character.MinHitPoints); AssertCurrentCreated(f, items);
    }
}
