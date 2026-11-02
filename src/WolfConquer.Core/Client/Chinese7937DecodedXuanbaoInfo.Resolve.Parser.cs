namespace WolfConquer.Core.Client;

public static partial class Chinese7937DecodedXuanbaoInfo
{
    private sealed partial class SelectedReader
    {
        private const string ResolveTableName = "XuanBaoResolve";
        private const string ResolveNumberGetterName = "GetXuanBaoResolveNum";
        private const string ResolveMaterialGetterName = "GetXuanBaoResolveDataID";
        private const string ResolveNumberAlias = "XuanBao_GetXuanBaoResolveNum";
        private const string ResolveMaterialAlias = "XuanBao_GetXuanBaoResolveDataID";
        private const string ResolveUnloadName = "Unload";
        private const string ResolveUnloadAlias = "RuneSys_Unload";

        internal Chinese7937XuanbaoInfoField[] ParseResolve()
        {
            var literal = UniqueTopLevel("local", ResolveTableName, "=", "{");
            selectedUses.Add(literal + 1);
            var fields = ResolveFields(literal + 4, out var close);
            EndOfStatement(close);
            var numberGetter = ResolveNumberGetter();
            var materialGetter = ResolveMaterialGetter();
            var unload = ResolveUnloadReset();
            var module = ResolveModuleExports();
            var numberAlias = ResolveGlobalAlias(ResolveNumberAlias, ResolveNumberGetterName);
            var materialAlias = ResolveGlobalAlias(ResolveMaterialAlias, ResolveMaterialGetterName);
            var unloadAlias = ResolveGlobalAlias(ResolveUnloadAlias, ResolveUnloadName);
            var terminal = ResolveTerminalReturn();
            if (!(close < numberGetter && close < materialGetter && numberGetter < unload && materialGetter < unload &&
                unload < module && module < numberAlias && module < materialAlias && module < unloadAlias &&
                numberAlias < terminal && materialAlias < terminal && unloadAlias < terminal))
                Refuse(Chinese7937XuanbaoInfoFailure.AmbiguousSelectedForm);
            RefuseOtherResolveUses(module + 1);
            return fields;
        }

        private Chinese7937XuanbaoInfoField[] ResolveFields(int index, out int close)
        {
            List<Chinese7937XuanbaoInfoField> fields = [];
            HashSet<string> names = new(StringComparer.Ordinal);
            while (!Is(index, "}"))
            {
                if (index >= tokens.Length || tokens[index].Kind != TokenKind.Identifier ||
                    tokens[index].Text is not ("CommonNum" or "GoldNum" or "MaterialID"))
                    Refuse(Chinese7937XuanbaoInfoFailure.UnsupportedSyntax);
                var name = tokens[index++].Text;
                if (!names.Add(name)) Refuse(Chinese7937XuanbaoInfoFailure.DuplicateField);
                Expect(index++, "=");
                var value = FieldValue(index++);
                Expect(index++, ",");
                fields.Add(new(name, value));
            }
            if (fields.Count != 3) Refuse(Chinese7937XuanbaoInfoFailure.UnsupportedSyntax);
            close = index;
            return fields.ToArray();
        }

        private int ResolveNumberGetter() => ResolveExactGetter(
            "local", "function", ResolveNumberGetterName, "(", "bGold", ")",
            "if", "bGold", "then", "return", ResolveTableName, ".", "GoldNum",
            "else", "return", ResolveTableName, ".", "CommonNum", "end", "end");

        private int ResolveMaterialGetter() => ResolveExactGetter(
            "local", "function", ResolveMaterialGetterName, "(", ")",
            "return", ResolveTableName, ".", "MaterialID", "end");

        private int ResolveExactGetter(params string[] sequence)
        {
            var start = UniqueTopLevel(sequence);
            for (var offset = 0; offset < sequence.Length; offset++)
                if (IsResolveName(sequence[offset])) selectedUses.Add(start + offset);
            EndOfStatement(start + sequence.Length - 1);
            return start;
        }

        private int ResolveUnloadReset()
        {
            var start = UniqueTopLevel("local", "function", ResolveUnloadName, "(", ")");
            selectedUses.Add(start + 2);
            var end = start + 5;
            while (end < tokens.Length && !(Is(end, "end") && tokens[end].Blocks == 1 && tokens[end].Delimiters == 0)) end++;
            if (end == tokens.Length) Refuse(Chinese7937XuanbaoInfoFailure.UnsupportedSyntax);
            var reset = -1;
            HashSet<string> resetNames = new(StringComparer.Ordinal);
            for (var index = start + 5; index < end;)
            {
                if (tokens[index].Kind != TokenKind.Identifier || tokens[index].Blocks != 1 || tokens[index].Delimiters != 0 ||
                    ResolveResetNameReserved(tokens[index].Text) || !Matches(index, tokens[index].Text, "=", "nil"))
                    Refuse(Chinese7937XuanbaoInfoFailure.UnsupportedSyntax);
                var name = tokens[index].Text;
                if (!resetNames.Add(name))
                    Refuse(Chinese7937XuanbaoInfoFailure.AmbiguousSelectedForm);
                StartOfStatement(index); EndOfStatement(index + 2);
                if (name == ResolveTableName) reset = index;
                index += 3;
                if (Is(index, ";")) index++;
            }
            if (reset < 0) Refuse(Chinese7937XuanbaoInfoFailure.SelectedRelationshipMissing);
            selectedUses.Add(reset);
            EndOfStatement(end);
            return start;
        }

        // The shared lexer labels keywords as Identifier tokens. Only this
        // selected direct-assignment LHS needs the stricter Lua name rule.
        private static bool ResolveResetNameReserved(string name) => name is
            "and" or "break" or "do" or "else" or "elseif" or "end" or "false" or "for" or "function" or "goto" or
            "if" or "in" or "local" or "nil" or "not" or "or" or "repeat" or "return" or "then" or "true" or "until" or "while";
    }
}
