namespace WolfConquer.Core.Data.SqlWorld;

public enum SqlWorldStateFailure
{
    None, InvalidAccountId, InvalidCharacterUid, ImageBounds, InvalidCharacterImage, InvalidFunds, InvalidItemImage,
    InvalidOperationId, InvalidOperationKind, ParticipantCount, ParticipantIdentityConflict, InvalidRevision,
    DescriptorBounds, DuplicateItemUid, MissingState, StateExists, RevisionConflict, CharacterIdentityConflict,
    ItemUidConflict, OperationConflict, InvalidStoredState,
}

public sealed class SqlWorldStateRecord
{
    public const long MaximumRevision = long.MaxValue;
    private SqlWorldStateRecord(SqlWorldStateImage image, long revision) { Image = image; Revision = revision; }
    public SqlWorldStateImage Image { get; }
    public long Revision { get; }

    public static bool TryCreate(SqlWorldStateImage? image, long revision,
        out SqlWorldStateRecord? record, out SqlWorldStateFailure failure)
    {
        record = null;
        if (image is null) { failure = SqlWorldStateFailure.InvalidCharacterImage; return false; }
        if (revision <= 0) { failure = SqlWorldStateFailure.InvalidRevision; return false; }
        record = new(image, revision); failure = SqlWorldStateFailure.None; return true;
    }
}

public enum SqlWorldStateReadStatus { Found, Missing, Invalid }

public sealed class SqlWorldStateReadResult
{
    internal SqlWorldStateReadResult(SqlWorldStateReadStatus status, SqlWorldStateRecord? record, SqlWorldStateFailure failure)
    {
        var valid = status switch
        {
            SqlWorldStateReadStatus.Found => record is not null && failure == SqlWorldStateFailure.None,
            SqlWorldStateReadStatus.Missing => record is null && failure == SqlWorldStateFailure.MissingState,
            SqlWorldStateReadStatus.Invalid => record is null && failure is not (SqlWorldStateFailure.None or SqlWorldStateFailure.MissingState),
            _ => false,
        };
        if (!valid || !Enum.IsDefined(failure))
            throw new ArgumentException("SQL world read result shape is invalid.");
        Status = status; Record = record; Failure = failure;
    }
    public SqlWorldStateReadStatus Status { get; }
    public SqlWorldStateRecord? Record { get; }
    public SqlWorldStateFailure Failure { get; }
}

public enum SqlWorldStateCommitStatus { Committed, Replayed, Refused }

/// <summary>Committed/replayed records are historical storage outcomes, never current native ownership receipts.</summary>
public sealed class SqlWorldStateCommitResult
{
    private readonly SqlWorldStateRecord[] records;
    internal SqlWorldStateCommitResult(SqlWorldStateCommitStatus status, SqlWorldStateFailure failure,
        ReadOnlySpan<SqlWorldStateRecord> records)
    {
        var valid = status switch
        {
            SqlWorldStateCommitStatus.Refused => failure != SqlWorldStateFailure.None && records.Length == 0,
            SqlWorldStateCommitStatus.Committed or SqlWorldStateCommitStatus.Replayed =>
                failure == SqlWorldStateFailure.None && records.Length is >= 1 and <= 2,
            _ => false,
        };
        if (!valid || !Enum.IsDefined(failure))
            throw new ArgumentException("SQL world commit result shape is invalid.");
        this.records = records.ToArray();
        var accounts = new HashSet<long>(); var characters = new HashSet<uint>(); var uids = new HashSet<uint>();
        foreach (var record in this.records)
        {
            if (record is null || !accounts.Add(record.Image.AccountId) || !characters.Add(record.Image.CharacterUid))
                throw new ArgumentException("SQL world result participant identities are invalid.");
            foreach (var uid in record.Image.ItemUids)
                if (!uids.Add(uid)) throw new ArgumentException("SQL world result item identities overlap.");
        }
        Status = status; Failure = failure;
    }
    public SqlWorldStateCommitStatus Status { get; }
    public SqlWorldStateFailure Failure { get; }
    public ReadOnlySpan<SqlWorldStateRecord> Records => records;
}
