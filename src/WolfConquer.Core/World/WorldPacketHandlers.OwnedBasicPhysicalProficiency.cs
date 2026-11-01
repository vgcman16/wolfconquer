using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Calculation only. The real prepared hit and its later private committed
    // publication are independently required by the concrete file writer.
    internal sealed class OwnedBasicPhysicalProficiencyPlan
    {
        internal readonly OwnedBasicPhysicalReceivePreparation Hit;
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
            RatedExperience = ApplyLegacyProficiencyExperienceRate(RawExperience);
            var rows = Before.Proficiencies.ToList();
            var frames = new List<ReadOnlyMemory<byte>>();
            if (RawExperience != 0 && RatedExperience != 0)
            {
                foreach (uint id in WeaponIds)
                {
                    int index = rows.FindIndex(row => row.Id == id);
                    if (index < 0)
                    {
                        // Retained Proficiency.Add(ID,0,0,0), not invented
                        // first-hit experience on a newly created row.
                        var added = new PlayerProficiencySnapshotEntry(id, 0, 0, 0,
                            LegacyProficiencyPacket.GetNeededExperience(0));
                        rows.Add(added);
                        frames.Add(LegacyProficiencyPacket.FromSnapshot(added).ToFrameBytes());
                    }
                    else
                    {
                        var result = LegacyProficiencyPacket.ApplyExperience(rows[index], RatedExperience);
                        if (!result.Updated) continue;
                        rows[index] = result.Proficiency;
                        frames.Add(result.Packet.ToFrameBytes());
                    }
                }
            }
            After = frames.Count == 0 ? Before : LegacyNativeProficiencyStorage.FreezeSessionSnapshot(new PlayerProficiencySnapshot(rows));
            Frames = frames.AsReadOnly();
            if (!hit.IsCurrentPlain()) throw Invalid();
        }

        internal bool MatchesSource(WorldSession actor) => ReferenceEquals(actor.NativeGameClient, Hit.ActorBinding.Client) &&
            actor.AccountId == Hit.ActorBinding.AccountId && actor.Character.Uid == Hit.ActorBinding.Uid &&
            ReferenceEquals(actor.PlayerProficiencies, Before);

        private static InvalidOperationException Invalid() => new("basicPhysicalProficiencyRequiresPreparedHit");
    }
}
