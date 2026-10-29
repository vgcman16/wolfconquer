using MySql.Data.MySqlClient;
using System.Globalization;
using System.Text;

namespace WolfConquer.Core.Data;

internal partial class LegacyNativeMySqlCommand
{
    private void Reset(LegacyNativeMySqlCommandType type)
    {
        string template = type switch
        {
            LegacyNativeMySqlCommandType.SELECT => "SELECT * FROM <R>",
            LegacyNativeMySqlCommandType.UPDATE => "UPDATE <R> SET ",
            LegacyNativeMySqlCommandType.INSERT => "INSERT INTO <R> (<F>) VALUES (<V>)",
            LegacyNativeMySqlCommandType.DELETE => "DELETE FROM <R> WHERE <C> = <V>",
            LegacyNativeMySqlCommandType.COUNT => "SELECT count(*) FROM <R>",
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
        ClearState();
        _type = type;
        _command = new StringBuilder(template);
    }

    private void ClearState()
    {
        _values.Clear();
        _insertFields.Clear();
        _hasSet = _hasWhere = _hasOrder = _rawSql = false;
    }

    private void RequireKind(params LegacyNativeMySqlCommandType[] kinds)
    {
        ThrowIfDisposed();
        if (_rawSql) throw new InvalidOperationException("Reset the type before using the fluent builder after raw SQL assignment.");
        if (!kinds.Contains(_type)) throw new InvalidOperationException("Operation does not match the command type.");
    }

    private static string QuoteIdentifier(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (name.Length == 0 || !(char.IsAsciiLetter(name[0]) || name[0] == '_') ||
            name.Any(c => !(char.IsAsciiLetterOrDigit(c) || c == '_')))
            throw new ArgumentException("A simple SQL identifier is required.", nameof(name));
        return "`" + name + "`";
    }

    private LegacyNativeMySqlCommand Table(string table, LegacyNativeMySqlCommandType type)
    {
        RequireKind(type);
        string name = QuoteIdentifier(table);
        if (!_command.ToString().Contains("<R>", StringComparison.Ordinal))
            throw new InvalidOperationException("The table is already specified.");
        _command.Replace("<R>", name);
        return this;
    }

    private LegacyNativeMySqlCommand DeleteValue(string table, string column, object value)
    {
        RequireKind(LegacyNativeMySqlCommandType.DELETE);
        string tableName = QuoteIdentifier(table);
        string columnName = QuoteIdentifier(column);
        BoundValue bound = PrepareValue(value);
        if (_values.Count != 0 || !_command.ToString().Contains("<R>", StringComparison.Ordinal))
            throw new InvalidOperationException("The delete condition is already specified.");
        string parameter = Bind(bound);
        _command.Replace("<R>", tableName).Replace("<C>", columnName).Replace("<V>", parameter);
        _hasWhere = true;
        return this;
    }

    private LegacyNativeMySqlCommand SetValue(string column, object value)
    {
        RequireKind(LegacyNativeMySqlCommandType.UPDATE);
        string name = QuoteIdentifier(column);
        BoundValue bound = PrepareValue(value);
        if (_hasWhere || _hasOrder) throw new InvalidOperationException("SET must precede predicates and ordering.");
        string parameter = Bind(bound);
        if (_hasSet) _command.Append(", ");
        _command.Append(name).Append(" = ").Append(parameter);
        _hasSet = true;
        return this;
    }

    private LegacyNativeMySqlCommand InsertValue(string field, object value)
    {
        RequireKind(LegacyNativeMySqlCommandType.INSERT);
        string name = QuoteIdentifier(field);
        BoundValue bound = PrepareValue(value);
        if (_rawSql) throw new InvalidOperationException("Reset the type before building an INSERT.");
        if (_insertFields.Contains(name, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("An INSERT field cannot appear twice.", nameof(field));
        _insertFields.Add(name);
        Bind(bound);
        return this;
    }

    private LegacyNativeMySqlCommand Predicate(string join, string column, object value, string comparison = "=")
    {
        RequireKind(LegacyNativeMySqlCommandType.SELECT, LegacyNativeMySqlCommandType.COUNT,
            LegacyNativeMySqlCommandType.UPDATE, LegacyNativeMySqlCommandType.DELETE);
        string name = QuoteIdentifier(column);
        BoundValue bound = PrepareValue(value);
        if (_hasOrder || (join == "WHERE" ? _hasWhere : !_hasWhere) ||
            (_type == LegacyNativeMySqlCommandType.UPDATE && !_hasSet))
            throw new InvalidOperationException("The predicate is out of order.");
        string parameter = Bind(bound);
        AppendClause(join + " " + name + " " + comparison + " " + parameter);
        _hasWhere = true;
        return this;
    }

    private void AppendClause(string clause)
    {
        if (_command.Length != 0 && !char.IsWhiteSpace(_command[^1])) _command.Append(' ');
        _command.Append(clause);
    }

    private string Bind(BoundValue value)
    {
        string name = "@p" + _values.Count.ToString(CultureInfo.InvariantCulture);
        _values.Add(value);
        return name;
    }

    private string RenderCommand()
    {
        string text = _command.ToString();
        if (_type != LegacyNativeMySqlCommandType.INSERT || _rawSql || _insertFields.Count == 0) return text;
        string parameters = string.Join(", ", Enumerable.Range(0, _values.Count)
            .Select(index => "@p" + index.ToString(CultureInfo.InvariantCulture)));
        return text.Replace("<F>", string.Join(", ", _insertFields), StringComparison.Ordinal)
            .Replace("<V>", parameters, StringComparison.Ordinal);
    }

    private static BoundValue PrepareValue(object value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value switch
        {
            bool flag => new(MySqlDbType.Byte, (byte)(flag ? 1 : 0)),
            string text => new(MySqlDbType.VarChar, text),
            sbyte number => new(MySqlDbType.Byte, number),
            byte number => new(MySqlDbType.UByte, number),
            short number => new(MySqlDbType.Int16, number),
            ushort number => new(MySqlDbType.UInt16, number),
            int number => new(MySqlDbType.Int32, number),
            uint number => new(MySqlDbType.UInt32, number),
            long number => new(MySqlDbType.Int64, number),
            ulong number => new(MySqlDbType.UInt64, number),
            float number when float.IsFinite(number) => new(MySqlDbType.Float, number),
            double number when double.IsFinite(number) => new(MySqlDbType.Double, number),
            float or double => throw new ArgumentOutOfRangeException(nameof(value), "A finite numeric value is required."),
            decimal number => new(MySqlDbType.Decimal, number),
            _ => new(MySqlDbType.VarChar, value.ToString() ?? throw new ArgumentException("A value string is required.", nameof(value)))
        };
    }
}
