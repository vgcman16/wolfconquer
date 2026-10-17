namespace WolfConquer.Core.Client;

public sealed record ClientTextReplacement(string Key, string Value)
{
    public int Occurrence { get; init; } = 1;
    public bool ReplaceLine { get; init; }

    public ClientTextReplacement(string key, string value, int occurrence)
        : this(key, value)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(occurrence, 1);
        Occurrence = occurrence;
    }

    public ClientTextReplacement(string key, string value, bool replaceLine)
        : this(key, value)
    {
        ReplaceLine = replaceLine;
    }
}
