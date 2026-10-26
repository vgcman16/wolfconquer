namespace WolfConquer.Core.Client;

/// <summary>Pure 64-word adjacent-XOR/rotation recipe; no key literals or payload decoding.</summary>
public static class ClientConfigWordRecipe
{
    public const int WordCount = 64;
    private const int Rotation = 17;

    public static uint[] Decode(ReadOnlySpan<uint> encodedWords)
    {
        RequireWordCount(encodedWords.Length);
        var words = new uint[WordCount];
        for (var index = 0; index < WordCount - 1; index++)
        {
            words[(Rotation + index) & (WordCount - 1)] = encodedWords[index] ^ encodedWords[index + 1];
        }

        words[(Rotation + WordCount - 1) & (WordCount - 1)] = encodedWords[WordCount - 1];
        return words;
    }

    public static uint[] Encode(ReadOnlySpan<uint> words)
    {
        RequireWordCount(words.Length);
        var encoded = new uint[WordCount];
        encoded[WordCount - 1] = words[(Rotation + WordCount - 1) & (WordCount - 1)];
        for (var index = WordCount - 2; index >= 0; index--)
        {
            encoded[index] = encoded[index + 1] ^ words[(Rotation + index) & (WordCount - 1)];
        }

        return encoded;
    }

    private static void RequireWordCount(int length)
    {
        if (length != WordCount)
        {
            throw new ArgumentException("The configuration word recipe requires exactly 64 words.");
        }
    }
}
