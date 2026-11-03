using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeFilePrefixValues
{
    private sealed class Reader(LegacyNativeItemCounterShellIni ini, CharacterSnapshotFileBaseline.Snapshot baseline)
    {
        internal string Reason { get; private set; } = "normalPrefixOptionalValueInvalid";

        internal bool Text(string key, string fallback, out string value)
        {
            value = fallback;
            if (!baseline.Fields.TryGetValue(key, out var raw)) return true;
            // Win32 terminates at NUL; it must not hide a malformed present tail.
            if (raw is null || raw.Contains('\0')) { Reason = "normalPrefixFieldControlValue"; return false; }
            var shortRead = ini.ReadString("Character", key, fallback, 255);
            var probe = ini.ReadString("Character", key, fallback, 256);
            if (!string.Equals(shortRead, probe, StringComparison.Ordinal))
            { Reason = "normalPrefixFieldTruncated"; return false; }
            // Preserve ordinary boundary whitespace, but never admit hidden
            // control data inside the actual Win32-unquoted value.
            if (shortRead.Trim().Any(char.IsControl))
            { Reason = "normalPrefixFieldControlValue"; return false; }
            value = shortRead;
            return true;
        }

        internal bool Bool(string key, bool fallback, out bool value)
        {
            value = fallback;
            if (!Text(key, fallback ? "True" : "False", out var raw)) return false;
            return bool.TryParse(raw, out value);
        }

        internal bool UInt(string key, out uint value)
        {
            value = 0;
            return Text(key, "0", out var raw) && uint.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        internal bool Byte(string key, out byte value)
        {
            value = 0;
            return Text(key, "0", out var raw) && byte.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        internal bool UShort(string key, ushort fallback, out ushort value)
        {
            value = fallback;
            return Text(key, fallback.ToString(CultureInfo.InvariantCulture), out var raw) &&
                ushort.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }
    }
}
