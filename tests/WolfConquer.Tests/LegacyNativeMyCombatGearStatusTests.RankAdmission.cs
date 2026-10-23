using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Ranks = WolfConquer.Core.Data.LegacyNativeCombatGearRankTable;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMyCombatGearStatusTests
{
    [Fact]
    public Task MissingWeeklyPirateRankCannotAuthorizeRemovingTheExistingRankSpell() =>
        NativePacketTestThread.Run(() =>
        {
            using var metadata = new NativeCombatGearQueryScope();
            using var fixture = new NativeSpellTestFixture();
            using var collection = new RankCollectionLease();
            SetClass(fixture.Owner, 7001);
            Ranks.Create();
            Ranks.Ranks.Remove(Ranks.Type.WEEK_Pirate);
            var spell = new LegacyNativeMsgSpell { ID = 21930, Level = 4 };
            fixture.Storage.ClientSpells[spell.ID] = spell;
            var gear = new LegacyNativeMyCombatGear(fixture.Owner);
            fixture.Owner.Status.MaxAttack = 123;
            fixture.Owner.Status.Defence = 456;
            var status = fixture.Owner.Status;
            var before = RankStatusFields(status);

            Assert.Throws<InvalidDataException>(gear.Status);

            Assert.Same(status, fixture.Owner.Status);
            Assert.Equal(before, RankStatusFields(status));
            Assert.Same(spell, Assert.Single(fixture.Storage.ClientSpells).Value);
            Assert.Equal((ushort)4, spell.Level);
            Assert.Empty(fixture.Queue);
            Assert.Empty(gear.Items);
            Assert.False(Ranks.Ranks.ContainsKey(Ranks.Type.WEEK_Pirate));
        });

    [Theory]
    [InlineData(2001U, 22U)]
    [InlineData(4001U, 30U)]
    [InlineData(13001U, 38U)]
    [InlineData(14001U, 38U)]
    [InlineData(7001U, 40U)]
    public Task LateUnavailableRankCannotApplyAnEarlierBonusOrPublishASpell(uint classId, uint lastType) =>
        NativePacketTestThread.Run(() =>
        {
            using var metadata = new NativeCombatGearQueryScope();
            using var fixture = new NativeSpellTestFixture();
            using var collection = new RankCollectionLease();
            SetClass(fixture.Owner, classId);
            Ranks.Create();
            foreach (var rank in Ranks.Ranks.Values)
                rank.RankingList[17] = new() { UID = 17, TotalPoints = 100 };
            var last = (Ranks.Type)lastType;
            Ranks.Ranks[last].RankingList = null!;
            var spell = new LegacyNativeMsgSpell { ID = 21930, Level = 4 };
            fixture.Storage.ClientSpells[spell.ID] = spell;
            var gear = new LegacyNativeMyCombatGear(fixture.Owner);
            fixture.Owner.Status.MaxAttack = 77;
            fixture.Owner.Status.Defence = 88;
            var before = RankStatusFields(fixture.Owner.Status);

            Assert.Throws<InvalidDataException>(gear.Status);

            Assert.Equal(before, RankStatusFields(fixture.Owner.Status));
            Assert.Same(spell, Assert.Single(fixture.Storage.ClientSpells).Value);
            Assert.Equal((ushort)4, spell.Level);
            Assert.Empty(fixture.Queue);
            Assert.Null(Ranks.Ranks[last].RankingList);
        });

    [Fact]
    public Task ValidEmptyPirateRanksStillRemoveSpellThroughTheRealWirePath() =>
        NativePacketTestThread.Run(() =>
        {
            using var metadata = new NativeCombatGearQueryScope();
            using var fixture = new NativeSpellTestFixture();
            var prior = LegacyNativeDictCollectionTable.Info;
            try
            {
                LegacyNativeDictCollectionTable.Info = new();
                SetClass(fixture.Owner, 7001);
                Ranks.Create();
                fixture.Storage.ClientSpells[21930] = new() { ID = 21930, Level = 4 };
                new LegacyNativeMyCombatGear(fixture.Owner).Status();
                Assert.Empty(fixture.Storage.ClientSpells);
                var wire = Assert.Single(fixture.Queue);
                Assert.Equal((ushort)2046, BitConverter.ToUInt16(wire, 2));
            }
            finally { LegacyNativeDictCollectionTable.Info = prior; }
        });

    [Fact]
    public Task ValidWeeklyPirateThirdPlaceRetainsTheRealLevelEightSpellFrame() =>
        NativePacketTestThread.Run(() =>
        {
            using var metadata = new NativeCombatGearQueryScope();
            using var fixture = new NativeSpellTestFixture();
            var prior = LegacyNativeDictCollectionTable.Info;
            try
            {
                LegacyNativeDictCollectionTable.Info = new();
                SetClass(fixture.Owner, 7001);
                Ranks.Create();
                var list = Ranks.Ranks[Ranks.Type.WEEK_Pirate].RankingList;
                list[8001] = new() { UID = 101 };
                list[8002] = new() { UID = 102 };
                list[8003] = new() { UID = 17, TotalPoints = uint.MaxValue };
                new LegacyNativeMyCombatGear(fixture.Owner).Status();
                var learned = Assert.Single(fixture.Storage.ClientSpells).Value;
                Assert.Equal((ushort)21930, learned.ID);
                Assert.Equal((ushort)8, learned.Level);
                var wire = NativeSpellTestFixture.ReadSpell(Assert.Single(fixture.Queue));
                Assert.Equal((ushort)21930, wire.ID);
                Assert.Equal((ushort)8, wire.Level);
                Assert.Equal(new uint[] { 8001, 8002, 8003 }, list.Keys);
            }
            finally { LegacyNativeDictCollectionTable.Info = prior; }
        });

    private static object?[] RankStatusFields(LegacyNativeMsgStatus status) =>
        typeof(LegacyNativeMsgStatus).GetFields(BindingFlags.Public | BindingFlags.Instance)
            .OrderBy(field => field.Name, StringComparer.Ordinal).Select(field => field.GetValue(status)).ToArray();

    private sealed class RankCollectionLease : IDisposable
    {
        private readonly Dictionary<uint, LegacyNativeDictCollectionTable.CQ> prior = LegacyNativeDictCollectionTable.Info;
        internal RankCollectionLease() => LegacyNativeDictCollectionTable.Info = new();
        public void Dispose() => LegacyNativeDictCollectionTable.Info = prior;
    }
}
