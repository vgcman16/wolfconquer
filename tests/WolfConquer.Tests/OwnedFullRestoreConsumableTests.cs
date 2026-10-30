using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;
using Fixture = WolfConquer.Tests.WorldServerFrameHandlerTests.FullRestoreConsumableFixture;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class OwnedFullRestoreConsumableTests
{
    private const int Maximum = 100000;
    private static Task Run(ushort stack, Func<Fixture, Task> body, bool metadata = true,
        bool protectedPin = false, string cap = "10", long initialMana = 40, int initialHp = 10) =>
        WorldServerFrameHandlerTests.WithOwnedFullRestoreConsumableAsync(stack, body, metadata, protectedPin, cap, initialMana, initialHp);
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
    private static void AssertVitals(Fixture f, int hp, long mana)
    {
        Assert.Equal((long)hp, f.Current.Character.MinHitPoints); Assert.Equal(hp, f.Player.HitPoints);
        Assert.Equal(hp > 0, f.Player.Alive); Assert.Equal(hp <= 0, f.Player.NotLife);
        Assert.Equal(mana, f.Current.Character.MinMana); Assert.Equal(5000L, f.Current.Character.MaxMana);
        Assert.Equal(hp.ToString(System.Globalization.CultureInfo.InvariantCulture), f.Current.Character.Attributes["MinHitPoints"]);
        Assert.Equal(mana.ToString(System.Globalization.CultureInfo.InvariantCulture), f.Current.Character.Attributes["MinMana"]);
        var saved = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(f.CharacterPath), out _));
        Assert.Equal((long)hp, saved.MinHitPoints); Assert.Equal(mana, saved.MinMana); Assert.Equal(5000L, saved.MaxMana);
        // Physical Mana is not a fabricated owner/equality or staleness contract.
    }

    [Theory]
    [InlineData((ushort)0)] [InlineData((ushort)1)] [InlineData((ushort)3)]
    public Task RealFullRestoreCommitsBothExactImagesAndTypedHpBeforeItsFourD13Outputs(ushort stack) => Run(stack, async f =>
    {
        await f.Login(); var before = f.Current; var files = f.Files();
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Native.OrdinarySpellBinding);
        var other = Assert.Single(before.PlayerItems.InventoryItems, row => row.Uid == Fixture.OtherUid);
        var nativeOther = Assert.IsType<LegacyNativeInventoryRuntime>(f.Native.Inventory).ClientItems[Fixture.OtherUid];
        var otherImage = PlayerItemSnapshotLoader.ToLegacyBytes(PlayerItemSnapshot.Empty with { InventoryItems = new[] { other } });
        var characterAfter = LegacyBoundHitpointsScalarWrite.ProjectAfterImage(File.ReadAllBytes(f.CharacterPath),
            before.Character with { MinHitPoints = Maximum, MinMana = 5000 });
        var itemsAfter = ExpectedItems(File.ReadAllBytes(f.ItemsPath), stack);
        int first = f.Sent.Count; bool observed = false; Exception? observationFailure = null;
        f.OnSend = _ =>
        {
            if (observed) return; observed = true;
            try
            {
                Assert.Equal(first, f.Sent.Count); Assert.Equal(characterAfter, File.ReadAllBytes(f.CharacterPath));
                Assert.Equal(itemsAfter, File.ReadAllBytes(f.ItemsPath)); AssertVitals(f, Maximum, 5000);
                var currentOther = Assert.Single(f.Current.PlayerItems.InventoryItems, row => row.Uid == Fixture.OtherUid);
                Assert.Equal(otherImage, PlayerItemSnapshotLoader.ToLegacyBytes(PlayerItemSnapshot.Empty with { InventoryItems = new[] { currentOther } }));
                Assert.Same(nativeOther, f.Native.Inventory!.ClientItems[Fixture.OtherUid]);
                Assert.NotNull(f.Current.LastLegacyPotionUseUtc); Assert.Same(binding, f.Native.OrdinarySpellBinding);
                Assert.False(binding.IsRetired); Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
                Assert.False(f.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(f.Users));
            }
            catch (Exception error) { observationFailure = error; throw; }
        };
        await f.Use(); f.OnSend = null;
        if (observationFailure is not null) ExceptionDispatchInfo.Capture(observationFailure).Throw();
        Assert.True(observed, f.Log.ToString()); Assert.Equal(4, f.Sent.Count - first);
        if (stack > 1)
        {
            Assert.True(LegacyGameItemPacket.TryParse(Frame(f.Sent[first]), out var item));
            Assert.Equal(Fixture.PotionUid, item.Uid); Assert.Equal(Fixture.PotionType, item.ItemId);
            Assert.Equal((ushort)(stack - 1), item.StackSize); Assert.Equal(LegacyGameItemPacket.UpdateItemMode, item.Mode);
        }
        else
        {
            Assert.True(MsgItemUsagePacket.TryParse(Frame(f.Sent[first]), out var item));
            Assert.Equal(new MsgItemUsagePacket(Fixture.PotionUid, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0), item);
        }
        AssertHpFrames(f.Sent, first + 1);
        Assert.True(LegacyMsgUpdatePacket.TryParse(Frame(f.Sent[first + 3]), out var mana));
        Assert.Equal(Fixture.Uid, mana.Uid);
        Assert.Equal([new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mana, 5000)], mana.Updates);
        AssertVitals(f, Maximum, 5000);
        Assert.Equal(before.Character.Money, f.Current.Character.Money); Assert.Equal(before.Character.ConquerPoints, f.Current.Character.ConquerPoints);
        Assert.Equal(before.Character.BoundConquerPoints, f.Current.Character.BoundConquerPoints);
        var text = CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(f.CharacterPath), out _);
        Assert.Contains(Fixture.OpaqueComment, text); Assert.Contains(Fixture.OpaqueAttribute, text);
        foreach (var pair in files.Where(pair => pair.Key != f.CharacterPath && pair.Key != f.ItemsPath)) Assert.Equal(pair.Value, File.ReadAllBytes(pair.Key));
        Assert.DoesNotContain("PARITY-BUG", f.Log.ToString(), StringComparison.Ordinal);
    });

    private static void AssertHpFrames(IReadOnlyList<byte[]> frames, int start)
    {
        Assert.True(LegacyMsgUpdatePacket.TryParse(Frame(frames[start]), out var maximum));
        Assert.Equal([new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.MaxHitpoints, Maximum)], maximum.Updates);
        Assert.True(LegacyMsgUpdatePacket.TryParse(Frame(frames[start + 1]), out var current));
        Assert.Equal([new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.MaxHitpoints, Maximum),
            new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Hitpoints, Maximum)], current.Updates);
    }

    [Fact]
    public Task RestoringHpRetainsAlreadyAboveCapManaAndAddsNoManaPacket() => Run(3, async f =>
    {
        await f.Login(); int first = f.Sent.Count; await f.Use();
        Assert.Equal(3, f.Sent.Count - first); AssertHpFrames(f.Sent, first + 1); AssertVitals(f, Maximum, 6000);
        Assert.Equal((ushort)2, Assert.Single(f.Current.PlayerItems.InventoryItems, row => row.Uid == Fixture.PotionUid).StackSize);
    }, initialMana: 6000);

    [Fact]
    public Task CanonicalBothVitalsSurviveAnotherRealHpChangeUseAndFreshOwnedReload() => Run(3, async f =>
    {
        await f.Login(); await f.Use(); AssertVitals(f, Maximum, 5000);
        var current = f.Current; f.Store.Set(f.Connection, current with { Character = current.Character with { MinHitPoints = 20 } });
        AssertVitals(f, 20, 5000); await Task.Delay(175);
        int first = f.Sent.Count; await f.Use(); Assert.Equal(3, f.Sent.Count - first); AssertVitals(f, Maximum, 5000);
        AssertHpFrames(f.Sent, first + 1); var binding = f.Native.OrdinarySpellBinding;
        await f.Reload(); Assert.True(binding!.IsRetired); AssertVitals(f, Maximum, 5000);
        Assert.NotSame(binding, f.Native.OrdinarySpellBinding); Assert.False(f.Recovery.IsBlocked);
    });
}
