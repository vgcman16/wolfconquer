using MySql.Data.MySqlClient;
using System.Data;
using Command = WolfConquer.Core.Data.LegacyNativeMySqlCommand;
using Kind = WolfConquer.Core.Data.LegacyNativeMySqlCommandType;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMySqlCommandBuilderTests
{
    [Fact]
    public void TypeResetStartsCleanInsertUpdateAndSelectState()
    {
        using var builder = new Command(Kind.UPDATE);
        builder.Update("accounts").Set("State", 1L).Where("ID", 3UL);
        using MySqlCommand retained = builder.CreateProviderCommand();
        builder.Type = Kind.INSERT;
        builder.Insert("owned_values").Insert("Only", "new");
        using MySqlCommand insert = builder.CreateProviderCommand();
        Assert.Equal("INSERT INTO `owned_values` (`Only`) VALUES (@p0)", insert.CommandText);
        Assert.Single(insert.Parameters.Cast<MySqlParameter>());
        AssertParameter(insert, 0, MySqlDbType.VarChar, "new");
        builder.Type = Kind.UPDATE;
        builder.Update("accounts").Set("State", 0L);
        using MySqlCommand update = builder.CreateProviderCommand();
        Assert.Equal("UPDATE `accounts` SET `State` = @p0", update.CommandText);
        AssertParameter(update, 0, MySqlDbType.Int64, 0L);
        builder.Type = Kind.SELECT;
        builder.Select("accounts").Where("ID", 7L);
        using MySqlCommand select = builder.CreateProviderCommand();
        Assert.Single(select.Parameters.Cast<MySqlParameter>());
        AssertParameter(select, 0, MySqlDbType.Int64, 7L);
        Assert.Equal(2, retained.Parameters.Count);
    }

    [Fact]
    public void RawAssignmentDropsEveryBindingAndRequiresExplicitFluentReset()
    {
        using var builder = new Command(Kind.INSERT);
        builder.Insert("owned_values").Insert("Secret", "previous");
        builder.Command = "SELECT 7";
        using MySqlCommand raw = builder.CreateProviderCommand();
        Assert.Equal("SELECT 7", raw.CommandText);
        Assert.Empty(raw.Parameters.Cast<MySqlParameter>());
        Assert.Throws<InvalidOperationException>(() => builder.Insert("Field", "no"));
        builder.Type = Kind.UPDATE;
        builder.Update("accounts").Set("State", 1L);
        using MySqlCommand reset = builder.CreateProviderCommand();
        AssertParameter(reset, 0, MySqlDbType.Int64, 1L);
        Assert.DoesNotContain("previous", reset.CommandText, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidTypeAndNullRawTextPreserveThePreviousPreparedCommand()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Command((Kind)int.MaxValue));
        using var builder = new Command(Kind.SELECT);
        builder.Select("accounts").Where("Username", "kept");
        string before = builder.Command;
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.Type = (Kind)int.MaxValue);
        Assert.Throws<ArgumentNullException>(() => builder.Command = null!);
        Assert.Equal(before, builder.Command);
        using MySqlCommand command = builder.CreateProviderCommand();
        AssertParameter(command, 0, MySqlDbType.VarChar, "kept");
    }

    [Theory]
    [InlineData("")]
    [InlineData("a` OR 1=1")]
    [InlineData("a.b")]
    [InlineData("a; DROP TABLE accounts")]
    [InlineData("a\0")]
    [InlineData("a b")]
    [InlineData("1name")]
    public void InvalidIdentifiersRejectWithoutChangingTextOrBindings(string name)
    {
        using var builder = new Command(Kind.UPDATE);
        builder.Update("accounts").Set("State", 1L);
        string before = builder.Command;
        Assert.Throws<ArgumentException>(() => builder.Set(name, "ignored"));
        Assert.Throws<ArgumentException>(() => builder.Where(name, "ignored"));
        Assert.Equal(before, builder.Command);
        using MySqlCommand command = builder.CreateProviderCommand();
        Assert.Single(command.Parameters.Cast<MySqlParameter>());
        using var select = new Command(Kind.SELECT);
        string template = select.Command;
        Assert.Throws<ArgumentException>(() => select.Select(name));
        Assert.Equal(template, select.Command);
        using var delete = new Command(Kind.DELETE);
        string deleteTemplate = delete.Command;
        Assert.Throws<ArgumentException>(() => delete.Delete("accounts", name, "ignored"));
        Assert.Equal(deleteTemplate, delete.Command);
    }

    [Theory]
    [InlineData("ID; DELETE FROM accounts")]
    [InlineData("ID DESC, State")]
    [InlineData("ID sideways")]
    [InlineData("ID DESC --")]
    public void InvalidOrderingCannotBecomeSqlSyntax(string order)
    {
        using var builder = new Command(Kind.SELECT);
        builder.Select("accounts").Where("ID", 1L);
        string before = builder.Command;
        Assert.Throws<ArgumentException>(() => builder.Order(order));
        Assert.Equal(before, builder.Command);
        using MySqlCommand command = builder.CreateProviderCommand();
        Assert.Single(command.Parameters.Cast<MySqlParameter>());
    }

    [Fact]
    public void NullValuesNonFiniteNumbersAndDuplicateFieldsDoNotPartiallyMutate()
    {
        using var update = new Command(Kind.UPDATE);
        update.Update("accounts").Set("State", 1L);
        string before = update.Command;
        Assert.Throws<ArgumentNullException>(() => update.Set("Value", (string)null!));
        Assert.Throws<ArgumentNullException>(() => update.Set("Value", (object)null!));
        Assert.Throws<ArgumentNullException>(() => update.Where("Value", (string)null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => update.Set("Value", double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => update.Set("Value", double.PositiveInfinity));
        Assert.Equal(before, update.Command);
        using MySqlCommand command = update.CreateProviderCommand();
        Assert.Single(command.Parameters.Cast<MySqlParameter>());
        using var insert = new Command(Kind.INSERT);
        insert.Insert("owned_values").Insert("Value", 1L);
        string insertBefore = insert.Command;
        Assert.Throws<ArgumentNullException>(() => insert.Insert("Other", (string)null!));
        Assert.Throws<ArgumentException>(() => insert.Insert("value", 2L));
        Assert.Equal(insertBefore, insert.Command);
        using MySqlCommand insertCommand = insert.CreateProviderCommand();
        Assert.Single(insertCommand.Parameters.Cast<MySqlParameter>());
    }

    [Fact]
    public void InvalidSequenceRejectsBeforeAddingAValue()
    {
        using var builder = new Command(Kind.SELECT);
        builder.Select("accounts");
        Assert.Throws<InvalidOperationException>(() => builder.Set("State", 1L));
        Assert.Throws<InvalidOperationException>(() => builder.And("ID", 1L));
        builder.Where("ID", 2L).Order("ID");
        string before = builder.Command;
        Assert.Throws<InvalidOperationException>(() => builder.Where("ID", 3L));
        Assert.Throws<InvalidOperationException>(() => builder.And("ID", 3L));
        Assert.Throws<InvalidOperationException>(() => builder.Order("State"));
        Assert.Equal(before, builder.Command);
        using MySqlCommand command = builder.CreateProviderCommand();
        Assert.Single(command.Parameters.Cast<MySqlParameter>());
    }

    [Fact]
    public void IncompleteCommandsFailDuringPreparationWithoutAnyDatabaseAccess()
    {
        foreach (Kind kind in Enum.GetValues<Kind>())
        {
            using var builder = new Command(kind);
            Assert.Throws<InvalidOperationException>(() => builder.CreateProviderCommand());
        }
        using var insert = new Command(Kind.INSERT);
        insert.Insert("owned_values");
        Assert.Throws<InvalidOperationException>(() => insert.CreateProviderCommand());
        using var update = new Command(Kind.UPDATE);
        update.Update("accounts");
        Assert.Throws<InvalidOperationException>(() => update.CreateProviderCommand());
        update.Command = " ";
        Assert.Throws<InvalidOperationException>(() => update.CreateProviderCommand());
    }

    [Fact]
    public void BuilderDisposalIsIdempotentAndDoesNotOwnProviderSnapshotsOrExternalConnections()
    {
        var builder = new Command(Kind.SELECT);
        builder.Select("accounts").Where("ID", 1L);
        using var connection = new MySqlConnection();
        bool connectionDisposed = false;
        connection.Disposed += (_, _) => connectionDisposed = true;
        using MySqlCommand command = builder.CreateProviderCommand(connection);
        bool commandDisposed = false;
        command.Disposed += (_, _) => commandDisposed = true;
        builder.Dispose();
        ((IDisposable)builder).Dispose();
        Assert.False(commandDisposed);
        Assert.Equal(1L, command.Parameters[0].Value);
        Assert.Throws<ObjectDisposedException>(() => builder.CreateProviderCommand());
        Assert.Throws<ObjectDisposedException>(() => _ = builder.Command);
        Assert.Throws<ObjectDisposedException>(() => builder.Type = Kind.UPDATE);
        Assert.Throws<ObjectDisposedException>(() => builder.Command = "SELECT 7");
        command.Dispose();
        Assert.True(commandDisposed);
        Assert.False(connectionDisposed);
        Assert.Equal(ConnectionState.Closed, connection.State);
    }
}
