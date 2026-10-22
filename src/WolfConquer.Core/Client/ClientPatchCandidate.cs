namespace WolfConquer.Core.Client;

/// <summary>An owned, in-memory patch result. Building it never writes a target file.</summary>
public sealed class ClientPatchCandidate
{
    private readonly byte[] _bytes;

    internal ClientPatchCandidate(byte[] bytes, ClientPatchFileResult result)
    {
        _bytes = bytes;
        Result = result;
    }

    public ClientPatchFileResult Result { get; }

    public byte[] ToArray() => _bytes.AsSpan().ToArray();
}
