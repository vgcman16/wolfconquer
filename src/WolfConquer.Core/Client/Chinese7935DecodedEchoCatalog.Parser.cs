using System.Globalization;

namespace WolfConquer.Core.Client;

public static partial class Chinese7935DecodedEchoCatalog
{
    private sealed partial class LiteralReader
    {
        internal Chinese7935EchoCatalogRow[] Parse()
        {
            ExpectName("local"); ExpectName("ret"); Expect(TokenKind.Equal);
            Expect(TokenKind.Open); Expect(TokenKind.Close); Expect(TokenKind.Semicolon);
            ExpectName("ret"); Expect(TokenKind.Dot); ExpectName(ExportName);
            Expect(TokenKind.Equal); Expect(TokenKind.Open);
            var rows = new List<Chinese7935EchoCatalogRow>(); var ids = new HashSet<int>();
            while (current.Kind != TokenKind.Close)
            {
                Expect(TokenKind.LeftIndex); var number = Integer();
                if (number is 0 or > int.MaxValue) throw Refuse(Chinese7935EchoCatalogFailure.NumericWidth);
                var id = checked((int)number);
                if (!ids.Add(id)) throw Refuse(Chinese7935EchoCatalogFailure.DuplicateRow);
                Expect(TokenKind.RightIndex); Expect(TokenKind.Equal); Member();
                rows.Add(Row(id));
                if (current.Kind == TokenKind.Close) break;
                Expect(TokenKind.Comma);
            }
            Expect(TokenKind.Close); Expect(TokenKind.Semicolon);
            ExpectName("return"); ExpectName("ret"); Expect(TokenKind.Semicolon); Expect(TokenKind.End);
            return rows.ToArray();
        }

        private Chinese7935EchoCatalogRow Row(int id)
        {
            Expect(TokenKind.Open);
            int type = 0, extraType = 0, seen = 0; uint value = 0, extraValue = 0;
            while (current.Kind != TokenKind.Close)
            {
                if (current.Kind != TokenKind.Name) throw Refuse(Chinese7935EchoCatalogFailure.RowSchema);
                var field = Value switch
                {
                    "attr_type" => 1, "attr_value" => 2, "extra_attr_type" => 4, "extra_attr_value" => 8,
                    _ => throw Refuse(Chinese7935EchoCatalogFailure.RowSchema),
                };
                if ((seen & field) != 0) throw Refuse(Chinese7935EchoCatalogFailure.DuplicateField);
                seen |= field; Advance(); Expect(TokenKind.Equal); Member(); var number = Integer();
                if (number > ((field is 1 or 4) ? (ulong)int.MaxValue : uint.MaxValue))
                    throw Refuse(Chinese7935EchoCatalogFailure.NumericWidth);
                switch (field)
                {
                    case 1: type = checked((int)number); break;
                    case 2: value = checked((uint)number); break;
                    case 4: extraType = checked((int)number); break;
                    case 8: extraValue = checked((uint)number); break;
                }
                if (current.Kind == TokenKind.Close) break;
                Expect(TokenKind.Comma);
            }
            Expect(TokenKind.Close);
            if (seen != 15) throw Refuse(Chinese7935EchoCatalogFailure.RowSchema);
            return new(id, type, value, extraType, extraValue);
        }

        private ulong Integer()
        {
            if (current.Kind != TokenKind.Integer) throw Refuse();
            if (!ulong.TryParse(Value, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value > (ulong)long.MaxValue)
                throw Refuse(Chinese7935EchoCatalogFailure.LiteralBounds);
            Advance(); return value;
        }
    }
}
