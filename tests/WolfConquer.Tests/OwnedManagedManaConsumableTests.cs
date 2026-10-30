using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;
using Fixture = WolfConquer.Tests.WorldServerFrameHandlerTests.ManagedManaConsumableFixture;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class OwnedManagedManaConsumableTests
{
    private static Task Run(ushort stack, Func<Fixture, Task> body, bool metadata = true,
        bool protectedPin = false, string amount = "70", long initialMana = 10) =>
        WorldServerFrameHandlerTests.WithOwnedManagedManaConsumableAsync(stack, body, metadata, protectedPin, amount, initialMana);
    private static PacketFrame Frame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _)); return frame;
    }
    private static byte[] ExpectedItems(byte[] original, ushort stack)
    {
        if (stack > 1)
        {
            var image = original.ToArray(); BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(4 + 52), (ushort)(stack - 1)); return image;
        }
        var removed = original[..4].Concat(original[(4 + PlayerItemSnapshotLoader.LegacyDbItemSize)..]).ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(removed, 1); return removed;
    }
    private static void AssertManagedMana(Fixture f, long mana, int hp = 10)
    {
        Assert.Equal(mana, f.Current.Character.MinMana);
        Assert.Equal(mana.ToString(System.Globalization.CultureInfo.InvariantCulture), f.Current.Character.Attributes["MinMana"]);
        var disk = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(f.CharacterPath), out _));
        Assert.Equal(mana, disk.MinMana); Assert.Equal(5000L, disk.MaxMana); Assert.Equal(5000L, f.Current.Character.MaxMana);
        Assert.Equal((long)hp, disk.MinHitPoints); Assert.Equal((long)hp, f.Current.Character.MinHitPoints);
        Assert.Equal(hp, f.Player.HitPoints); Assert.Equal(hp > 0, f.Player.Alive); Assert.Equal(hp <= 0, f.Player.NotLife);
        // Physical Mana is deliberately not an equality/staleness contract here.
        // A later genuine hydration/publication fix must remain free to agree.
    }

    [Theory]
    [InlineData((ushort)0)] [InlineData((ushort)1)] [InlineData((ushort)3)]
    public Task ActualAdmittedManaUseCommitsBothExactImagesBeforeItsFirstD13Output(ushort stack) => Run(stack, async f =>
    {
        await f.Login(); var before = f.Current; var files = f.Files();
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Native.OrdinarySpellBinding);
        var other = Assert.Single(before.PlayerItems.InventoryItems, row => row.Uid == Fixture.OtherUid);
        var nativeOther = Assert.IsType<LegacyNativeInventoryRuntime>(f.Native.Inventory).ClientItems[Fixture.OtherUid];
        var otherImage = PlayerItemSnapshotLoader.ToLegacyBytes(PlayerItemSnapshot.Empty with { InventoryItems = new[] { other } });
        var characterAfter = LegacyBoundHitpointsScalarWrite.ProjectAfterImage(File.ReadAllBytes(f.CharacterPath),
            before.Character with { MinMana = 80 });
        var itemsAfter = ExpectedItems(File.ReadAllBytes(f.ItemsPath), stack);
        int first = f.Sent.Count; bool observed = false; Exception? observationFailure = null;
        f.OnSend = _ =>
        {
            if (observed) return; observed = true;
            try
            {
                Assert.Equal(first, f.Sent.Count); Assert.NotSame(before, f.Current);
                Assert.Equal(characterAfter, File.ReadAllBytes(f.CharacterPath)); Assert.Equal(itemsAfter, File.ReadAllBytes(f.ItemsPath));
                AssertManagedMana(f, 80); Assert.NotNull(f.Current.LastLegacyPotionUseUtc);
                var currentOther = Assert.Single(f.Current.PlayerItems.InventoryItems, row => row.Uid == Fixture.OtherUid);
                Assert.Equal(otherImage, PlayerItemSnapshotLoader.ToLegacyBytes(PlayerItemSnapshot.Empty with { InventoryItems = new[] { currentOther } }));
                Assert.Same(nativeOther, f.Native.Inventory!.ClientItems[Fixture.OtherUid]);
                Assert.Same(binding, f.Native.OrdinarySpellBinding); Assert.False(binding.IsRetired);
                Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding)); Assert.False(f.Recovery.IsBlocked);
                Assert.False(LegacyTradeJournal.HasPending(f.Users));
            }
            catch (Exception error) { observationFailure = error; throw; }
        };
        await f.Use(); f.OnSend = null;
        if (observationFailure is not null) ExceptionDispatchInfo.Capture(observationFailure).Throw();
        Assert.True(observed, f.Log.ToString()); Assert.Equal(2, f.Sent.Count - first);
        if (stack > 1)
        {
            Assert.True(LegacyGameItemPacket.TryParse(Frame(f.Sent[first]), out var item));
            Assert.Equal(Fixture.PotionUid, item.Uid); Assert.Equal((ushort)(stack - 1), item.StackSize);
            Assert.Equal(LegacyGameItemPacket.UpdateItemMode, item.Mode);
        }
        else
        {
            Assert.True(MsgItemUsagePacket.TryParse(Frame(f.Sent[first]), out var removed));
            Assert.Equal(MsgItemUsageAction.RemoveInventory, removed.Action); Assert.Equal(Fixture.PotionUid, removed.Id);
        }
        Assert.True(LegacyMsgUpdatePacket.TryParse(Frame(f.Sent[first + 1]), out var current));
        Assert.Equal((uint)Fixture.Uid, current.Uid);
        var mana = Assert.Single(current.Updates); Assert.Equal(LegacyMsgUpdateDataType.Mana, mana.Type); Assert.Equal(80UL, mana.Value);
        AssertManagedMana(f, 80);
        Assert.Equal(stack > 1 ? new[] { Fixture.PotionUid, Fixture.OtherUid } : new[] { Fixture.OtherUid },
            f.Current.PlayerItems.InventoryItems.Select(row => row.Uid));
        Assert.Equal(before.Character.Money, f.Current.Character.Money); Assert.Equal(before.Character.ConquerPoints, f.Current.Character.ConquerPoints);
        Assert.Equal(before.Character.BoundConquerPoints, f.Current.Character.BoundConquerPoints);
        var text = CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(f.CharacterPath), out _);
        Assert.Contains(Fixture.OpaqueComment, text); Assert.Contains(Fixture.OpaqueAttribute, text);
        foreach (var pair in files.Where(pair => pair.Key != f.CharacterPath && pair.Key != f.ItemsPath)) Assert.Equal(pair.Value, File.ReadAllBytes(pair.Key));
        Assert.DoesNotContain("PARITY-BUG", f.Log.ToString(), StringComparison.Ordinal);
    });

    [Fact]
    public Task GenuineProtectedButUnverifiedPinDoesNotBecomeANewManaPermissionGate() => Run(3, async f =>
    {
        await f.Login(); Assert.True(LegacySecondaryPasswordRuntime.TryGet(f.Connection, f.Current, out var pin, out var verified));
        Assert.NotEqual(0U, pin.Password); Assert.False(verified);
        int first = f.Sent.Count; await f.Use(); Assert.Equal(2, f.Sent.Count - first); AssertManagedMana(f, 80);
        Assert.True(LegacySecondaryPasswordRuntime.TryGet(f.Connection, f.Current, out var retained, out verified));
        Assert.Equal(pin, retained); Assert.False(verified);
    }, protectedPin: true);

    [Fact]
    public Task TheActualSourceAmountClampsToTheSavedManagedMaximum() => Run(3, async f =>
    {
        await f.Login(); int first = f.Sent.Count; await f.Use();
        Assert.Equal(2, f.Sent.Count - first); AssertManagedMana(f, 5000);
        Assert.Equal((ushort)2, Assert.Single(f.Current.PlayerItems.InventoryItems, row => row.Uid == Fixture.PotionUid).StackSize);
        Assert.True(LegacyMsgUpdatePacket.TryParse(Frame(f.Sent[first + 1]), out var current));
        Assert.Equal(5000UL, Assert.Single(current.Updates).Value);
    }, initialMana: 4980);

    [Fact]
    public Task TwoAcceptedUsesAndFreshOwnedAdmissionRetainTheCanonicalSavedManaBaseline() => Run(3, async f =>
    {
        await f.Login(); var oldBinding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Native.OrdinarySpellBinding);
        var firstItems = File.ReadAllBytes(f.ItemsPath); int first = f.Sent.Count;
        await f.Use(); Assert.Equal(2, f.Sent.Count - first); AssertManagedMana(f, 80);
        Assert.Equal(ExpectedItems(firstItems, 3), File.ReadAllBytes(f.ItemsPath));
        await Task.Delay(175); // Exercise the real 150ms clock rule; never rewrite its timestamp for acceptance.
        first = f.Sent.Count; await f.Use(); Assert.Equal(2, f.Sent.Count - first); AssertManagedMana(f, 150);
        Assert.Equal((ushort)1, Assert.Single(f.Current.PlayerItems.InventoryItems, row => row.Uid == Fixture.PotionUid).StackSize);
        await f.Reload(); Assert.True(oldBinding.IsRetired); AssertManagedMana(f, 150);
        var newBinding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Native.OrdinarySpellBinding);
        Assert.NotSame(oldBinding, newBinding); Assert.True(f.Store.IsBoundSpellOwnerCurrent(newBinding));
        first = f.Sent.Count; await f.Use(); Assert.Equal(2, f.Sent.Count - first); AssertManagedMana(f, 220);
        Assert.DoesNotContain(f.Current.PlayerItems.InventoryItems, row => row.Uid == Fixture.PotionUid);
        Assert.False(f.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(f.Users));
        Assert.DoesNotContain("runtime exception", f.Log.ToString(), StringComparison.OrdinalIgnoreCase);
    });
}
