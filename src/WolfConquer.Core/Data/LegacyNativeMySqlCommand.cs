using MySql.Data.MySqlClient;
using System.Text;

namespace WolfConquer.Core.Data;

// This builder owns SQL structure and values; execution owns a fresh provider snapshot.
internal partial class LegacyNativeMySqlCommand : IDisposable
{
    private LegacyNativeMySqlCommandType _type;
    protected StringBuilder _command = new();
    private readonly List<BoundValue> _values = new();
    private readonly List<string> _insertFields = new();
    private bool _hasSet;
    private bool _hasWhere;
    private bool _hasOrder;
    private bool _rawSql;
    private bool _disposed;

    public LegacyNativeMySqlCommand(LegacyNativeMySqlCommandType type) => Reset(type);

    public LegacyNativeMySqlCommandType Type
    {
        get => _type;
        set { ThrowIfDisposed(); Reset(value); }
    }

    // Raw SQL is an explicit operator-owned API. Resetting it discards all bindings.
    public string Command
    {
        get { ThrowIfDisposed(); return RenderCommand(); }
        set
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(value);
            ClearState();
            _command = new StringBuilder(value);
            _rawSql = true;
        }
    }

    public LegacyNativeMySqlCommand Select(string table) => Table(table, LegacyNativeMySqlCommandType.SELECT);
    public LegacyNativeMySqlCommand Count(string table) => Table(table, LegacyNativeMySqlCommandType.COUNT);
    public LegacyNativeMySqlCommand Update(string table) => Table(table, LegacyNativeMySqlCommandType.UPDATE);
    public LegacyNativeMySqlCommand Insert(string table) => Table(table, LegacyNativeMySqlCommandType.INSERT);

    public LegacyNativeMySqlCommand Delete(string table, string column, string value) => DeleteValue(table, column, value);
    public LegacyNativeMySqlCommand Delete(string table, string column, long value) => DeleteValue(table, column, value);
    public LegacyNativeMySqlCommand Delete(string table, string column, ulong value) => DeleteValue(table, column, value);
    public LegacyNativeMySqlCommand Delete(string table, string column, bool value) => DeleteValue(table, column, value);

    public LegacyNativeMySqlCommand Set(string column, long value) => SetValue(column, value);
    public LegacyNativeMySqlCommand Set(string column, double value) => SetValue(column, value);
    public LegacyNativeMySqlCommand Set(string column, ulong value) => SetValue(column, value);
    public LegacyNativeMySqlCommand Set(string column, string value) => SetValue(column, value);
    public LegacyNativeMySqlCommand Set(string column, bool value) => SetValue(column, value);
    public LegacyNativeMySqlCommand Set(string column, object value) => SetValue(column, value);

    public LegacyNativeMySqlCommand Insert(string field, long value) => InsertValue(field, value);
    public LegacyNativeMySqlCommand Insert(string field, ulong value) => InsertValue(field, value);
    public LegacyNativeMySqlCommand Insert(string field, bool value) => InsertValue(field, value);
    public LegacyNativeMySqlCommand Insert(string field, string value) => InsertValue(field, value);

    public LegacyNativeMySqlCommand Where(string column, long value) => Predicate("WHERE", column, value);
    public LegacyNativeMySqlCommand Where(string column, ulong value) => Predicate("WHERE", column, value);
    public LegacyNativeMySqlCommand Where(string column, string value) => Predicate("WHERE", column, value);
    public LegacyNativeMySqlCommand Where(string column, bool value) => Predicate("WHERE", column, value);
    public LegacyNativeMySqlCommand Where(string column, long value, bool greater) => Predicate("WHERE", column, value, greater ? ">" : "<");

    public LegacyNativeMySqlCommand And(string column, long value) => Predicate("AND", column, value);
    public LegacyNativeMySqlCommand And(string column, ulong value) => Predicate("AND", column, value);
    public LegacyNativeMySqlCommand And(string column, string value) => Predicate("AND", column, value);
    public LegacyNativeMySqlCommand And(string column, bool value) => Predicate("AND", column, value);
    public LegacyNativeMySqlCommand And(string column, long value, bool greater) => Predicate("AND", column, value, greater ? ">" : "<");

    public LegacyNativeMySqlCommand Or(string column, long value) => Predicate("OR", column, value);
    public LegacyNativeMySqlCommand Or(string column, ulong value) => Predicate("OR", column, value);
    public LegacyNativeMySqlCommand Or(string column, string value) => Predicate("OR", column, value);
    public LegacyNativeMySqlCommand Or(string column, bool value) => Predicate("OR", column, value);

    public LegacyNativeMySqlCommand Order(string column)
    {
        RequireKind(LegacyNativeMySqlCommandType.SELECT, LegacyNativeMySqlCommandType.COUNT);
        if (_hasOrder) throw new InvalidOperationException("ORDER BY is already specified.");
        ArgumentNullException.ThrowIfNull(column);
        string[] parts = column.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length is < 1 or > 2 || (parts.Length == 2 &&
            !parts[1].Equals("ASC", StringComparison.OrdinalIgnoreCase) &&
            !parts[1].Equals("DESC", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Order requires a column and optional ASC or DESC.", nameof(column));
        string name = QuoteIdentifier(parts[0]);
        AppendClause("ORDER BY " + name + (parts.Length == 2 ? " " + parts[1].ToUpperInvariant() : ""));
        _hasOrder = true;
        return this;
    }

    public LegacyNativeMySqlReader CreateReader() => new(this);

    public void Dispose()
    {
        if (_disposed) return;
        ClearState();
        _command.Clear();
        _disposed = true;
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
    private sealed record BoundValue(MySqlDbType Type, object Value);
}
