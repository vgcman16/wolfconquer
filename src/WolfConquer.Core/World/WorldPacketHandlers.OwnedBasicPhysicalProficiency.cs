using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Calculation only. The real prepared hit and its later private committed
    // publication are independently required by the concrete file writer.
    internal sealed class OwnedBasicPhysicalProficiencyPlan
    {
        internal readonly OwnedBasicPhysicalReceivePreparation Hit;
        internal readonly BasicPhysicalProficiencyCalculation Calculation;
        internal readonly PlayerProficiencySnapshot Before, After;
        internal readonly IReadOnlyList<ReadOnlyMemory<byte>> Frames;
        internal readonly uint RawExperience, RatedExperience;
        internal readonly IReadOnlyList<uint> WeaponIds;
        internal bool HasChanges => Frames.Count != 0;

        internal OwnedBasicPhysicalProficiencyPlan(OwnedBasicPhysicalReceivePreparation hit)
        {
            if (!hit.IsCurrentPlain()) throw Invalid();
            Hit = hit;
            Before = LegacyNativeProficiencyStorage.FreezeSessionSnapshot(hit.ExpectedActor.PlayerProficiencies);
            if (!ReferenceEquals(Before, hit.ExpectedActor.PlayerProficiencies)) throw Invalid();
            RawExperience = hit.ProficiencyDamage;
            WeaponIds = Array.AsReadOnly(GetLegacyCurrentWeaponProficiencyIds(hit.Actor).ToArray());
            var calculation = Calculation = new BasicPhysicalProficiencyCalculation(Before, WeaponIds, RawExperience);
            RatedExperience = calculation.RatedExperience;
            After = calculation.After;
            Frames = calculation.Frames;
            if (!hit.IsCurrentPlain()) throw Invalid();
        }

        internal bool MatchesSource(WorldSession actor) => ReferenceEquals(actor.NativeGameClient, Hit.ActorBinding.Client) &&
            actor.AccountId == Hit.ActorBinding.AccountId && actor.Character.Uid == Hit.ActorBinding.Uid &&
            ReferenceEquals(actor.PlayerProficiencies, Before);

        private static InvalidOperationException Invalid() => new("basicPhysicalProficiencyRequiresPreparedHit");
    }
}
