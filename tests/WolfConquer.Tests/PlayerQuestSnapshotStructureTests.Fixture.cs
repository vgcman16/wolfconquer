using System.Buffers.Binary;
using System.Runtime.ExceptionServices;
using System.Text;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class PlayerQuestSnapshotStructureTests
{
    private const long CharacterUid = 1_293_600;

    private static byte[] HeaderOnly(int declaredQuests)
    {
        var bytes = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, declaredQuests);
        return bytes;
    }

    private static byte[] OneHeader(int intentions)
    {
        var bytes = new byte[sizeof(int) + PlayerQuestSnapshotLoader.LegacyQuestHeaderSize];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 3_944);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), intentions);
        return bytes;
    }

    private static byte[] ValidImage() => Convert.FromHexString(
        "03000000" +
        "D08800000000000040E20100020000000700000009000000" +
        "D1070000010000000C0000000100000003000000" +
        "05000000020000006300000000000000");

    private static Exception? ParseFailure(byte[] bytes)
    {
        try { _ = PlayerQuestSnapshotLoader.Parse(bytes, "owned-malformed.bin"); return null; }
        catch (Exception error) { return error; }
    }

    private static (Exception? Error, long Allocated) MeasureFailure(byte[] bytes)
    {
        // Initialize the real failure path before observing this thread's
        // allocation. No injected allocator, forced OOM, or background counter.
        _ = ParseFailure(HeaderOnly(-1));
        var before = GC.GetAllocatedBytesForCurrentThread();
        var error = ParseFailure(bytes);
        return (error, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    private static void Run(Action<OwnedFiles> test)
    {
        var owned = new OwnedFiles();
        Exception? primary = null;
        try { test(owned); }
        catch (Exception error) { primary = error; }

        Exception? cleanup = null;
        try { owned.Dispose(); }
        catch (Exception error) { cleanup = error; }
        if (primary is not null && cleanup is not null) throw new AggregateException(primary, cleanup);
        if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();
        if (cleanup is not null) ExceptionDispatchInfo.Capture(cleanup).Throw();
    }

    private sealed class OwnedFiles : IDisposable
    {
        private readonly string name = "wolf-quest-structure-" + Guid.NewGuid().ToString("N");
        internal string Root { get; }
        internal string Users => Path.Combine(Root, "Users");
        internal string Quests => Path.Combine(Root, "Quests");
        internal string UserPath => Path.Combine(Users, CharacterUid + ".ini");
        internal string QuestPath => Path.Combine(Quests, CharacterUid + ".bin");
        internal CharacterSnapshotLoader Loader => new(
            Users, playerItemsRoot: null, playerSpellsRoot: null,
            playerProficienciesRoot: null, playersCombatGearRoot: null, playerQuestsRoot: Quests);

        internal OwnedFiles()
        {
            Root = Path.Combine(Path.GetTempPath(), name);
            try
            {
                Directory.CreateDirectory(Users);
                Directory.CreateDirectory(Quests);
            }
            catch (Exception primary)
            {
                try { Dispose(); }
                catch (Exception cleanup) { throw new AggregateException(primary, cleanup); }
                throw;
            }
        }

        internal void Seed(byte[]? questImage, bool expiredPaidNobility = false, long uid = CharacterUid)
        {
            File.WriteAllText(UserPath, UserContent(uid, expiredPaidNobility),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: true, throwOnInvalidBytes: true));
            if (questImage is not null) File.WriteAllBytes(QuestPath, questImage);
        }

        internal Dictionary<string, byte[]> CaptureFiles() =>
            Directory.GetFiles(Root, "*", SearchOption.AllDirectories)
                .ToDictionary(path => Path.GetRelativePath(Root, path), File.ReadAllBytes, StringComparer.Ordinal);

        internal void AssertFilesEqual(Dictionary<string, byte[]> expected)
        {
            var actual = CaptureFiles();
            Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), actual.Keys.Order(StringComparer.Ordinal));
            foreach (var entry in expected) Assert.Equal(entry.Value, actual[entry.Key]);
        }

        public void Dispose()
        {
            var full = Path.GetFullPath(Root);
            var parent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
            if (!string.Equals(Path.GetDirectoryName(full), parent, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Path.GetFileName(full), name, StringComparison.Ordinal))
                throw new IOException("Refusing cleanup outside the exact owned quest fixture root: " + full);
            for (string? path = full; !string.IsNullOrEmpty(path); path = Path.GetDirectoryName(path))
                if ((Directory.Exists(path) || File.Exists(path)) &&
                    (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Refusing cleanup through a reparse entry: " + path);
            DeleteOrdinary(full, full);
        }

        private static void DeleteOrdinary(string directory, string root)
        {
            if (!Directory.Exists(directory)) return;
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                if (!Path.GetFullPath(entry).StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Refusing cleanup outside the owned quest fixture.");
                var attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Refusing cleanup of a reparse entry: " + entry);
                if ((attributes & FileAttributes.Directory) != 0) DeleteOrdinary(entry, root);
                else File.Delete(entry);
            }
            Directory.Delete(directory, recursive: false);
        }

        private static string UserContent(long uid, bool expired) => $$"""
            ; preserve exact owned character encoding and unrelated data
            [Character]
            UID={{uid}}
            Body=1008
            Face=154
            Name=quest-fixture
            Class=7051
            FirstClass=0
            SecoundeClass=7050
            Reborn=2
            Level=140
            Haire=27
            Experience=0
            Map=1015
            X=830
            Y=584
            PMap=1000
            PMapX=500
            PMapY=650
            Agility=36
            Strength=0
            Vitaliti=447
            Spirit=0
            Atributes=12
            MinHitPoints=61258
            MinMana=5000
            Money=998877
            ConquerPoints=1122
            BoundConquerPoints=33
            BankCps=44
            PkPoints=0
            Online=7
            DonationNobility=0
            [PaidNobility]
            Identifier=1293600
            LastNobilityDonation=100000000
            PaidRank=12
            PeriodTime=0
            IsActive={{expired}}
            Rank=King
            Position=0
            [Notes]
            Keep=unchanged-µ
            """;
    }
}
