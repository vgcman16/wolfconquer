using System.Globalization;
using System.Runtime.InteropServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Attr = WolfConquer.Core.Data.LegacyNativeHundredWeaponsTable.AttributeType;
using Weapon = WolfConquer.Core.Data.LegacyNativeMagicType.WeaponsType;
using Info = WolfConquer.Core.World.LegacyNativeMsgHundredWeaponsInfo;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeHundredMessageTests : IDisposable
{
    private readonly LegacyNativeSafeRandom previousRandom = LegacyNativePoolRandom.GetRandom;

    public LegacyNativeHundredMessageTests()
    {
        // Scope the real shared RNG before any fixture or direct member-test Player birth.
        LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(71);
    }

    public void Dispose() => LegacyNativePoolRandom.GetRandom = previousRandom;

    private sealed class Scope : IDisposable
    {
        private readonly Dictionary<uint, LegacyNativeHundredWeaponsTable.HundredWeapon> previous = LegacyNativeHundredWeaponsTable.HundredWeaponsList;
        private readonly CultureInfo culture = CultureInfo.CurrentCulture, ui = CultureInfo.CurrentUICulture;
        internal NativeSpellTestFixture Native { get; } = new();
        internal LegacyNativeGameClientRuntime Client => Native.Owner;
        internal LegacyNativePacket Packet => Native.Packet;
        internal LegacyNativeHundredWeapons Weapons { get; }
        internal Dictionary<uint, LegacyNativeHundredWeaponsTable.HundredWeapon> Metadata { get; } = new();

        internal Scope()
        {
            LegacyNativeHundredWeaponsTable.HundredWeaponsList = Metadata;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture; CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            Weapons = new LegacyNativeHundredWeapons(Client); Client.HundredWeapons = Weapons;
            Fill(Packet);
        }

        internal LegacyNativeHundredWeaponInfo Row(Weapon subtype, byte level, params int[] attributes) => new()
        {
            WeaponSubtype = subtype, Level = level, Attributes = Attrs(attributes), BitVector = new LegacyNativeBitVector32(32),
        };

        internal LegacyNativeHundredWeaponsTable.HundredWeapon Meta(uint key, Weapon subtype, byte level, params int[] attributes)
        {
            var row = new LegacyNativeHundredWeaponsTable.HundredWeapon
                { WeaponSubtype = subtype, Level = level, Attributes = Attrs(attributes) };
            Metadata.Add(key, row); return row;
        }

        internal static Dictionary<Attr, int> Attrs(params int[] values)
        {
            Assert.Equal(5, values.Length);
            return new() { [Attr.Hitpoints] = values[0], [Attr.PhysicalAttack] = values[1],
                [Attr.PhysicalDefense] = values[2], [Attr.MagicAttack] = values[3], [Attr.MagicDefense] = values[4] };
        }

        internal static unsafe void Fill(LegacyNativePacket packet)
        {
            Marshal.Copy(Enumerable.Repeat((byte)0xA5, LegacyNativePacket.MAX_SIZE).ToArray(), 0, (IntPtr)packet.Memory, LegacyNativePacket.MAX_SIZE);
            packet.Size = 321; packet.Seek(237);
        }

        internal unsafe void Untouched()
        {
            Assert.Equal(321, Packet.Size); Assert.Equal(237, Packet.Position);
            Assert.All(NativeSpellTestFixture.Bytes(Packet), value => Assert.Equal((byte)0xA5, value));
        }

        internal T Read<T>(ushort opcode)
        {
            byte[] bytes = NativeSpellTestFixture.Bytes(Packet);
            Assert.Equal((ushort)bytes.Length, BitConverter.ToUInt16(bytes, 0));
            Assert.Equal(opcode, BitConverter.ToUInt16(bytes, 2)); Assert.Equal(4, Packet.Position);
            Packet.Seek(237);
            T decoded = Packet.ProtoBufferDeserialize(default(T)!);
            Assert.Equal(bytes.Length, Packet.Position); Assert.Equal(bytes, NativeSpellTestFixture.Bytes(Packet));
            Assert.Empty(Native.Queue); return decoded;
        }

        public void Dispose()
        {
            Native.Dispose(); LegacyNativeHundredWeaponsTable.HundredWeaponsList = previous;
            CultureInfo.CurrentCulture = culture; CultureInfo.CurrentUICulture = ui;
        }
    }

    private static object[] Values(Info.WeaponInfo row) =>
        [(ushort)row.SubType, row.AppearancePosition, row.PositionFlags, row.Level, row.Progress,
         row.Hitpoints, row.PhysicalAttack, row.PhysicalDefense, row.MagicAttack, row.MagicDefense, row.Score];
}
