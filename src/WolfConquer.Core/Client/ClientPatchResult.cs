namespace WolfConquer.Core.Client;

public sealed record ClientPatchResult(IReadOnlyList<ClientPatchFileResult> Files)
{
    public int Applied => Files.Sum(file => file.Applied);

    public int Missing => Files.Sum(file => file.Missing);

    public int ChangedFiles => Files.Count(file => file.Changed);
}
