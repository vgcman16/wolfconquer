using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeQuestPacketTests
{
    [Fact]
    public Task FirstDataWriterIgnoresUnknowAndGetterReadsCombinedCountThenOnlyOneIntent() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); uint[] intents = { 7, 9, uint.MaxValue };
        Assert.Same(scope.Packet, scope.Packet.MsgQuestDataCreate(uint.MaxValue, 17, intents));
        Assert.Equal(Frame(2136, 0x00030000, 17, 7, 9, uint.MaxValue, 0), NativeSpellTestFixture.Bytes(scope.Packet));
        Assert.Equal(4, scope.Packet.Position);
        scope.Packet.GetQuestData(out uint unknown, out uint uid, out uint[] read);
        Assert.Equal(0x00030000u, unknown); Assert.Equal(17u, uid); Assert.Equal(new uint[] { 7 }, read);
        Assert.Equal(16, scope.Packet.Position); Assert.Equal(new uint[] { 7, 9, uint.MaxValue }, intents);
        read[0] = 99; Assert.Equal(7u, intents[0]);
    });

    [Fact]
    public Task FirstWriterSpecial35024DuplicatesAllIntentsBeforeTrailingZero() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); uint[] intents = { 7, 9 };
        scope.Packet.MsgQuestDataCreate(123, 35024, intents);
        Assert.Equal(Frame(2136, 0x00020000, 35024, 7, 9, 7, 9, 0), NativeSpellTestFixture.Bytes(scope.Packet));
        Assert.Equal(4, scope.Packet.Position);
    });

    [Fact]
    public Task FirstWriterSpecial35007InsertsThreeZeroWordsThenRepeatsAllIntents() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); uint[] intents = { 7, 9 };
        scope.Packet.MsgQuestDataCreate(123, 35007, intents);
        Assert.Equal(Frame(2136, 0x00020000, 35007, 7, 9, 0, 0, 0, 0, 7, 9), NativeSpellTestFixture.Bytes(scope.Packet));
        Assert.Equal(4, scope.Packet.Position);
    });

    [Theory]
    [InlineData(17u)]
    [InlineData(35024u)]
    [InlineData(35007u)]
    public Task OwnerOverloadIgnoresBothInputsAndHasNoSpecialMissionOrTrailingZeroTail(uint uid) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); uint[] intents = { 7, uint.MaxValue };
        scope.Packet.MsgQuestDataCreate(123, uid, uint.MaxValue, intents);
        byte[] expected = Frame(2136, 0x00020000, uid, 7, uint.MaxValue);
        Assert.Equal(expected, NativeSpellTestFixture.Bytes(scope.Packet));
        scope.Packet.MsgQuestDataCreate(999, uid, 5, intents);
        Assert.Equal(expected, NativeSpellTestFixture.Bytes(scope.Packet)); Assert.Equal(4, scope.Packet.Position);
    });

    [Fact]
    public Task EmptyArraysKeepDistinctOverloadTailsAndReaderRefusesAMissingIntentWord() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Packet.MsgQuestDataCreate(55, 17, Array.Empty<uint>());
        Assert.Equal(Convert.FromHexString("10005808000000001100000000000000"), NativeSpellTestFixture.Bytes(scope.Packet));
        scope.Packet.GetQuestData(out _, out _, out var first); Assert.Equal(new uint[] { 0 }, first);
        scope.Fill(); scope.Packet.MsgQuestDataCreate(55, 17, 9, Array.Empty<uint>());
        Assert.Equal(Convert.FromHexString("0C0058080000000011000000"), NativeSpellTestFixture.Bytes(scope.Packet));
        uint unknown = 91, uid = 92; uint[] preserved = [93]; uint[] second = preserved;
        byte[] before = scope.Raw(0, scope.Packet.Capacity);
        Assert.Throws<EndOfStreamException>(() => scope.Packet.GetQuestData(out unknown, out uid, out second));
        Assert.Equal(91u, unknown); Assert.Equal(92u, uid); Assert.Same(preserved, second);
        Assert.Equal(new uint[] { 93 }, second); Assert.Equal(4, scope.Packet.Position); Assert.Equal(12, scope.Packet.Size);
        Assert.Equal(before, scope.Raw(0, scope.Packet.Capacity));
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task NullIntentsRefuseBeforeChangingAnyTargetBytesOrMetadata(bool owner) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        byte[] before = scope.Raw(0, scope.Packet.Capacity);
        if (owner) Assert.Throws<ArgumentNullException>(() => scope.Packet.MsgQuestDataCreate(1, 2, 3, null!));
        else Assert.Throws<ArgumentNullException>(() => scope.Packet.MsgQuestDataCreate(1, 2, (uint[])null!));
        Assert.Equal(237, scope.Packet.Position); Assert.Equal(321, scope.Packet.Size);
        Assert.Equal(before, scope.Raw(0, scope.Packet.Capacity));
    });

    [Fact]
    public Task DataGetterUsesCapturedInputAndPreservesAllOutputsWhenTheBodyIsIncomplete() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        byte[] input = Frame(9999, 1, 2, 3, 4); scope.Input(input); scope.Packet.Seek(8);
        scope.Packet.GetQuestData(out var unknown, out var uid, out var intents);
        Assert.Equal(2u, unknown); Assert.Equal(3u, uid); Assert.Equal(new uint[] { 4 }, intents);
        Assert.Equal(20, scope.Packet.Position); Assert.Equal(input.Length, scope.Packet.Size);
        uint[] beforeIntents = intents; byte[] before = scope.Raw(0, scope.Packet.Capacity);
        scope.Packet.Seek(16383);
        Assert.Throws<EndOfStreamException>(() => scope.Packet.GetQuestData(out unknown, out uid, out intents));
        Assert.Equal(2u, unknown); Assert.Equal(3u, uid); Assert.Same(beforeIntents, intents);
        Assert.Equal(new uint[] { 4 }, intents); Assert.Equal(16383, scope.Packet.Position); Assert.Equal(input.Length, scope.Packet.Size);
        Assert.Equal(before, scope.Raw(0, scope.Packet.Capacity));
    });

    [Fact]
    public Task DataWriterUsesGenuineSealAndKeepsDeclaredLengthExcludingSeal() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.UseSeal("TQServer");
        scope.Packet.MsgQuestDataCreate(0, 17, new uint[] { 7 });
        var expected = Convert.FromHexString("14005808000001001100000007000000000000005451536572766572");
        Assert.Equal(expected, NativeSpellTestFixture.Bytes(scope.Packet)); Assert.Equal(28, scope.Packet.Size); Assert.Equal(4, scope.Packet.Position);
    });
}
