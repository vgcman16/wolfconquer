namespace WolfConquer.Core.Client;

public sealed record LegacyMonsterSaySourceProbe(
    string Location,
    string Result);

public sealed record LegacyMonsterSayBlockedLane(
    string RelativePath,
    int CjkTextLines,
    int CjkKeyValueRows,
    int AffectedMonsterFamilies,
    IReadOnlyList<string> SpeechKeyFamilies,
    string Reason);

public static class LegacyMonsterSaySourceCatalog
{
    public const string RelativePath = "ini\\monstersay.ini";
    public const string LiveTargetSha256 = "CAB440D0D0D160E312A9168F2E65ABA5ABE395E2424C30BA018B9051C88EC247";

    public static readonly IReadOnlyList<LegacyMonsterSaySourceProbe> SourceProbes =
    [
        new(
            "references/working-client",
            "No local references/working-client directory was present during the source audit."),
        new(
            "client\\ini\\monstersay.ini",
            "Live target only; UTF-8 monstersay.ini contains Chinese speech and must not be patched in place."),
        new(
            "references/client-backup/ini/monstersay.ini",
            "External audit backup (not bundled); SHA-256 matches the live target, so it is not an independent English source."),
        new(
            "references/DecompiledSource",
            "Exact searches for monstersay.ini, MonsterSay identifiers, sampled section keys, and sampled speech strings found no English source."),
        new(
            "Repository fixtures",
            "No fixture monstersay.ini or exact English source with matching section/key semantics was found.")
    ];

    public static readonly IReadOnlyList<LegacyMonsterSayBlockedLane> SourceBlockedLanes =
    [
        new(
            RelativePath,
            CjkTextLines: 2744,
            CjkKeyValueRows: 2743,
            AffectedMonsterFamilies: 1183,
            SpeechKeyFamilies: ["battle", "blood", "magic", "die", "kill"],
            "The prior source audit found no authoritative English source with matching section/key semantics. External client data, backups, and decompiled references are not bundled. Leave these rows source-blocked rather than inventing or machine-translating monster speech.")
    ];
}
