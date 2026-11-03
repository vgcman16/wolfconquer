namespace WolfConquer.Core.Data.SqlWorld;

/// <summary>A real storage-connection acquisition, not authentication or native publication.</summary>
public sealed class SqlWorldBundleCustodyAcquireResult
{
    internal SqlWorldBundleCustodyAcquireResult(SqlWorldBundleCustody? custody, SqlWorldStateFailure failure)
    {
        if (!Enum.IsDefined(failure) || (custody is not null) != (failure == SqlWorldStateFailure.None))
            throw new ArgumentException("SQL bundle custody acquisition result shape is invalid.");
        Custody = custody;
        Failure = failure;
    }

    public bool IsAcquired => Custody is not null;
    public SqlWorldBundleCustody? Custody { get; }
    public SqlWorldStateFailure Failure { get; }
}
