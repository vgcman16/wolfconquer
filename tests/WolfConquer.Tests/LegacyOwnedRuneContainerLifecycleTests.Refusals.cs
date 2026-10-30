using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using F = WolfConquer.Tests.LegacyOwnedRuneGameplayFixture;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedRuneContainerLifecycleTests
{
    [Theory]
    [InlineData("locked")] [InlineData("deposit")]
    public Task DestructionRejectsLockedRootsOrUnprovenChildDestructionWithoutAnyEffect(string fault)
    {
        var root = F.Item(F.First, F.FirstType);
        root = fault == "locked" ? root.WithLocked(1) : root.WithDepositedItems([F.Item(F.Other, F.OtherType)]);
        return WithSaved(new([], [], [], [], [root]), OwnedEquipmentBehaviorFixture.Character(classCode: 6000), async f =>
        {
            var before = f.Current(); var files = f.Data.Files(); int first = f.ActorFrames.Count;
            await f.DispatchAsync(new MsgItemUsagePacket(F.First, 0, MsgItemUsageAction.DropRune, 0, 0, 0, 0));
            Assert.Same(before, f.Current()); Assert.Equal(first, f.ActorFrames.Count); f.Data.AssertFiles(files);
            Assert.False(f.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        });
    }

    [Theory]
    [InlineData((byte)0)] [InlineData((byte)1)]
    public Task StoragePreservesLockedStackedAndDepositedClosureWithoutImportingEquipRules(byte type)
    {
        var root = F.Item(F.First, F.FirstType).WithLocked(1).WithStackSize(2)
            .WithDepositedItems([F.Item(F.Other, F.OtherType)]).WithPosition(type == 0 ? (ushort)211 : (ushort)212);
        var inventory = Enumerable.Range(0, 40).Select(index => new PlayerItemSnapshotItem
            { Uid = checked(2_000_000U + (uint)index), ItemId = 1_000_000, StackSize = 1, Durability = 1, MaximumDurability = 1 }).ToArray();
        var character = OwnedEquipmentBehaviorFixture.Character(classCode: 7051, level: 14) with
        { Attributes = new Dictionary<string, string> { ["RuntimeVendorUid"] = "100" } };
        return WithSaved(new(inventory, [], [], [], [root]), character, async f =>
        {
            var ledger = File.ReadAllBytes(f.Data.State); int first = f.ActorFrames.Count;
            await f.SendAsync(OwnedEquipmentBehaviorFixture.Actor, new LegacyRuneStoragePacket(type, F.First, 0).ToFrameBytes());
            Assert.Equal(11, f.ActorFrames.Count - first);
            var expected = root.WithPosition(type == 0 ? (ushort)212 : (ushort)211);
            Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(new(inventory, [], [], [], [expected])), File.ReadAllBytes(f.ItemPath()));
            Assert.Equal(ledger, File.ReadAllBytes(f.Data.State)); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(f.Current().NativeGameClient!.OrdinarySpellBinding!));
        });
    }

    [Theory]
    [InlineData(99, true)] [InlineData(100, false)]
    public Task OnlyAnAdditionalBagRowConsumesTheRealHundredRowCapacity(int count, bool admitted)
    {
        var selected = F.Item(F.First, F.FirstType);
        var rows = Enumerable.Range(0, count).Select(index => F.Item(checked(2_000_000U + (uint)index), F.YellowType).WithPosition(211))
            .Append(selected).ToArray();
        return WithSaved(new([], [], [], [], rows), OwnedEquipmentBehaviorFixture.Character(classCode: 6000), async f =>
        {
            var before = f.Current(); var files = f.Data.Files(); int first = f.ActorFrames.Count;
            await f.SendAsync(OwnedEquipmentBehaviorFixture.Actor, new LegacyRuneStoragePacket(1, F.First, 0).ToFrameBytes());
            if (!admitted)
            { Assert.Same(before, f.Current()); Assert.Equal(first, f.ActorFrames.Count); f.Data.AssertFiles(files); }
            else
            {
                Assert.Equal(11, f.ActorFrames.Count - first); Assert.Equal(100, f.Current().PlayerItems.RuneItems.Count(row => row.Position == 211));
                Assert.Equal((ushort)211, Assert.Single(f.Current().PlayerItems.RuneItems, row => row.Uid == F.First).Position);
            }
            Assert.False(f.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        });
    }

    [Theory]
    [InlineData("store")] [InlineData("bag")] [InlineData("drop")]
    public Task AStaleActualMetadataDependencyCannotPublishAContainerAction(string kind) => Run(async f =>
    {
        await PrepareClaim(f, kind);
        var fields = Assert.IsType<string[]>(f.Catalog.Items.Single(row => row.Id == F.FirstType).Fields);
        var original = fields[ItemType.RuneSpellIdFieldIndex];
        try
        {
            fields[ItemType.RuneSpellIdFieldIndex] = "1";
            await f.AssertRefusedAsync(() => Send(f, kind));
        }
        finally { fields[ItemType.RuneSpellIdFieldIndex] = original; }
    });

    [Theory]
    [InlineData("store")] [InlineData("bag")]
    public Task RetiringAContainerProviderDoesNotRequireAnUnrelatedBeastAvailabilityOwner(string kind) => Run(async f =>
    {
        await PrepareClaim(f, kind); var actual = f.Native.Beasts; f.Native.Beasts = null!; int first = f.Sent.Count;
        try
        {
            await Send(f, kind); f.AssertConverged(); f.AssertMarker("");
            Assert.True(f.Sent.Count >= first + 2);
            Assert.Equal(new MsgActionPacket { ObjId = F.Uid, DwParam = F.FirstSpell, Type = (MsgActionType)109 }.ToFrameBytes(), f.Sent[first]);
            Assert.Equal(new LegacyRuneStoragePacket(kind == "store" ? (byte)0 : (byte)1, F.First, 0).ToFrameBytes(), f.Sent[first + 1]);
            // The separate post-response equipment query retains its own real
            // dependencies; this asserts the completed container transaction.
        }
        finally { f.Native.Beasts = actual!; }
    });

    private static Task WithSaved(PlayerItemSnapshot items, CharacterSnapshot character, Func<OwnedEquipmentBehaviorFixture, Task> body)
    {
        ItemType Metadata(uint id, uint encoded, uint job = 6000) => OwnedEquipmentBehaviorFixture.Item(id, "OwnedContainerSavedInput",
            new Dictionary<int, string> { [ItemType.RequiredClassFieldIndex] = job.ToString(System.Globalization.CultureInfo.InvariantCulture),
                [ItemType.RuneSpellIdFieldIndex] = encoded.ToString(System.Globalization.CultureInfo.InvariantCulture) });
        var catalog = NativeCallerWorldDataFixture.Create(new ItemType[]
        {
            Metadata(F.FirstType, 1416016), Metadata(F.OtherType, 1547000), Metadata(F.YellowType, F.YellowType, 0),
            OwnedEquipmentBehaviorFixture.Item(1_000_000, "OwnedContainerOrdinary"),
        }, [], magicEffects: MagicEffectParser.Parse("[1416000]\r\nIsRuneSkill=1\r\n[1547000]\r\nIsRuneSkill=1\r\n", "owned-container-MagicEffect.ini"));
        return OwnedEquipmentBehaviorFixture.RunAsync(new(character, items), catalog, body);
    }
}
