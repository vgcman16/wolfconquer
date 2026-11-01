using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyAuthenticatedOrdinaryCreationTests
{
    private const uint HitRecoveryWeaponUid = 5_337_701;
    private sealed record HitRecoveryCapture(CreationFixture Owner, ConquerTcpConnection Target, Dictionary<string, byte[]> Before,
        Dictionary<string, byte[]> After, Dictionary<string, byte[]> History, byte[] Ledger, byte[] CreationPayload,
        JsonNode Record, string[] Targets, string Transaction, long TargetHp, uint ActorAccount);

    private static Task RunHitRecoveryAsync(bool lethal, Func<CreationFixture, ConquerTcpConnection, Task> body,
        bool changedDeath = false, uint? actorAccount = null) => Run(async f =>
    {
        await CreateAndAssert(f); await DepositNewbornMoney(f);
        if (actorAccount is { } account) await f.ConnectHitRecoveryActorAsync(account);
        else await f.ConnectRuneActorAsync();
        var target = f.NewConnection(DeathTargetAccount);
        await f.SendOnAsync(target, new LegacyMsgLoginGamePacket(DeathTargetAccount).ToFrameBytes());
        var actor = f.CurrentFor(f.RuneConnection!); var victim = f.CurrentFor(target);
        Assert.Equal((long)DeathTargetAccount, victim.AccountId); Assert.Equal((long)DeathTargetUid, victim.Character.Uid);
        Assert.NotEqual(victim.AccountId, victim.Character.Uid);
        Assert.Equal(lethal ? 200L : 2000L, victim.Character.MinHitPoints);
        Assert.Equal(HitRecoveryWeaponUid, Assert.Single(actor.PlayerItems.EquipmentItems).Uid);
        f.Sent.Clear(); f.Frames(actorAccount ?? CreationFixture.RuneActor).Clear(); f.Frames(DeathTargetAccount).Clear();
        await body(f, target);
    }, beforeBootstrap: f => { SeedHitRecovery(f, lethal, changedDeath); if (actorAccount is { } account)
        File.Move(f.Data.UserPath, Path.Combine(f.Data.Users, account.ToString(CultureInfo.InvariantCulture) + ".ini")); });

    private static void SeedHitRecovery(CreationFixture f, bool lethal, bool changedDeath)
    {
        f.ConfigurePhysicalDeathInputs(true, deferredDeathQuery: changedDeath);
        if (!lethal) WriteDeathCharacter(DeathFile(f, LegacyTradeJournalFileKind.Character),
            DeathSavedCharacter(DeathTargetUid, "DeathTarget", 7051, 831, 2000, 0));
        var loader = new PlayerItemSnapshotLoader(f.Data.Items);
        var actorItems = loader.LoadByCharacterUid(CreationFixture.RuneActor);
        loader.SaveByCharacterUid(CreationFixture.RuneActor, actorItems with
        {
            EquipmentItems = [new PlayerItemSnapshotItem { Uid = HitRecoveryWeaponUid, ItemId = 410009, Position = 4,
                Durability = 100, MaximumDurability = 100, StackSize = 1 }]
        });
        var prof = PlayerProficiencySnapshotLoader.ToLegacyBytes(new PlayerProficiencySnapshot([new(410, 5, 0, 100, 1_600_000)]));
        prof[13] = 0xA5; prof[14] = 0xC7; prof[15] = 0xD9;
        File.WriteAllBytes(Path.Combine(f.Data.Proficiencies, CreationFixture.RuneActor.ToString(CultureInfo.InvariantCulture) + ".bin"), prof);
        f.ConfigureHitRecoveryCatalog();
    }

    internal sealed partial class CreationFixture
    {
        internal void ConfigureHitRecoveryCatalog()
        {
            Assert.Null(Runtime); // Only declared pre-bootstrap fixture input configures the catalog.
            var catalog = Catalog;
            Catalog = NativeCallerWorldDataFixture.Create(catalog.Items.Concat([OwnedEquipmentBehaviorFixture.Item(410009)]).ToArray(), [],
                magic: catalog.Magic.ToArray(), magicEffects: catalog.MagicEffects.ToArray(),
                professionLevelBenefits: catalog.ProfessionLevelBenefits.ToArray(), runeStorageAttributes: catalog.RuneStorageAttributes.ToArray(),
                runeLevelExperience: catalog.RuneLevelExperience.ToArray(), innerPowerRanks: catalog.InnerPowerRanks.ToArray());
        }
    }

    private static async Task<HitRecoveryCapture> CaptureHitRecoveryAsync(CreationFixture f, ConquerTcpConnection target,
        bool lethal, int boundary, bool flushed = false, bool changedDeath = false,
        LegacyJournalFault? thrownFault = null, int? leaseTarget = null)
    {
        var before = f.Data.Files(); var after = before.ToDictionary(row => row.Key, row => row.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
        string character = DeathFile(f, LegacyTradeJournalFileKind.Character);
        string spells = DeathFile(f, LegacyTradeJournalFileKind.Spells);
        string prof = Path.Combine(f.Data.Proficiencies, CreationFixture.RuneActor.ToString(CultureInfo.InvariantCulture) + ".bin");
        string charKey = Path.GetRelativePath(f.Data.Root, character), profKey = Path.GetRelativePath(f.Data.Root, prof);
        string text = CharacterSnapshotFileEncoding.Decode(before[charKey], out var encoding);
        string oldHp = lethal ? "MinHitPoints=200\r\n" : "MinHitPoints=2000\r\n";
        string newHp = lethal ? "MinHitPoints=0\r\n" : "MinHitPoints=1700\r\n";
        Assert.Equal(2, text.Split(oldHp, StringSplitOptions.None).Length);
        after[charKey] = changedDeath ? DeathHpZero(before[charKey], changed: true) :
            encoding.GetPreamble().Concat(encoding.GetBytes(text.Replace(oldHp, newHp, StringComparison.Ordinal))).ToArray();
        if (changedDeath)
        {
            Assert.True(lethal);
            string spellKey = Path.GetRelativePath(f.Data.Root, spells);
            var originalSpells = before[spellKey]; Assert.Equal(16, originalSpells.Length); Assert.Equal((byte)0xC7, originalSpells[15]);
            var updatedSpells = new byte[28]; originalSpells.CopyTo(updatedSpells, 0);
            BinaryPrimitives.WriteInt32LittleEndian(updatedSpells, 2);
            BinaryPrimitives.WriteUInt16LittleEndian(updatedSpells.AsSpan(16), 20000);
            BinaryPrimitives.WriteUInt16LittleEndian(updatedSpells.AsSpan(18), 5);
            after[spellKey] = updatedSpells;
        }
        BinaryPrimitives.WriteUInt32LittleEndian(after[profKey].AsSpan(16), 50_100);
        var actor = f.CurrentFor(f.RuneConnection!); var victim = f.CurrentFor(target);
        var actorAccount = checked((uint)actor.AccountId);
        var actorBinding = actor.NativeGameClient!.OrdinarySpellBinding!; var targetBinding = victim.NativeGameClient!.OrdinarySpellBinding!;
        var stamp = actorBinding.Player.AttackStamp; var action = actorBinding.Player.Action;
        var proficiencyMap = actorBinding.Proficiencies.ClientProf;
        var proficiency = proficiencyMap[410]; var proficiencyExperience = proficiency.Experience;
        var history = HistoryFiles(f); var ledger = File.ReadAllBytes(f.Data.State); var payload = CompletedIntent(f).CopyPayload();
        using (var fault = thrownFault is { } kind ? LegacyJournalInterruption.ThrowAfter(boundary, kind) :
            leaseTarget is { } ordinal ? LegacyJournalInterruption.LeaseTargetAfter(boundary, ordinal) :
            flushed ? LegacyJournalInterruption.StopAfterStageFlushed(boundary) : LegacyJournalInterruption.StopAfter(boundary))
        {
            f.Handler.PhysicalDeathJournalInterruption = fault;
            try { await f.SendOnAsync(f.RuneConnection!, DeathAttackRequest()); }
            finally { f.Handler.PhysicalDeathJournalInterruption = null; }
            Assert.True(fault.PendingObserved, f.Log.ToString());
            Assert.Equal(flushed ? new[] { 0, boundary } : Enumerable.Range(0, boundary + 1), fault.Visited);
        }
        Assert.True(f.Runtime.Recovery.IsBlocked); Assert.False(f.Store.TryGet(f.RuneConnection!, out _));
        if (thrownFault is not null || leaseTarget is not null)
        {
            Assert.False(f.Runtime.Recovery.TryRequireLiveJournalClear(out var failure));
            string expectedFailure = thrownFault switch
            {
                LegacyJournalFault.ThrowSecurity => "SecurityException",
                LegacyJournalFault.ThrowInvalidOperation => "InvalidOperationException",
                _ => "IOException",
            };
            Assert.Contains("hitProgressionUncertain:" + expectedFailure, failure, StringComparison.Ordinal);
        }
        Assert.False(f.Store.TryGet(target, out _)); Assert.True(actorBinding.IsRetired); Assert.True(targetBinding.IsRetired);
        Assert.Empty(f.Frames(actorAccount)); Assert.Empty(f.Frames(DeathTargetAccount)); Assert.Empty(f.Sent);
        Assert.Equal(lethal ? 200 : 2000, targetBinding.Player.HitPoints); Assert.False(actorBinding.Client.OnAutoAttack);
        Assert.Equal(stamp, actorBinding.Player.AttackStamp); Assert.Equal(action, actorBinding.Player.Action);
        Assert.Same(proficiencyMap, actorBinding.Proficiencies.ClientProf); Assert.Same(proficiency, proficiencyMap[410]);
        Assert.Equal(proficiencyExperience, proficiency.Experience);
        var record = JsonNode.Parse(File.ReadAllBytes(f.PendingPath))!; Assert.Equal(24, record["Version"]!.GetValue<int>());
        var operation = record["PhysicalHitProgression"]!;
        Assert.Equal((long)actorAccount, operation["ActorAccountId"]!.GetValue<long>());
        Assert.Equal(CreationFixture.RuneActor, operation["ActorUid"]!.GetValue<uint>());
        Assert.Equal((long)DeathTargetAccount, operation["TargetAccountId"]!.GetValue<long>());
        Assert.Equal(DeathTargetUid, operation["TargetUid"]!.GetValue<uint>());
        Assert.Equal(300U, operation["RawExperience"]!.GetValue<uint>());
        Assert.Equal(lethal ? 200U : 300U, operation["AppliedDamage"]!.GetValue<uint>());
        Assert.Equal(lethal, operation["Death"] is not null);
        var targets = lethal ? new[] { character, spells, prof } : new[] { character, prof };
        var changes = record["Changes"]!.AsArray(); Assert.Equal(targets.Length, changes.Count);
        for (int index = 0; index < targets.Length; index++)
        {
            var change = changes[index]!; string key = Path.GetRelativePath(f.Data.Root, targets[index]);
            var kind = index == 0 ? LegacyTradeJournalFileKind.Character : lethal && index == 1 ? LegacyTradeJournalFileKind.Spells : LegacyTradeJournalFileKind.Proficiencies;
            long id = index == 0 ? DeathTargetAccount : lethal && index == 1 ? DeathTargetUid : CreationFixture.RuneActor;
            Assert.Equal((int)kind, change["Kind"]!.GetValue<int>()); Assert.Equal(id, change["FileId"]!.GetValue<long>());
            Assert.Equal(before[key], Convert.FromBase64String(change["Before"]!.GetValue<string>()));
            Assert.Equal(after[key], Convert.FromBase64String(change["After"]!.GetValue<string>()));
            int completed = flushed ? 0 : boundary;
            Assert.Equal(index < completed ? after[key] : before[key], File.ReadAllBytes(targets[index]));
        }
        string transaction = record["TransactionId"]!.GetValue<string>();
        var expectedKeys = before.Keys.Append(Path.GetRelativePath(f.Data.Root, f.PendingPath));
        if (flushed)
            foreach (string path in targets.Take(boundary))
                expectedKeys = expectedKeys.Append(Path.GetRelativePath(f.Data.Root, path + ".managed-trade-" + transaction + ".tmp"));
        Assert.Equal(expectedKeys.Order(StringComparer.OrdinalIgnoreCase), f.Data.Files().Keys.Order(StringComparer.OrdinalIgnoreCase));
        foreach (var row in before)
            if (!targets.Contains(Path.Combine(f.Data.Root, row.Key), StringComparer.OrdinalIgnoreCase))
                Assert.Equal(row.Value, File.ReadAllBytes(Path.Combine(f.Data.Root, row.Key)));
        AssertHistoricalFiles(history, HistoryFiles(f)); Assert.Equal(ledger, File.ReadAllBytes(f.Data.State));
        return new(f, target, before, after, history, ledger, payload, record, targets,
            transaction, lethal ? 200 : 2000, actorAccount);
    }

    private static async Task<HitRecoveryCapture> CaptureHitRecoveryDuringEquipAsync(CreationFixture f,
        ConquerTcpConnection target, int boundary, bool flushed = false, LegacyJournalFault? thrownFault = null,
        int? leaseTarget = null)
    {
        var clock = Assert.IsType<PhysicalDeathClock>(f.DeathClock);
        var initial = f.CurrentFor(target);
        Assert.Equal(10_000, clock.Now);
        Assert.True(LegacyPlayerDeathState.HasStatusFlag(initial.Character.Attributes, 212));
        Assert.Equal(10_000, LegacyFineRainEquipmentAdmission.ForOwner(initial).StampMilliseconds);
        // The real equip completes before the hit. FineRain makes its own
        // query refuse, leaving the actual derived spell for the death owner.
        await f.SendOnAsync(target, new MsgItemUsagePacket(DeathArmorUid, 3, MsgItemUsageAction.Equip, 1234, 0, 0, 0).ToFrameBytes());
        Assert.Contains(f.Frames(DeathTargetAccount), bytes => bytes.AsSpan().SequenceEqual(
            LegacyMsgMessagePacket.SystemTalk("You Shoud wait 6 seconds.").ToFrameBytes()));
        var equipped = Assert.Single(f.CurrentFor(target).PlayerItems.EquipmentItems);
        Assert.Equal(DeathArmorUid, equipped.Uid); Assert.Equal((ushort)3, equipped.Position);
        Assert.False(f.CurrentFor(target).PlayerSpells.ContainsSpell(20000));
        Assert.False(f.CurrentFor(target).NativeGameClient!.MySpells!.ClientSpells.ContainsKey(20000));
        Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        clock.Now = 16_001;
        Assert.True(LegacyEquipmentRebuildAdmission.ApplyCurrent(f.CurrentFor(target),
            LegacyEquipmentRebuildReason.PlayerDeath, clock).Admitted);
        f.Sent.Clear(); f.Frames(CreationFixture.RuneActor).Clear(); f.Frames(DeathTargetAccount).Clear();
        return await CaptureHitRecoveryAsync(f, target, true, boundary, flushed, changedDeath: true,
            thrownFault: thrownFault, leaseTarget: leaseTarget);
    }
}
