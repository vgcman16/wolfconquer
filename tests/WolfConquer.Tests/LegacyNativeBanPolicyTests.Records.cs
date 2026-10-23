using System.Globalization;
using System.Reflection;
using IpBan = WolfConquer.Core.Data.LegacyNativeSystemBanned;
using AccountBan = WolfConquer.Core.Data.LegacyNativeSystemBannedAccount;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeBanPolicyTests
{
    [Fact]
    public void IpRecordHasTheOriginalMutableFieldsAndZeroDefaults()
    {
        var record = new IpBan.Client();
        Assert.Equal("", record.IP);
        Assert.Equal(0u, record.Hours);
        Assert.Equal(0L, record.StartBan);
        Assert.Equal("/0/0/", record.ToString());
        var fields = typeof(IpBan.Client).GetFields(BindingFlags.Instance | BindingFlags.Public)
            .OrderBy(field => field.MetadataToken).ToArray();
        Assert.Equal(new[] { "IP", "Hours", "StartBan" }, fields.Select(field => field.Name));
        Assert.Equal(new[] { typeof(string), typeof(uint), typeof(long) }, fields.Select(field => field.FieldType));
        Assert.All(fields, field => Assert.False(field.IsInitOnly));
    }

    [Fact]
    public void AccountRecordNameIsGenuinelyUnbornAndItsFormatterFaultsRatherThanRepairingIt()
    {
        var record = new AccountBan.Client();
        Assert.Equal(0u, record.UID);
        Assert.Null(record.Name);
        Assert.Equal(0u, record.Hours);
        Assert.Equal(0L, record.StartBan);
        Assert.Equal("", record.Reason);
        Assert.Throws<NullReferenceException>(() => record.ToString());
        var fields = typeof(AccountBan.Client).GetFields(BindingFlags.Instance | BindingFlags.Public)
            .OrderBy(field => field.MetadataToken).ToArray();
        Assert.Equal(new[] { "UID", "Name", "Hours", "StartBan", "Reason" }, fields.Select(field => field.Name));
        Assert.Equal(new[] { typeof(uint), typeof(string), typeof(uint), typeof(long), typeof(string) },
            fields.Select(field => field.FieldType));
        Assert.All(fields, field => Assert.False(field.IsInitOnly));
    }

    [Fact]
    public void WholeFormattersKeepWidthsUnescapedSlashesAndTheTrailingSeparator()
    {
        using var state = new State();
        Assert.Equal("ip/part/4294967295/-9223372036854775808/", Ip("ip/part", uint.MaxValue, long.MinValue).ToString());
        Assert.Equal("4294967295/4294967295/9223372036854775807/Wolf/part/reason/part/",
            Account(uint.MaxValue, "Wolf/part", uint.MaxValue, long.MaxValue, "reason/part").ToString());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AccountFormatterDoesNotRepairEitherNullableString(bool name)
    {
        var record = Account(17);
        if (name) record.Name = null; else record.Reason = null;
        Assert.Throws<NullReferenceException>(() => record.ToString());
    }

    [Fact]
    public void IpFormatterDoesNotRepairANullIp()
    {
        var record = new IpBan.Client { IP = null };
        Assert.Throws<NullReferenceException>(() => record.ToString());
    }

    [Fact]
    public void FormatterUsesTheRealCurrentCultureForSignedTicks()
    {
        using var state = new State();
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.NegativeSign = "~";
        CultureInfo.CurrentCulture = culture;
        Assert.Equal("ip/4/~17/", Ip("ip", 4, -17).ToString());
        Assert.Equal("2/3/~19/Wolf//", Account(2, hours: 3, start: -19).ToString());
    }
}
