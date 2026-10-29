namespace WolfConquer.Core.Client;

public static partial class Chinese7937DecodedXuanbaoInfo
{
    private enum TokenKind { Identifier, Number, String, Symbol }
    private sealed record Token(TokenKind Kind, string Text, int Start, int End, int Blocks, int Delimiters);
    private sealed class Block(string kind, bool awaitsDo = false)
    {
        internal string Kind { get; } = kind;
        internal bool AwaitsDo { get; set; } = awaitsDo;
    }

    private sealed class LexicalReader(string text)
    {
        private readonly List<Token> tokens = [];
        private readonly Stack<Block> blocks = [];
        private readonly Stack<char> delimiters = [];
        private int cursor;

        internal Token[] Read()
        {
            while (cursor < text.Length)
            {
                if (char.IsWhiteSpace(text[cursor])) { cursor++; continue; }
                if (text.AsSpan(cursor).StartsWith("--", StringComparison.Ordinal)) { Comment(); continue; }
                var start = cursor;
                var kind = ReadToken();
                if (tokens.Count == MaximumTokens) Refuse(Chinese7937XuanbaoInfoFailure.LiteralBounds);
                var value = text[start..cursor];
                tokens.Add(new(kind, value, start, cursor, blocks.Count, delimiters.Count));
                TrackBoundaries(kind, value);
            }
            if (blocks.Count != 0 || delimiters.Count != 0) Refuse(Chinese7937XuanbaoInfoFailure.UnsupportedSyntax);
            return tokens.ToArray();
        }

        private TokenKind ReadToken()
        {
            var start = cursor;
            var character = text[cursor];
            if (IdentifierStart(character))
            {
                while (cursor < text.Length && IdentifierPart(text[cursor])) cursor++;
                if (cursor - start > MaximumIdentifierLength) Refuse(Chinese7937XuanbaoInfoFailure.LiteralBounds);
                return TokenKind.Identifier;
            }
            if (char.IsAsciiDigit(character))
            {
                while (cursor < text.Length && (IdentifierPart(text[cursor]) || text[cursor] == '.')) cursor++;
                return TokenKind.Number;
            }
            if (character is '\'' or '"') { ShortString(character); return TokenKind.String; }
            if (character == '[' && LongOpening(cursor, out var equals, out var content))
            { LongString(equals, content); return TokenKind.String; }
            cursor++;
            return TokenKind.Symbol;
        }

        private void Comment()
        {
            cursor += 2;
            if (LongOpening(cursor, out var equals, out var content)) { LongString(equals, content); return; }
            while (cursor < text.Length && text[cursor] is not ('\r' or '\n')) cursor++;
        }

        private void ShortString(char quote)
        {
            cursor++;
            while (cursor < text.Length)
            {
                var character = text[cursor++];
                if (character == quote) return;
                if (character is '\r' or '\n') Refuse(Chinese7937XuanbaoInfoFailure.UnsupportedSyntax);
                if (character != '\\') continue;
                if (cursor == text.Length) Refuse(Chinese7937XuanbaoInfoFailure.UnsupportedSyntax);
                if (text[cursor++] == '\r' && cursor < text.Length && text[cursor] == '\n') cursor++;
            }
            Refuse(Chinese7937XuanbaoInfoFailure.UnsupportedSyntax);
        }

        private bool LongOpening(int start, out int equals, out int content)
        {
            equals = 0; content = start;
            if (start >= text.Length || text[start] != '[') return false;
            var index = start + 1;
            while (index < text.Length && text[index] == '=') { equals++; index++; }
            if (index >= text.Length || text[index] != '[') return false;
            content = index + 1;
            return true;
        }

        private void LongString(int equals, int content)
        {
            var close = "]" + new string('=', equals) + "]";
            var end = text.IndexOf(close, content, StringComparison.Ordinal);
            if (end < 0) Refuse(Chinese7937XuanbaoInfoFailure.UnsupportedSyntax);
            cursor = end + close.Length;
        }

        private void TrackBoundaries(TokenKind kind, string value)
        {
            if (kind == TokenKind.Identifier) { TrackBlock(value); return; }
            if (kind != TokenKind.Symbol || value.Length != 1) return;
            var character = value[0];
            if (character is '(' or '{' or '[') { delimiters.Push(character); return; }
            if (character is not (')' or '}' or ']')) return;
            var expected = character switch { ')' => '(', '}' => '{', _ => '[' };
            if (!delimiters.TryPop(out var actual) || actual != expected) Refuse(Chinese7937XuanbaoInfoFailure.UnsupportedSyntax);
        }

        private void TrackBlock(string word)
        {
            switch (word)
            {
                case "function": case "if": case "repeat": blocks.Push(new(word)); break;
                case "for": case "while": blocks.Push(new(word, awaitsDo: true)); break;
                case "do":
                    if (blocks.TryPeek(out var loop) && loop.AwaitsDo) loop.AwaitsDo = false;
                    else blocks.Push(new(word));
                    break;
                case "end":
                    if (!blocks.TryPop(out var block) || block.Kind == "repeat" || block.AwaitsDo)
                        Refuse(Chinese7937XuanbaoInfoFailure.UnsupportedSyntax);
                    break;
                case "until":
                    if (!blocks.TryPop(out var repeated) || repeated.Kind != "repeat")
                        Refuse(Chinese7937XuanbaoInfoFailure.UnsupportedSyntax);
                    break;
            }
        }
    }

    private static bool IdentifierStart(char character) => char.IsAsciiLetter(character) || character == '_';
    private static bool IdentifierPart(char character) => IdentifierStart(character) || char.IsAsciiDigit(character);
}
