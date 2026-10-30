using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyNpcSalePersistence
{
    private const int MaxSnapshotBytes = 16 * 1024 * 1024;
    private sealed record TextLine(string Text, string Ending);

    private static bool TryPrepareCharacter(string path, WorldSession original, WorldSession updated, long warehouse,
        CharacterWriteMode mode,
        out byte[] before, out byte[] after, out string reason)
    {
        before = [];
        after = [];
        reason = string.Empty;
        EnsureSafePath(path);
        using var lease = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (lease.Length > MaxSnapshotBytes) { reason = "npcSaleSnapshotTooLarge"; return false; }
        before = new byte[checked((int)lease.Length)];
        lease.ReadExactly(before);
        var text = CharacterSnapshotFileEncoding.ReadAllText(path, out var encoding);
        var parts = Regex.Split(text, "(\\r\\n|\\r|\\n)");
        var lines = new List<TextLine>();
        for (var i = 0; i < parts.Length; i += 2)
            lines.Add(new(parts[i], i + 1 < parts.Length ? parts[i + 1] : string.Empty));
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var indices = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var inCharacter = false;
        var sections = 0;
        var insertAt = 0;
        for (var i = 0; i < lines.Count; i++)
        {
            var raw = lines[i].Text;
            var trimmed = raw.Trim();
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                inCharacter = trimmed[1..^1].Trim().Equals("Character", StringComparison.OrdinalIgnoreCase);
                if (inCharacter && ++sections != 1)
                { reason = "npcSaleCharacterSectionAmbiguous"; return false; }
                if (inCharacter) insertAt = i + 1;
                continue;
            }
            if (!inCharacter) continue;
            insertAt = i + 1;
            if (trimmed.Length == 0 || trimmed.StartsWith(';') || trimmed.StartsWith('#')) continue;
            var equals = raw.IndexOf('=');
            if (equals <= 0 || raw[..equals].Trim().Length == 0)
            { reason = "npcSaleCharacterLineInvalid"; return false; }
            var key = raw[..equals].Trim();
            if (!fields.TryAdd(key, raw[(equals + 1)..]))
            { reason = "npcSaleCharacterFieldsAmbiguous"; return false; }
            indices.Add(key, i);
        }
        if (sections != 1 || !fields.TryGetValue("UID", out var uidText) ||
            !TryNonnegative(uidText, out var uid) || uid != original.Character.Uid ||
            !fields.TryGetValue("Money", out var moneyText) || !TryNonnegative(moneyText, out var money) ||
            money != original.Character.Money || !fields.TryGetValue("ConquerPoints", out var cpText) ||
            !TryNonnegative(cpText, out var cp) || mode != CharacterWriteMode.BankConquerPoints && cp > uint.MaxValue ||
            cp != original.Character.ConquerPoints)
        { reason = "npcSaleWalletOrUidBaselineInvalid"; return false; }
        if (!TryFields(original.Character.Attributes, out var expected, out reason) ||
            !TryWarehouse(fields, out _, out reason)) return false;
        foreach (var key in new[] { "WHMoney", "WarehouseMoney", "SecurityPass",
            LegacyTradeEscrowClaims.IdKey, LegacyTradeEscrowClaims.MoneyKey, LegacyTradeEscrowClaims.CpKey })
        {
            var hasExpected = expected.TryGetValue(key, out var expectedValue);
            var hasActual = fields.TryGetValue(key, out var actualValue);
            if (hasExpected == hasActual && (!hasExpected || expectedValue == actualValue)) continue;
            reason = "npcSaleProtectedBaselineStale";
            return false;
        }
        if (mode == CharacterWriteMode.BankConquerPoints &&
            (!SameBankCpsProtectedField(expected, fields, "BoundConquerPoints") ||
             !SameBankCpsProtectedField(expected, fields, "BankCps")))
        { reason = "bankCpsProtectedBaselineStale"; return false; }
        if (!LegacySecondaryPasswordState.TryRead(fields, out _, out reason) ||
            !LegacyTradeEscrowClaims.TryRead(fields, out var claim, out reason) || claim is not null)
        { if (reason.Length == 0) reason = "npcSaleTradeEscrowActive"; return false; }
        if (mode is CharacterWriteMode.PickupConquerPoints or CharacterWriteMode.BankConquerPoints &&
            (!fields.TryGetValue("BankCps", out var bankText) || !TryNonnegative(bankText, out var bank) ||
             bank != original.Character.BankConquerPoints))
        { reason = mode == CharacterWriteMode.BankConquerPoints ? "bankCpsSavedBaselineInvalid" : "floorPickupBankCpsBaselineInvalid"; return false; }
        if (mode == CharacterWriteMode.Preserve)
        {
            // Item removal has no wallet/INI projection. In particular, a missing
            // WHMoney stays absent and noncanonical but valid numeric text stays raw.
            after = before;
            return true;
        }
        var updates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (mode is CharacterWriteMode.PickupConquerPoints or CharacterWriteMode.BankConquerPoints)
        {
            updates["ConquerPoints"] = updated.Character.ConquerPoints.ToString(CultureInfo.InvariantCulture);
            updates["BankCps"] = updated.Character.BankConquerPoints.ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            updates["Money"] = updated.Character.Money.ToString(CultureInfo.InvariantCulture);
            updates["WHMoney"] = warehouse.ToString(CultureInfo.InvariantCulture);
            if (fields.ContainsKey("WarehouseMoney")) updates["WarehouseMoney"] = warehouse.ToString(CultureInfo.InvariantCulture);
        }
        var newline = lines.Select(line => line.Ending).FirstOrDefault(ending => ending.Length > 0) ?? Environment.NewLine;
        foreach (var (key, value) in updates)
        {
            if (indices.TryGetValue(key, out var index))
            {
                var old = lines[index];
                lines[index] = old with { Text = old.Text[..(old.Text.IndexOf('=') + 1)] + value };
                continue;
            }
            if (insertAt > 0 && lines[insertAt - 1].Ending.Length == 0)
                lines[insertAt - 1] = lines[insertAt - 1] with { Ending = newline };
            lines.Insert(insertAt++, new(key + "=" + value, newline));
        }
        var payload = encoding.GetBytes(string.Concat(lines.Select(line => line.Text + line.Ending)));
        after = encoding.GetPreamble().Concat(payload).ToArray();
        return true;
    }

    private static bool TryFields(IReadOnlyDictionary<string, string> attrs,
        out Dictionary<string, string> fields, out string reason)
    {
        fields = new(StringComparer.OrdinalIgnoreCase);
        reason = string.Empty;
        foreach (var (key, value) in attrs)
            if (value is null || !fields.TryAdd(key, value))
            { reason = "npcSaleAttributesAmbiguous"; return false; }
        return true;
    }

    private static bool TryWarehouse(IReadOnlyDictionary<string, string> fields, out long value, out string reason)
    {
        value = 0;
        reason = string.Empty;
        if (!LegacyWarehouseMoneyState.TryRead(fields, out value))
        { reason = "npcSaleWarehouseInvalidOrAmbiguous"; return false; }
        return true;
    }

    private static bool TryNonnegative(string text, out long value) =>
        long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) && value >= 0;

    private static byte[]? ReadBytes(string path, bool allowMissing)
    {
        EnsureSafePath(path);
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > MaxSnapshotBytes) throw new IOException("Snapshot exceeds the supported size.");
            var bytes = new byte[checked((int)stream.Length)];
            stream.ReadExactly(bytes);
            return bytes;
        }
        catch (FileNotFoundException) when (allowMissing) { return null; }
        catch (DirectoryNotFoundException) when (allowMissing) { return null; }
    }

    private static void EnsureSafePath(string path)
    {
        var current = Path.GetFullPath(path);
        while (!string.IsNullOrEmpty(current))
        {
            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Reparse paths are not supported.");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
            var parent = Path.GetDirectoryName(current);
            if (parent == current) break;
            current = parent;
        }
    }
}
