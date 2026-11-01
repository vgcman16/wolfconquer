using System.Security.Cryptography;
using System.Text.Json;
using WolfConquer.Core.Client;

internal static partial class Program
{
    private static int InspectChinese7937CombatGear(string[] args)
    {
        try
        {
            if (args.Length != 2) throw new ArgumentException("One absolute packed input is required.");
            var path = ConfigDecodeFullPath(args[1]);
            ValidateConfigDecodeInput(path);
            using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (input.Length is <= 0 or > Chinese7937CombatGearCatalogDocument.MaximumBytes)
                throw new InvalidDataException("CombatGear catalog exceeds the bounded input size.");
            var captured = new byte[checked((int)input.Length)];
            input.ReadExactly(captured);
            if (input.ReadByte() != -1 || input.Length != captured.Length)
                throw new IOException("CombatGear source changed while reading.");
            ValidateConfigDecodeInput(path);
            var document = Chinese7937CombatGearCatalogDocument.ParsePacked(captured,
                Chinese7937CombatGearCatalogDocument.ClientVersion);
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                DeclaredClientVersion = Chinese7937CombatGearCatalogDocument.ClientVersion,
                Rows = document.Rows.Count,
                FieldsPerRow = Chinese7937CombatGearCatalogDocument.FieldCount,
                PackedBytes = captured.Length,
                PackedSha256 = Convert.ToHexString(SHA256.HashData(captured)),
                ExactPackedInverse = true,
                OutputFilesWritten = false,
                RuntimeActivated = false,
                Boundary = "Raw numeric data only; field semantics and native integration remain unverified.",
            }));
            return 0;
        }
        catch (Exception error) when (IsConfigDecodeFailure(error))
        {
            Console.Error.WriteLine($"Chinese7937 CombatGear inspection failed: {error.GetType().Name}; no output file was written.");
            return 1;
        }
    }
}
