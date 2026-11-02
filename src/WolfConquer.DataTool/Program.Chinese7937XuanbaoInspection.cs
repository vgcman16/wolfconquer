using System.Text.Json;
using WolfConquer.Core.Client;

internal static partial class Program
{
    // Explicit pinned inputs only. Core owns the bounded ordinary-file reads,
    // selected syntax and packed-table validation. Nothing here binds gameplay.
    private static int InspectChinese7937Xuanbao(string[] args)
    {
        if (args.Length != 8 || args[1] != "7937") return XuanbaoRefusal("arguments", "ExpectedBuildAndThreePinnedFiles");
        if (!TryXuanbaoSha256(args[3], out var infoPin) ||
            !TryXuanbaoSha256(args[5], out var composePin) ||
            !TryXuanbaoSha256(args[7], out var randomPin)) return XuanbaoRefusal("arguments", "InvalidSha256");
        try
        {
            const Chinese7937XuanbaoInfoVersion version = Chinese7937XuanbaoInfoVersion.Catalog7937;
            if (!Chinese7937DecodedXuanbaoInfo.TryLoadDecodedFile(args[2], version, infoPin, out var info, out var infoFailure) || info is null)
                return XuanbaoRefusal("info", infoFailure.ToString());
            if (!Chinese7937EchoRequirements.TryCreate(info, out var echo, out var echoFailure) || echo is null)
                return XuanbaoRefusal("echo", echoFailure.ToString());
            if (!Chinese7937DecodedXuanbaoInfo.TryLoadAttributeCatalogFile(args[2], version, infoPin, out var attributes, out var attributeFailure) || attributes is null)
                return XuanbaoRefusal("attributes", attributeFailure.ToString());
            if (!Chinese7937XuanbaoRuleCatalogs.TryLoadPackedFiles(args[4], args[6], 7937, composePin, randomPin,
                    out var rules, out var rulesFailure) || rules is null)
                return XuanbaoRefusal("rules", rulesFailure.ToString());
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                Status = "accepted", ClientBuild = 7937,
                InfoSha256 = info.ContentSha256,
                EchoFields = info.Count, EchoCostRows = echo.CostRows.Count, EchoOpeningRows = echo.OpeningRows.Count,
                ScoreLimits = attributes.ScoreLimits.Count, GoldLimitFlags = attributes.GoldLimitFlags.Count,
                GoldLimitItemTypes = attributes.GoldLimitItemTypes.Count,
                rules.ComposePackedSha256, rules.ComposeDecodedSha256,
                rules.RandomPackedSha256, rules.RandomDecodedSha256,
                ComposeRows = rules.ComposeLimits.Count, RandomRows = rules.RandomAttributes.Count,
                CatalogSyntaxAndPinsValidated = true, MainSubReferencesValidated = false,
                ServerBindingValidated = false, RuntimeActivated = false, LiveSupportValidated = false,
                OutputFilesWritten = false
            }));
            return 0;
        }
        catch (Exception)
        { return XuanbaoRefusal("internal", "InspectionFailed"); }
    }

    private static bool TryXuanbaoSha256(string value, out byte[] digest)
    {
        digest = [];
        if (value is null || value.Length != 64) return false;
        try { digest = Convert.FromHexString(value); return digest.Length == 32; }
        catch (FormatException) { return false; }
    }

    private static int XuanbaoRefusal(string stage, string reason)
    {
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            Status = "refused", Stage = stage, Reason = reason,
            CatalogSyntaxAndPinsValidated = false, MainSubReferencesValidated = false,
            ServerBindingValidated = false, RuntimeActivated = false, LiveSupportValidated = false,
            OutputFilesWritten = false
        }));
        return 1;
    }
}
