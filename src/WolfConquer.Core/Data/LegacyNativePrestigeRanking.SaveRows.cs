using System.Globalization;
using System.Text;

namespace WolfConquer.Core.Data;

internal partial class LegacyNativePrestigeRanking
{
    private static readonly UTF8Encoding PrestigeSaveTextEncoding = new(false, true);

    private static byte[] PreparePrestigeSaveImage()
    {
        var rows = CapturePrestigeSaveRows();
        var text = new StringBuilder();
        text.Append("Count=").Append(rows.Count.ToString(CultureInfo.InvariantCulture))
            .Append(Environment.NewLine);
        foreach (var row in rows)
        {
            AppendPrestigeSaveRow(text, row);
            text.Append(Environment.NewLine);
        }
        return PrestigeSaveTextEncoding.GetBytes(text.ToString());
    }

    private static void AppendPrestigeSaveRow(StringBuilder text, PrestigeSaveRow row)
    {
        AppendPrestigeSaveNumber(text, row.Numbers[0]);
        text.Append(row.Name).Append('/');
        for (var index = 1; index < row.Numbers.Length; index++)
        {
            AppendPrestigeSaveNumber(text, row.Numbers[index]);
        }
        text.Append(row.GuildName).Append('/');
        text.Append(row.Points.Length.ToString(CultureInfo.InvariantCulture)).Append('/');
        foreach (var point in row.Points)
        {
            AppendPrestigeSaveNumber(text, point);
        }
    }

    private static void AppendPrestigeSaveNumber(StringBuilder text, uint value)
        => text.Append(value.ToString(CultureInfo.InvariantCulture)).Append('/');

    private sealed record PrestigeSaveRow(
        uint[] Numbers,
        string Name,
        string GuildName,
        uint[] Points);
}
