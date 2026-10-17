using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool IsLegacySobNpcMagicGuild6PoleWarPoleAttackBlocked(
        CharacterSnapshot caster,
        SobNpc sobNpc,
        out string failureDiagnostic)
    {
        failureDiagnostic = string.Empty;
        if (!LegacySobNpcEventRules.TryGetGuild6PoleWarPoleSchedule(sobNpc, out var scheduleName, out _))
        {
            return false;
        }

        var diagnosticPrefix =
            $"map={sobNpc.MapId} kind={sobNpc.Kind}; source CanAttackNpc.Verified rejected " +
            $"{scheduleName} pole uid={sobNpc.Id} because ";
        var sourcePole = $"MsgSchedules.{scheduleName}.Furnitures[Pole]";
        var guildId = ReadAttributeUInt64(caster.Attributes, "GuildID", "GuildId");
        if (guildId == 0)
        {
            failureDiagnostic = diagnosticPrefix + "caster has no guild";
            return true;
        }

        if (TryReadUInt64(
                caster.Attributes,
                out var poleHitpoints,
                "Legacy" + scheduleName + "PoleHitPoints",
                "Legacy" + scheduleName + "PoleHitpoints",
                scheduleName + "PoleHitPoints",
                scheduleName + "PoleHitpoints",
                "Runtime" + scheduleName + "PoleHitPoints",
                "Runtime" + scheduleName + "PoleHitpoints") &&
            poleHitpoints == 0)
        {
            failureDiagnostic = diagnosticPrefix + sourcePole + ".HitPoints is zero";
            return true;
        }

        var guildName = ReadAttributeString(caster.Attributes, "GuildName", "LegacyGuildName");
        if (!string.IsNullOrWhiteSpace(guildName) &&
            string.Equals(guildName, sobNpc.Name, StringComparison.Ordinal))
        {
            failureDiagnostic =
                diagnosticPrefix + "client.Player.MyGuild.GuildName matches " + sourcePole + ".Name";
            return true;
        }

        var process = ReadAttributeString(
            caster.Attributes,
            "Legacy" + scheduleName + "Process",
            "Legacy" + scheduleName + "Proces",
            scheduleName + "Process",
            scheduleName + "Proces",
            "Runtime" + scheduleName + "Process",
            "Runtime" + scheduleName + "Proces");
        if (!process.Equals("Alive", StringComparison.OrdinalIgnoreCase))
        {
            var sourceProcess = string.IsNullOrWhiteSpace(process) ? "Dead" : process;
            failureDiagnostic =
                diagnosticPrefix + $"MsgSchedules.{scheduleName}.Proces is {sourceProcess}";
            return true;
        }

        return false;
    }
}
