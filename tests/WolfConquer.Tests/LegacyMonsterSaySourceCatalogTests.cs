using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed class LegacyMonsterSaySourceCatalogTests
{
    [Fact]
    public void CatalogDocumentsMonsterSayAsSourceBlocked()
    {
        var lane = Assert.Single(LegacyMonsterSaySourceCatalog.SourceBlockedLanes);

        Assert.Equal("ini\\monstersay.ini", lane.RelativePath);
        Assert.Equal(2744, lane.CjkTextLines);
        Assert.Equal(2743, lane.CjkKeyValueRows);
        Assert.Equal(1183, lane.AffectedMonsterFamilies);
        Assert.Equal(["battle", "blood", "magic", "die", "kill"], lane.SpeechKeyFamilies);
        Assert.Contains("no authoritative English source", lane.Reason, StringComparison.Ordinal);
        Assert.Contains("machine-translating", lane.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void CatalogRecordsOnlyNonTranslatorEvidence()
    {
        Assert.Equal("CAB440D0D0D160E312A9168F2E65ABA5ABE395E2424C30BA018B9051C88EC247",
            LegacyMonsterSaySourceCatalog.LiveTargetSha256);
        Assert.Single(LegacyMonsterSaySourceCatalog.SourceBlockedLanes);
        Assert.Contains(
            LegacyMonsterSaySourceCatalog.SourceProbes,
            probe => probe.Location == "references/client-backup/ini/monstersay.ini" &&
                probe.Result.Contains("SHA-256 matches the live target", StringComparison.Ordinal));
        Assert.Contains(
            LegacyMonsterSaySourceCatalog.SourceProbes,
            probe => probe.Location == "references/DecompiledSource" &&
                probe.Result.Contains("found no English source", StringComparison.Ordinal));
    }
}
