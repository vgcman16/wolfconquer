using System.Globalization;

namespace WolfConquer.Core.World;

// Pure native Player security state, separate from the runtime issuer of IsCheckedPass.
// Invalid present storage is fenced rather than taking native ReadLine's password=0 fallback.
internal sealed record LegacySecondaryPasswordState(uint Password, uint OnReset, DateTime ResetAt)
{
    internal static bool TryRead(IReadOnlyDictionary<string, string> attrs,
        out LegacySecondaryPasswordState state, out string reason)
    {
        ArgumentNullException.ThrowIfNull(attrs);
        state = new(0, 0, default);
        reason = string.Empty;
        string? text = null;
        var count = 0;
        foreach (var (key, value) in attrs)
        {
            if (!key.Equals("SecurityPass", StringComparison.OrdinalIgnoreCase)) continue;
            text = value;
            count++;
        }
        if (count == 0) return true;
        if (count != 1 || text is null)
        { reason = "secondaryPasswordStorageAmbiguous"; return false; }
        var fields = text.Split(',');
        if ((fields.Length != 3 && (fields.Length != 4 || fields[3].Length != 0)) ||
            !uint.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var password) ||
            !uint.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var onReset))
        { reason = "secondaryPasswordStorageInvalid"; return false; }
        var resetAt = default(DateTime);
        if (onReset == 1)
        {
            if (!long.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var binary))
            { reason = "secondaryPasswordResetTimeInvalid"; return false; }
            try { resetAt = DateTime.FromBinary(binary); }
            catch (ArgumentException)
            { reason = "secondaryPasswordResetTimeInvalid"; return false; }
        }
        state = new(password, onReset, resetAt);
        return true;
    }

    // Native ServerDatabase.LoadSecurityPassword checks strictly 'now > deadline' only at load.
    internal LegacySecondaryPasswordState LoadAt(DateTime localNow, out bool expired)
    {
        expired = false;
        if (OnReset != 1) return this with { ResetAt = default };
        if (localNow <= ResetAt) return this;
        expired = true;
        return this with { Password = 0, OnReset = 0 };
    }

    // DBActions.WriteLine appends its separator after EVERY value, including the final ticks field.
    internal string ToLegacyText() => Password.ToString(CultureInfo.InvariantCulture) + "," +
        OnReset.ToString(CultureInfo.InvariantCulture) + "," + ResetAt.Ticks.ToString(CultureInfo.InvariantCulture) + ",";

    internal LegacySecondaryPasswordTransition? Apply(
        LegacySecondaryPasswordPacket packet, bool wasVerified, DateTime localNow)
    {
        var success = new LegacySecondaryPasswordPacket(5, 257, 0);
        switch (packet.Action)
        {
            case 1 when Password != 0:
                return Password == packet.NewPassword
                    ? new(this, true, success, "Secondary verified!")
                    : new(this, wasVerified, new(6, 0, 0), string.Empty);
            case 2 when Password != 0:
                var resetting = this with { OnReset = 1, ResetAt = localNow.AddDays(7) };
                return new(resetting, wasVerified, success,
                    "Your secondary password will be removed on " + resetting.ResetAt.ToString("d/M/yyyy (H:mm)") + ".");
            case 3 when Password == 0:
            case 4 when Password == 0:
                return new(this with { Password = packet.NewPassword }, wasVerified, success,
                    "Successfully set! Please remember your secondary password");
            case 4 when packet.OldPassword == Password:
                return new(this with { Password = packet.NewPassword, OnReset = 0 }, wasVerified, success,
                    "Successfully modified! Please remember your secondary password");
            case 4:
                return new(this, wasVerified, new(6, 1, 0), string.Empty);
            default:
                return null;
        }
    }
}

internal sealed record LegacySecondaryPasswordTransition(
    LegacySecondaryPasswordState State, bool IsChecked, LegacySecondaryPasswordPacket Reply, string Message);
