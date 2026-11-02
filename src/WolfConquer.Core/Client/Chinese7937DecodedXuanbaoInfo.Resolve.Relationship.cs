namespace WolfConquer.Core.Client;

public static partial class Chinese7937DecodedXuanbaoInfo
{
    private sealed partial class SelectedReader
    {
        private int ResolveModuleExports()
        {
            var module = UniqueTopLevel("local", ModuleName, "=", "{");
            var close = module + 4;
            while (close < tokens.Length && !(Is(close, "}") && tokens[close].Blocks == 0 && tokens[close].Delimiters == 1)) close++;
            if (close == tokens.Length) Refuse(Chinese7937XuanbaoInfoFailure.UnsupportedSyntax);
            HashSet<string> exports = new(StringComparer.Ordinal);
            var start = module + 4;
            for (var index = start; index <= close; index++)
            {
                if (index != close && !(Is(index, ",") && tokens[index].Blocks == 0 && tokens[index].Delimiters == 1)) continue;
                if (start == index)
                {
                    if (index != close) Refuse(Chinese7937XuanbaoInfoFailure.UnsupportedSyntax);
                    continue;
                }
                if (tokens[start].Kind != TokenKind.Identifier || !Is(start + 1, "=") || index - start < 3)
                    Refuse(Chinese7937XuanbaoInfoFailure.UnsupportedSyntax);
                var name = tokens[start].Text;
                if (name is ResolveNumberGetterName or ResolveMaterialGetterName or ResolveUnloadName)
                {
                    if (!exports.Add(name)) Refuse(Chinese7937XuanbaoInfoFailure.AmbiguousSelectedForm);
                    if (index == close || index - start != 3 || !Matches(start, name, "=", name))
                        Refuse(Chinese7937XuanbaoInfoFailure.UnsupportedSyntax);
                    selectedUses.Add(start); selectedUses.Add(start + 2);
                }
                start = index + 1;
            }
            if (exports.Count != 3) Refuse(Chinese7937XuanbaoInfoFailure.SelectedRelationshipMissing);
            EndOfStatement(close);
            return module;
        }

        private int ResolveGlobalAlias(string alias, string getter)
        {
            var start = UniqueTopLevel(alias, "=", ModuleName, ".", getter);
            selectedUses.Add(start); selectedUses.Add(start + 4);
            EndOfStatement(start + 4);
            return start;
        }

        private int ResolveTerminalReturn()
        {
            var start = UniqueTopLevel("return", ModuleName);
            if (start + 2 != tokens.Length && !(start + 3 == tokens.Length && Is(start + 2, ";")))
                Refuse(Chinese7937XuanbaoInfoFailure.AmbiguousSelectedForm);
            EndOfStatement(start + 1);
            return start;
        }

        private void RefuseOtherResolveUses(int moduleDeclaration)
        {
            for (var index = 0; index < tokens.Length; index++)
            {
                var token = tokens[index];
                if (token.Kind != TokenKind.Identifier) continue;
                if (IsResolveName(token.Text) && !selectedUses.Contains(index))
                    Refuse(Chinese7937XuanbaoInfoFailure.AmbiguousSelectedForm);
                if (token.Text == ModuleName && index != moduleDeclaration) RequireAttributeModuleAlias(index);
            }
        }

        private static bool IsResolveName(string name) => name is ResolveTableName or ResolveNumberGetterName or
            ResolveMaterialGetterName or ResolveNumberAlias or ResolveMaterialAlias or ResolveUnloadName or ResolveUnloadAlias;
    }
}
