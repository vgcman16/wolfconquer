using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeExistingCharacterPrefix
{
    internal static bool TryPrepare(ConquerTcpConnection connection, CharacterSnapshot snapshot, string usersRoot,
        out LegacyNativeExistingCharacterPrefix? prepared, out string reason)
    {
        prepared = null; reason = "normalPrefixPreparationRefused";
        try
        {
            if (snapshot?.Attributes is null || snapshot.PaidNobility is null ||
                connection.NativeGameClient is not { } client || client.Player is not { } player ||
                !ReferenceEquals(player.Owner, client) || client.FullLoading || client.Fake ||
                snapshot.Uid is <= 0 or > uint.MaxValue || string.IsNullOrWhiteSpace(usersRoot)) return false;
            // Capture every native participant before ANY caller dictionary or
            // genuine INI converter delegate can execute same-value callbacks.
            var playerBefore = new LegacyNativeExistingPrefixBefore(client);
            var beastsBefore = new LegacyNativeBeastsRuntime.PrefixBefore(client);
            var hairfaceBefore = new LegacyNativeHairfaceStorageRuntime.PrefixBefore(client);
            var path = Path.GetFullPath(Path.Combine(usersRoot, snapshot.Uid.ToString(CultureInfo.InvariantCulture) + ".ini"));
            var input = snapshot with
            {
                Attributes = new Dictionary<string, string>(snapshot.Attributes, StringComparer.OrdinalIgnoreCase),
                PaidNobility = new Dictionary<string, string>(snapshot.PaidNobility, StringComparer.OrdinalIgnoreCase),
            };
            LegacyNativeExistingCharacterPrefix? candidate = null;
            var failure = "normalPrefixSnapshotChanged";
            var read = SnapshotFileLockCoordinator.ExecuteLocked(path, () =>
            {
                // Keep the actual source file read-leased across native INI
                // reads, so a same-length/ABA replacement cannot feed them a
                // different image while the captured baseline is being prepared.
                using var sourceLease = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (sourceLease.Length > CharacterSnapshotFileBaseline.MaximumBytes) return false;
                if (!CharacterSnapshotFileBaseline.TryRead(path, "normalPrefix", out var saved, out failure) || saved is null ||
                    !SnapshotMatches(input, saved.Bytes, path)) return false;
                var ini = new LegacyNativeItemCounterShellIni(path, overridePath: true);
                var playerLoad = LegacyNativePlayerRuntime.ExistingPrefixLoad.TryPrepare(client, input, ini);
                if (playerLoad is null) { failure = "normalPrefixNativePlayerUnavailable"; return false; }
                if (!LegacyNativeBeastsRuntime.PrefixLoad.TryPrepare(client, ini.ReadBool("Character", "TailedBeastActivated", false),
                        ini.ReadUInt32("Character", "TailedBeastTotalPoints"), ini.ReadUInt32("Character", "TailedBeastFlag"), out var beasts))
                { failure = "normalPrefixBeastStateUnavailable"; return false; }
                var hairfaceLine = ini.ReadString("Character", "HairfaceStorage", "0/");
                // The source buffer holds254 characters. A complete valid
                // prefix at that boundary must not hide an unvalidated tail.
                // Use the genuine reader's quote/whitespace semantics rather
                // than invent a second INI value parser.
                if (hairfaceLine != ini.ReadString("Character", "HairfaceStorage", "0/", 256) ||
                    !LegacyNativeHairfaceStorageRuntime.PrefixLoad.TryPrepare(client, hairfaceLine, out var hairface))
                { failure = "normalPrefixHairfaceStateUnavailable"; return false; }
                var fields = new Dictionary<string, string>(input.Attributes, StringComparer.OrdinalIgnoreCase) { ["Online"] = "1" };
                var updated = input with { Online = 1, Attributes = fields };
                if (!CharacterSnapshotSectionLineTransformer.TryApply(saved.Lines, "Character",
                        new Dictionary<string, string> { ["Online"] = "1" }, false, out var lines)) return false;
                var text = string.Join(Environment.NewLine, lines) + Environment.NewLine;
                var after = saved.Encoding.GetPreamble().Concat(saved.Encoding.GetBytes(text)).ToArray();
                if (!CharacterSnapshotFileBaseline.TryReadBytes(path, "normalPrefix", out var current, out failure) ||
                    !current.AsSpan().SequenceEqual(saved.Bytes)) return false;
                candidate = new(connection, path, saved.Bytes, after, snapshot, updated, playerLoad, beasts!, hairface!,
                    playerBefore, beastsBefore, hairfaceBefore);
                return candidate.IsCurrent();
            });
            if (!read || candidate is null) { reason = string.IsNullOrEmpty(failure) ? "normalPrefixOwnerChanged" : failure; return false; }
            prepared = candidate; reason = string.Empty; return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or FormatException or
            InvalidDataException or InvalidOperationException or ArgumentException or OverflowException)
        { reason = "normalPrefix:" + error.GetType().Name; return false; }
    }

    private static bool SnapshotMatches(CharacterSnapshot expected, byte[] bytes, string path)
    {
        var actual = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(bytes, out _), Path.GetFileName(path));
        var empty = new Dictionary<string, string>();
        var expectedScalars = expected with { SourceFile = "", Attributes = empty, PaidNobility = empty, PlayerQuests = PlayerQuestSnapshot.Empty };
        var actualScalars = actual with { SourceFile = "", Attributes = empty, PaidNobility = empty, PlayerQuests = PlayerQuestSnapshot.Empty };
        return expectedScalars == actualScalars && SameFields(expected.Attributes, actual.Attributes) && SameFields(expected.PaidNobility, actual.PaidNobility);
    }

    private static bool SameFields(IReadOnlyDictionary<string, string> expected, IReadOnlyDictionary<string, string> actual)
    {
        var copy = new Dictionary<string, string>(expected, StringComparer.OrdinalIgnoreCase);
        return copy.Count == actual.Count && actual.All(pair => copy.TryGetValue(pair.Key, out var value) && value == pair.Value);
    }
}
