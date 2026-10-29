using System.Globalization;

namespace WolfConquer.Core.Client;

public static partial class Chinese7937DecodedXuanbaoInfo
{
    private sealed partial class SelectedReader
    {
        private readonly string text;
        private readonly Token[] tokens;
        private readonly HashSet<int> selectedUses = [];

        internal SelectedReader(string text)
        {
            this.text = text;
            tokens = new LexicalReader(text).Read();
        }

        internal Chinese7937XuanbaoInfoField[] Parse()
        {
            var literal = UniqueTopLevel("local", TableName, "=", "{");
            selectedUses.Add(literal + 1);
            var fields = Fields(literal + 4, out var close);
            EndOfStatement(close);
            var getter = Getter();
            var unload = UnloadReset();
            var module = ModuleExport();
            var global = GlobalAlias();
            if (!(close < getter && getter < unload && unload < module && module < global))
                Refuse(Chinese7937XuanbaoInfoFailure.AmbiguousSelectedForm);
            RefuseOtherSelectedUses(module + 1);
            return fields;
        }

        private Chinese7937XuanbaoInfoField[] Fields(int index, out int close)
        {
            List<Chinese7937XuanbaoInfoField> fields = [];
            HashSet<string> names = new(StringComparer.Ordinal);
            while (!Is(index, "}"))
            {
                Expect(index++, "[");
                var key = FieldName(index++);
                Expect(index++, "]"); Expect(index++, "=");
                var value = FieldValue(index++);
                Expect(index++, ",");
                if (!names.Add(key)) Refuse(Chinese7937XuanbaoInfoFailure.DuplicateField);
                if (fields.Count == MaximumFields) Refuse(Chinese7937XuanbaoInfoFailure.LiteralBounds);
                fields.Add(new(key, value));
            }
            if (fields.Count == 0) Refuse(Chinese7937XuanbaoInfoFailure.UnsupportedSyntax);
            close = index;
            return fields.ToArray();
        }

        private string FieldName(int index)
        {
            if (index >= tokens.Length || tokens[index].Kind != TokenKind.String)
                Refuse(Chinese7937XuanbaoInfoFailure.UnsupportedSyntax);
            var raw = tokens[index].Text;
            if (raw.Length < 3 || raw[0] != '"' || raw[^1] != '"')
                Refuse(Chinese7937XuanbaoInfoFailure.UnsupportedSyntax);
            var name = raw[1..^1];
            if (name.Length > MaximumIdentifierLength) Refuse(Chinese7937XuanbaoInfoFailure.LiteralBounds);
            if (!IdentifierStart(name[0]) || !name.All(IdentifierPart))
                Refuse(Chinese7937XuanbaoInfoFailure.UnsupportedSyntax);
            return name;
        }

        private uint FieldValue(int index)
        {
            if (index >= tokens.Length || tokens[index].Kind != TokenKind.Number)
                Refuse(Chinese7937XuanbaoInfoFailure.UnsupportedSyntax);
            var value = tokens[index].Text;
            if (!value.All(char.IsAsciiDigit) || value.Length > 1 && value[0] == '0')
                Refuse(Chinese7937XuanbaoInfoFailure.UnsupportedSyntax);
            if (!uint.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
                Refuse(Chinese7937XuanbaoInfoFailure.NumericWidth);
            return number;
        }

        private int UniqueTopLevel(params string[] sequence)
        {
            var found = -1;
            for (var index = 0; index < tokens.Length; index++)
            {
                if (tokens[index].Blocks != 0 || tokens[index].Delimiters != 0 || !Matches(index, sequence)) continue;
                if (found >= 0) Refuse(Chinese7937XuanbaoInfoFailure.AmbiguousSelectedForm);
                StartOfStatement(index);
                found = index;
            }
            if (found < 0) Refuse(Chinese7937XuanbaoInfoFailure.SelectedRelationshipMissing);
            return found;
        }

        private bool Matches(int index, params string[] sequence)
        {
            if (index + sequence.Length > tokens.Length) return false;
            for (var offset = 0; offset < sequence.Length; offset++)
                if (tokens[index + offset].Text != sequence[offset] || tokens[index + offset].Kind == TokenKind.String) return false;
            return true;
        }

        private bool Is(int index, string value) => index < tokens.Length && tokens[index].Text == value;
        private void Expect(int index, string value)
        {
            if (!Is(index, value)) Refuse(Chinese7937XuanbaoInfoFailure.UnsupportedSyntax);
        }

        private bool LineBreak(int left, int right) => text.AsSpan(left, right - left).IndexOfAny('\r', '\n') >= 0;

        private void StartOfStatement(int index)
        {
            if (index == 0 || Is(index - 1, ";")) return;
            var previous = tokens[index - 1];
            if (!LineBreak(previous.End, tokens[index].Start) || previous.Text is "local" or "." or ":" or "," or "=" or "(" or "[")
                Refuse(Chinese7937XuanbaoInfoFailure.UnsupportedSyntax);
        }

        private void EndOfStatement(int index)
        {
            if (index + 1 == tokens.Length || Is(index + 1, ";") || Is(index + 1, "end")) return;
            var next = tokens[index + 1];
            if (!LineBreak(tokens[index].End, next.Start) || next.Kind != TokenKind.Identifier || next.Text is "and" or "or")
                Refuse(Chinese7937XuanbaoInfoFailure.UnsupportedSyntax);
        }

        private void RefuseOtherSelectedUses(int moduleDeclaration)
        {
            for (var index = 0; index < tokens.Length; index++)
            {
                var token = tokens[index];
                if (token.Kind != TokenKind.Identifier) continue;
                if ((token.Text is TableName or GetterName or GlobalAccessorName) && !selectedUses.Contains(index))
                    Refuse(Chinese7937XuanbaoInfoFailure.AmbiguousSelectedForm);
                if (token.Text == ModuleName && token.Delimiters == 0 && Is(index + 1, "=") && index != moduleDeclaration)
                    Refuse(Chinese7937XuanbaoInfoFailure.AmbiguousSelectedForm);
            }
        }
    }
}
