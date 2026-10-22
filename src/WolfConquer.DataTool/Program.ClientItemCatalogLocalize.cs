using WolfConquer.Core.Client;

internal static partial class Program
{
    private const string ClientItemCatalogLocalizeUsage =
        "Usage: WolfConquer.DataTool client-item-catalog-localize <packed-source> --english-itemtype <67-field-English-source> [--include-text-fields] [--output-new <fresh-path>]";

    private static int ClientItemCatalogLocalizeCommand(string[] args)
    {
        try
        {
            if (args.Length < 4 || args.Length > 7 ||
                string.IsNullOrWhiteSpace(args[1]) || args[1].StartsWith("--", StringComparison.Ordinal) ||
                !string.Equals(args[2], "--english-itemtype", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(args[3]) || args[3].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException("Expected a packed source, one --english-itemtype donor, and the ordered optional flags.");
            }

            var optionIndex = 4;
            var includeTextFields = optionIndex < args.Length &&
                string.Equals(args[optionIndex], "--include-text-fields", StringComparison.OrdinalIgnoreCase);
            if (includeTextFields)
            {
                optionIndex++;
            }
            string? outputPath = null;
            if (optionIndex < args.Length)
            {
                if (args.Length - optionIndex != 2 ||
                    !string.Equals(args[optionIndex], "--output-new", StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(args[optionIndex + 1]) ||
                    args[optionIndex + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    throw new ArgumentException("--include-text-fields may appear once, before the optional --output-new path.");
                }

                outputPath = Path.GetFullPath(args[optionIndex + 1]);
            }
            var sourcePath = Path.GetFullPath(args[1]);
            var donorPath = Path.GetFullPath(args[3]);
            ValidateClientItemCatalogPath(sourcePath, mustBeNew: false);
            ValidateClientItemCatalogPath(donorPath, mustBeNew: false);
            if (outputPath is not null)
            {
                ValidateClientItemCatalogPath(outputPath, mustBeNew: true);
            }

            var packed = ReadClientItemCatalogLocalizationInput(sourcePath, "Packed source");
            var donorBytes = ReadClientItemCatalogLocalizationInput(donorPath, "English donor");
            var decoded = ClientItemCatalogCodec.Decode(packed);
            var document = ClientItemCatalogDocument.Parse(decoded);
            if (!decoded.AsSpan().SequenceEqual(document.ToArray()) ||
                !packed.AsSpan().SequenceEqual(ClientItemCatalogCodec.Encode(decoded)))
            {
                throw new InvalidDataException("Original client catalog roundtrip was not byte-exact; no output was written.");
            }

            // Only separately typed text fields are donated; no source rows,
            // statistics, IDs, delimiters or row endings are transplanted.
            IReadOnlyDictionary<uint, ReadOnlyMemory<byte>> names;
            IReadOnlyDictionary<uint, ReadOnlyMemory<byte>>? categories = null, descriptions = null;
            int sourceRows, skippedNames;
            if (includeTextFields)
            {
                var donor = ClientItemCatalogEnglishTextSource.Parse(donorBytes);
                names = donor.Names;
                categories = donor.Categories;
                descriptions = donor.Descriptions;
                sourceRows = donor.SourceRowCount;
                skippedNames = donor.SkippedNameCount;
            }
            else
            {
                var donor = ClientItemCatalogEnglishNameSource.Parse(donorBytes);
                names = donor.Names;
                sourceRows = donor.SourceRowCount;
                skippedNames = donor.SkippedNameCount;
            }
            var changes = new Dictionary<uint, ClientItemCatalogTextFields>();
            var matchedNames = 0;
            var changedNames = 0;
            var matchedCategories = 0;
            var changedCategories = 0;
            var matchedDescriptions = 0;
            var changedDescriptions = 0;
            var skippedFormatDescriptions = 0;
            foreach (var row in document.Rows)
            {
                ReadOnlyMemory<byte>? nextName = null, nextCategory = null, nextDescription = null;
                if (names.TryGetValue(row.ItemId, out var name))
                {
                    matchedNames++;
                    if (!row.GetField(1).Span.SequenceEqual(name.Span))
                    {
                        changedNames++;
                        nextName = name;
                    }
                }
                if (categories is not null && categories.TryGetValue(row.ItemId, out var category))
                {
                    matchedCategories++;
                    if (!row.GetField(52).Span.SequenceEqual(category.Span))
                    {
                        changedCategories++;
                        nextCategory = category;
                    }
                }
                if (descriptions is not null && descriptions.TryGetValue(row.ItemId, out var description))
                {
                    var current = row.GetField(53);
                    // Formatting consumers are not proven: even literal %% or
                    // unchanged percent-bearing descriptions remain untouched.
                    if (current.Span.IndexOf((byte)'%') >= 0 || description.Span.IndexOf((byte)'%') >= 0)
                    {
                        skippedFormatDescriptions++;
                    }
                    else
                    {
                        matchedDescriptions++;
                        if (!current.Span.SequenceEqual(description.Span))
                        {
                            changedDescriptions++;
                            nextDescription = description;
                        }
                    }
                }
                if (nextName.HasValue || nextCategory.HasValue || nextDescription.HasValue)
                {
                    changes.Add(row.ItemId, new ClientItemCatalogTextFields(nextName, nextCategory, nextDescription));
                }
            }

            var localized = document.WithTextFields(changes);
            var localizedBytes = localized.ToArray();
            var outputBytes = ClientItemCatalogCodec.Encode(localizedBytes);
            if (outputBytes.Length != localizedBytes.Length ||
                !localizedBytes.AsSpan().SequenceEqual(ClientItemCatalogCodec.Decode(outputBytes)))
            {
                throw new InvalidDataException("Localized catalog roundtrip was not byte-exact; no output was written.");
            }

            if (outputPath is not null)
            {
                ValidateClientItemCatalogPath(outputPath, mustBeNew: true);
                using var output = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                output.Write(outputBytes);
            }

            Console.WriteLine($"Rows={document.Rows.Count} SourceRows={sourceRows} MatchedNames={matchedNames} ChangedNames={changedNames} UnmatchedRows={document.Rows.Count - matchedNames} SkippedSourceNames={skippedNames} ExactRoundtrip=True");
            if (includeTextFields)
            {
                Console.WriteLine($"TextFields=52,53 MatchedCategories={matchedCategories} ChangedCategories={changedCategories} MatchedDescriptions={matchedDescriptions} ChangedDescriptions={changedDescriptions} SkippedFormatDescriptions={skippedFormatDescriptions}");
            }
            Console.WriteLine(includeTextFields
                ? "Text-field-only candidate; not a full English client or live-client loading/rendering proof. Server-style 67-field rows were not copied. Field 52/53 UI semantics remain unverified."
                : "Name-field-only candidate; not a full English client or live-client loading/rendering proof. Server-style 67-field rows were not copied.");
            return 0;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException or OverflowException)
        {
            Console.Error.WriteLine($"Client item catalog localization failed: {ex.Message}");
            Console.Error.WriteLine(ClientItemCatalogLocalizeUsage);
            return 1;
        }
    }

    private static byte[] ReadClientItemCatalogLocalizationInput(string path, string description)
    {
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (input.Length > ClientItemCatalogMaximumBytes)
        {
            throw new InvalidDataException($"{description} exceeds the 64 MiB limit.");
        }

        var bytes = new byte[checked((int)input.Length)];
        input.ReadExactly(bytes);
        if (input.ReadByte() != -1)
        {
            throw new IOException($"{description} changed while reading.");
        }

        return bytes;
    }
}
