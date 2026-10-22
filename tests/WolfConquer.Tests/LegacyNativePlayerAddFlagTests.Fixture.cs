using System.Reflection;
using WolfConquer.Core.World;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;

namespace WolfConquer.Tests;

// Owned component fixture only. Timer assertions observe real native DateTime.Now;
// private-level reflection declares getter prestate, not an implementation of its setter.
internal sealed class NativeAddFlagTestFixture : IDisposable
{
    private readonly LegacyNativeItemTypeRuneMetadata? priorItems = LegacyNativePoolItems.ItemsBase;
    private readonly NativeSpellTestFixture packets = new();
    internal LegacyNativeGameClientRuntime Owner => packets.Owner;
    internal LegacyNativePlayerRuntime Player => Owner.Player;
    internal Queue<byte[]> Queue => packets.Queue;
    internal LegacyNativeRuneItemStorage Runes { get; }

    internal NativeAddFlagTestFixture()
    {
        LegacyNativePoolItems.ItemsBase = new();
        Owner.Equipment = new(Owner);
        Runes = new(Owner);
        Owner.Rune = Runes;
    }

    internal static void Ready(LegacyNativeGameClientRuntime owner, byte reborn = 2, ushort level = 15)
    {
        owner.Player.Reborn = reborn;
        typeof(LegacyNativePlayerRuntime).GetField("_level", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(owner.Player, level);
    }

    internal LegacyNativeGameItemRuntime Rune(uint id = 4_031_107, ushort position = 101)
    {
        LegacyNativePoolItems.ItemsBase![id] = new() { ID = id, RuneSpellID = id, Name = "XPBooster" };
        var item = new LegacyNativeGameItemRuntime { Uid = id, ITEM_ID = id, Position = position };
        Assert.True(Runes.Add(item));
        return item;
    }

    internal LegacyNativeStatusFlagsBigVector32.Flag Row(Flag flag) => Player.BitVector.ArrayFlags[(int)flag];

    // Independent expected wire bytes from native required UID/type + unpacked uint[] schema.
    // No production packet factory/parser supplies this expected value.
    internal static byte[] Status(Flag flag, uint uid = 17)
    {
        uint[] words = new uint[18];
        words[(int)flag / 32] = 1u << ((int)flag % 32);
        var update = new List<byte> { 8, 25 };
        foreach (uint word in words) { update.Add(24); Varint(update, word); }
        var payload = new List<byte> { 8 };
        Varint(payload, uid);
        payload.Add(18);
        Varint(payload, (uint)update.Count);
        payload.AddRange(update);
        var frame = new List<byte>();
        frame.AddRange(BitConverter.GetBytes((ushort)(payload.Count + 4)));
        frame.AddRange(new byte[] { 0x80, 0x08 });
        frame.AddRange(payload);
        return frame.ToArray();
    }

    internal static byte[] Timed(Flag flag, int seconds, uint shown = 0, uint amount = 0, uint uid = 17)
    {
        var frame = new List<byte> { 32, 0, 0xA7, 0x08 };
        foreach (uint value in new[] { uid, 1u, (uint)flag, 256u, shown, unchecked((uint)seconds),
            (int)flag == 53 ? uint.MaxValue - amount : amount })
            frame.AddRange(BitConverter.GetBytes(value));
        return frame.ToArray();
    }

    private static void Varint(List<byte> bytes, uint value)
    {
        while (value >= 128) { bytes.Add((byte)((value & 127) | 128)); value >>= 7; }
        bytes.Add((byte)value);
    }

    public void Dispose()
    {
        LegacyNativePoolItems.ItemsBase = priorItems;
        packets.Dispose();
    }
}
