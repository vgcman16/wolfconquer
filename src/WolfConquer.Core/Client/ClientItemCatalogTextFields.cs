namespace WolfConquer.Core.Client;

/// <summary>
/// Immutable optional replacements for observed client text slots 1, 52 and 53.
/// A null value leaves its slot unchanged; no tooltip rendering semantics are implied.
/// </summary>
public sealed class ClientItemCatalogTextFields
{
    private readonly byte[]? _name;
    private readonly byte[]? _category;
    private readonly byte[]? _description;

    public ClientItemCatalogTextFields(
        ReadOnlyMemory<byte>? name = null,
        ReadOnlyMemory<byte>? category = null,
        ReadOnlyMemory<byte>? description = null)
    {
        _name = name?.ToArray();
        _category = category?.ToArray();
        _description = description?.ToArray();
    }

    public ReadOnlyMemory<byte>? Name => Copy(_name);

    public ReadOnlyMemory<byte>? Category => Copy(_category);

    public ReadOnlyMemory<byte>? Description => Copy(_description);

    private static ReadOnlyMemory<byte>? Copy(byte[]? bytes)
    {
        if (bytes is null)
        {
            return null;
        }

        return new ReadOnlyMemory<byte>(bytes.AsSpan().ToArray());
    }
}
