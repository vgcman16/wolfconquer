using System.Buffers.Binary;
using System.Reflection;
using WolfConquer.Core.World;
using Extra = WolfConquer.Core.World.LegacyNativeItemExtra;

namespace WolfConquer.Tests;

// Includes the unchanged actual CharacterRead fixture/Detained helper partials.
public sealed partial class LegacyNativeCharacterReadTests
{
    private static bool DetainedPurificationAvailable(Extra.Purification item) => (bool)typeof(Extra.Purification)
        .GetField("Available", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(item)!;

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task DetainedExpiredAndOverflowingTimedExtrasEmitRealEmpty2134AfterDetainedFrame(bool overflow) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var row = Detained();
        row.Purification = new Extra.Purification { ItemUID = 71, PurificationItemID = 1001, Typ = Extra.Typing.ExpireTime };
        Assert.True(row.Purification.InLife); row.Purification.PurificationDuration = overflow ? uint.MaxValue : 1;
        row.Purification.AddedOn = overflow ? DateTime.Now : DateTime.Now.AddMinutes(-1);
        row.Refinary = new Extra.Refinery { ItemUID = 82, EffectID = 1001, EffectDuration = overflow ? uint.MaxValue : 1,
            AddedOn = overflow ? DateTime.Now : DateTime.Now.AddMinutes(-1), Available = true, Typ = Extra.Typing.StabilizationEffectRefined };
        Assert.Same(s.Client.Inventory, row.Send(s.Client, s.Packet));
        Assert.Equal(new ushort[] { 2262, 2134 }, s.Queue.Select(Opcode));
        Assert.Equal(Convert.FromHexString("0800560800000000"), s.Queue.Last());
        Assert.False(DetainedPurificationAvailable(row.Purification)); Assert.False(row.Refinary.Available);
        Assert.Equal(Extra.Typing.ExpireTime, row.Purification.Typ); Assert.Equal(Extra.Typing.StabilizationEffectRefined, row.Refinary.Typ);
    });

    [Fact]
    public Task DetainedActiveTimedExtrasKeepBothRealRowsAddingTypesAndIndependentWireWords() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var row = Detained(); DateTime added = DateTime.Now.AddSeconds(-7);
        row.Purification = new Extra.Purification { ItemUID = 71, PurificationItemID = 1001, PurificationLevel = 3,
            EffectPercent2 = 17, PurificationDuration = 300, AddedOn = added };
        row.Refinary = new Extra.Refinery { ItemUID = 82, EffectID = 1001, EffectLevel = 4, EffectPercent = 11,
            EffectPercent2 = 22, EffectDuration = 600, AddedOn = added };
        DateTime earliest = DateTime.Now; row.Send(s.Client, s.Packet); DateTime latest = DateTime.Now;
        Assert.Equal(new ushort[] { 2262, 2134 }, s.Queue.Select(Opcode)); byte[] actual = s.Queue.Last();
        int ps = BinaryPrimitives.ReadInt32LittleEndian(actual.AsSpan(32)); int rs = BinaryPrimitives.ReadInt32LittleEndian(actual.AsSpan(64));
        LegacyNativeItemExtraTimerTests.AssertExpiryWindow(added, 300, earliest, latest, ps);
        LegacyNativeItemExtraTimerTests.AssertExpiryWindow(added, 600, earliest, latest, rs);
        Assert.Equal(ExtraTimerFrame(2, 71, 6, 1001, 3, 17, 300, (uint)ps, 0, 82, 2, 1001, 4, 11, 22, (uint)rs), actual);
        Assert.True(DetainedPurificationAvailable(row.Purification)); Assert.True(row.Refinary.Available);
        Assert.Equal(Extra.Typing.PurificationAdding, row.Purification.Typ); Assert.Equal(Extra.Typing.RefinaryAdding, row.Refinary.Typ);
    });

    [Fact]
    public Task DetainedZeroExtraUidsSkipInvalidTimedExpressionsAfterSendingOnlyDetainedFrame() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var row = Detained();
        row.Purification.PurificationDuration = 1; row.Purification.AddedOn = DateTime.MaxValue;
        row.Refinary.EffectDuration = 1; row.Refinary.AddedOn = DateTime.MaxValue; row.Refinary.Available = true;
        row.Send(s.Client, s.Packet);
        Assert.Equal((ushort)2262, Opcode(Assert.Single(s.Queue))); Assert.True(row.Refinary.Available);
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task DetainedTimedDateFaultKeepsNativeFirstPacketAndEarlierPurificationMutationOnly(bool refineryFault) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var row = Detained();
        row.Purification = new Extra.Purification { ItemUID = 71, PurificationItemID = 1001,
            PurificationDuration = refineryFault ? 0u : 1u, AddedOn = DateTime.MaxValue };
        row.Refinary = new Extra.Refinery { ItemUID = 82, EffectID = 1001, EffectDuration = 1,
            AddedOn = DateTime.MaxValue, Available = true };
        Assert.Throws<ArgumentOutOfRangeException>(() => row.Send(s.Client, s.Packet));
        Assert.Equal((ushort)2262, Opcode(Assert.Single(s.Queue)));
        Assert.Equal(refineryFault, DetainedPurificationAvailable(row.Purification)); Assert.True(row.Refinary.Available);
        Assert.Equal(refineryFault ? Extra.Typing.PurificationAdding : (Extra.Typing)0, row.Purification.Typ);
        Assert.Equal((Extra.Typing)0, row.Refinary.Typ);
    });

    private static byte[] ExtraTimerFrame(params uint[] words)
    {
        byte[] frame = new byte[4 + words.Length * 4]; BinaryPrimitives.WriteUInt16LittleEndian(frame, (ushort)frame.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2), 2134);
        for (int i = 0; i < words.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(4 + 4 * i), words[i]);
        return frame;
    }
}
