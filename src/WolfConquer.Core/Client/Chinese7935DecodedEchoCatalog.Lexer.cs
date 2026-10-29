namespace WolfConquer.Core.Client;

public static partial class Chinese7935DecodedEchoCatalog
{
    private enum TokenKind { End, Name, Integer, Open, Close, LeftIndex, RightIndex, Equal, Comma, Semicolon, Dot }
    private readonly record struct Token(TokenKind Kind, int Start, int Length);

    private sealed partial class LiteralReader
    {
        private readonly string text;
        private int offset, tokens, members;
        private Token current;
        internal LiteralReader(string text) { this.text = text; Advance(); }
        private static bool NameStart(char value) => value is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or '_';
        private static bool Digit(char value) => value is >= '0' and <= '9';
        private ReadOnlySpan<char> Value => text.AsSpan(current.Start, current.Length);
        private static LiteralRefusal Refuse(Chinese7935EchoCatalogFailure failure = Chinese7935EchoCatalogFailure.UnsupportedSyntax) => new(failure);

        private void Advance()
        {
            SkipTrivia();
            if (offset == text.Length) { current = new(TokenKind.End, offset, 0); return; }
            if (++tokens > MaximumTokens) throw Refuse(Chinese7935EchoCatalogFailure.LiteralBounds);
            var start = offset; var value = text[offset++];
            if (NameStart(value))
            {
                while (offset < text.Length && (NameStart(text[offset]) || Digit(text[offset]))) offset++;
                if (offset - start > MaximumIdentifierLength) throw Refuse(Chinese7935EchoCatalogFailure.LiteralBounds);
                current = new(TokenKind.Name, start, offset - start); return;
            }
            if (Digit(value))
            {
                while (offset < text.Length && Digit(text[offset])) offset++;
                if (offset - start > 19) throw Refuse(Chinese7935EchoCatalogFailure.LiteralBounds);
                current = new(TokenKind.Integer, start, offset - start); return;
            }
            var kind = value switch
            {
                '{' => TokenKind.Open, '}' => TokenKind.Close, '[' => TokenKind.LeftIndex, ']' => TokenKind.RightIndex,
                '=' => TokenKind.Equal, ',' => TokenKind.Comma, ';' => TokenKind.Semicolon, '.' => TokenKind.Dot,
                _ => throw Refuse(),
            };
            current = new(kind, start, 1);
        }

        private void SkipTrivia()
        {
            while (offset < text.Length)
            {
                if (text[offset] is ' ' or '\t' or '\r' or '\n' or '\f' or '\v') { offset++; continue; }
                if (text[offset] != '-' || offset + 1 >= text.Length || text[offset + 1] != '-') return;
                offset += 2;
                var begin = offset;
                if (offset < text.Length && text[offset] == '[')
                {
                    var scan = offset + 1;
                    while (scan < text.Length && text[scan] == '=') scan++;
                    if (scan < text.Length && text[scan] == '[')
                    {
                        var close = "]" + text[(begin + 1)..scan] + "]";
                        var end = text.IndexOf(close, scan + 1, StringComparison.Ordinal);
                        if (end < 0) throw Refuse();
                        offset = end + close.Length; continue;
                    }
                }
                var newline = text.IndexOf('\n', offset);
                offset = newline < 0 ? text.Length : newline + 1;
            }
        }
        private void Expect(TokenKind kind)
        {
            if (current.Kind != kind) throw Refuse();
            Advance();
        }
        private void ExpectName(string expected)
        {
            if (current.Kind != TokenKind.Name || !Value.SequenceEqual(expected.AsSpan())) throw Refuse();
            Advance();
        }
        private void Member()
        {
            if (++members > MaximumMembers) throw Refuse(Chinese7935EchoCatalogFailure.LiteralBounds);
        }
    }
}
