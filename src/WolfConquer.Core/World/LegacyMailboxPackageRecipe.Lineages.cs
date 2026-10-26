namespace WolfConquer.Core.World;

internal sealed partial class LegacyMailboxPackageRecipe
{
    internal static string[] CaptureLineageKeys(IEnumerable<LegacyMailboxEntry> selected) =>
        selected.Where(mail => mail.Flag != 0).DistinctBy(mail => mail.Id).OrderBy(mail => mail.Id).Select(Lineage).ToArray();
}
