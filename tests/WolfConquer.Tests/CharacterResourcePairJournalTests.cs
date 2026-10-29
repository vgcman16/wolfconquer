using System.Globalization;
using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class CharacterResourcePairJournalTests
{
    [Theory]
    [InlineData((int)LegacyCharacterResourcesJournalInterruption.AfterPending, 0)]
    [InlineData((int)LegacyCharacterResourcesJournalInterruption.AfterFirstCharacter, 1)]
    [InlineData((int)LegacyCharacterResourcesJournalInterruption.AfterSecondCharacter, 2)]
    public void CharacterResourcePairFreshRecoveryFinishesActualTwoCharacterPrefix(int interruption, int replacements)
    {
        // No runtime/registry owner is opened: this is a real journal/restart
        // fixture, not authority to replay files belonging to an active World.
        using var data = new LegacyOwnedAdmissionRuntimeFixture(bootstrap: false);
        var prepared = Images(data); var unrelated = data.Files();
        Assert.False(LegacyTradeJournal.TryCommitCharacterResources(data.Users, prepared.Changes.Reverse().ToArray(),
            out var reason, (LegacyCharacterResourcesJournalInterruption)interruption));
        Assert.StartsWith(LegacyTradeJournal.RecoveryRequired + ":", reason);
        for (var index = 0; index < 2; index++)
            Assert.Equal(index < replacements ? prepared.Changes[index].After : prepared.Changes[index].Before,
                File.ReadAllBytes(prepared.Paths[index]));
        var pending = Path.Combine(data.Users, ".managed-trade-journal", "pending.json");
        using (var document = JsonDocument.Parse(File.ReadAllBytes(pending)))
        {
            var root = document.RootElement; Assert.Equal(11, root.GetProperty("Version").GetInt32());
            Assert.Equal(data.Users, root.GetProperty("CharacterResourcesDomain").GetProperty("Users").GetString());
            Assert.Equal(2, root.GetProperty("Changes").GetArrayLength());
            Assert.DoesNotContain(root.EnumerateObject(), property => property.Name == "CharacterItemsSpellsDomain");
        }
        var fresh = new LegacyTradeRecoveryState(data.Users, data.Items);
        Assert.False(fresh.IsReadyForUsers(data.Users)); Assert.True(fresh.TryEnsureInitialized(out reason), reason);
        Assert.True(fresh.IsReadyForUsers(data.Users)); Assert.False(LegacyTradeJournal.HasPending(data.Users));
        for (var index = 0; index < 2; index++) Assert.Equal(prepared.Changes[index].After, File.ReadAllBytes(prepared.Paths[index]));
        // Files() keys are relative to this OWNED fixture root, while journal
        // target paths are absolute. Never resolve snapshot keys against the
        // test process's current directory or compare the two path forms.
        var pathComparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        foreach (var (relativePath, bytes) in unrelated)
        {
            var path = Path.GetFullPath(Path.Combine(data.Root, relativePath));
            if (!prepared.Paths.Contains(path, pathComparer)) Assert.Equal(bytes, File.ReadAllBytes(path));
        }
        Assert.Contains("Sentinel=retain", File.ReadAllText(prepared.Paths[0]), StringComparison.Ordinal);
        Assert.True(new LegacyTradeRecoveryState(data.Users, data.Items).TryEnsureInitialized(out reason), reason);
    }

    [Fact]
    public void CharacterResourcePairForeignImageStopsRecoveryBeforeEitherTargetChanges()
    {
        using var data = new LegacyOwnedAdmissionRuntimeFixture(bootstrap: false);
        var prepared = Images(data);
        Assert.False(LegacyTradeJournal.TryCommitCharacterResources(data.Users, prepared.Changes, out _,
            LegacyCharacterResourcesJournalInterruption.AfterPending));
        File.AppendAllText(prepared.Paths[1], "; independent foreign image\r\n");
        var files = data.Files();
        var fresh = new LegacyTradeRecoveryState(data.Users, data.Items);
        Assert.False(fresh.TryEnsureInitialized(out var reason)); Assert.StartsWith(LegacyTradeJournal.RecoveryRequired, reason);
        data.AssertFiles(files); Assert.True(LegacyTradeJournal.HasPending(data.Users));
    }

    private static (LegacyTradeJournalChange[] Changes, string[] Paths) Images(LegacyOwnedAdmissionRuntimeFixture data)
    {
        var firstId = LegacyOwnedAdmissionRuntimeFixture.Uid; var secondId = checked(firstId + 1);
        var firstBefore = File.ReadAllBytes(data.UserPath);
        var firstText = CharacterSnapshotFileEncoding.Decode(firstBefore, out var encoding);
        var secondText = firstText.Replace("UID=" + firstId.ToString(CultureInfo.InvariantCulture),
            "UID=" + secondId.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        byte[] Bytes(string text) => encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray();
        var secondBefore = Bytes(secondText);
        var secondPath = Path.Combine(data.Users, secondId.ToString(CultureInfo.InvariantCulture) + ".ini");
        File.WriteAllBytes(secondPath, secondBefore);
        byte[] After(string text) => Bytes(LegacyOrdinaryAdmissionCharacterText.Project(text,
            new Dictionary<string, string> { ["Stamina"] = "60" }, paid: null));
        return ([new(LegacyTradeJournalFileKind.Character, firstId, firstBefore, After(firstText)),
            new(LegacyTradeJournalFileKind.Character, secondId, secondBefore, After(secondText))], [data.UserPath, secondPath]);
    }
}
