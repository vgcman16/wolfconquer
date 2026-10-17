using System.Globalization;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask PersistLegacyBankCpsAsync(
        WorldPacketHandlerContext context,
        WorldSession updatedSession)
    {
        if (TryPersistLegacyCharacterAttributes(
                updatedSession.UsersRoot,
                updatedSession.AccountId,
                updatedSession.Character.Uid,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ConquerPoints"] = updatedSession.Character.ConquerPoints.ToString(CultureInfo.InvariantCulture),
                    ["BankCps"] = updatedSession.Character.BankConquerPoints.ToString(CultureInfo.InvariantCulture),
                },
                out var path,
                out var reason))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer persisted LegacyNpc BankCps transfer " +
                    $"{FormatSession(context with { Session = updatedSession })} source={LegacyBankCpsSource} " +
                    $"path={Path.GetFileName(path)} keys=ConquerPoints,BankCps.")
                .ConfigureAwait(false);
        }
        else
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc BankCps transfer persistence " +
                    $"{FormatSession(context with { Session = updatedSession })} source={LegacyBankCpsSource} " +
                    $"reason={reason}.")
                .ConfigureAwait(false);
        }
    }
}
