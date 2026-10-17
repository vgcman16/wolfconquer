using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession> TryApplyLegacyWeaponProficiencyExperienceAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint rawExperience,
        CancellationToken cancellationToken,
        string activity = "monster weapon proficiency experience")
    {
        if (rawExperience == 0 ||
            string.IsNullOrWhiteSpace(context.PlayerProficienciesRoot))
        {
            return session;
        }

        var proficiencyIds = GetLegacyCurrentWeaponProficiencyIds(session);
        if (proficiencyIds.Count == 0)
        {
            return session;
        }

        var ratedExperience = ApplyLegacyProficiencyExperienceRate(rawExperience);
        if (ratedExperience == 0)
        {
            return session;
        }

        var proficiencies = session.PlayerProficiencies.Proficiencies.ToList();
        var updatedAny = false;
        foreach (var proficiencyId in proficiencyIds)
        {
            var index = proficiencies.FindIndex(proficiency => proficiency.Id == proficiencyId);
            if (index < 0)
            {
                var created = new PlayerProficiencySnapshotEntry(
                    proficiencyId,
                    Level: 0,
                    PreviousLevel: 0,
                    Experience: 0,
                    NeededExperience: LegacyProficiencyPacket.GetNeededExperience(0));
                proficiencies.Add(created);
                await context.Connection.SendAsync(
                        LegacyProficiencyPacket.FromSnapshot(created).ToFrameBytes(),
                        cancellationToken)
                    .ConfigureAwait(false);
                updatedAny = true;
                continue;
            }

            var result = LegacyProficiencyPacket.ApplyExperience(proficiencies[index], ratedExperience);
            if (!result.Updated)
            {
                continue;
            }

            proficiencies[index] = result.Proficiency;
            await context.Connection.SendAsync(result.Packet.ToFrameBytes(), cancellationToken)
                .ConfigureAwait(false);
            updatedAny = true;
        }

        if (!updatedAny)
        {
            return session;
        }

        var updatedProficiencies = new PlayerProficiencySnapshot(proficiencies);
        new PlayerProficiencySnapshotLoader(context.PlayerProficienciesRoot)
            .SaveByCharacterUid(session.Character.Uid, updatedProficiencies);

        var updatedSession = session with { PlayerProficiencies = updatedProficiencies };
        context.Sessions?.Set(context.Connection, updatedSession);
        await context.Log.WriteLineAsync(
                $"WorldServer sent legacy {activity} " +
                $"{FormatSession(context with { Session = updatedSession })} " +
                $"rawExperience={rawExperience} ratedExperience={ratedExperience} " +
                $"proficiencyIds={string.Join(',', proficiencyIds.Select(id => id.ToString(CultureInfo.InvariantCulture)))}; " +
                "source=UpdateSpell.CheckUpdate Damage * Program.ServerConfig.ExpRateProf -> Proficiency.CheckUpdate -> ServerDatabase.SaveClientProfs.")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static uint ApplyLegacyProficiencyExperienceRate(uint rawExperience)
    {
        return ClampLegacyProficiencyRawExperience((ulong)rawExperience * LegacyServerConfigProficiencyExperienceRate);
    }

    private static uint ClampLegacyProficiencyRawExperience(ulong rawExperience)
    {
        return rawExperience > uint.MaxValue
            ? uint.MaxValue
            : (uint)rawExperience;
    }

    private static IReadOnlyList<uint> GetLegacyCurrentWeaponProficiencyIds(WorldSession session)
    {
        var ids = new List<uint>(capacity: 2);
        if (TryGetCurrentWeaponType(session, normalizedPosition: 4, mapLegacyBladeToSword: false, out var rightWeaponType) &&
            LegacyProficiencyPacket.IsLegacyWeaponProficiencyId(rightWeaponType))
        {
            ids.Add(rightWeaponType);
        }

        if (TryGetCurrentWeaponType(session, normalizedPosition: 5, mapLegacyBladeToSword: false, out var leftWeaponType) &&
            leftWeaponType != rightWeaponType &&
            LegacyProficiencyPacket.IsLegacyWeaponProficiencyId(leftWeaponType))
        {
            ids.Add(leftWeaponType);
        }

        return ids;
    }
}
