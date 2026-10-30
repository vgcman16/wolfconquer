using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyRuneComposeProgressWitness
{
    private sealed class PlainRows<T> where T : class
    {
        internal readonly IReadOnlyList<T> Source;
        internal readonly T[] Values;
        private readonly T[]? array;
        private readonly List<T>? list;
        internal PlainRows(IReadOnlyList<T> source)
        {
            Source = source; array = source as T[]; list = source as List<T>;
            if (array is null && (list is null || list.GetType() != typeof(List<T>))) throw Invalid();
            int count = array?.Length ?? list!.Count;
            Values = new T[count];
            for (int position = 0; position < count; position++) Values[position] = array is not null ? array[position] : list![position];
        }
        internal bool IsCurrent()
        {
            if ((array?.Length ?? list!.Count) != Values.Length) return false;
            for (int position = 0; position < Values.Length; position++)
                if (!ReferenceEquals(Values[position], array is not null ? array[position] : list![position])) return false;
            return true;
        }
    }

    private sealed class RowWitness
    {
        private readonly RuneLevelExperience source;
        private readonly PlainRows<string> fields;
        private readonly int id, family, level, lineNumber;
        private readonly long experience;
        private readonly string sourceFile, rawLine;
        internal LegacyRuneComposeProgressRow Record { get; }

        internal RowWitness(RuneLevelExperience source, LegacyRuneComposeProgressRow record)
        {
            this.source = source; Record = record;
            fields = new(source.Fields);
            id = source.Id; family = source.RuneType; level = source.Level; experience = source.Experience;
            sourceFile = source.SourceFile; lineNumber = source.LineNumber; rawLine = source.RawLine;
            if (fields.Values.Length != 5 || fields.Values.Any(value => value is null) || fields.Values[4].Length != 0 ||
                rawLine is null || !AtAtTableParser.SplitRow(rawLine).SequenceEqual(fields.Values) ||
                !int.TryParse(fields.Values[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var rawId) || rawId != id ||
                !uint.TryParse(fields.Values[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var rawFamily) || rawFamily != family ||
                !byte.TryParse(fields.Values[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var rawLevel) || rawLevel != level ||
                !uint.TryParse(fields.Values[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var rawProgress) || rawProgress != experience ||
                record.Family != rawFamily || record.Level != rawLevel || record.Progress != rawProgress) throw Invalid();
        }

        internal bool IsCurrent() => source.Id == id && source.RuneType == family && source.Level == level && source.Experience == experience &&
            source.LineNumber == lineNumber && ReferenceEquals(source.SourceFile, sourceFile) && ReferenceEquals(source.RawLine, rawLine) &&
            ReferenceEquals(source.Fields, fields.Source) && fields.IsCurrent() && Record.Family == family && Record.Level == level && Record.Progress == experience;
    }
}
