using System.Text.Json.Nodes;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyAuthenticatedOrdinaryCreationTests
{
    private static async Task<HitRecoveryCapture> CaptureFirstUseRecoveryAsync(FirstUseHitFixture f, int boundary, bool flushed)
    {
        var owner = f.Owner; var before = owner.Data.Files(); var after = FirstUseAfterImages(f, before);
        var actor = f.Actor; var target = f.Victim;
        var binding = actor.NativeGameClient!.OrdinarySpellBinding!;
        var targetBinding = target.NativeGameClient!.OrdinarySpellBinding!;
        var map = binding.Proficiencies.ClientProf; Assert.Empty(map);
        var origin = binding.SavedProficiencies; Assert.True(origin.Matches(null));
        var targetOrigin = targetBinding.SavedSpells;
        var stamp = binding.Player.AttackStamp; var action = binding.Player.Action;
        var history = HistoryFiles(owner); var ledger = File.ReadAllBytes(owner.Data.State); var creation = CompletedIntent(owner).CopyPayload();
        using (var fault = flushed ? LegacyJournalInterruption.StopAfterStageFlushed(boundary) : LegacyJournalInterruption.StopAfter(boundary))
        {
            owner.Handler.PhysicalDeathJournalInterruption = fault;
            try { await f.AttackAsync(); }
            finally { owner.Handler.PhysicalDeathJournalInterruption = null; }
            Assert.True(fault.PendingObserved, owner.Log.ToString());
            Assert.Equal(flushed ? new[] { 0, boundary } : Enumerable.Range(0, boundary + 1), fault.Visited);
        }
        Assert.True(owner.Runtime.Recovery.IsBlocked);
        Assert.False(owner.Store.TryGet(owner.RuneConnection!, out _)); Assert.False(owner.Store.TryGet(f.Target, out _));
        Assert.True(binding.IsRetired); Assert.True(targetBinding.IsRetired); Assert.False(binding.Client.OnAutoAttack);
        Assert.Equal(stamp, binding.Player.AttackStamp); Assert.Equal(action, binding.Player.Action);
        Assert.Same(map, binding.Proficiencies.ClientProf); Assert.Empty(map); Assert.Same(origin, binding.SavedProficiencies);
        Assert.Same(targetOrigin, targetBinding.SavedSpells);
        Assert.Equal(f.Lethal ? 200 : 2000, targetBinding.Player.HitPoints);
        Assert.Empty(owner.Sent); Assert.Empty(owner.Frames(CreationFixture.RuneActor)); Assert.Empty(owner.Frames(DeathTargetAccount));
        var record = JsonNode.Parse(File.ReadAllBytes(owner.PendingPath))!;
        Assert.Equal(24, record["Version"]!.GetValue<int>());
        var operation = record["PhysicalHitProgression"]!;
        Assert.Equal((long)CreationFixture.RuneActor, operation["ActorAccountId"]!.GetValue<long>());
        Assert.Equal(CreationFixture.RuneActor, operation["ActorUid"]!.GetValue<uint>());
        Assert.Equal((long)DeathTargetAccount, operation["TargetAccountId"]!.GetValue<long>());
        Assert.Equal(DeathTargetUid, operation["TargetUid"]!.GetValue<uint>());
        Assert.Equal(300U, operation["RawExperience"]!.GetValue<uint>());
        Assert.Equal(f.Lethal ? 200U : 300U, operation["AppliedDamage"]!.GetValue<uint>());
        Assert.Equal(f.Lethal, operation["Death"] is not null);
        string[] targets = f.Lethal ? [f.CharacterPath, f.SpellsPath, f.ProfsPath] : [f.CharacterPath, f.ProfsPath];
        var changes = record["Changes"]!.AsArray(); Assert.Equal(targets.Length, changes.Count);
        var expected = before.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
        var transaction = record["TransactionId"]!.GetValue<string>();
        for (var i = 0; i < targets.Length; i++)
        {
            var key = Path.GetRelativePath(owner.Data.Root, targets[i]); var prior = before.GetValueOrDefault(key);
            var change = changes[i]!;
            var kind = i == 0 ? LegacyTradeJournalFileKind.Character : f.Lethal && i == 1
                ? LegacyTradeJournalFileKind.Spells : LegacyTradeJournalFileKind.Proficiencies;
            long id = i == 0 ? DeathTargetAccount : f.Lethal && i == 1 ? DeathTargetUid : CreationFixture.RuneActor;
            Assert.Equal((int)kind, change["Kind"]!.GetValue<int>()); Assert.Equal(id, change["FileId"]!.GetValue<long>());
            if (prior is null) Assert.Null(change["Before"]);
            else Assert.Equal(prior, Convert.FromBase64String(change["Before"]!.GetValue<string>()));
            Assert.Equal(after[key], Convert.FromBase64String(change["After"]!.GetValue<string>()));
            if (!flushed && i < boundary) expected[key] = after[key];
            if (flushed && i < boundary)
                expected[Path.GetRelativePath(owner.Data.Root, targets[i] + ".managed-trade-" + transaction + ".tmp")] = after[key];
        }
        expected[Path.GetRelativePath(owner.Data.Root, owner.PendingPath)] = File.ReadAllBytes(owner.PendingPath);
        owner.Data.AssertFiles(expected); AssertHistoricalFiles(history, HistoryFiles(owner));
        Assert.Equal(ledger, File.ReadAllBytes(owner.Data.State));
        return new(owner, f.Target, before, after, history, ledger, creation, record, targets,
            transaction, f.Lethal ? 200 : 2000, CreationFixture.RuneActor);
    }
}
