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
        private readonly LegacyNativeSafeRandom priorRandom = LegacyNativePoolRandom.GetRandom;
        private readonly DateTime priorReset = LegacyNativePoolRandom.ResetRandom;
        internal NativeSpellTestFixture Native { get; }
        internal LegacyNativeGameClientRuntime Owner => Native.Owner;
        internal LegacyNativePlayerRuntime Player => Owner.Player;
        internal Queue<byte[]> Queue => Native.Queue;
        internal LegacyNativeInteractQuery Attack { get; } = new() { SpellID = 100, OpponentUID = 123 };
        internal LegacyNativeMsgSpell Spell { get; } = new() { ID = 100, Level = 2 };
        internal LegacyNativeMagicType.Magic Row { get; } = new() { ID = 100, Level = 2, Type = LegacyNativeMagicType.MagicSort.Bomb };
        internal Dictionary<ushort, LegacyNativeMagicType.Magic> Rows { get; }
        internal StringWriter Log { get; } = new(CultureInfo.InvariantCulture);

        internal Fixture()
        {
            // Own constructor RNG rather than advancing another test's shared sequence.
            LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(7531);
            Native = new();
            Console.SetOut(TextWriter.Synchronized(Log));
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

        internal void SelectIdentity(ushort id)
        {
            Native.Storage.ClientSpells.Remove(Spell.ID);
            Attack.SpellID = Spell.ID = Row.ID = id;
            Native.Storage.ClientSpells[id] = Spell;
        }

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
            LegacyNativePoolRandom.GetRandom = priorRandom;
            LegacyNativePoolRandom.ResetRandom = priorReset;
        }
    }

    private static void SetClassLevel(LegacyNativePlayerRuntime player, uint job, ushort level)
    {
        typeof(LegacyNativePlayerRuntime).GetField("_class", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(player, job);
        typeof(LegacyNativePlayerRuntime).GetField("_level", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(player, level);
    }

    private static byte[] Update(uint uid, LegacyMsgUpdateDataType type, uint value) =>
        new LegacyMsgUpdatePacket(uid, [new LegacyMsgUpdateEntry(type, value)]).ToFrameBytes();

    private sealed record Snapshot(ushort Stamina, ushort Mana, ushort RandomSpell,
        ushort Soul, DateTime ColdTime, bool HasColdTime, byte[][] Frames);

    private static Snapshot Capture(Fixture f) => new(f.Player.Stamina, f.Player.Mana,
        f.Player.RandomSpell, f.Owner.OnSoulSpell, f.Spell.ColdTime,
        f.Spell.IsSpellWithColdTime, f.Queue.ToArray());

    private static void Unchanged(Fixture f, Snapshot before)
    {
        Assert.Equal(before.Stamina, f.Player.Stamina);
        Assert.Equal(before.Mana, f.Player.Mana);
        Assert.Equal(before.RandomSpell, f.Player.RandomSpell);
        Assert.Equal(before.Soul, f.Owner.OnSoulSpell);
        Assert.Equal(before.ColdTime, f.Spell.ColdTime);
        Assert.Equal(before.HasColdTime, f.Spell.IsSpellWithColdTime);
        var after = f.Queue.ToArray();
        Assert.Equal(before.Frames.Length, after.Length);
        for (int index = 0; index < after.Length; index++)
            Assert.Same(before.Frames[index], after[index]);
    }
}
