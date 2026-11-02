using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class OwnedEquipmentNativeCombatStatusTests
{
    private const uint Actor = OwnedEquipmentBehaviorFixture.Actor;
    private static Task Query(OwnedEquipmentBehaviorFixture f) => f.SendAsync(Actor,
        new MsgActionPacket { ObjId = Actor, Type = (MsgActionType)251 }.ToFrameBytes());

    private static Task Run(Func<OwnedEquipmentBehaviorFixture, Task> action)
    {
        var inputs = LegacyMagicAttackRuntimeTests.CreateOwnedEquipmentCacheInputs();
        var clock = new FrozenClock();
        return OwnedEquipmentBehaviorFixture.RunWithOptionsAsync(inputs.Input, inputs.World,
            new LegacyRuntimeUpdateLoopOptions(false, TimeSpan.FromSeconds(1), clock,
                LegacySignedMillisecondClock.FromTimeProvider(clock, 10_000)), action);
    }
    private sealed class FrozenClock : TimeProvider
    {
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => 0;
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
    }

    private static (uint, uint, uint, uint, uint) Values(LegacyNativeMsgStatus s) =>
        (s.MaxAttack, s.MinAttack, s.Defence, s.MagicAttack, s.MagicDefence);
    private static (uint, uint, uint, uint, uint) Values(LegacyPlayerStatusPacket p) =>
        (p.MaxAttack, p.MinAttack, p.Defence, p.MagicAttack, p.MagicDefence);
    private static LegacyNativeMsgStatus Native(OwnedEquipmentBehaviorFixture f) =>
        f.Current().NativeGameClient!.OrdinarySpellBinding!.Client.Status;

    // Read-only observation of the actual query registry; it cannot issue an
    // epoch or completion. Currentness uses the real Store/actor gates.
    private static bool HasActualCurrentCompletion(OwnedEquipmentBehaviorFixture f)
    {
        var current = f.Current(); var binding = current.NativeGameClient!.OrdinarySpellBinding!;
        var type = typeof(LegacyNativeMagicQueryClient);
        var entries = type.GetField("Entries", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        var completions = type.GetField("CurrentMaximumCompletions", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        object?[] args = [binding.NativeIdentity, null];
        if (!(bool)entries.GetType().GetMethod("TryGetValue")!.Invoke(entries, args)!) return false;
        var entry = args[1]!; args = [entry, null];
        if (!(bool)completions.GetType().GetMethod("TryGetValue")!.Invoke(completions, args)!) return false;
        var proof = Assert.IsType<LegacyNativeMagicQueryClient.CompletedMaximums>(args[1]);
        bool valid = false;
        Assert.True(f.Store.WithCurrentNativeClientIdentity(f.Connection(Actor), Actor, identity =>
        {
            Assert.Same(binding.NativeIdentity, identity);
            lock (binding.Client.SpellAdmissionSyncRoot)
            lock (binding.Player.SpellAdmissionSyncRoot)
                valid = proof.IsCurrentPlain(binding, current);
        }));
        return valid;
    }

    [Fact]
    public Task CompletedActualQuerySynchronizesSameNativeStatusToEmittedPacket() => Run(async f =>
    {
        var status = Native(f); var files = f.Data.Files(); int start = f.ActorFrames.Count;
        await Query(f);
        var packetBytes = Assert.Single(f.ActorFrames.Skip(start), bytes =>
            LegacyPlayerStatusPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out _));
        Assert.True(LegacyPlayerStatusPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(packetBytes), out var packet));
        Assert.Same(status, Native(f));
        Assert.Equal(Values(packet), Values(status));
        Assert.True(HasActualCurrentCompletion(f));
        Assert.True(f.ObserveCurrentVigorQualification());
        Assert.True(packet.MaxAttack > 0 || packet.Defence > 0 || packet.MagicAttack > 0, f.Log.ToString());
        f.Data.AssertFiles(files);
    });

    [Fact]
    public Task ActualNativeStatusDriftDuringSentStatusRefusesNativePublication() => Run(async f =>
    {
        Assert.True(HasActualCurrentCompletion(f)); Assert.True(f.ObserveCurrentVigorQualification());
        var status = Native(f); var before = Values(status);
        var maxima = (status.MaxHitpoints, status.MaxMana); var files = f.Data.Files(); bool observed = false;
        f.BeforeSend = (uid, bytes) =>
        {
            if (uid != Actor || observed || !LegacyPlayerStatusPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out _)) return;
            observed = true;
            status.Defence = unchecked(status.Defence + 1);
        };
        try { await Query(f); } finally { f.BeforeSend = null; }
        Assert.True(observed, f.Log.ToString());
        Assert.Same(status, Native(f));
        Assert.Equal((before.Item1, before.Item2, unchecked(before.Item3 + 1), before.Item4, before.Item5), Values(status));
        Assert.Equal(maxima, (status.MaxHitpoints, status.MaxMana));
        Assert.False(HasActualCurrentCompletion(f)); Assert.False(f.ObserveCurrentVigorQualification());
        f.Data.AssertFiles(files);
    });

    [Fact]
    public Task ReplacedActualNativeHolderDuringQueryIsNotGrantedPublication() => Run(async f =>
    {
        Assert.True(HasActualCurrentCompletion(f)); Assert.True(f.ObserveCurrentVigorQualification());
        var binding = f.Current().NativeGameClient!.OrdinarySpellBinding!;
        var original = binding.Client.Status; var before = Values(original);
        var replacement = new LegacyNativeMsgStatus(); var replacementBefore = Values(replacement);
        var files = f.Data.Files(); bool observed = false;
        f.BeforeSend = (uid, bytes) =>
        {
            if (uid != Actor || observed || !LegacyPlayerStatusPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out _)) return;
            observed = true;
            binding.Client.Status = replacement;
        };
        try { await Query(f); }
        finally { f.BeforeSend = null; binding.Client.Status = original; }
        Assert.True(observed, f.Log.ToString());
        Assert.Equal(before, Values(original)); Assert.Equal(replacementBefore, Values(replacement));
        Assert.False(HasActualCurrentCompletion(f)); Assert.False(f.ObserveCurrentVigorQualification());
        f.Data.AssertFiles(files);
    });

    [Fact]
    public Task MissingActualNativeStatusRefusesWithoutNullDereferenceOrCombatPublication() => Run(async f =>
    {
        var binding = f.Current().NativeGameClient!.OrdinarySpellBinding!;
        var original = binding.Client.Status;
        var before = Values(original);
        var files = f.Data.Files();
        int start = f.ActorFrames.Count;
        binding.Client.Status = null!; // Remove the actual required holder; do not supply a substitute.
        try { await Query(f); }
        finally { binding.Client.Status = original; }
        Assert.Equal(before, Values(original));
        Assert.DoesNotContain(f.ActorFrames.Skip(start), bytes =>
            LegacyPlayerStatusPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out _));
        Assert.DoesNotContain(nameof(NullReferenceException), f.Log.ToString(), StringComparison.Ordinal);
        f.Data.AssertFiles(files);
    });

    [Fact]
    public Task FabricatedCombatTokenCannotRetireARealCompletedQuery() => Run(f =>
    {
        var current = f.Current(); var binding = current.NativeGameClient!.OrdinarySpellBinding!;
        Assert.True(HasActualCurrentCompletion(f)); Assert.True(f.ObserveCurrentVigorQualification());
        var type = typeof(LegacyNativeMagicQueryClient);
        var entries = type.GetField("Entries", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        object?[] args = [binding.NativeIdentity, null];
        Assert.True((bool)entries.GetType().GetMethod("TryGetValue")!.Invoke(entries, args)!);
        var entry = args[1]!;
        var epoch = Assert.IsType<LegacyNativeMagicQueryClient.QueryEpoch>(entry.GetType().GetField("Epoch",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(entry));
        var status = binding.Client.Status; var before = Values(status); var maxima = (status.MaxHitpoints, status.MaxMana);
        var files = f.Data.Files(); bool accepted = true;
        Assert.True(f.Store.WithCurrentNativeClientIdentity(f.Connection(Actor), Actor, identity =>
        {
            Assert.Same(binding.NativeIdentity, identity);
            lock (binding.Client.SpellAdmissionSyncRoot)
            lock (binding.Player.SpellAdmissionSyncRoot)
                accepted = LegacyNativeMagicQueryClient.PublishCompletedMaximums(binding, current, epoch,
                    new WorldPacketHandlers.NativeQueryCombatCommit(), out _);
        }));
        Assert.False(accepted); Assert.Equal(before, Values(status));
        Assert.Equal(maxima, (status.MaxHitpoints, status.MaxMana));
        Assert.True(HasActualCurrentCompletion(f)); Assert.True(f.ObserveCurrentVigorQualification());
        f.Data.AssertFiles(files);
        return Task.CompletedTask;
    });

    [Fact]
    public Task FineRainDeniedActualQueryDoesNotPublishNativeCombatStatus() => Run(async f =>
    {
        await f.SendAsync(Actor, new MsgAttackPacket(new MsgAttackQuery(Uid: Actor,
            OpponentUid: Actor, X: 830, Y: 584, AttackType: MsgAttackType.Magic,
            Damage: 0, KilledMonster: 0, ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0,
            SpellId: 14_500)).ToFrameBytes());
        Assert.True(f.Current().Character.Attributes.ContainsKey("StatusFlags"), f.Log.ToString());
        var status = Native(f); var before = Values(status); var files = f.Data.Files(); int start = f.ActorFrames.Count;
        await Query(f);
        Assert.DoesNotContain(f.ActorFrames.Skip(start), bytes =>
            LegacyPlayerStatusPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out _));
        Assert.Same(status, Native(f)); Assert.Equal(before, Values(status));
        f.Data.AssertFiles(files);
    });
}
