using static WolfConquer.Core.Data.WindowsJournalRootIdentity;

namespace WolfConquer.Tests;

public sealed partial class WindowsJournalRootIdentityTests
{
    [WindowsTradeJournalFailureFact]
    public void ActualDosAndVolumeGuidSpellingsIdentifyTheSameOwnedDirectory() => Run(f =>
    {
        var dosRoot = Path.GetPathRoot(f.Users)!;
        Assert.True(dosRoot.Length == 3 && char.IsAsciiLetter(dosRoot[0]) &&
            dosRoot[1] == ':' && dosRoot[2] == '\\');
        var alias = ObservedVolumeGuidPath(f.Users);
        using var captured = Capture(f.Users, alias, f.Quests);
        Assert.Equal(ObservedRootRelation.SameDirectory, captured.Inspect(RootRole.Users, RootRole.Items));
    });

    [WindowsTradeJournalFailureFact]
    public void ActualVolumeGuidChildChainRetainsBothPhysicalAncestorDirections() => Run(f =>
    {
        var childAlias = ObservedVolumeGuidPath(f.Child);
        using var captured = Capture(f.Users, childAlias, f.Quests);
        Assert.Equal(ObservedRootRelation.FirstIsAncestor, captured.Inspect(RootRole.Users, RootRole.Items));
        Assert.Equal(ObservedRootRelation.SecondIsAncestor, captured.Inspect(RootRole.Items, RootRole.Users));
    });

    [WindowsTradeJournalFailureFact]
    public void OnlyCompleteLocalVolumeGuidAbsoluteComponentsAreAcceptedAsInputs() => Run(f =>
    {
        var actual = ObservedVolumeGuidPath(f.Users).TrimEnd('\\');
        foreach (var input in new[]
        {
            actual + @"\.", actual + @"\..", actual + @"\\child", actual + @"\child:stream",
            actual + @"\child/other", actual + @"\child?", actual + @"\child*", actual + "\\child\0tail",
            @"\\?\Volume{not-a-guid}\Users", @"\\?\Volume{00000000-0000-0000-0000-000000000000}relative",
            @"\\?\GLOBALROOT\Device\HarddiskVolume1", @"\\.\C:\Users", @"\\?\UNC\server\share\Users",
        })
            Assert.ThrowsAny<ArgumentException>(() => { using var unexpected = Capture(input, f.Items, f.Quests); });
        // Unknown extended DOS input remains unsupported; this is not generic
        // device namespace support, normalization, or an NT object-path fallback.
        Assert.ThrowsAny<ArgumentException>(() => { using var unexpected = Capture(@"\\?\C:\Users", f.Items, f.Quests); });
    });
}
