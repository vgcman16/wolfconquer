namespace WolfConquer.Core.Client;

public static partial class Chinese7937DecodedXuanbaoInfo
{
    private sealed partial class SelectedReader
    {
        private int Getter()
        {
            var index = UniqueTopLevel("local", "function", GetterName, "(", "key", ")",
                "return", TableName, "[", "key", "]", "end");
            selectedUses.Add(index + 2);
            selectedUses.Add(index + 7);
            EndOfStatement(index + 11);
            return index;
        }

        private int UnloadReset()
        {
            var function = UniqueTopLevel("local", "function", "Unload", "(", ")");
            var end = function + 5;
            while (end < tokens.Length && !(Is(end, "end") && tokens[end].Blocks == 1 && tokens[end].Delimiters == 0)) end++;
            if (end == tokens.Length) Refuse(Chinese7937XuanbaoInfoFailure.UnsupportedSyntax);
            var reset = -1;
            for (var index = function + 5; index < end; index++)
            {
                if (!Matches(index, TableName, "=", "nil")) continue;
                if (reset >= 0 || tokens[index].Blocks != 1 || tokens[index].Delimiters != 0)
                    Refuse(Chinese7937XuanbaoInfoFailure.AmbiguousSelectedForm);
                StartOfStatement(index); EndOfStatement(index + 2);
                reset = index;
            }
            if (reset < 0) Refuse(Chinese7937XuanbaoInfoFailure.SelectedRelationshipMissing);
            selectedUses.Add(reset);
            return function;
        }

        private int ModuleExport()
        {
            var module = UniqueTopLevel("local", ModuleName, "=", "{");
            var closing = module + 4;
            while (closing < tokens.Length && !(Is(closing, "}") && tokens[closing].Blocks == 0 && tokens[closing].Delimiters == 1)) closing++;
            if (closing == tokens.Length) Refuse(Chinese7937XuanbaoInfoFailure.UnsupportedSyntax);
            var exported = false;
            var start = module + 4;
            for (var index = start; index <= closing; index++)
            {
                var separator = index == closing || Is(index, ",") && tokens[index].Blocks == 0 && tokens[index].Delimiters == 1;
                if (!separator) continue;
                if (start == index)
                {
                    if (index != closing) Refuse(Chinese7937XuanbaoInfoFailure.UnsupportedSyntax);
                    continue;
                }
                if (tokens[start].Kind != TokenKind.Identifier || !Is(start + 1, "=") || index - start < 3)
                    Refuse(Chinese7937XuanbaoInfoFailure.UnsupportedSyntax);
                if (Is(start, GetterName))
                {
                    if (exported) Refuse(Chinese7937XuanbaoInfoFailure.AmbiguousSelectedForm);
                    if (index == closing || index - start != 3 || !Matches(start, GetterName, "=", GetterName))
                        Refuse(Chinese7937XuanbaoInfoFailure.UnsupportedSyntax);
                    selectedUses.Add(start); selectedUses.Add(start + 2); exported = true;
                }
                start = index + 1;
            }
            if (!exported) Refuse(Chinese7937XuanbaoInfoFailure.SelectedRelationshipMissing);
            EndOfStatement(closing);
            return module;
        }

        private int GlobalAlias()
        {
            var index = UniqueTopLevel(GlobalAccessorName, "=", ModuleName, ".", GetterName);
            selectedUses.Add(index); selectedUses.Add(index + 4);
            EndOfStatement(index + 4);
            return index;
        }
    }
}
