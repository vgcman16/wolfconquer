namespace WolfConquer.Core.Client;

public static partial class Chinese7937DecodedXuanbaoInfo
{
    private sealed partial class SelectedReader
    {
        private int AttributeScoreGetter() => AttributeGetter(
            "local", "function", ScoreGetter, "(", "nItemType", ",", "nAttriType", ",", "nIndex", ",", "default", ")",
            "local", "t1", "=", ScoreTable, "[", "nItemType", "]",
            "if", "(", "t1", "=", "=", "nil", ")", "then", "return", "default", "end",
            "local", "t2", "=", "t1", "[", "nAttriType", "]",
            "if", "(", "t2", "=", "=", "nil", ")", "then", "return", "default", "end",
            "local", "t3", "=", "t2", "[", "nIndex", "]",
            "if", "(", "t3", "=", "=", "nil", ")", "then", "return", "default", "end", "return", "t3", "end");

        private int AttributeGoldGetter() => AttributeGetter(
            "local", "function", GoldGetter, "(", "nItemType", ",", "nAttriType", ",", "default", ")",
            "local", "t", "=", GoldTable, "[", "nItemType", "]",
            "if", "(", "t", "=", "=", "nil", ")", "then", "return", "default", "end",
            "local", "res", "=", "t", "[", "nAttriType", "]",
            "if", "(", "res", "=", "=", "nil", ")", "then", "return", "default", "end", "return", "res", "end");

        private int AttributeGetter(params string[] sequence)
        {
            var index = UniqueTopLevel(sequence);
            for (var offset = 0; offset < sequence.Length; offset++)
                if (IsAttributeName(sequence[offset])) selectedUses.Add(index + offset);
            EndOfStatement(index + sequence.Length - 1);
            return index;
        }

        private int AttributeUnloadResets()
        {
            var function = UniqueTopLevel("local", "function", "Unload", "(", ")");
            var end = function + 5;
            while (end < tokens.Length && !(Is(end, "end") && tokens[end].Blocks == 1 && tokens[end].Delimiters == 0)) end++;
            if (end == tokens.Length) Refuse(Chinese7937XuanbaoInfoFailure.UnsupportedSyntax);
            foreach (var table in new[] { ScoreTable, GoldTable })
            {
                var reset = -1;
                for (var index = function + 5; index < end; index++)
                {
                    if (!Matches(index, table, "=", "nil")) continue;
                    if (reset >= 0 || tokens[index].Blocks != 1 || tokens[index].Delimiters != 0)
                        Refuse(Chinese7937XuanbaoInfoFailure.AmbiguousSelectedForm);
                    StartOfStatement(index); EndOfStatement(index + 2);
                    reset = index;
                }
                if (reset < 0) Refuse(Chinese7937XuanbaoInfoFailure.SelectedRelationshipMissing);
                selectedUses.Add(reset);
            }
            return function;
        }

        private int AttributeModuleExports()
        {
            var module = UniqueTopLevel("local", ModuleName, "=", "{");
            var closing = module + 4;
            while (closing < tokens.Length && !(Is(closing, "}") && tokens[closing].Blocks == 0 && tokens[closing].Delimiters == 1)) closing++;
            if (closing == tokens.Length) Refuse(Chinese7937XuanbaoInfoFailure.UnsupportedSyntax);
            HashSet<string> exported = new(StringComparer.Ordinal);
            var start = module + 4;
            for (var index = start; index <= closing; index++)
            {
                if (index != closing && !(Is(index, ",") && tokens[index].Blocks == 0 && tokens[index].Delimiters == 1)) continue;
                if (start == index)
                {
                    if (index != closing) Refuse(Chinese7937XuanbaoInfoFailure.UnsupportedSyntax);
                    continue;
                }
                if (tokens[start].Kind != TokenKind.Identifier || !Is(start + 1, "=") || index - start < 3)
                    Refuse(Chinese7937XuanbaoInfoFailure.UnsupportedSyntax);
                var name = tokens[start].Text;
                if (name is ScoreGetter or GoldGetter)
                {
                    if (!exported.Add(name)) Refuse(Chinese7937XuanbaoInfoFailure.AmbiguousSelectedForm);
                    if (index == closing || index - start != 3 || !Matches(start, name, "=", name))
                        Refuse(Chinese7937XuanbaoInfoFailure.UnsupportedSyntax);
                    selectedUses.Add(start); selectedUses.Add(start + 2);
                }
                start = index + 1;
            }
            if (exported.Count != 2) Refuse(Chinese7937XuanbaoInfoFailure.SelectedRelationshipMissing);
            EndOfStatement(closing);
            return module;
        }

        private int AttributeAlias(string alias, string getter)
        {
            var index = UniqueTopLevel(alias, "=", ModuleName, ".", getter);
            selectedUses.Add(index); selectedUses.Add(index + 4);
            EndOfStatement(index + 4);
            return index;
        }

        private void RefuseOtherAttributeUses(int moduleDeclaration)
        {
            for (var index = 0; index < tokens.Length; index++)
            {
                var token = tokens[index];
                if (token.Kind != TokenKind.Identifier) continue;
                if (IsAttributeName(token.Text) && !selectedUses.Contains(index))
                    Refuse(Chinese7937XuanbaoInfoFailure.AmbiguousSelectedForm);
                if (token.Text == ModuleName && index != moduleDeclaration) RequireAttributeModuleAlias(index);
            }
        }

        private void RequireAttributeModuleAlias(int index)
        {
            // The complete observed module exposes ordinary top-level direct
            // aliases. Computed access or passing the module owner can replace
            // a selected export without another selected identifier token.
            if (index > 0 && Is(index - 1, "return") && tokens[index - 1].Blocks == 0 && tokens[index - 1].Delimiters == 0 &&
                (index + 1 == tokens.Length || index + 2 == tokens.Length && Is(index + 1, ";")))
            {
                StartOfStatement(index - 1); EndOfStatement(index);
                return;
            }
            if (index < 2 || index + 2 >= tokens.Length || tokens[index - 2].Kind != TokenKind.Identifier ||
                tokens[index - 2].Blocks != 0 || tokens[index - 2].Delimiters != 0 || !Is(index - 1, "=") ||
                !Is(index + 1, ".") || tokens[index + 2].Kind != TokenKind.Identifier)
                Refuse(Chinese7937XuanbaoInfoFailure.AmbiguousSelectedForm);
            StartOfStatement(index - 2); EndOfStatement(index + 2);
        }

        private static bool IsAttributeName(string value) =>
            value is ScoreTable or GoldTable or ScoreGetter or GoldGetter or ScoreAlias or GoldAlias;
    }
}
