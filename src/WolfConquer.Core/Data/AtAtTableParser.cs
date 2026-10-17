namespace WolfConquer.Core.Data;

public static class AtAtTableParser
{
    private static readonly string[] Separator = ["@@"];

    public static IReadOnlyList<string> SplitRow(string row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return row.Split(Separator, StringSplitOptions.None);
    }
}
