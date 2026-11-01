using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyAuthenticatedOrdinaryCreationTests
{
    [Theory]
    [InlineData("unknown-operation-field")]
    [InlineData("source-string")]
    [InlineData("missing-granted-row")]
    [InlineData("wrong-granted-row")]
    [InlineData("items-drift")]
    [InlineData("reverse-prefix")]
    [InlineData("duplicate-player")]
    [InlineData("foreign-stage")]
    public Task CorruptActualDerivedQueryIntentCannotAuthorizeRecovery(string fault) => RunPhysicalDeathAsync(async f =>
    {
        DerivedQueryBefore before;
        using (var interruption = LegacyJournalInterruption.StopAfter(0))
        {
            before = await EquipForDerivedQueryAsync(f, interruption);
            Assert.True(interruption.PendingObserved, f.Owner.Log.ToString());
            Assert.Equal(new[] { 0 }, interruption.Visited);
        }
        Assert.True(f.Owner.Runtime.Recovery.IsBlocked);
        Assert.False(f.Owner.Store.TryGet(f.Target, out _));
        Assert.True(before.Binding.IsRetired);
        Assert.DoesNotContain(f.Owner.Frames(DeathTargetAccount), bytes => bytes.AsSpan().SequenceEqual(DeathGrantedGolden()));
        var record = JsonNode.Parse(File.ReadAllBytes(f.Owner.PendingPath))!;
        Assert.Equal(23, record["Version"]!.GetValue<int>());
        var operation = record["DerivedSpellQuery"]!.AsObject();
        Assert.Equal(DeathTargetUid, operation["TargetUid"]!.GetValue<uint>());
        Assert.Equal(2, operation["Source"]!.GetValue<int>());
        Assert.Equal(20006, operation["GrantedRow"]!["Id"]!.GetValue<int>());
        foreach (string path in new[] { f.CharacterPath, f.SpellsPath })
            Assert.Equal(before.Files[Path.GetRelativePath(f.Owner.Data.Root, path)], File.ReadAllBytes(path));
        AssertDerivedQueryHistory(f, before);

        await f.Owner.CloseAsync();
        bool changedRecord = true;
        switch (fault)
        {
            case "unknown-operation-field": operation["Accepted"] = true; break;
            case "source-string": operation["Source"] = "2"; break;
            case "missing-granted-row": Assert.True(operation.Remove("GrantedRow")); break;
            case "wrong-granted-row": operation["GrantedRow"]!["Id"] = 20007; break;
            case "items-drift":
            {
                byte[] items = File.ReadAllBytes(f.ItemsPath); Assert.NotEmpty(items);
                items[^1] ^= 1; File.WriteAllBytes(f.ItemsPath, items);
                changedRecord = false; break;
            }
            case "reverse-prefix":
                File.WriteAllBytes(f.SpellsPath, before.After[Path.GetRelativePath(f.Owner.Data.Root, f.SpellsPath)]);
                changedRecord = false; break;
            case "duplicate-player":
                File.WriteAllBytes(Path.Combine(f.Owner.Data.Users, (DeathTargetAccount + 1).ToString(CultureInfo.InvariantCulture) + ".ini"),
                    File.ReadAllBytes(f.CharacterPath));
                changedRecord = false; break;
            case "foreign-stage":
                File.WriteAllBytes(f.SpellsPath + ".managed-trade-" + Guid.NewGuid().ToString("N") + ".tmp", [1]);
                changedRecord = false; break;
            default: throw new ArgumentOutOfRangeException(nameof(fault));
        }
        if (changedRecord) File.WriteAllText(f.Owner.PendingPath, record.ToJsonString(), new UTF8Encoding(false));
        var files = f.Owner.Data.Files();
        var targetFrames = f.Owner.Frames(DeathTargetAccount).Select(bytes => bytes.ToArray()).ToArray();
        var actorFrames = f.Owner.Frames(CreationFixture.RuneActor).Select(bytes => bytes.ToArray()).ToArray();
        var newbornFrames = f.Owner.Sent.Select(bytes => bytes.ToArray()).ToArray();
        for (int restart = 0; restart < 2; restart++)
        {
            Assert.False(LegacyOwnedAdmissionRuntime.TryOpen(f.Owner.Data.Loader, f.Owner.Data.Options(),
                out var opened, out var reason), reason);
            Assert.Null(opened); Assert.False(string.IsNullOrWhiteSpace(reason));
            f.Owner.Data.AssertFiles(files); AssertDerivedQueryHistory(f, before);
            Assert.Equal(targetFrames, f.Owner.Frames(DeathTargetAccount));
            Assert.Equal(actorFrames, f.Owner.Frames(CreationFixture.RuneActor));
            Assert.Equal(newbornFrames, f.Owner.Sent);
            using var lease = new FileStream(f.Owner.Data.OwnerLease, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }
    });
}
