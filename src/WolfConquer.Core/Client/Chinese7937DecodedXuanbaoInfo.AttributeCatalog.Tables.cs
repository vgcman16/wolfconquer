using System.Globalization;

namespace WolfConquer.Core.Client;

public static partial class Chinese7937DecodedXuanbaoInfo
{
    private sealed partial class SelectedReader
    {
        private const string ScoreTable = "XuanBaoScoreGradeStep";
        private const string GoldTable = "XuanBaoLimitAttri";
        private const string ScoreGetter = "GetXuanBaoScoreGradeStep";
        private const string GoldGetter = "GetXuanBaoGoldLimitAttri";
        private const string ScoreAlias = "XuanBao_GetXuanBaoScoreGradeStep";
        private const string GoldAlias = "XuanBao_GetXuanBaoGoldLimitAttri";

        internal (Chinese7937XuanbaoScoreLimit[] Scores, Chinese7937XuanbaoGoldLimitFlag[] Flags) ParseAttributeCatalog()
        {
            var scoreStart = UniqueTopLevel("local", ScoreTable, "=", "{");
            var goldStart = UniqueTopLevel("local", GoldTable, "=", "{");
            selectedUses.Add(scoreStart + 1); selectedUses.Add(goldStart + 1);
            var scores = ReadScoreItems(scoreStart + 4, out var scoreEnd);
            var flags = ReadGoldItems(goldStart + 4, out var goldEnd);
            EndOfStatement(scoreEnd); EndOfStatement(goldEnd);
            var scoreGetter = AttributeScoreGetter();
            var goldGetter = AttributeGoldGetter();
            var unload = AttributeUnloadResets();
            var module = AttributeModuleExports();
            var scoreAlias = AttributeAlias(ScoreAlias, ScoreGetter);
            var goldAlias = AttributeAlias(GoldAlias, GoldGetter);
            if (!(scoreEnd < scoreGetter && goldEnd < goldGetter && scoreGetter < unload && goldGetter < unload &&
                unload < module && module < scoreAlias && module < goldAlias))
                Refuse(Chinese7937XuanbaoInfoFailure.AmbiguousSelectedForm);
            RefuseOtherAttributeUses(module + 1);
            return (scores, flags);
        }

        private Chinese7937XuanbaoScoreLimit[] ReadScoreItems(int index, out int close)
        {
            List<Chinese7937XuanbaoScoreLimit> rows = [];
            HashSet<uint> items = [];
            while (!Is(index, "}"))
            {
                var item = NumericKey(ref index);
                if (!items.Add(item)) Refuse(Chinese7937XuanbaoInfoFailure.DuplicateField);
                Expect(index++, "{");
                HashSet<uint> attributes = [];
                while (!Is(index, "}"))
                {
                    var attribute = NumericKey(ref index);
                    if (!attributes.Add(attribute)) Refuse(Chinese7937XuanbaoInfoFailure.DuplicateField);
                    Expect(index++, "{");
                    HashSet<uint> limits = [];
                    while (!Is(index, "}"))
                    {
                        var limit = ScoreIndex(index++);
                        Expect(index++, "=");
                        var value = FieldValue(index++);
                        Expect(index++, ",");
                        if (!limits.Add(limit)) Refuse(Chinese7937XuanbaoInfoFailure.DuplicateField);
                        if (rows.Count == MaximumFields) Refuse(Chinese7937XuanbaoInfoFailure.LiteralBounds);
                        rows.Add(new(item, attribute, limit, value));
                    }
                    if (limits.Count == 0) Refuse(Chinese7937XuanbaoInfoFailure.UnsupportedSyntax);
                    Expect(index++, "}"); Expect(index++, ",");
                }
                if (attributes.Count == 0) Refuse(Chinese7937XuanbaoInfoFailure.UnsupportedSyntax);
                Expect(index++, "}"); Expect(index++, ",");
            }
            if (items.Count == 0) Refuse(Chinese7937XuanbaoInfoFailure.UnsupportedSyntax);
            close = index;
            return rows.ToArray();
        }

        private Chinese7937XuanbaoGoldLimitFlag[] ReadGoldItems(int index, out int close)
        {
            List<Chinese7937XuanbaoGoldLimitFlag> rows = [];
            HashSet<uint> items = [];
            while (!Is(index, "}"))
            {
                var item = NumericKey(ref index);
                if (!items.Add(item)) Refuse(Chinese7937XuanbaoInfoFailure.DuplicateField);
                Expect(index++, "{");
                HashSet<uint> attributes = [];
                while (!Is(index, "}"))
                {
                    var attribute = NumericKey(ref index);
                    if (index >= tokens.Length || tokens[index].Kind != TokenKind.Identifier || tokens[index].Text is not ("true" or "false"))
                        Refuse(Chinese7937XuanbaoInfoFailure.UnsupportedSyntax);
                    var value = Is(index++, "true");
                    Expect(index++, ",");
                    if (!attributes.Add(attribute)) Refuse(Chinese7937XuanbaoInfoFailure.DuplicateField);
                    if (rows.Count == MaximumFields) Refuse(Chinese7937XuanbaoInfoFailure.LiteralBounds);
                    rows.Add(new(item, attribute, value));
                }
                if (attributes.Count == 0) Refuse(Chinese7937XuanbaoInfoFailure.UnsupportedSyntax);
                Expect(index++, "}"); Expect(index++, ",");
            }
            if (items.Count == 0) Refuse(Chinese7937XuanbaoInfoFailure.UnsupportedSyntax);
            close = index;
            return rows.ToArray();
        }

        private uint NumericKey(ref int index)
        {
            Expect(index++, "[");
            var value = FieldValue(index++);
            Expect(index++, "]"); Expect(index++, "=");
            return value;
        }

        private uint ScoreIndex(int index)
        {
            if (index >= tokens.Length || tokens[index].Kind != TokenKind.Identifier)
                Refuse(Chinese7937XuanbaoInfoFailure.UnsupportedSyntax);
            var name = tokens[index].Text;
            if (name.Length != "ScoreLimit0".Length || !name.StartsWith("ScoreLimit", StringComparison.Ordinal) ||
                name[^1] is < '0' or > '8') Refuse(Chinese7937XuanbaoInfoFailure.UnsupportedSyntax);
            return uint.Parse(name.AsSpan("ScoreLimit".Length), NumberStyles.None, CultureInfo.InvariantCulture);
        }
    }
}
