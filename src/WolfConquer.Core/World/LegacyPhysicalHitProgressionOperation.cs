using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Historical DATA recipe only. Recorded damage is not an attack grant,
// random-outcome proof, current owner, persistence receipt or host authentication.
internal sealed class LegacyPhysicalHitProgressionOperation
{
    private const int MaximumBytes = LegacyTradeJournal.PhysicalDeathMaximumReferenceBytes;
    private readonly byte[] actorItems;
    internal long ActorAccountId { get; }
    internal uint ActorUid { get; }
    internal long TargetAccountId { get; }
    internal uint TargetUid { get; }
    internal uint RawExperience { get; }
    internal uint AppliedDamage { get; }
    internal bool ActorAlternate { get; }
    internal byte[] ActorItems => actorItems.ToArray();
    internal LegacyPhysicalDeathOperation? Death { get; }

    internal LegacyPhysicalHitProgressionOperation(long actorAccountId, uint actorUid, long targetAccountId,
        uint targetUid, uint rawExperience, uint appliedDamage, bool actorAlternate, byte[] actorItemsBefore,
        LegacyPhysicalDeathOperation? death)
    {
        if (actorAccountId is <= 0 or > uint.MaxValue || targetAccountId is <= 0 or > uint.MaxValue || actorAccountId == targetAccountId ||
            actorUid == 0 || targetUid == 0 || actorUid == targetUid || rawExperience == 0 || appliedDamage > rawExperience ||
            actorItemsBefore is null || actorItemsBefore.Length is 0 or > MaximumBytes || death is not null && death.TargetUid != targetUid)
            throw Invalid();
        ActorAccountId = actorAccountId; ActorUid = actorUid; TargetAccountId = targetAccountId; TargetUid = targetUid;
        RawExperience = rawExperience; AppliedDamage = appliedDamage; ActorAlternate = actorAlternate;
        actorItems = actorItemsBefore.ToArray();
        Death = death is null ? null : new LegacyPhysicalDeathOperation(death.TargetUid, death.QueryAdmitted, death.Alternate,
            death.ItemsBefore, death.RankingRows, death.DerivedSpells, death.GrantedRows);
        // The normalized DTO alone cannot certify complete physical UID rows.
        LegacyItemUidRawCatalog.ValidateAdmissionPlayerItems(actorItems);
        _ = PlayerItemSnapshotLoader.Parse(actorItems);
    }

    internal Projection Project(byte[] characterBefore, byte[]? proficiencyBefore, byte[]? spellsBefore = null)
    {
        try
        {
            if (characterBefore is null || characterBefore.Length is 0 or > MaximumBytes ||
                proficiencyBefore is { Length: > MaximumBytes } || spellsBefore is { Length: > MaximumBytes } ||
                Death is null && spellsBefore is not null) throw Invalid();
            var characterBytes = characterBefore.ToArray();
            var profBytes = proficiencyBefore?.ToArray(); var spellBytes = spellsBefore?.ToArray();
            if (!LegacyExperienceContinuationFence.AllowsOrdinaryCharacterImage(characterBytes, TargetAccountId, TargetUid))
                throw Invalid();
            var target = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(characterBytes, out _));
            if (target.Uid != TargetUid || target.MinHitPoints is <= 0 or > int.MaxValue ||
                target.MapId is <= 0 or > uint.MaxValue || target.X is < 0 or > ushort.MaxValue || target.Y is < 0 or > ushort.MaxValue ||
                AppliedDamage > target.MinHitPoints || (Death is null) != (AppliedDamage < target.MinHitPoints)) throw Invalid();
            if (!LegacyPairMovementCharacterProjection.TryProjectCoordinates(characterBytes, TargetUid, checked((uint)target.MapId),
                checked((ushort)target.X), checked((ushort)target.Y), checked((ushort)target.X), checked((ushort)target.Y),
                out var neutral, out _) || !neutral.AsSpan().SequenceEqual(characterBytes)) throw Invalid();

            var items = PlayerItemSnapshotLoader.Parse(actorItems);
            var weapons = WorldPacketHandlers.GetLegacyCurrentWeaponProficiencyIds(items, ActorAlternate);
            if (weapons.Count is 0 or > 2 || weapons.Any(id => id == 0 || !LegacyProficiencyPacket.IsLegacyWeaponProficiencyId(id)) ||
                weapons.Distinct().Count() != weapons.Count) throw Invalid();
            var before = profBytes is null ? PlayerProficiencySnapshot.Empty : PlayerProficiencySnapshotLoader.Parse(profBytes);
            var calculation = new WorldPacketHandlers.BasicPhysicalProficiencyCalculation(before, weapons, RawExperience);
            if (!calculation.HasChanges) throw Invalid();
            var proficiencyAfter = LegacyBasicPhysicalProficiencyImage.Project(profBytes, calculation);
            byte[] targetAfter = Death is null
                ? LegacyBoundHitpointsScalarWrite.ProjectAfterImage(characterBytes, target with { MinHitPoints = target.MinHitPoints - AppliedDamage })
                : Death.ProjectCharacter(characterBytes, spellBytes);
            byte[]? spellsAfter = Death?.ProjectSpells(spellBytes, target);
            if (targetAfter.Length is 0 or > MaximumBytes || proficiencyAfter.Length is 0 or > MaximumBytes ||
                spellsAfter is { Length: > MaximumBytes }) throw Invalid();
            return new(targetAfter, proficiencyAfter, spellsAfter, weapons);
        }
        catch (Exception error) when (error is InvalidOperationException or FormatException or OverflowException or ArgumentException)
        { throw new InvalidDataException("physicalHitProgression24ProjectionInvalid", error); }
    }

    internal sealed class Projection
    {
        private readonly byte[] character, proficiencies;
        private readonly byte[]? spells;
        internal IReadOnlyList<uint> WeaponIds { get; }
        internal Projection(byte[] character, byte[] proficiencies, byte[]? spells, IReadOnlyList<uint> weapons)
        {
            this.character = character.ToArray(); this.proficiencies = proficiencies.ToArray(); this.spells = spells?.ToArray();
            WeaponIds = Array.AsReadOnly(weapons.ToArray());
        }
        internal byte[] CopyCharacterAfter() => character.ToArray();
        internal byte[] CopyProficienciesAfter() => proficiencies.ToArray();
        internal byte[]? CopySpellsAfter() => spells?.ToArray();
    }

    private static InvalidDataException Invalid() => new("physicalHitProgression24IntentInvalid");
}
