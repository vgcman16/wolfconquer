using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    // The old event-rule component fixtures have a managed store, but no native
    // admission or save owner. Genuine native/file controls remain separate.
    private sealed class DetachedSobRejectionSnapshot
    {
        private readonly WorldSession owner;
        private readonly CharacterSnapshot character;
        private readonly PlayerSpellSnapshotEntry[] spells;
        private readonly PlayerProficiencySnapshotEntry[] proficiencies;
        private readonly byte[] items;
        private readonly KeyValuePair<ushort, DateTimeOffset>[]? cooldowns;
        private readonly ushort authorization;

        internal DetachedSobRejectionSnapshot(WorldSession before)
        {
            Assert.Null(before.NativeGameClient);
            Assert.Null(before.UsersRoot);
            owner = before;
            character = before.Character with
            {
                Attributes = before.Character.Attributes.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
                PaidNobility = before.Character.PaidNobility.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
            };
            spells = before.PlayerSpells.Spells.Select(row => row with { }).ToArray();
            proficiencies = before.PlayerProficiencies.Proficiencies.Select(row => row with { }).ToArray();
            items = PlayerItemSnapshotLoader.ToLegacyBytes(before.PlayerItems);
            cooldowns = before.MagicCooldowns?.OrderBy(pair => pair.Key).ToArray();
            authorization = before.LegacyRandomSpell;
        }

        internal void AssertUnchanged(WorldSession after, IReadOnlyList<byte[]> sent)
        {
            Assert.Same(owner, after);
            Assert.Null(after.NativeGameClient);
            Assert.Null(after.UsersRoot);
            Assert.Equal(character.MinMana, after.Character.MinMana);
            Assert.Equal(character.Attributes.OrderBy(pair => pair.Key, StringComparer.Ordinal),
                after.Character.Attributes.OrderBy(pair => pair.Key, StringComparer.Ordinal));
            Assert.Equal(character.PaidNobility.OrderBy(pair => pair.Key, StringComparer.Ordinal),
                after.Character.PaidNobility.OrderBy(pair => pair.Key, StringComparer.Ordinal));
            Assert.Equal(character with { Attributes = after.Character.Attributes, PaidNobility = after.Character.PaidNobility },
                after.Character);
            Assert.Equal(spells, after.PlayerSpells.Spells);
            Assert.Equal(proficiencies, after.PlayerProficiencies.Proficiencies);
            Assert.Equal(items, PlayerItemSnapshotLoader.ToLegacyBytes(after.PlayerItems));
            if (cooldowns is null) Assert.Null(after.MagicCooldowns);
            else
            {
                Assert.NotNull(after.MagicCooldowns);
                Assert.Equal(cooldowns, after.MagicCooldowns.OrderBy(pair => pair.Key));
            }
            Assert.Equal(authorization, after.LegacyRandomSpell);
            Assert.Empty(sent);
        }
    }
}
