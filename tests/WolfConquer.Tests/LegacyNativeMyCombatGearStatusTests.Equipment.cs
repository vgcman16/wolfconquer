using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Gear = WolfConquer.Core.World.LegacyNativeCombatGear;
using Ranks = WolfConquer.Core.Data.LegacyNativeCombatGearRankTable;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMyCombatGearStatusTests
{
    [Fact]
    public Task WholeClassPreludeUsesRankEffectsEquippedRowBlackJadesAndFivePercentUnequippedRows() =>
        NativePacketTestThread.Run(() =>
        {
            using var metadata = new NativeCombatGearQueryScope();
            using var fixture = new NativeSpellTestFixture();
            SetClass(fixture.Owner, 4001);
            Ranks.Create();
            Ranks.Ranks[Ranks.Type.WEEK_Archer].RankingList[17] = new() { UID = 17 };
            var gear = new LegacyNativeMyCombatGear(fixture.Owner);
            LegacyNativeCombatGearTable.Info[(uint)Gear.TypeID.StoneCracker] = new()
            {
                [1] = new() { MaxHP = 100, PAttack = 200, MDefense = 300, PDefense = 400 }
            };
            LegacyNativeCombatGearTable.Info[(uint)Gear.TypeID.ColdMoon] = new()
            {
                [1] = new() { MaxHP = 199, PAttack = 299, MDefense = 399, PDefense = 499 }
            };
            var equipped = new Gear.Weapon { ItemID = Gear.TypeID.StoneCracker, Level = 1, Equip = 1 };
            gear.Items[Gear.TypeID.StoneCracker] = equipped;
            gear.Items[Gear.TypeID.ColdMoon] = new() { ItemID = Gear.TypeID.ColdMoon, Level = 1, Equip = 0 };
            LegacyNativeDaoqiDictTypeTable.Info[71] = new() { attr_type1 = 1, attr_value1 = 7, attr_type2 = 5, attr_value2 = 11 };
            equipped.BlackJades[0] = new(71, 0);

            gear.Status();

            Assert.Equal(116u, fixture.Owner.Status.MaxHitpoints);
            Assert.Equal(714u, fixture.Owner.Status.MaxAttack);
            Assert.Equal(714u, fixture.Owner.Status.MinAttack);
            Assert.Equal(924u, fixture.Owner.Status.Defence);
            Assert.Equal(630u, fixture.Owner.Status.MagicDefence);
            Assert.Empty(fixture.Queue);
            equipped.Equip = 0;
            fixture.Owner.Status = new();
            gear.Status();
            Assert.Equal(0u, fixture.Owner.Status.MaxHitpoints); // No active weapon => no5% family stats either.
            Assert.Equal(500u, fixture.Owner.Status.MaxAttack); // Rank effects still execute.
            Assert.Equal(500u, fixture.Owner.Status.Defence);
            Assert.Equal(300u, fixture.Owner.Status.MagicDefence);
        });

    [Fact]
    public Task PirateRankAddsRealSpellAndCollectionRowsThenRemovesSpellWhenBothRanksDisappear() =>
        NativePacketTestThread.Run(() =>
        {
            using var metadata = new NativeCombatGearQueryScope();
            using var fixture = new NativeSpellTestFixture();
            var prior = LegacyNativeDictCollectionTable.Info;
            try
            {
                SetClass(fixture.Owner, 7001);
                Ranks.Create();
                Ranks.Ranks[Ranks.Type.Overall_Pirate].RankingList[17] = new() { UID = 17 };
                LegacyNativeDictCollectionTable.Info = new()
                {
                    [1] = new() { Type = 700, Min = 0, Mix = 0, attr_type1 = LegacyNativeDaoqiDictTypeTable.AttrType.MaxHP, attr_value1 = 123 },
                    [2] = new() { Type = 701, Min = 150, Mix = 150, attr_type1 = LegacyNativeDaoqiDictTypeTable.AttrType.PDefense, attr_value1 = 456 }
                };
                LegacyNativeCombatGearTable.Info[(uint)Gear.TypeID.Pirate1] = new() { [1] = new() { Score = 150 } };
                var gear = new LegacyNativeMyCombatGear(fixture.Owner);
                gear.Items[Gear.TypeID.Pirate1] = new() { ItemID = Gear.TypeID.Pirate1, Level = 1 };
                gear.Status();
                Assert.Equal(123u, fixture.Owner.Status.MaxHitpoints);
                Assert.Equal(456u, fixture.Owner.Status.Defence);
                var learned = Assert.Single(fixture.Storage.ClientSpells).Value;
                Assert.Equal((ushort)21930, learned.ID);
                Assert.Equal((ushort)0, learned.Level); // Overall-only rank: native weekly-dependent level remains0.
                var wire = NativeSpellTestFixture.ReadSpell(fixture.Queue.Dequeue());
                Assert.Equal((ushort)21930, wire.ID);
                Assert.Equal((ushort)0, wire.Level);
                Assert.Empty(fixture.Queue);
                Ranks.Ranks[Ranks.Type.Overall_Pirate].RankingList.Clear();
                gear.Status();
                Assert.Empty(fixture.Storage.ClientSpells);
                Assert.Single(fixture.Queue); // Actual SpellStorage.Remove/ActionCreate/send path.
                Assert.Equal(246u, fixture.Owner.Status.MaxHitpoints);
                Assert.Equal(912u, fixture.Owner.Status.Defence);
            }
            finally { LegacyNativeDictCollectionTable.Info = prior; }
        });

    [Fact]
    public Task NonPirateClassRemovesThePirateRankSpellThroughTheNativeStorageOwner() =>
        NativePacketTestThread.Run(() =>
        {
            using var metadata = new NativeCombatGearQueryScope();
            using var fixture = new NativeSpellTestFixture();
            SetClass(fixture.Owner, 2001);
            Ranks.Create();
            fixture.Storage.ClientSpells[21930] = new() { ID = 21930, Level = 4 };
            new LegacyNativeMyCombatGear(fixture.Owner).Status();
            Assert.Empty(fixture.Storage.ClientSpells);
            Assert.Single(fixture.Queue);
        });

    private static void SetClass(LegacyNativeGameClientRuntime owner, uint value) =>
        typeof(LegacyNativePlayerRuntime).GetField("_class", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(owner.Player, value);
}
