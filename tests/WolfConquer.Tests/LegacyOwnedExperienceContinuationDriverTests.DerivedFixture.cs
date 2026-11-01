using System.Buffers.Binary;
using System.Globalization;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedExperienceContinuationDriverTests
{
    private const uint ContinuationArmorUid = 5_337_710, ContinuationArmorType = 130_009;
    private const ushort ContinuationVenom = 20_000;

    private sealed class DerivedContinuationClock : ILegacySignedMillisecondClock
    {
        internal int Now = 10_000;
        public int GetMilliseconds() => Now;
    }

    private static Task RunDerivedContinuation(
        Func<OwnedEquipmentBehaviorFixture, LegacyMonsterInstance, WorldDataCatalog, Task> body)
    {
        var family = new Monster(911, "OwnedDerivedContinuation", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = "911", ["life"] = "2500000", ["Boss"] = "0", ["lookface"] = "911", ["level"] = "137",
            ["attack_range"] = "1", ["view_range"] = "30", ["attack_speed"] = "1000", ["move_speed"] = "500",
            ["attack_min"] = "5", ["attack_max"] = "6", ["attack_user"] = "3", ["settings"] = "53",
        }, []);
        // Same retained row/descriptor used by the genuine journal22/23 fixtures.
        const string venom = "20006@@20000@@16@@Venom@@1@@0@@1@@8@@5@@0@@30150@@0@@550@@2@@16@@16@@355@@0@@0@@0@@1@@0@@0@@3@@16@@0@@0@@0@@0@@0@@1@@0@@0@@800@@0@@3000@@1@@20@@0@@0@@0@@0@@0@@0@@30100@@0@@0@@800@@1000@@0@@0@@0@@0@@";
        const string effect = "[2000000]\r\nName=Venom\r\nClientRepresent=4\r\nSortOfAct=5\r\nDesc=Venom\r\nDescEx=Venom\r\nHide=1\r\n";
        var catalog = LegacyItemWorldDataFixture.Create(new[] { OwnedEquipmentBehaviorFixture.Item(ContinuationArmorType) }, [],
            monsters: new[] { family }, magic: new[] { MagicTypeParser.ParseLine(venom) }, magicEffects: MagicEffectParser.Parse(effect).ToArray(),
            levelExperience: new[]
            {
                new LevelExperience(0, 137, 1, 0, 0, 1, "0 137 1 0 0", new[] { "0", "137", "1", "0", "0" }),
                new LevelExperience(0, 138, 1_000_000_000, 0, 0, 2, "0 138 1000000000 0 0",
                    new[] { "0", "138", "1000000000", "0", "0" }),
            });
        var flags = new ulong[16]; flags[212 / 32] = 1UL << (212 % 32);
        var actor = OwnedEquipmentBehaviorFixture.Character(classCode: 1000, level: 137, reborn: 1,
            strength: 100, mapId: 1011, x: 430, y: 380, minHitPoints: 5000) with
        {
            MinMana = 0, MaxMana = 0,
            Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["DynamicID"] = "77", ["Action"] = "0", ["Stamina"] = "100", ["UserExpRate"] = "1",
                ["StatusFlags"] = string.Join('/', flags), ["KeepDerivedBytes"] = "retained-query-input",
            },
        };
        var items = new PlayerItemSnapshot(new PlayerItemSnapshotItem[]
        {
            new() { Uid = ContinuationArmorUid, ItemId = ContinuationArmorType, Position = 0,
                Durability = 100, MaximumDurability = 100, StackSize = 1, MythSoul = 15 },
        }, [], [], [], []);
        var clock = new DerivedContinuationClock();
        var options = LegacyRuntimeUpdateLoopOptions.Disabled with { LegacyClock = clock };
        return OwnedEquipmentBehaviorFixture.RunWithOptionsAsync(new(actor, items), catalog, options, async f =>
        {
            Assert.Equal(10_000, LegacyFineRainEquipmentAdmission.ForOwner(f.Current()).StampMilliseconds);
            Assert.True(LegacyPlayerDeathState.HasStatusFlag(f.Current().Character.Attributes, 212));
            Assert.False(f.Current().PlayerSpells.ContainsSpell(ContinuationVenom));
            f.ActorFrames.Clear();
            await f.DispatchAsync(new MsgItemUsagePacket(ContinuationArmorUid, 3, MsgItemUsageAction.Equip, 1234, 0, 0, 0));
            Assert.Contains(f.ActorFrames, bytes => bytes.AsSpan().SequenceEqual(
                LegacyMsgMessagePacket.SystemTalk("You Shoud wait 6 seconds.").ToFrameBytes()));
            var equipped = Assert.Single(f.Current().PlayerItems.EquipmentItems);
            Assert.Equal(ContinuationArmorUid, equipped.Uid); Assert.Equal((ushort)3, equipped.Position);
            Assert.False(f.Current().PlayerSpells.ContainsSpell(ContinuationVenom));
            Assert.False(f.Current().NativeGameClient!.MySpells!.ClientSpells.ContainsKey(ContinuationVenom));
            // Class1000 receives Cyclone1110 from the actual ordinary admission
            // lineage grant. That unrelated row is part of the durable baseline.
            var admissionSpells = new byte[16];
            BinaryPrimitives.WriteInt32LittleEndian(admissionSpells, 1);
            BinaryPrimitives.WriteUInt16LittleEndian(admissionSpells.AsSpan(4), 1110);
            Assert.Equal(admissionSpells, File.ReadAllBytes(DerivedSpellsPath(f)));
            Assert.Equal(new PlayerSpellSnapshotEntry(1110, 0, 0, 0, 0, 0), Assert.Single(f.Current().PlayerSpells.Spells));
            Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
            clock.Now = 16_001; // Actual guarded query becomes eligible; no query receipt is supplied.
            Assert.True(LegacyEquipmentRebuildAdmission.ApplyCurrent(f.Current(), LegacyEquipmentRebuildReason.LevelUp, clock).Admitted);
            var states = Assert.IsType<MonsterRuntimeStateStore>(typeof(WorldServerFrameHandler)
                .GetField("_monsterStates", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Handler));
            var spawn = new MonsterSpawn(911, 1011, family.Id, 431, 380, 1, 1, 1, 10, 1, 0, 0, 0, 0,
                1011, family.Name, "fixture://owned-derived-continuation/911.ini", new Dictionary<string, string> { ["DynamicID"] = "77" });
            var monster = Assert.Single(LegacyMonsterInstance.FromGenerator(spawn, family, states.NextLegacyDynamicMonsterUid()));
            Assert.Same(monster, states.RegisterDynamicMonster(monster, family));
            Assert.True(f.Store.TryReadMonsterPhysicalRange(monster, family, out _));
            f.ActorFrames.Clear();
            await body(f, monster, catalog);
        });
    }

    private static string DerivedSpellsPath(OwnedEquipmentBehaviorFixture f) => Path.Combine(f.Data.Spells,
        OwnedEquipmentBehaviorFixture.Actor.ToString(CultureInfo.InvariantCulture) + ".bin");

    private static byte[] DerivedGrantedImage(byte[] before)
    {
        int count = BinaryPrimitives.ReadInt32LittleEndian(before);
        Assert.Equal(checked(4 + count * 12), before.Length);
        for (int i = 0; i < count; i++)
            Assert.NotEqual(ContinuationVenom, BinaryPrimitives.ReadUInt16LittleEndian(before.AsSpan(4 + i * 12)));
        // Independent twelve-byte append: retain every original record byte,
        // including opaque byte11, and change only the count and new record.
        var bytes = new byte[checked(before.Length + 12)]; before.CopyTo(bytes, 0);
        BinaryPrimitives.WriteInt32LittleEndian(bytes, checked(count + 1));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(before.Length), ContinuationVenom);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(before.Length + 2), 5); return bytes;
    }

    private sealed record DerivedSpellBaseline(byte[] Bytes, PlayerSpellSnapshotEntry[] Rows,
        IReadOnlyDictionary<ushort, DerivedNativeSpellValues> Native);

    private sealed record DerivedNativeSpellValues(DateTime ColdTime, bool WithColdTime,
        ushort Size, ushort PacketType, int PacketStamp, int Experience, ushort Id, ushort Unknown,
        ushort Level, ushort Type, LegacyNativeMsgSpell.UseSpellSoulTyp UseSoul, byte Soul,
        int HashLevelSoul, DateTime Stamp, byte PreviousLevel, byte SoulLevel);

    private static DerivedNativeSpellValues CaptureDerivedNativeSpell(LegacyNativeMsgSpell row) =>
        new(row.ColdTime, row.IsSpellWithColdTime, row.Size, row.PacketType, row.PacketStamp,
            row.Experience, row.ID, row.UnKnow, row.Level, row.Type, row.UseSoul, row.Soul,
            row.HashLevelSoul, row.StampSpell, row.PreviousLevel, row.SoulLevel);

    private static DerivedSpellBaseline CaptureDerivedSpellBaseline(OwnedEquipmentBehaviorFixture f)
    {
        var before = f.Current();
        var bytes = File.ReadAllBytes(DerivedSpellsPath(f));
        var rows = before.PlayerSpells.Spells.ToArray();
        Assert.Equal(rows, PlayerSpellSnapshotLoader.Parse(bytes).Spells);
        Assert.DoesNotContain(rows, row => row.Id == ContinuationVenom);
        var native = before.NativeGameClient!.MySpells!.ClientSpells;
        Assert.Equal(rows.Length, native.Count);
        foreach (var row in rows) AssertDerivedNativeRow(row, native[row.Id]);
        return new(bytes, rows, native.ToDictionary(pair => pair.Key, pair => CaptureDerivedNativeSpell(pair.Value)));
    }

    private static void AssertDerivedNativeRow(PlayerSpellSnapshotEntry expected, LegacyNativeMsgSpell actual)
    {
        Assert.Equal(expected.Id, actual.ID); Assert.Equal(expected.Level, actual.Level);
        Assert.Equal(expected.Experience, actual.Experience); Assert.Equal(expected.PreviousLevel, actual.PreviousLevel);
        Assert.Equal(expected.SoulLevel, actual.SoulLevel); Assert.Equal(expected.UseSpellSoul, actual.UseSpellSoul);
    }

    private static void AssertDerivedSpellState(DerivedSpellBaseline before, LegacyOrdinarySpellBinding binding,
        PlayerSpellSnapshot? managed, bool granted)
    {
        var grant = new PlayerSpellSnapshotEntry(ContinuationVenom, 5, 0, 0, 0, 0);
        var expectedRows = granted ? before.Rows.Append(grant).ToArray() : before.Rows;
        if (managed is not null) Assert.Equal(expectedRows, managed.Spells);
        Assert.Equal(expectedRows.Length, binding.Storage.ClientSpells.Count);
        foreach (var row in expectedRows) AssertDerivedNativeRow(row, binding.Storage.ClientSpells[row.Id]);
        foreach (var row in before.Native)
            Assert.Equal(row.Value, CaptureDerivedNativeSpell(binding.Storage.ClientSpells[row.Key]));
    }

    private static byte[] DerivedGrantedFrame()
    {
        var bytes = new byte[25]; BinaryPrimitives.WriteUInt16LittleEndian(bytes, 25);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 2101);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), ContinuationVenom);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 5); return bytes;
    }

    private static byte[] AppendDerivedOrigin(byte[] before)
    {
        string text = CharacterSnapshotFileEncoding.Decode(before, out var encoding);
        Assert.EndsWith("\r\n", text); Assert.DoesNotContain("ManagedDerivedSpellOrigins=", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\r\n[", text, StringComparison.Ordinal);
        return encoding.GetPreamble().Concat(encoding.GetBytes(text + "ManagedDerivedSpellOrigins=20000:5\r\n")).ToArray();
    }
}
