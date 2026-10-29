using MySql.Data.MySqlClient;
using System.Data;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeDialogExistsSafetyTests
{
    // Unsupported option parsing is a real provider refusal before any connection Open.
    private const string UnsupportedConfiguration = "synthetic_account_exists_unsupported_option=1";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData("\r\n")]
    [InlineData("\u2003")]
    public void InvalidRequiredUsernameRejectsBeforeConsultingSqlConfiguration(string? username)
    {
        using var restore = new RestoreConfiguration();
        LegacyNativeDataHolder.ConnectionString = UnsupportedConfiguration;

        ArgumentException error = Assert.ThrowsAny<ArgumentException>(() => LegacyNativeDialog.Exists(username!));

        Assert.Equal("username", error.ParamName);
        if (username is null) Assert.IsType<ArgumentNullException>(error);
        else Assert.IsType<ArgumentException>(error);
        Assert.Equal(UnsupportedConfiguration, LegacyNativeDataHolder.ConnectionString);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData("\r\n")]
    public void ActualLookupPropagatesMissingConfigurationInsteadOfReportingAbsence(string? missing)
    {
        using var restore = new RestoreConfiguration();
        LegacyNativeDataHolder.ConnectionString = missing;

        var error = Assert.Throws<InvalidOperationException>(() => LegacyNativeDialog.Exists("synthetic O'Brien\\中文"));

        Assert.Equal("Reader requires an explicitly configured database connection string.", error.Message);
        Assert.Same(missing, LegacyNativeDataHolder.ConnectionString);
    }

    [Fact]
    public void ActualLookupPropagatesTheRealProviderConfigurationRefusalBeforeOpen()
    {
        // Both calls parse a synthetic unsupported option. Neither can reach socket/DB I/O.
        var provider = Assert.Throws<ArgumentException>(() => new MySqlConnectionStringBuilder(UnsupportedConfiguration));
        using var restore = new RestoreConfiguration();
        LegacyNativeDataHolder.ConnectionString = UnsupportedConfiguration;

        var lookup = Assert.Throws<ArgumentException>(() => LegacyNativeDialog.Exists("synthetic_account"));

        Assert.Equal(provider.Message, lookup.Message);
        Assert.Equal(provider.ParamName, lookup.ParamName);
        Assert.Equal(UnsupportedConfiguration, LegacyNativeDataHolder.ConnectionString);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    public void RealBufferedSelectResultsRetainDistinctEmptyAndPresentRowMeanings(int rows, bool expected)
    {
        // This is the existing real buffered result owner, not an alternate Exists callback.
        // It establishes Read semantics only; actual successful SQL lookup remains unverified.
        var dataset = new DataSet();
        DataTable table = dataset.Tables.Add("table");
        table.Columns.Add("Username", typeof(string));
        for (int index = 0; index < rows; index++) table.Rows.Add("synthetic_account");
        using var reader = new LegacyNativeMySqlReader(dataset);

        Assert.Equal(rows, reader.NumberOfRows);
        Assert.Equal(expected, reader.Read());
        if (expected) Assert.Equal("synthetic_account", reader.ReadString("Username"));
    }

    private sealed class RestoreConfiguration : IDisposable
    {
        private readonly string? previous = LegacyNativeDataHolder.ConnectionString;
        public void Dispose() => LegacyNativeDataHolder.ConnectionString = previous;
    }
}
