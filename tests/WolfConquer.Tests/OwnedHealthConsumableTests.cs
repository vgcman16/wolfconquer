using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;
using Fixture = WolfConquer.Tests.WorldServerFrameHandlerTests.HealthConsumableFixture;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class OwnedHealthConsumableTests
{
    private static Task Run(ushort stack, Func<Fixture, Task> body, bool metadata = true, bool protectedPin = false) =>
        WorldServerFrameHandlerTests.WithOwnedHealthConsumableAsync(stack, body, metadata, protectedPin);
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
    private static void AssertHp(Fixture f, int hp)
    {
        Assert.Equal((long)hp, f.Current.Character.MinHitPoints); Assert.Equal(hp, f.Player.HitPoints);
        Assert.Equal(hp > 0, f.Player.Alive); Assert.Equal(hp <= 0, f.Player.NotLife);
        Assert.Equal((long)hp, CharacterSnapshotParser.ParseText(
            CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(f.CharacterPath), out _)).MinHitPoints);
    }

    [Theory]
    [InlineData((ushort)0)] [InlineData((ushort)1)] [InlineData((ushort)3)]
    public Task ActualAdmittedHealthUseCommitsBothExactImagesBeforeItsFirstD13Output(ushort stack) => Run(stack, async f =>
    {
        await f.Login(); var before = f.Current; var files = f.Files();
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Native.OrdinarySpellBinding);
        var characterAfter = LegacyBoundHitpointsScalarWrite.ProjectAfterImage(File.ReadAllBytes(f.CharacterPath),
            before.Character with { MinHitPoints = 80 });
        var itemsAfter = ExpectedItems(File.ReadAllBytes(f.ItemsPath), stack);
        int first = f.Sent.Count; bool observed = false; Exception? observationFailure = null;
        f.OnSend = sentFrame =>
        {
            if (observed) return; observed = true;
            try
            {
                Assert.Equal(first, f.Sent.Count); Assert.NotSame(before, f.Current);
                Assert.Equal(characterAfter, File.ReadAllBytes(f.CharacterPath)); Assert.Equal(itemsAfter, File.ReadAllBytes(f.ItemsPath));
                AssertHp(f, 80); Assert.NotNull(f.Current.LastLegacyPotionUseUtc);
                Assert.Same(binding, f.Native.OrdinarySpellBinding); Assert.False(binding.IsRetired);
                Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding)); Assert.False(f.Recovery.IsBlocked);
                Assert.False(LegacyTradeJournal.HasPending(f.Users));
            }
            catch (Exception error) { observationFailure = error; throw; }
        };
        await f.Use(); f.OnSend = null;
        if (observationFailure is not null) ExceptionDispatchInfo.Capture(observationFailure).Throw();
        Assert.True(observed, f.Log.ToString()); Assert.Equal(3, f.Sent.Count - first);
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
        Assert.True(LegacyMsgUpdatePacket.TryParse(Frame(f.Sent[first + 1]), out var maximum));
        Assert.Equal(LegacyMsgUpdateDataType.MaxHitpoints, Assert.Single(maximum.Updates).Type);
        Assert.True(LegacyMsgUpdatePacket.TryParse(Frame(f.Sent[first + 2]), out var current));
        Assert.Equal(new[] { LegacyMsgUpdateDataType.MaxHitpoints, LegacyMsgUpdateDataType.Hitpoints }, current.Updates.Select(row => row.Type));
        Assert.Equal(80UL, current.Updates[1].Value); Assert.Equal(maximum.Updates[0].Value, current.Updates[0].Value);
        AssertHp(f, 80);
        Assert.Equal(stack > 1 ? new[] { Fixture.PotionUid, Fixture.OtherUid } : new[] { Fixture.OtherUid },
            f.Current.PlayerItems.InventoryItems.Select(row => row.Uid));
        Assert.Equal(before.Character.Money, f.Current.Character.Money); Assert.Equal(before.Character.ConquerPoints, f.Current.Character.ConquerPoints);
        var text = CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(f.CharacterPath), out _);
        Assert.Contains(Fixture.OpaqueComment, text); Assert.Contains(Fixture.OpaqueAttribute, text);
        foreach (var pair in files.Where(pair => pair.Key != f.CharacterPath && pair.Key != f.ItemsPath)) Assert.Equal(pair.Value, File.ReadAllBytes(pair.Key));
        Assert.DoesNotContain("PARITY-BUG", f.Log.ToString(), StringComparison.Ordinal);
    });

    [Fact]
    public Task GenuineProtectedButUnverifiedPinDoesNotBecomeANewHealthPermissionGate() => Run(3, async f =>
    {
        await f.Login(); Assert.True(LegacySecondaryPasswordRuntime.TryGet(f.Connection, f.Current, out var pin, out var verified));
        Assert.NotEqual(0U, pin.Password); Assert.False(verified);
        int first = f.Sent.Count; await f.Use(); Assert.Equal(3, f.Sent.Count - first); AssertHp(f, 80);
        Assert.True(LegacySecondaryPasswordRuntime.TryGet(f.Connection, f.Current, out var retained, out verified));
        Assert.Equal(pin, retained); Assert.False(verified);
    }, protectedPin: true);
}
