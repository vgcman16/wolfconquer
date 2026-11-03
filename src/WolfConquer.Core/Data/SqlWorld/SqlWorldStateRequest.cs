namespace WolfConquer.Core.Data.SqlWorld;

public enum SqlWorldStateOperationKind : byte { Create = 1, Save = 2, SavePair = 3 }

public readonly record struct SqlWorldStateChange(SqlWorldStateImage Image, long ExpectedRevision);

/// <summary>Immutable bounded storage request, not gameplay permission or a committed result.</summary>
public sealed partial class SqlWorldStateRequest
{
    public const int MaximumDescriptorBytes = 65 * 1024 * 1024;
    private readonly SqlWorldStateChange[] changes;
    private readonly byte[] descriptor;
    private readonly byte[] descriptorHash;

    private SqlWorldStateRequest(Guid operationId, SqlWorldStateOperationKind kind, SqlWorldStateChange[] changes,
        byte[] descriptor, byte[] descriptorHash)
    { OperationId = operationId; Kind = kind; this.changes = changes; this.descriptor = descriptor; this.descriptorHash = descriptorHash; }

    public Guid OperationId { get; }
    public SqlWorldStateOperationKind Kind { get; }
    public ReadOnlySpan<SqlWorldStateChange> Changes => changes;
    public byte[] CopyDescriptorBytes() => descriptor.ToArray();
    public byte[] CopyDescriptorHash() => descriptorHash.ToArray();

    public static bool TryCreate(Guid operationId, SqlWorldStateOperationKind kind, ReadOnlySpan<SqlWorldStateChange> changes,
        out SqlWorldStateRequest? request, out SqlWorldStateFailure failure)
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
