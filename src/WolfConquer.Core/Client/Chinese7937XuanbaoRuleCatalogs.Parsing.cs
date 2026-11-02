using System.Security.Cryptography;
using System.Text;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.Client;

public sealed partial class Chinese7937XuanbaoRuleCatalogs
{
    public static bool TryParsePacked(int declaredVersion, ReadOnlySpan<byte> packedCompose, ReadOnlySpan<byte> packedRandom,
        ReadOnlySpan<byte> expectedComposePin, ReadOnlySpan<byte> expectedRandomPin,
        out Chinese7937XuanbaoRuleCatalogs? catalogs, out Chinese7937XuanbaoRulesFailure failure)
    {
        catalogs = null;
        if (declaredVersion != 7937) { failure = Chinese7937XuanbaoRulesFailure.UnsupportedVersion; return false; }
        if (expectedComposePin.Length != SHA256.HashSizeInBytes || expectedRandomPin.Length != SHA256.HashSizeInBytes)
        { failure = Chinese7937XuanbaoRulesFailure.PinRequired; return false; }
        if (packedCompose.Length is <= 0 or > MaximumPackedBytes || packedRandom.Length is <= 0 or > MaximumPackedBytes)
        { failure = Chinese7937XuanbaoRulesFailure.InputBounds; return false; }
        var compose = packedCompose.ToArray(); var random = packedRandom.ToArray();
        var composePin = expectedComposePin.ToArray(); var randomPin = expectedRandomPin.ToArray();
        var composeHash = SHA256.HashData(compose); var randomHash = SHA256.HashData(random);
        if (!CryptographicOperations.FixedTimeEquals(composePin, composeHash) ||
            !CryptographicOperations.FixedTimeEquals(randomPin, randomHash))
        { failure = Chinese7937XuanbaoRulesFailure.PinMismatch; return false; }
        var decodedCompose = ClientItemCatalogCodec.Decode(compose);
        var decodedRandom = ClientItemCatalogCodec.Decode(random);
        if (!ClientItemCatalogCodec.Encode(decodedCompose).AsSpan().SequenceEqual(compose) ||
            !ClientItemCatalogCodec.Encode(decodedRandom).AsSpan().SequenceEqual(random))
        { failure = Chinese7937XuanbaoRulesFailure.Reconstruction; return false; }
        try
        {
            var limits = ComposeRows(ReadRows(decodedCompose));
            var attributes = RandomRows(ReadRows(decodedRandom));
            catalogs = new(limits, attributes, Hex(composeHash), Hex(SHA256.HashData(decodedCompose)),
                Hex(randomHash), Hex(SHA256.HashData(decodedRandom)));
            failure = Chinese7937XuanbaoRulesFailure.None;
            return true;
        }
        catch (RuleRefusal error) { failure = error.Failure; return false; }
    }

    private static XuanbaoComposeAttributeLimit[] ComposeRows(NumericRow[] source)
    {
        var result = new XuanbaoComposeAttributeLimit[source.Length];
        HashSet<int> identities = [];
        for (var index = 0; index < source.Length; index++)
        {
            var row = source[index]; var values = row.Values;
            if (!identities.Add(values[0])) throw new RuleRefusal(Chinese7937XuanbaoRulesFailure.DuplicateComposeId);
            result[index] = new(values[0], Array.AsReadOnly(values[1..]), ComposeSourceFile,
                index + 1, row.RawLine, Array.AsReadOnly(row.Fields));
        }
        return result;
    }

    private static XuanbaoRandomAttribute[] RandomRows(NumericRow[] source)
    {
        var result = new XuanbaoRandomAttribute[source.Length];
        HashSet<(int, int)> keys = [];
        for (var index = 0; index < source.Length; index++)
        {
            var row = source[index]; var values = row.Values;
            if (!keys.Add((values[0], values[1]))) throw new RuleRefusal(Chinese7937XuanbaoRulesFailure.DuplicateRandomKey);
            result[index] = new(values[0], values[1], values[2], values[3], RandomSourceFile,
                index + 1, row.RawLine, Array.AsReadOnly(row.Fields));
        }
        return result;
    }

    private static NumericRow[] ReadRows(ReadOnlySpan<byte> decoded)
    {
        List<NumericRow> rows = [];
        var offset = 0;
        while (offset < decoded.Length)
        {
            if (rows.Count == MaximumRows) throw new RuleRefusal(Chinese7937XuanbaoRulesFailure.RowBounds);
            var start = offset;
            while (offset < decoded.Length && decoded[offset] is not (13 or 10))
            {
                if (decoded[offset] is 0 or > 127) throw new RuleRefusal(Chinese7937XuanbaoRulesFailure.InvalidTable);
                offset++;
            }
            if (start == offset || offset + 1 >= decoded.Length || decoded[offset] != 13 || decoded[offset + 1] != 10)
                throw new RuleRefusal(Chinese7937XuanbaoRulesFailure.InvalidTable);
            var rawLine = Encoding.ASCII.GetString(decoded[start..offset]);
            offset += 2;
            var fields = rawLine.Split("@@", StringSplitOptions.None);
            if (fields.Length != 5 || fields[^1].Length != 0) throw new RuleRefusal(Chinese7937XuanbaoRulesFailure.InvalidTable);
            var values = new int[4];
            for (var column = 0; column < values.Length; column++) values[column] = ReadNumber(fields[column]);
            rows.Add(new(rawLine, fields, values));
        }
        if (rows.Count == 0) throw new RuleRefusal(Chinese7937XuanbaoRulesFailure.InvalidTable);
        return rows.ToArray();
    }

    private static int ReadNumber(string field)
    {
        if (field.Length == 0) throw new RuleRefusal(Chinese7937XuanbaoRulesFailure.InvalidTable);
        var result = 0;
        foreach (var character in field)
        {
            if (!char.IsAsciiDigit(character)) throw new RuleRefusal(Chinese7937XuanbaoRulesFailure.InvalidTable);
            var digit = character - '0';
            if (result > (int.MaxValue - digit) / 10) throw new RuleRefusal(Chinese7937XuanbaoRulesFailure.NumericWidth);
            result = result * 10 + digit;
        }
        return result;
    }

    private static string Hex(byte[] bytes) => Convert.ToHexString(bytes).ToLowerInvariant();
    private sealed record NumericRow(string RawLine, string[] Fields, int[] Values);
    private sealed class RuleRefusal(Chinese7937XuanbaoRulesFailure failure) : Exception
    { internal Chinese7937XuanbaoRulesFailure Failure { get; } = failure; }
}
