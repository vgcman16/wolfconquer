namespace WolfConquer.Core.Data.SqlWorld;

public readonly record struct SqlWorldStateBundleChange(SqlWorldStateBundle Image, long ExpectedRevision);

/// <summary>Immutable complete storage request, not a gameplay or native ownership receipt.</summary>
public sealed partial class SqlWorldStateBundleRequest
{
    public const int MaximumDescriptorBytes = 65 * 1024 * 1024;
    private readonly SqlWorldStateBundleChange[] changes;
    private readonly byte[] descriptor;
    private readonly byte[] descriptorHash;

    private SqlWorldStateBundleRequest(Guid operationId, SqlWorldStateOperationKind kind,
        SqlWorldStateBundleChange[] changes, byte[] descriptor, byte[] descriptorHash)
    {
        OperationId = operationId;
        Kind = kind;
        this.changes = changes;
        this.descriptor = descriptor;
        this.descriptorHash = descriptorHash;
    }

    public Guid OperationId { get; }
    public SqlWorldStateOperationKind Kind { get; }
    public ReadOnlySpan<SqlWorldStateBundleChange> Changes => changes;
    public byte[] CopyDescriptorBytes() => descriptor.ToArray();
    public byte[] CopyDescriptorHash() => descriptorHash.ToArray();

    public static bool TryCreate(Guid operationId, SqlWorldStateOperationKind kind,
        ReadOnlySpan<SqlWorldStateBundleChange> changes, out SqlWorldStateBundleRequest? request,
        out SqlWorldStateFailure failure)
    {
        request = null;
        if (operationId == Guid.Empty) { failure = SqlWorldStateFailure.InvalidOperationId; return false; }
        if (kind is not (SqlWorldStateOperationKind.Create or SqlWorldStateOperationKind.Save or SqlWorldStateOperationKind.SavePair))
        { failure = SqlWorldStateFailure.InvalidOperationKind; return false; }
        if (changes.Length != (kind == SqlWorldStateOperationKind.SavePair ? 2 : 1))
        { failure = SqlWorldStateFailure.ParticipantCount; return false; }
        var captured = changes.ToArray();
        foreach (var change in captured)
        {
            if (change.Image is null) { failure = SqlWorldStateFailure.InvalidCharacterImage; return false; }
            if (kind == SqlWorldStateOperationKind.Create ? change.ExpectedRevision != 0 :
                change.ExpectedRevision is <= 0 or >= SqlWorldStateRecord.MaximumRevision)
            { failure = SqlWorldStateFailure.InvalidRevision; return false; }
        }
        Array.Sort(captured, static (left, right) => left.Image.AccountId.CompareTo(right.Image.AccountId));
        if (captured.Length == 2 && (captured[0].Image.AccountId == captured[1].Image.AccountId ||
            captured[0].Image.CharacterUid == captured[1].Image.CharacterUid))
        { failure = SqlWorldStateFailure.ParticipantIdentityConflict; return false; }
        var uids = new HashSet<uint>();
        foreach (var change in captured)
            foreach (var uid in change.Image.ItemUids)
                if (!uids.Add(uid)) { failure = SqlWorldStateFailure.DuplicateItemUid; return false; }
        if (!TryBuildDescriptor(operationId, kind, captured, out var descriptor, out var hash))
        { failure = SqlWorldStateFailure.DescriptorBounds; return false; }
        request = new(operationId, kind, captured, descriptor, hash);
        failure = SqlWorldStateFailure.None;
        return true;
    }
}
