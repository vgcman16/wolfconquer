namespace WolfConquer.Core.Data.SqlWorld;

public sealed class SqlWorldStateBundleRecord
{
    public const long MaximumRevision = long.MaxValue;
    private SqlWorldStateBundleRecord(SqlWorldStateBundle image, long revision) { Image = image; Revision = revision; }
    public SqlWorldStateBundle Image { get; }
    public long Revision { get; }

    public static bool TryCreate(SqlWorldStateBundle? image, long revision,
        out SqlWorldStateBundleRecord? record, out SqlWorldStateFailure failure)
    {
        record = null;
        if (image is null) { failure = SqlWorldStateFailure.InvalidCharacterImage; return false; }
        if (revision <= 0) { failure = SqlWorldStateFailure.InvalidRevision; return false; }
        record = new(image, revision); failure = SqlWorldStateFailure.None; return true;
    }
}

public sealed class SqlWorldStateBundleReadResult
{
    internal SqlWorldStateBundleReadResult(SqlWorldStateReadStatus status, SqlWorldStateBundleRecord? record,
        SqlWorldStateFailure failure)
    {
        var valid = status switch
        {
            SqlWorldStateReadStatus.Found => record is not null && failure == SqlWorldStateFailure.None,
            SqlWorldStateReadStatus.Missing => record is null && failure == SqlWorldStateFailure.MissingState,
            SqlWorldStateReadStatus.Invalid => record is null && failure is not (SqlWorldStateFailure.None or SqlWorldStateFailure.MissingState),
            _ => false,
        };
        if (!valid || !Enum.IsDefined(failure)) throw new ArgumentException("SQL complete world read result shape is invalid.");
        Status = status; Record = record; Failure = failure;
    }
    public SqlWorldStateReadStatus Status { get; }
    public SqlWorldStateBundleRecord? Record { get; }
    public SqlWorldStateFailure Failure { get; }
}

/// <summary>Historical complete SQL outcomes, never current native ownership or publication receipts.</summary>
public sealed class SqlWorldStateBundleCommitResult
{
    private readonly SqlWorldStateBundleRecord[] records;

    internal SqlWorldStateBundleCommitResult(SqlWorldStateCommitStatus status, SqlWorldStateFailure failure,
        ReadOnlySpan<SqlWorldStateBundleRecord> records)
    {
        var valid = status switch
        {
            SqlWorldStateCommitStatus.Refused => failure != SqlWorldStateFailure.None && records.Length == 0,
            SqlWorldStateCommitStatus.Committed or SqlWorldStateCommitStatus.Replayed =>
                failure == SqlWorldStateFailure.None && records.Length is >= 1 and <= 2,
            _ => false,
        };
        if (!valid || !Enum.IsDefined(failure)) throw new ArgumentException("SQL complete world commit result shape is invalid.");
        this.records = records.ToArray();
        var accounts = new HashSet<long>(); var characters = new HashSet<uint>(); var uids = new HashSet<uint>();
        foreach (var record in this.records)
        {
            if (record is null || !accounts.Add(record.Image.AccountId) || !characters.Add(record.Image.CharacterUid))
                throw new ArgumentException("SQL complete world result participant identities are invalid.");
            foreach (var uid in record.Image.ItemUids)
                if (!uids.Add(uid)) throw new ArgumentException("SQL complete world result item identities overlap.");
        }
        Status = status; Failure = failure;
    }
    public SqlWorldStateCommitStatus Status { get; }
    public SqlWorldStateFailure Failure { get; }
    public ReadOnlySpan<SqlWorldStateBundleRecord> Records => records;
}
