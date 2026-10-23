using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Ranks = WolfConquer.Core.Data.LegacyNativeCombatGearRankTable;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMyCombatGearStatusTests
{
    private sealed class SelectedStatusCase : IDisposable
    {
        private readonly NativeCombatGearQueryScope metadata;
        private readonly LegacyNativeMagicType priorMagic = LegacyNativePoolMagic.Magic;

        internal SelectedStatusCase(bool enabled = true, bool withClassPrelude = true)
        {
            metadata = new(enabled);
            NativeSpellTestFixture? owned = null;
            try
            {
                Fixture = owned = new();
                Gear = new(Fixture.Owner);
                SetClass(Fixture.Owner, withClassPrelude ? 4001U : 0U);
                if (enabled)
                {
                    Ranks.Create();
                    Ranks.Ranks[Ranks.Type.WEEK_Archer].RankingList[17] = new() { UID = 17 };
                }
                Fixture.Owner.Status.ItemBless = 50;
                Fixture.Owner.Status.MaxAttack = 101;
                Fixture.Owner.Status.MinAttack = 202;
                Fixture.Owner.Status.Defence = 303;
                Fixture.Owner.Status.MagicDefence = 404;
                Fixture.Owner.Status.MaxHitpoints = 505;
                Fixture.Owner.PerfectionStatus.LuckyStrike = 17;
                Fixture.Owner.PerfectionStatus.StrikeLock = 18;
                Fixture.Owner.PerfectionStatus.CoreStrike = 19;
                // Genuine prior spell publication, not an arbitrary queue byte.
                Fixture.Storage.Add(Fixture.Packet, 21540, level: 3);
                Fixture.Storage.ClientSpells[21930] = new() { ID = 21930, Level = 4 };
            }
            catch (Exception failure)
            {
                try { owned?.Dispose(); }
                catch (Exception cleanup) { failure.Data["SelectedStatusFixtureCleanup"] = cleanup; }
                finally { LegacyNativePoolMagic.Magic = priorMagic; metadata.Dispose(); }
                throw;
            }
        }

        internal NativeSpellTestFixture Fixture { get; }
        internal LegacyNativeMyCombatGear Gear { get; }

        internal void SelectEarlyAndTarget(int index, string missingName)
        {
            if (missingName != "HolyProtection")
            {
                Gear.SetValue("HolyProtection", 25.9);
                Assert.True(Fixture.Owner.Player.BitVector.TryAdd(376, 60, false, 0));
            }
            Assert.True(Fixture.Owner.Player.BitVector.TryAdd(index, 60, false, 0));
        }

        internal void EnableReal18160Prelude()
        {
            LegacyNativePoolMagic.Magic = new()
            {
                [18160] = new() { [2] = new() { GDamage = 9, DamageOnHuman = 11, Damage2 = 13, Damage3 = 17 } },
            };
            Fixture.Storage.ClientSpells[18160] = new() { ID = 18160, Level = 2 };
            foreach (var index in new[] { 317, 318, 319 })
                Assert.True(Fixture.Owner.Player.BitVector.TryAdd(index, 60, false, 0));
        }

        public void Dispose()
        {
            try { Fixture.Dispose(); }
            finally { LegacyNativePoolMagic.Magic = priorMagic; metadata.Dispose(); }
        }
    }

    private sealed class SelectedStatusImage
    {
        private readonly object status;
        private readonly object perfection;
        private readonly FieldImage[] statusFields;
        private readonly FieldImage[] perfectionFields;
        private readonly LegacyNativeSpellStorage storage;
        private readonly System.Collections.Concurrent.ConcurrentDictionary<ushort, LegacyNativeMsgSpell> spells;
        private readonly SpellImage[] spellRows;
        private readonly byte[][] queueRows;
        private readonly byte[][] queueBytes;
        private readonly byte[] callerPacket;
        private readonly LegacyNativeStatusFlagsBigVector32 vector;
        private readonly uint[] words;
        private readonly uint[] wordImage;
        private readonly System.Collections.Concurrent.ConcurrentDictionary<int, LegacyNativeStatusFlagsBigVector32.Flag> registrations;
        private readonly FlagImage[] flagRows;

        private SelectedStatusImage(NativeSpellTestFixture fixture)
        {
            status = fixture.Owner.Status;
            perfection = fixture.Owner.PerfectionStatus;
            statusFields = Fields(status);
            perfectionFields = Fields(perfection);
            storage = fixture.Storage;
            spells = storage.ClientSpells;
            spellRows = spells.Select(pair => new SpellImage(pair.Key, pair.Value, Fields(pair.Value))).ToArray();
            queueRows = fixture.Queue.ToArray();
            queueBytes = queueRows.Select(row => (byte[])row.Clone()).ToArray();
            callerPacket = NativeSpellTestFixture.Bytes(fixture.Packet);
            vector = fixture.Owner.Player.BitVector;
            words = vector.bits;
            wordImage = (uint[])words.Clone();
            registrations = vector.ArrayFlags;
            flagRows = registrations.Select(pair => new FlagImage(pair.Key, pair.Value, Fields(pair.Value))).ToArray();
        }

        internal static SelectedStatusImage Capture(NativeSpellTestFixture fixture) => new(fixture);

        internal void AssertUnchanged(NativeSpellTestFixture fixture)
        {
            Assert.Same(status, fixture.Owner.Status);
            Assert.Same(perfection, fixture.Owner.PerfectionStatus);
            AssertFields(status, statusFields);
            AssertFields(perfection, perfectionFields);
            Assert.Same(storage, fixture.Owner.MySpells);
            Assert.Same(spells, storage.ClientSpells);
            Assert.Equal(spellRows.Length, spells.Count);
            foreach (var row in spellRows)
            {
                Assert.Same(row.Row, spells[row.Id]);
                AssertFields(row.Row, row.Fields);
            }
            var currentFrames = fixture.Queue.ToArray();
            Assert.Equal(queueRows.Length, currentFrames.Length);
            for (var index = 0; index < currentFrames.Length; index++)
            {
                Assert.Same(queueRows[index], currentFrames[index]);
                Assert.Equal(queueBytes[index], currentFrames[index]);
            }
            Assert.Equal(callerPacket, NativeSpellTestFixture.Bytes(fixture.Packet));
            Assert.Same(vector, fixture.Owner.Player.BitVector);
            Assert.Same(words, vector.bits);
            Assert.Equal(wordImage, words);
            Assert.Same(registrations, vector.ArrayFlags);
            Assert.Equal(flagRows.Length, registrations.Count);
            foreach (var row in flagRows)
            {
                Assert.Same(row.Row, registrations[row.Key]);
                AssertFields(row.Row, row.Fields);
            }
        }

        private static FieldImage[] Fields(object owner) => owner.GetType()
            .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Select(field => new FieldImage(field, field.GetValue(owner))).ToArray();

        private static void AssertFields(object owner, FieldImage[] fields)
        {
            foreach (var field in fields) Assert.Equal(field.Value, field.Field.GetValue(owner));
        }

        private sealed record FieldImage(FieldInfo Field, object? Value);
        private sealed record SpellImage(ushort Id, LegacyNativeMsgSpell Row, FieldImage[] Fields);
        private sealed record FlagImage(int Key, LegacyNativeStatusFlagsBigVector32.Flag Row, FieldImage[] Fields);
    }
}
