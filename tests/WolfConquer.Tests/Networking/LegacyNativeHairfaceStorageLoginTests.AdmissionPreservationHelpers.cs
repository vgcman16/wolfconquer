using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHairfaceStorageLoginTests
{
    private static void AssertOrdinaryBusinessPreserved(CharacterSnapshot before, CharacterSnapshot after,
        long? expectedRuntimeHitpoints = null)
    {
        var empty = new Dictionary<string, string>();
        Assert.Equal(before with { Online = 1, MinHitPoints = expectedRuntimeHitpoints ?? before.MinHitPoints,
                SourceFile = "", Attributes = empty, PaidNobility = empty, PlayerQuests = PlayerQuestSnapshot.Empty },
            after with { SourceFile = "", Attributes = empty, PaidNobility = empty, PlayerQuests = PlayerQuestSnapshot.Empty });
    }

    private static void AssertOnlyPresenceChanged(LoginInput input, byte[] before)
    {
        var expected = Encoding.UTF8.GetString(before).Replace("\r\nOnline=7\r\n", "\r\nOnline=1\r\n", StringComparison.Ordinal);
        Assert.Equal(Encoding.UTF8.GetBytes(expected), File.ReadAllBytes(input.FilePath));
    }

    private static Func<int> ObserveRawAdmissionCommit(NativePlayerDiagnosticLog log, Action assertCommitted)
    {
        var observations = 0;
        log.ObserveLine = line =>
        {
            if (!line.Contains("ordinary admission committed key=", StringComparison.Ordinal)) return;
            Interlocked.Increment(ref observations);
            assertCommitted();
        };
        return () => Volatile.Read(ref observations);
    }

    private static void AssertAdmissionFilesAfterRuntimeHpSave(Dictionary<string, byte[]?> committed,
        LoginInput input, long hitpoints, long maxMana = 0, long expBallUsed = 0)
    {
        var expected = new Dictionary<string, byte[]?>(committed);
        expected[input.FilePath] = OrdinaryAdmissionImageExpectations.RuntimeHpSave(
            committed[input.FilePath]!, hitpoints, maxMana, expBallUsed);
        AssertAdmissionFiles(expected);
    }

    private static void AssertNoOrdinaryAllocation(AdmissionUidFixture uids, byte[] before)
    {
        Assert.Equal(before, uids.StateBytes());
        Assert.True(uids.Registry.TryReadActiveProducerIntents(uids.Domain, out var active, out var reason), reason);
        Assert.DoesNotContain(active, row => row.Request.Writer == LegacyOrdinaryAdmissionUidOwner.Writer);
        Assert.True(uids.Registry.TryReadProducerIntents(uids.Domain, out var all, out reason), reason);
        Assert.DoesNotContain(all, row => row.Request.Writer == LegacyOrdinaryAdmissionUidOwner.Writer);
    }

    private static void SeedOwnedOrdinaryParticipants(LoginInput input)
    {
        PrepareAdmissionInput(input, starter: true);
        var root = AdmissionRoot(input);
        var owned = new PlayerItemSnapshotItem { Uid = 100, ItemId = 420301, Position = 4, Durability = 17,
            MaximumDurability = 321, Plus = 3, Bound = 1, Locked = 1, Color = 7 };
        File.WriteAllBytes(Path.Combine(root, "PlayersItems", Uid + ".bin"),
            PlayerItemSnapshotLoader.ToLegacyBytes(new([], [], [], [owned], [])));
        File.WriteAllBytes(Path.Combine(root, "PlayersSpells", Uid + ".bin"),
            PlayerSpellSnapshotLoader.ToLegacyBytes(new PlayerSpellSnapshot([new(8002, 3, 123, 2, 1, 1)])));
        File.WriteAllBytes(Path.Combine(root, "PlayersProfs", Uid + ".bin"), new byte[4]);
    }
}
