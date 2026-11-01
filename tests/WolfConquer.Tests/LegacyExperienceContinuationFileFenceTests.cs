using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Negative file-boundary controls only. A DATA marker and an unregistered
// object do not issue a combat award, admission or continuation authority.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyExperienceContinuationFileFenceTests
{
    [Fact]
    public void MalformedPendingMarkerRefusesConfiguredEscrowRecoveryBeforeRefund()
    {
        var root = Path.Combine(Path.GetTempPath(), "wolf-continuation-invalid-" + Guid.NewGuid().ToString("N"));
        string Dir(string name) { var path = Path.Combine(root, name); Directory.CreateDirectory(path); return path; }
        var users = Dir("Users"); var items = Dir("Items"); var quests = Dir("Quests"); var mail = Dir("Mail");
        var gear = Dir("Gear"); var spells = Dir("Spells"); var profs = Dir("Proficiencies");
        try
        {
            var path = Path.Combine(users, "101.ini");
            var before = Encoding.UTF8.GetBytes("[Character]\r\nUID=22\r\nLevel=10\r\nExperience=11\r\nMoney=100\r\n" +
                "ConquerPoints=20\r\nManagedTradeEscrowId=79a45e4120eb4ead8a02f7891e44dfc8\r\n" +
                "ManagedTradeEscrowMoney=7\r\nManagedTradeEscrowCP=3\r\nExperienceContinuation=malformed\r\n");
            File.WriteAllBytes(path, before);
            var recovery = new LegacyTradeRecoveryState(users, items, quests, mail, gear, spells, profs);
            Assert.False(recovery.TryEnsureInitialized(out _));
            Assert.True(recovery.IsBlocked);
            Assert.Equal(before, File.ReadAllBytes(path));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void ConfiguredStartupRecoveryRefundsEscrowAndPreservesPendingMarkerAndCompanionBytes()
    {
        var root = Path.Combine(Path.GetTempPath(), "wolf-continuation-recovery-" + Guid.NewGuid().ToString("N"));
        string Dir(string name) { var path = Path.Combine(root, name); Directory.CreateDirectory(path); return path; }
        var users = Dir("Users"); var items = Dir("Items"); var quests = Dir("Quests"); var mail = Dir("Mail");
        var gear = Dir("Gear"); var spells = Dir("Spells"); var profs = Dir("Proficiencies");
        try
        {
            var path = Path.Combine(users, "101.ini");
            var state = LegacyExperienceContinuation.CreateCredit(101, 22,
                Guid.Parse("7c2f038a-3c4e-4d78-a537-7f0826c3050c"), 10, 1, 10);
            string claim = "79a45e4120eb4ead8a02f7891e44dfc8";
            var text = "[Character]\r\nUID=22\nLevel=10\r\nExperience=11\nStrength=1\r\nAgility=2\n" +
                "Vitaliti=3\r\nSpirit=4\nAtributes=5\r\nMinHitPoints=123\nMinMana=45\r\nMaxMana=300\n" +
                "Money=100\r\nConquerPoints=20\nManagedTradeEscrowId=" + claim + "\r\n" +
                "ManagedTradeEscrowMoney=7\nManagedTradeEscrowCP=3\r\nExperienceContinuation=" +
                LegacyExperienceContinuationCodec.Encode(state) + "\nOther=preserve\r\nother=opaque\n";
            File.WriteAllBytes(path, Encoding.UTF8.GetBytes(text));
            var recovery = new LegacyTradeRecoveryState(users, items, quests, mail, gear, spells, profs);
            Assert.True(recovery.TryEnsureInitialized(out var reason), reason);
            var expected = text.Replace("Money=100", "Money=107", StringComparison.Ordinal)
                .Replace("ConquerPoints=20", "ConquerPoints=23", StringComparison.Ordinal)
                .Replace("ManagedTradeEscrowId=" + claim, "ManagedTradeEscrowId=", StringComparison.Ordinal)
                .Replace("ManagedTradeEscrowMoney=7", "ManagedTradeEscrowMoney=0", StringComparison.Ordinal)
                .Replace("ManagedTradeEscrowCP=3", "ManagedTradeEscrowCP=0", StringComparison.Ordinal);
            Assert.Equal(Encoding.UTF8.GetBytes(expected), File.ReadAllBytes(path));
            Assert.Equal(state, LegacyExperienceContinuationCodec.ReadCharacterImage(File.ReadAllBytes(path), 101, 22));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void PendingCharacterRefusesGenericSectionAndCapturedImageWritersAndUnregisteredPermit()
    {
        var root = Path.Combine(Path.GetTempPath(), "wolf-continuation-fence-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "101.ini");
            var state = LegacyExperienceContinuation.CreateCredit(101, 22,
                Guid.Parse("7c2f038a-3c4e-4d78-a537-7f0826c3050c"), 10, 1, 10);
            var text = "[Character]\r\nUID=22\r\nLevel=10\r\nExperience=11\r\nMoney=5\r\n" +
                "ExperienceContinuation=" + LegacyExperienceContinuationCodec.Encode(state) + "\r\n";
            var before = Encoding.UTF8.GetBytes(text); File.WriteAllBytes(path, before);
            var changed = Encoding.UTF8.GetBytes(text.Replace("Money=5", "Money=10", StringComparison.Ordinal));
            Assert.False(CharacterSnapshotFileCoordinator.TryUpdateCharacterAttributes(path, 22,
                new Dictionary<string, string> { ["Money"] = "10" }, false, out var sectionReason));
            Assert.Equal("experienceContinuationPendingRequiresOwner", sectionReason);
            Assert.False(CharacterSnapshotFileCoordinator.TryReplaceCapturedImage(path, before, changed,
                out var uncertain, out var imageReason));
            Assert.False(uncertain); Assert.Equal("experienceContinuationPendingRequiresOwner", imageReason);
            Assert.False(CharacterSnapshotFileCoordinator.TryReplaceContinuationCapturedImage(path, before, changed,
                new WorldSessionStore.ContinuationFilePermit(), out uncertain, out imageReason));
            Assert.False(uncertain); Assert.Equal("experienceContinuationPendingRequiresOwner", imageReason);
            Assert.Equal(before, File.ReadAllBytes(path));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void GenericSectionWriterCannotInsertMarkerIntoClearCharacter()
    {
        var root = Path.Combine(Path.GetTempPath(), "wolf-continuation-fence-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "101.ini");
            var before = Encoding.UTF8.GetBytes("[Character]\r\nUID=22\r\nLevel=10\r\nExperience=1\r\n");
            File.WriteAllBytes(path, before);
            Assert.False(CharacterSnapshotFileCoordinator.TryUpdateCharacterAttributes(path, 22,
                new Dictionary<string, string> { ["experiencecontinuation"] = "malformed" }, false, out var reason));
            Assert.Equal("experienceContinuationPendingRequiresOwner", reason);
            Assert.Equal(before, File.ReadAllBytes(path));
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
