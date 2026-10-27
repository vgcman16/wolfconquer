using System.Collections.Concurrent;
using System.Reflection;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMyCombatGearStatusTests
{
    [Theory]
    [InlineData("tail")]
    [InlineData("map")]
    [InlineData("storage")]
    [InlineData("foreign")]
    public Task RankSpellPreflightRefusalCannotApplyTheEarlierRankBonuses(string failure) =>
        NativePacketTestThread.Run(() =>
        {
            using var test = new SelectedStatusCase();
            var fixture = test.Fixture;
            var foreignQueue = new Queue<byte[]>();
            var foreign = NativeSpellTestFixture.CreateOwner(foreignQueue);
            foreign.Player.UID = 99;
            var foreignSpells = new LegacyNativeSpellStorage(foreign);
            foreign.MySpells = foreignSpells; // A genuine self-bound foreign owner.
            foreignSpells.ClientSpells[21930] = new() { ID = 21930, Level = 4 };
            switch (failure)
            {
                case "tail": fixture.Storage.ClientSpells[44] = new() { ID = 12000, Level = 1 }; break;
                case "map": fixture.Storage.ClientSpells = null!; break;
                case "storage": fixture.Owner.MySpells = null; break;
                case "foreign": fixture.Owner.MySpells = foreignSpells; break;
                default: throw new InvalidOperationException("Unknown owned refusal case.");
            }
            var targets = new RankSpellTargetImage(fixture.Owner);
            var ownMap = new RankSpellMapImage(fixture.Storage);
            var foreignMap = new RankSpellMapImage(foreignSpells);
            var currentStorage = fixture.Owner.MySpells;
            var priorFrames = fixture.Queue.ToArray();
            var packet = NativeSpellTestFixture.Bytes(fixture.Packet);

            var error = Record.Exception(test.Gear.Status);

            targets.RequireUnchanged(fixture.Owner);
            ownMap.RequireUnchanged(fixture.Storage);
            foreignMap.RequireUnchanged(foreignSpells);
            Assert.Same(currentStorage, fixture.Owner.MySpells);
            Assert.Equal(priorFrames, fixture.Queue.ToArray());
            Assert.Empty(foreignQueue);
            Assert.Equal(packet, NativeSpellTestFixture.Bytes(fixture.Packet));
            Assert.IsType<InvalidOperationException>(error);
        });

    [Fact]
    public Task ValidRankSpellRemovalKeepsItsLiteralFrameAndExactRankArithmetic() =>
        NativePacketTestThread.Run(() =>
        {
            using var test = new SelectedStatusCase();
            var fixture = test.Fixture;
            var priorFrame = Assert.Single(fixture.Queue);

            test.Gear.Status();

            Assert.Equal(702U, fixture.Owner.Status.MinAttack);
            Assert.Equal(601U, fixture.Owner.Status.MaxAttack);
            Assert.Equal(803U, fixture.Owner.Status.Defence);
            Assert.Equal(704U, fixture.Owner.Status.MagicDefence);
            Assert.Equal(505U, fixture.Owner.Status.MaxHitpoints);
            Assert.Equal(17D, fixture.Owner.PerfectionStatus.LuckyStrike);
            Assert.Equal(18D, fixture.Owner.PerfectionStatus.StrikeLock);
            Assert.Equal(19D, fixture.Owner.PerfectionStatus.CoreStrike);
            Assert.False(fixture.Storage.ClientSpells.ContainsKey(21930));
            Assert.Equal(2, fixture.Queue.Count);
            Assert.Same(priorFrame, fixture.Queue.First());
            // Required Action fields: UID17, spell21930, timestamp0, action109,
            // facing/X/Y zero. Independent literal; no writer-generated expected bytes.
            Assert.Equal(Convert.FromHexString("1400FE07081118AAAB014800606D680070007800"),
                fixture.Queue.Last());
        });

    private sealed class RankSpellTargetImage
    {
        private readonly object status, perfection;
        private readonly object?[] statusFields, perfectionFields;
        internal RankSpellTargetImage(LegacyNativeGameClientRuntime client)
        {
            status = client.Status;
            perfection = client.PerfectionStatus;
            statusFields = PublicFields(status);
            perfectionFields = PublicFields(perfection);
        }
        internal void RequireUnchanged(LegacyNativeGameClientRuntime client)
        {
            Assert.Same(status, client.Status);
            Assert.Same(perfection, client.PerfectionStatus);
            Assert.Equal(statusFields, PublicFields(status));
            Assert.Equal(perfectionFields, PublicFields(perfection));
        }
    }

    private sealed class RankSpellMapImage
    {
        private readonly ConcurrentDictionary<ushort, LegacyNativeMsgSpell>? map;
        private readonly (ushort Key, LegacyNativeMsgSpell Row, object?[] Values)[] rows;
        internal RankSpellMapImage(LegacyNativeSpellStorage storage)
        {
            map = storage.ClientSpells;
            rows = map is null ? [] : map.Select(pair =>
                (pair.Key, pair.Value, PublicFields(pair.Value))).ToArray();
        }
        internal void RequireUnchanged(LegacyNativeSpellStorage storage)
        {
            Assert.Same(map, storage.ClientSpells);
            if (map is null) return;
            Assert.Equal(rows.Length, map.Count);
            foreach (var row in rows)
            {
                Assert.Same(row.Row, map[row.Key]);
                Assert.Equal(row.Values, PublicFields(row.Row));
            }
        }
    }

    private static object?[] PublicFields(object value) => value.GetType()
        .GetFields(BindingFlags.Public | BindingFlags.Instance)
        .OrderBy(field => field.Name, StringComparer.Ordinal)
        .Select(field => field.GetValue(value)).ToArray();
}
