using System.Globalization;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeCanUseSpellTests
{
    private sealed class Fixture : IDisposable
    {
        private readonly Dictionary<ushort, List<ushort>> priorWeapons = LegacyNativePoolMagic.WeaponSpells;
        private readonly LegacyNativeItemTypeRuneMetadata? priorItems = LegacyNativePoolItems.ItemsBase;
        private readonly Dictionary<uint, LegacyNativeNinjaFile.NinjaSprintItem>? priorNinja = LegacyNativeNinjaFile.gouyu_type;
        private readonly TextWriter priorOutput = Console.Out;
        internal NativeSpellTestFixture Native { get; } = new();
        internal LegacyNativeGameClientRuntime Owner => Native.Owner;
        internal LegacyNativePlayerRuntime Player => Owner.Player;
        internal Queue<byte[]> Queue => Native.Queue;
        internal LegacyNativeInteractQuery Attack { get; } = new() { SpellID = 100, OpponentUID = 123 };
        internal LegacyNativeMsgSpell Spell { get; } = new() { ID = 100, Level = 2 };
        internal LegacyNativeMagicType.Magic Row { get; } = new() { ID = 100, Type = LegacyNativeMagicType.MagicSort.Bomb };
        internal Dictionary<ushort, LegacyNativeMagicType.Magic> Rows { get; }
        internal StringWriter Log { get; } = new(CultureInfo.InvariantCulture);

        internal Fixture()
        {
            Console.SetOut(Log);
            LegacyNativePoolMagic.WeaponSpells = new();
            LegacyNativePoolItems.ItemsBase = new();
            LegacyNativeNinjaFile.gouyu_type = new();
            Owner.Equipment = new(Owner);
            Owner.Rune = new(Owner);
            Owner.MyNinja = new(Owner);
            Native.Storage.ClientSpells[100] = Spell;
            Rows = new() { [2] = Row };
            Player.Map = 1002;
            Player.Stamina = 100;
            Player.Mana = 100;
        }

        internal bool Run(out LegacyNativeMsgSpell spell, out LegacyNativeMagicType.Magic row) =>
            LegacyNativeCanUseSpell.Verified(Attack, Owner, Rows, out spell, out row);

        internal LegacyNativeGameItemRuntime MpRune(byte level)
        {
            SetClassLevel(Player, 1000, 15);
            Player.Reborn = 2;
            uint id = 4_030_000u + level;
            LegacyNativePoolItems.ItemsBase![id] = new() { ID = id, Name = "MPMaster", RuneSpellID = id };
            var item = new LegacyNativeGameItemRuntime { Uid = id, ITEM_ID = id, Position = 101 };
            Assert.True(Owner.Rune!.Add(item));
            return item;
        }

        public void Dispose()
        {
            Console.SetOut(priorOutput);
            Log.Dispose();
            LegacyNativePoolMagic.WeaponSpells = priorWeapons;
            LegacyNativePoolItems.ItemsBase = priorItems;
            LegacyNativeNinjaFile.gouyu_type = priorNinja!;
            Native.Dispose();
        }
    }

    private static void SetClassLevel(LegacyNativePlayerRuntime player, uint job, ushort level)
    {
        typeof(LegacyNativePlayerRuntime).GetField("_class", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(player, job);
        typeof(LegacyNativePlayerRuntime).GetField("_level", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(player, level);
    }

    private static byte[] Update(uint uid, LegacyMsgUpdateDataType type, uint value) =>
        new LegacyMsgUpdatePacket(uid, [new LegacyMsgUpdateEntry(type, value)]).ToFrameBytes();

    // Test-only map object: raw spell range must not use visibility/spawn/send behavior.
    private sealed class RangeTarget : ILegacyNativeMapObj
    {
        public uint UID => 123;
        public ushort X => 36;
        public ushort Y => 35;
        public LegacyNativeMapObjectType ObjType => LegacyNativeMapObjectType.Monster;
        public uint IndexInScreen { get; set; }
        public bool AllowDynamic { get => throw new InvalidOperationException(); set => throw new InvalidOperationException(); }
        public uint Map => throw new InvalidOperationException();
        public uint DynamicID => throw new InvalidOperationException();
        public bool Alive => throw new InvalidOperationException();
        public bool IsTrap() => throw new InvalidOperationException();
        public LegacyNativePacket GetArray(LegacyNativePacket stream, bool View, bool st) => throw new InvalidOperationException();
        public void Send(LegacyNativePacket msg) => throw new InvalidOperationException();
    }
}
