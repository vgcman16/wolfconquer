using System.Globalization;
using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Theory]
    [InlineData("cas")]
    [InlineData("set")]
    [InlineData("scalars")]
    [InlineData("items")]
    [InlineData("gear")]
    [InlineData("mailbox")]
    public Task RealAutosaveAndSnapshotWritersCannotChangeRetainedPackageBeforeimages(string writer) =>
        RunPackageJoinAsync((fixture, join) =>
        {
            var initial = CapturePackageJoin(fixture, join);
            var pending = PrepareDebtIntent(fixture, join, initial);
            var before = CapturePackageJoin(fixture, join);
            var fields = new Dictionary<string, string>(before.Owner.Character.Attributes, StringComparer.OrdinalIgnoreCase)
            { ["Money"] = (before.Owner.Character.Money + 1).ToString(CultureInfo.InvariantCulture) };
            var character = before.Owner.Character with { Money = before.Owner.Character.Money + 1, Attributes = fields };
            var changed = before.Owner with { Character = character };
            switch (writer)
            {
                case "cas":
                    Assert.False(join.SessionStore.TryCompareAndSet(join.Connection!, before.Owner, changed));
                    break;
                case "set":
                    Assert.Throws<InvalidOperationException>(() => join.SessionStore.Set(join.Connection!, changed));
                    break;
                case "scalars":
                    Assert.False(CharacterSnapshotScalarPersistence.TryPersist(fixture.Users, character, out _, out _));
                    break;
                case "items":
                    var items = before.Owner.PlayerItems with
                    { InventoryItems = before.Owner.PlayerItems.InventoryItems.Select(item => item.WithStackSize(2)).ToArray() };
                    Assert.Throws<IOException>(() => new PlayerItemSnapshotLoader(fixture.Items).SaveByCharacterUid(Fixture.Uid, items));
                    break;
                case "gear":
                    var gear = Assert.IsType<CombatGearSnapshot>(before.Owner.CombatGear);
                    Assert.Throws<IOException>(() => new CombatGearSnapshotLoader(fixture.PackageGearRoot)
                        .SaveByCharacterUid(Fixture.Uid, gear with { MasteryPoints = gear.MasteryPoints + 1 }));
                    break;
                case "mailbox":
                    var mailbox = ActualDebtFrameOwner<LegacyMailboxStore>(join, "_mailboxes");
                    Assert.Equal(2, mailbox.List(Fixture.Uid).Count);
                    var cache = JsonSerializer.Serialize(mailbox.List(Fixture.Uid));
                    var original = fixture.AtomicMailSeed;
                    Assert.Throws<IOException>(() => mailbox.Add(Fixture.Uid, new LegacyMailboxEntry
                    {
                        Id = original.Id, Title = original.Title, SenderName = original.SenderName, Content = original.Content,
                        ItemId = original.ItemId, Money = original.Money + 1, EMoney = original.EMoney,
                        EmoneyRecordType = original.EmoneyRecordType, Flag = original.Flag, Timer = original.Timer,
                    }));
                    Assert.Equal(cache, JsonSerializer.Serialize(mailbox.List(Fixture.Uid)));
                    Assert.Throws<IOException>(() => mailbox.Save(Fixture.Uid));
                    Assert.Equal(cache, JsonSerializer.Serialize(mailbox.List(Fixture.Uid)));
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(writer));
            }
            AssertDebtRetained(fixture, join, before, pending);
            return Task.CompletedTask;
        });
}
