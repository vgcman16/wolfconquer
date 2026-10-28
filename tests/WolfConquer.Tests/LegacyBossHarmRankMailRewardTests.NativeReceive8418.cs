using System.Globalization;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Existing fixture helpers and the actual PacketHandlerRegistry are reused; no synthetic route.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyBossHarmRankMailRewardTests
{
    private static readonly double[] Receive8418ExpectedAllFalseRates =
    [
        0.0001, 0.008, 0.003, 0.0004, 0.0005, 0.0006, 0.0002, 0.0006, 0.0002,
        0.0003, 0.0004, 0.0005, 0.0007, 0.0008, 0.0006, 0.009, 0.0003,
        0.0004, 0.0005, 0.0007, 0.008, 0.0003, 0.0004, 0.0005, 0.0007,
        0.0002, 0.0008, 0.0005, 0.0007, 0.0008, 0.0008,
        0.0008, 0.0007, 0.0009, 0.00003, 0.0004,
        0.0007, 0.0008, 0.0022, 0.008,
    ];

    [Fact]
    public void Receive8418AllFalseRatesReturnWholeDeadStopAfterExactlyFortyCalls()
    {
        var rates = new List<double>();
        var reward = InvokeReceive8418Selector(rate => { rates.Add(rate); return false; });
        Assert.Equal(Receive8418ExpectedAllFalseRates, rates);
        Assert.Equal("StopNativeDeath", ReadReceive8418RewardKind(reward));
    }

    [Fact]
    public void Receive8418FirstSuccessConsumesOneRateAndNoIntegerDraw()
    {
        var rates = new List<double>();
        var reward = InvokeReceive8418Selector(rate => { rates.Add(rate); return true; });
        Assert.Equal([0.0001], rates);
        Assert.Equal("FloorDrop", ReadReceive8418RewardKind(reward));
        Assert.Equal(3_001_044U, ReadReceive8418RewardItem(reward));
    }

    [Fact]
    public void Receive8418OuterInvertedGuardSuccessMeansContinueWithoutRewardNotWholeDeadStop()
    {
        var calls = 0;
        var reward = InvokeReceive8418Selector(_ => ++calls == 5);
        Assert.Equal(5, calls);
        Assert.Equal("None", ReadReceive8418RewardKind(reward));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Receive8418TerminalAndGuardsShortCircuitWithoutEnteringNestedReturn(int guard)
    {
        var calls = 0;
        var reward = InvokeReceive8418Selector(_ => ++calls == 32 + guard);
        Assert.Equal(32 + guard, calls);
        Assert.Equal("None", ReadReceive8418RewardKind(reward));
    }

    [Fact]
    public void Receive8418FinalSuccessConsumesSamePrefixThenChoosesFallbackDrop()
    {
        var calls = 0;
        var reward = InvokeReceive8418Selector(_ => ++calls == 40);
        Assert.Equal(40, calls);
        Assert.Equal("FloorDrop", ReadReceive8418RewardKind(reward));
        Assert.Equal(3_004_280U, ReadReceive8418RewardItem(reward));
    }

    [Fact]
    public void Receive8418SwitchDrawRunsOnlyAfterItsRateAndUsesNativeExclusiveUpperBound()
    {
        var events = new List<string>();
        var calls = 0;
        var reward = InvokeReceive8418Selector(
            rate => { events.Add("rate:" + rate.ToString("R", CultureInfo.InvariantCulture)); return ++calls == 9; },
            (min, max) => { events.Add($"next:{min}:{max}"); return 5; });
        Assert.Equal(11, events.Count);
        Assert.Equal("next:1:6", events[9]);
        Assert.Equal("rate:0.002", events[10]);
        Assert.Equal("None", ReadReceive8418RewardKind(reward));
    }

    [Fact]
    public async Task Receive8418StopPreservesPriorStateAndPublicationsButOmitsAllLaterDeadEffects()
    {
        using var scene = CreateReceive8418ContinuationScene(removeOnDead: true);
        var calls = 0;
        var result = await InvokeReceive8418Continuation(scene, rate =>
        {
            if (++calls == 1)
            {
                Assert.Equal(8U, scene.Session.NativePlayer!.Kills);
                Assert.True(scene.Sessions.TryGet(scene.Connection, out var current));
                Assert.Equal("8", current!.Character.Attributes["Kills"]);
                Assert.False(scene.States.TryGetVisibleInstance(scene.Monster, DateTimeOffset.UtcNow, out _));
                Assert.False(scene.States.IsBlackSpotted(scene.Monster, DateTimeOffset.UtcNow));
                var updates = scene.Sent.Select(ReadFrame)
                    .Where(frame => frame.Header.Opcode == LegacyMsgUpdatePacket.Opcode)
                    .Select(frame => { Assert.True(LegacyMsgUpdatePacket.TryParse(frame, out var value)); return value; })
                    .Where(packet => packet.Uid == scene.Monster.Uid).ToArray();
                Assert.Single(updates);
                var dead = new ulong[10]; dead[0] = 1UL << 5;
                Assert.Equal(dead, Assert.Single(updates[0].Updates).Values);
                Assert.Equal(2, scene.Sent.Count); // BlackSpot then Dead; ClearFlags(false) emits nothing.
            }
            return false;
        });
        Assert.Equal(40, calls);
        Assert.Equal("7", result.Character.Attributes["DailySpiritBeadCount"]);
        Assert.Equal(0, result.Character.ConquerPoints);
        Assert.Empty(ParseFloorItems(scene.Sent));
        Assert.DoesNotContain(scene.Sent, IsReceive8418DeathFrame);
        Assert.DoesNotContain("spirit bead", scene.Log.ToString());
        Assert.DoesNotContain("random Conquer Point reward", scene.Log.ToString());
        // RemoveOnDead is AFTER the source return: ordinary respawn must remain possible.
        var later = DateTimeOffset.UtcNow.AddDays(2);
        Assert.True(scene.States.TryConsumeDeathRemovalInstance(scene.Monster, later, out _));
        Assert.True(scene.States.TryConsumeRespawnedInstance(scene.Monster, later, out _));
    }

    [Fact]
    public async Task Receive8418ContinueReusesTheSelectedDecisionAndRunsSpiritBeforeDeath()
    {
        using var scene = CreateReceive8418ContinuationScene(removeOnDead: true);
        var calls = 0;
        var result = await InvokeReceive8418Continuation(scene, _ => ++calls == 5);
        Assert.Equal(5, calls); // no second selector invocation inside MapSideEffects.
        Assert.Equal("11", result.Character.Attributes["DailySpiritBeadCount"]);
        Assert.Equal(1, result.Character.ConquerPoints); // explicit deterministic tuned-policy fixture.
        Assert.Single(scene.Sent, IsReceive8418DeathFrame);
        var spirit = scene.Sent.FindIndex(bytes =>
            LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var message) &&
            message.Content[3] == "You received 4 spirites.");
        var death = scene.Sent.FindIndex(IsReceive8418DeathFrame);
        Assert.True(spirit >= 0 && spirit < death);
        Assert.False(scene.States.TryConsumeRespawnedInstance(scene.Monster,
            DateTimeOffset.UtcNow.AddDays(2), out _));
    }

    [Fact]
    public async Task Receive8418PrefixFailureDoesNotDrawOrIncrementKillsAfterTheFailingSend()
    {
        using var scene = CreateReceive8418ContinuationScene(failPrefixSendNumber: 1);
        var calls = 0;
        await Assert.ThrowsAsync<IOException>(async () =>
            await InvokeReceive8418Continuation(scene, _ => { calls++; return false; }));
        Assert.Equal(0, calls);
        Assert.Equal(7U, scene.Session.NativePlayer!.Kills);
        Assert.True(scene.States.TryGetVisibleInstance(scene.Monster, DateTimeOffset.UtcNow, out var visible));
        Assert.Equal(scene.Monster.MaxLife, visible.Hitpoints);
        Assert.True(scene.States.IsBlackSpotted(scene.Monster, DateTimeOffset.UtcNow));
        Assert.DoesNotContain(scene.Sent, IsReceive8418DeathFrame);
    }

    [Fact]
    public async Task Receive8418DeadFlagSendFailureKeepsCommittedHpButDoesNotStampKillsOrDraw()
    {
        using var scene = CreateReceive8418ContinuationScene(failPrefixSendNumber: 2);
        var calls = 0;
        await Assert.ThrowsAsync<IOException>(async () =>
            await InvokeReceive8418Continuation(scene, _ => { calls++; return false; }));
        Assert.Equal(0, calls);
        Assert.Equal(7U, scene.Session.NativePlayer!.Kills);
        Assert.False(scene.States.TryGetVisibleInstance(scene.Monster, DateTimeOffset.UtcNow, out _));
        Assert.False(scene.States.IsBlackSpotted(scene.Monster, DateTimeOffset.UtcNow));
        Assert.Single(scene.Sent); // the preceding inactive BlackSpot frame completed.
        Assert.False(scene.States.TryConsumeDeathRemovalInstance(
            scene.Monster, DateTimeOffset.UtcNow.AddDays(2), out _)); // DeadStamp was not reached.
        Assert.DoesNotContain(scene.Sent, IsReceive8418DeathFrame);
    }

    [Fact]
    public async Task Receive8418AdapterAppliesNonlethalDamageWithoutEnteringDeadOrDrawing()
    {
        using var scene = CreateReceive8418ContinuationScene();
        var outcome = await InvokeReceive8418Outcome(scene, _ =>
            throw new InvalidOperationException("Nonlethal receive must not select a death reward."),
            requestedDamage: 10);
        Assert.False(outcome.Handled);
        Assert.False(outcome.Damage.Killed);
        Assert.True(scene.States.TryGetVisibleInstance(scene.Monster, DateTimeOffset.UtcNow, out var visible));
        Assert.Equal(scene.Monster.MaxLife - 10U, visible.Hitpoints);
        Assert.True(scene.States.IsBlackSpotted(scene.Monster, DateTimeOffset.UtcNow));
        Assert.Empty(scene.Sent);
        Assert.Equal(7U, scene.Session.NativePlayer!.Kills);
    }

    [Fact]
    public async Task Receive8418PriorPublicationAndOwnerCommitOrderMatchesNativeWithoutClearPacket()
    {
        var observed = new List<int>();
        using var scene = CreateReceive8418ContinuationScene(observePrefixSend: (number, states, monster) =>
        {
            observed.Add(number);
            var now = DateTimeOffset.UtcNow;
            if (number == 1)
            {
                Assert.True(states.TryGetVisibleInstance(monster, now, out var live));
                Assert.Equal(monster.MaxLife, live.Hitpoints);
                Assert.True(states.IsBlackSpotted(monster, now));
                Assert.True(states.TryGetTimedStatusFlag(monster, 4, now, out _));
            }
            else if (number == 2)
            {
                Assert.False(states.TryGetVisibleInstance(monster, now, out _));
                Assert.False(states.IsBlackSpotted(monster, now));
                Assert.False(states.TryGetTimedStatusFlag(monster, 4, now, out _));
                Assert.True(states.TryGetTimedStatusFlag(monster, 5, now, out _));
            }
            else
            {
                throw new InvalidOperationException("No clear-status or later Dead packet may be published.");
            }
        });
        await InvokeReceive8418Continuation(scene, _ =>
        {
            Assert.Equal([1, 2], observed);
            return false;
        });
        Assert.Equal([1, 2], observed);
    }

    [Fact]
    public async Task Receive8418PhysicalKillsWrapIsCommittedBeforeFirstRate()
    {
        using var scene = CreateReceive8418ContinuationScene();
        scene.Session.NativePlayer!.Kills = uint.MaxValue;
        await InvokeReceive8418Continuation(scene, _ =>
        {
            Assert.Equal(0U, scene.Session.NativePlayer.Kills);
            return false;
        });
        Assert.True(scene.Sessions.TryGet(scene.Connection, out var current));
        Assert.Equal("0", current!.Character.Attributes["Kills"]);
    }

    [Fact]
    public async Task Receive8418ActualEagleEyeStopFinishesBeforeCountdownAnimationAndSpellContinuation()
    {
        using var scene = CreateReceive8418ContinuationScene();
        PrimeMonsterForServerAuthoritativeFinalHit(scene.States, scene.Monster);
        var calls = 0;
        await InvokeReceive8418EagleEye(scene, rate =>
        {
            ++calls;
            Assert.DoesNotContain(ParseReceive8418Animations(scene.Sent),
                packet => packet.SpellId is 11_030 or 11_130);
            Assert.Equal(8U, scene.Session.NativePlayer!.Kills);
            return false;
        });
        Assert.Equal(40, calls);
        Assert.True(scene.Sessions.TryGet(scene.Connection, out var final));
        Assert.Equal("7", final!.Character.Attributes["DailySpiritBeadCount"]);
        Assert.Equal("1", final.Character.Attributes["LegacyEagleEyeCountDown"]);
        Assert.True(final.PlayerSpells.Spells.Single(spell => spell.Id == 11_030).Experience > 0, scene.Log.ToString());
        scene.Owner.AssertSavedSpells(final);
        Assert.DoesNotContain(scene.Sent, IsReceive8418DeathFrame);
        Assert.DoesNotContain(ParseReceive8418Animations(scene.Sent), packet => packet.SpellId == 11_130);
        Assert.Single(ParseReceive8418Animations(scene.Sent), packet => packet.SpellId == 11_030);
        var spellUpdate = scene.Sent.FindIndex(bytes => ReadFrame(bytes).Header.Opcode == LegacySpellPacket.Opcode);
        var animation = scene.Sent.FindIndex(bytes =>
            LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out var packet) && packet.SpellId == 11_030);
        Assert.True(spellUpdate >= 0 && spellUpdate < animation);
    }

    [Fact]
    public async Task Receive8418EagleEyeContinueSelectsOnceAndPublishesSpiritAndDeathBeforeAnimation()
    {
        using var scene = CreateReceive8418ContinuationScene();
        PrimeMonsterForServerAuthoritativeFinalHit(scene.States, scene.Monster);
        var calls = 0;
        await InvokeReceive8418EagleEye(scene, _ => ++calls == 5);
        Assert.Equal(5, calls);
        Assert.True(scene.Sessions.TryGet(scene.Connection, out var final));
        Assert.Equal("11", final!.Character.Attributes["DailySpiritBeadCount"]);
        Assert.Equal(1, final.Character.ConquerPoints);
        var death = scene.Sent.FindIndex(IsReceive8418DeathFrame);
        var animation = scene.Sent.FindIndex(bytes =>
            LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out var packet) && packet.SpellId == 11_030);
        Assert.True(death >= 0 && death < animation);
        Assert.Single(scene.Sent, IsReceive8418DeathFrame);
    }

    [Fact]
    public async Task Receive8418EagleEyeDeadStopStillSharesRealTeamExperienceBeforeAnimation()
    {
        using var scene = CreateReceive8418ContinuationScene();
        PrimeMonsterForServerAuthoritativeFinalHit(scene.States, scene.Monster);
        var recipientSent = new List<byte[]>();
        var recipientConnection = CreateConnection(recipientSent);
        var recipient = CreateSession(KillerUid + 1, "Recipient", 10_342) with
        {
            Character = scene.Session.Character with { Uid = KillerUid + 1, Name = "Recipient", Level = 100 },
        };
        scene.Sessions.Set(recipientConnection, recipient);
        var teams = new TeamSessionStateStore();
        teams.CreateTeam(scene.Connection, KillerUid);
        Assert.True(teams.TryAddMember(scene.Connection, recipientConnection, KillerUid + 1));
        var withTeam = scene with { Context = scene.Context with { TeamStates = teams } };
        await InvokeReceive8418EagleEye(withTeam, _ => false);
        Assert.True(scene.Sessions.TryGet(recipientConnection, out var updated));
        Assert.True(updated!.Character.Experience > recipient.Character.Experience);
        Assert.Contains("WorldServer sent legacy monster team experience", scene.Log.ToString());
        Assert.Single(ParseReceive8418Animations(scene.Sent), packet => packet.SpellId == 11_030);
        Assert.DoesNotContain(scene.Sent, IsReceive8418DeathFrame);
    }

    [Fact]
    public async Task Receive8418AdapterLeavesOtherFamiliesForTheirOriginalCallerDeathPath()
    {
        using var original = CreateReceive8418ContinuationScene();
        var scene = original with
        {
            Monster = original.Monster with { MonsterId = 9_901 },
            Family = CreateBossFamily(9_901, "OrdinaryControl", boss: false),
        };
        var calls = 0;
        var result = await InvokeReceive8418Continuation(scene, _ =>
        {
            calls++;
            throw new InvalidOperationException("The8418 selector must not run for other families.");
        });
        Assert.Equal(0, calls);
        Assert.Equal(7U, scene.Session.NativePlayer!.Kills);
        Assert.Equal("7", result.Character.Attributes["DailySpiritBeadCount"]);
        Assert.DoesNotContain(scene.Sent, IsReceive8418DeathFrame);
        Assert.DoesNotContain(result.Character.Attributes.Keys, key => key == "Kills");
        Assert.False(scene.States.TryGetVisibleInstance(scene.Monster, DateTimeOffset.UtcNow, out _));
    }

    [Fact]
    public async Task Receive8418EagleEyeOrdinaryFamilyRetainsItsOldDeathAndOuterContinuation()
    {
        using var original = CreateReceive8418ContinuationScene();
        var scene = original with
        {
            Monster = original.Monster with { MonsterId = 9_901 },
            Family = CreateBossFamily(9_901, "OrdinaryControl", boss: false),
        };
        PrimeMonsterForServerAuthoritativeFinalHit(scene.States, scene.Monster);
        await InvokeReceive8418EagleEye(scene, _ =>
            throw new InvalidOperationException("Ordinary EagleEye must not use the8418 selector."));
        Assert.Equal(7U, scene.Session.NativePlayer!.Kills);
        Assert.Single(scene.Sent, IsReceive8418DeathFrame);
        Assert.Single(ParseReceive8418Animations(scene.Sent), packet => packet.SpellId == 11_030);
        Assert.True(scene.Sessions.TryGet(scene.Connection, out var final));
        Assert.Equal("11", final!.Character.Attributes["DailySpiritBeadCount"]);
    }

    [Fact]
    public async Task Receive8418MissingPhysicalPlayerFaultsAfterDeadPublicationBeforeRate()
    {
        using var original = CreateReceive8418ContinuationScene();
        var session = original.Session with { NativeGameClient = null };
        var scene = original with { Session = session, Context = original.Context with { Session = session } };
        var calls = 0;
        await Assert.ThrowsAsync<NullReferenceException>(async () =>
            await InvokeReceive8418Continuation(scene, _ => { calls++; return false; }));
        Assert.Equal(0, calls);
        Assert.Equal(2, scene.Sent.Count);
        Assert.False(scene.States.TryGetVisibleInstance(scene.Monster, DateTimeOffset.UtcNow, out _));
        Assert.DoesNotContain(scene.Sent, IsReceive8418DeathFrame);
    }

    [Fact]
    public void Receive8418UnconvertedSelectorKeepsItsOldContinueOnFinalMissContract()
    {
        var method = typeof(WorldPacketHandlers).GetMethod("SelectLegacyFamily8418RandomReward",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        var reward = method.Invoke(null, [(Func<double, bool>)(_ => false),
            (Func<int, int, int>)((_, _) => throw new InvalidOperationException()), false])!;
        Assert.Equal("None", ReadReceive8418RewardKind(reward));
    }

    // FALSE removal rows below are explicit MODEL substitutions into the actual owner dictionary.
    // Present public timed-status/ScarOfEarth writers are true-only; no gameplay reachability claim.
    [Fact]
    public async Task Receive8418SelectiveClearPublishesFalseSurvivorsAndDeadButRemovesTrueRows()
    {
        using var scene = CreateReceive8418ContinuationScene();
        ModelReceive8418Flag(scene, 7, removeOnDead: false);
        ModelReceive8418Flag(scene, 36, removeOnDead: false);
        ModelReceive8418Flag(scene, 9, removeOnDead: true);
        await InvokeReceive8418Continuation(scene, _ => false);
        var flags = Receive8418OwnedFlags(scene);
        Assert.False(flags.ContainsKey(4));
        Assert.False(flags.ContainsKey(9));
        Assert.False(flags[7].RemoveOnDead);
        Assert.False(flags[36].RemoveOnDead);
        Assert.True(flags[5].RemoveOnDead);
        var vector = Receive8418PublishedDeadVector(scene);
        Assert.Equal((1UL << 5) | (1UL << 7), vector[0]);
        Assert.Equal(1UL << 4, vector[1]);
        Assert.All(vector.Skip(2), value => Assert.Equal(0UL, value));
        Assert.Equal(8U, scene.Session.NativePlayer!.Kills);
        Assert.DoesNotContain(scene.Sent, IsReceive8418DeathFrame);
    }

    [Fact]
    public async Task Receive8418SelectiveClearDoesNotExpireFalseSurvivorsBeforePublication()
    {
        using var scene = CreateReceive8418ContinuationScene();
        var expired = DateTimeOffset.UtcNow.AddMinutes(-1);
        ModelReceive8418Flag(scene, 36, removeOnDead: false, expiresAt: expired);
        await InvokeReceive8418Continuation(scene, _ => false);
        Assert.Equal(expired, Receive8418OwnedFlags(scene)[36].ExpiresAt);
        Assert.Equal(1UL << 4, Receive8418PublishedDeadVector(scene)[1]);
    }

    [Fact]
    public async Task Receive8418FalseExistingDeadSurvivesWithoutRefreshOrStatusPublication()
    {
        using var scene = CreateReceive8418ContinuationScene();
        var original = ModelReceive8418Flag(scene, 5, removeOnDead: false,
            expiresAt: DateTimeOffset.UtcNow.AddMinutes(-1));
        ModelReceive8418Flag(scene, 36, removeOnDead: false);
        ModelReceive8418Flag(scene, 9, removeOnDead: true);
        var calls = 0;
        await InvokeReceive8418Continuation(scene, _ => { calls++; return false; });
        Assert.Equal(40, calls);
        Assert.Equal(original, Receive8418OwnedFlags(scene)[5]);
        Assert.False(Receive8418OwnedFlags(scene).ContainsKey(4));
        Assert.False(Receive8418OwnedFlags(scene).ContainsKey(9));
        Assert.True(Receive8418OwnedFlags(scene).ContainsKey(36));
        Assert.Single(scene.Sent); // inactive BlackSpot only; ClearFlags(false) and AddFlag(false) send nothing.
        Assert.Equal(8U, scene.Session.NativePlayer!.Kills);
        Assert.True(scene.States.TryConsumeDeathRemovalInstance(
            scene.Monster, DateTimeOffset.UtcNow.AddDays(2), out _)); // DeadStamp was reached.
    }

    [Fact]
    public async Task Receive8418TrueExistingDeadIsRemovedThenReaddedWithCurrentSurvivorVector()
    {
        using var scene = CreateReceive8418ContinuationScene();
        var original = ModelReceive8418Flag(scene, 5, removeOnDead: true,
            expiresAt: DateTimeOffset.UtcNow.AddMinutes(1));
        ModelReceive8418Flag(scene, 36, removeOnDead: false);
        await InvokeReceive8418Continuation(scene, _ => false);
        var replacement = Receive8418OwnedFlags(scene)[5];
        Assert.True(replacement.RemoveOnDead);
        Assert.True(replacement.ExpiresAt > original.ExpiresAt.AddDays(20));
        var vector = Receive8418PublishedDeadVector(scene);
        Assert.Equal(1UL << 5, vector[0]);
        Assert.Equal(1UL << 4, vector[1]);
        Assert.Equal(2, scene.Sent.Count);
    }

    [Fact]
    public async Task Receive8418FirstSendFailurePreservesBothRemovalKindsBeforeSelectiveClear()
    {
        using var scene = CreateReceive8418ContinuationScene(failPrefixSendNumber: 1);
        ModelReceive8418Flag(scene, 36, removeOnDead: false);
        ModelReceive8418Flag(scene, 9, removeOnDead: true);
        var before = Receive8418OwnedFlags(scene).OrderBy(pair => pair.Key).ToArray();
        var calls = 0;
        await Assert.ThrowsAsync<IOException>(async () =>
            await InvokeReceive8418Continuation(scene, _ => { calls++; return false; }));
        Assert.Equal(before, Receive8418OwnedFlags(scene).OrderBy(pair => pair.Key).ToArray());
        Assert.True(scene.States.TryGetVisibleInstance(scene.Monster, DateTimeOffset.UtcNow, out var visible));
        Assert.Equal(scene.Monster.MaxLife, visible.Hitpoints);
        Assert.Equal(0, calls);
        Assert.Equal(7U, scene.Session.NativePlayer!.Kills);
    }

    [Fact]
    public async Task Receive8418DeadSendFailureKeepsSelectiveCommitButNotStampKillsOrRate()
    {
        using var scene = CreateReceive8418ContinuationScene(failPrefixSendNumber: 2);
        var survivor = ModelReceive8418Flag(scene, 36, removeOnDead: false);
        ModelReceive8418Flag(scene, 9, removeOnDead: true);
        var calls = 0;
        await Assert.ThrowsAsync<IOException>(async () =>
            await InvokeReceive8418Continuation(scene, _ => { calls++; return false; }));
        var flags = Receive8418OwnedFlags(scene);
        Assert.Equal(survivor, flags[36]);
        Assert.False(flags.ContainsKey(4));
        Assert.False(flags.ContainsKey(9));
        Assert.True(flags[5].RemoveOnDead);
        Assert.False(scene.States.TryGetVisibleInstance(scene.Monster, DateTimeOffset.UtcNow, out _));
        Assert.False(scene.States.TryConsumeDeathRemovalInstance(
            scene.Monster, DateTimeOffset.UtcNow.AddDays(2), out _));
        Assert.Equal(0, calls);
        Assert.Equal(7U, scene.Session.NativePlayer!.Kills);
        Assert.Single(scene.Sent);
    }

    [Fact]
    public async Task Receive8418FalseExistingDeadNeverInvokesASecondSendAndStillContinues()
    {
        using var scene = CreateReceive8418ContinuationScene(failPrefixSendNumber: 2);
        ModelReceive8418Flag(scene, 5, removeOnDead: false);
        ModelReceive8418Flag(scene, 36, removeOnDead: false);
        var calls = 0;
        await InvokeReceive8418Continuation(scene, _ => { calls++; return false; });
        Assert.Equal(40, calls);
        Assert.Single(scene.Sent);
        Assert.Equal(8U, scene.Session.NativePlayer!.Kills);
    }

    private static LegacyMonsterTimedStatusState ModelReceive8418Flag(
        Receive8418ContinuationScene scene, int flag, bool removeOnDead, DateTimeOffset? expiresAt = null)
    {
        // Deliberate MODEL-only metadata substitution; NOT a new production false-row writer.
        var value = new LegacyMonsterTimedStatusState(
            flag, expiresAt ?? DateTimeOffset.UtcNow.AddMinutes(1), removeOnDead);
        Receive8418OwnedFlags(scene)[flag] = value;
        return value;
    }

    private static Dictionary<int, LegacyMonsterTimedStatusState> Receive8418OwnedFlags(
        Receive8418ContinuationScene scene)
    {
        var owners = (System.Collections.IDictionary)typeof(MonsterRuntimeStateStore)
            .GetField("_states", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(scene.States)!;
        var owner = Assert.Single(owners.Values.Cast<object>());
        return (Dictionary<int, LegacyMonsterTimedStatusState>)owner.GetType()
            .GetProperty("TimedStatusFlags")!.GetValue(owner)!;
    }

    private static IReadOnlyList<ulong> Receive8418PublishedDeadVector(Receive8418ContinuationScene scene)
    {
        var updates = scene.Sent.Select(ReadFrame)
            .Where(frame => frame.Header.Opcode == LegacyMsgUpdatePacket.Opcode)
            .Select(frame => { Assert.True(LegacyMsgUpdatePacket.TryParse(frame, out var packet)); return packet; })
            .Where(packet => packet.Uid == scene.Monster.Uid).ToArray();
        return Assert.Single(Assert.Single(updates).Updates).Values!;
    }

    private static object InvokeReceive8418Selector(
        Func<double, bool> rate, Func<int, int, int>? next = null)
    {
        var method = typeof(WorldPacketHandlers).GetMethod("SelectLegacyFamily8418RandomReward",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        return method.Invoke(null, [rate, next ?? ((_, _) => throw new InvalidOperationException("Unexpected integer RNG")), true])!;
    }

    private static string ReadReceive8418RewardKind(object reward) =>
        reward.GetType().GetProperty("Kind")!.GetValue(reward)!.ToString()!;

    private static uint ReadReceive8418RewardItem(object reward) =>
        (uint)reward.GetType().GetProperty("ItemId")!.GetValue(reward)!;

    private static bool IsReceive8418DeathFrame(byte[] bytes) =>
        MsgAttackPacket.TryParse(ReadFrame(bytes), out var packet) &&
        packet.Query.AttackType == MsgAttackType.Death;

    private static async Task<WorldSession> InvokeReceive8418Continuation(
        Receive8418ContinuationScene scene, Func<double, bool> rate) =>
        (await InvokeReceive8418Outcome(scene, rate)).Session;

    private static async Task<(WorldSession Session, MonsterDamageResult Damage, bool Handled)>
        InvokeReceive8418Outcome(Receive8418ContinuationScene scene, Func<double, bool> rate,
            uint? requestedDamage = null)
    {
        var method = typeof(WorldPacketHandlers).GetMethod("ApplyLegacyMonsterNativeReceiveAsync",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        var call = method.Invoke(null,
            [scene.Context, scene.Session, scene.Session, scene.Monster, scene.Family,
             requestedDamage ?? scene.Monster.MaxLife, DateTimeOffset.UtcNow, CancellationToken.None, rate,
             (Func<int, int, int>)((_, _) => throw new InvalidOperationException("Unexpected integer RNG"))])!;
        var task = (Task)call.GetType().GetMethod("AsTask")!.Invoke(call, null)!;
        await task;
        var result = task.GetType().GetProperty("Result")!.GetValue(task)!;
        var type = result.GetType();
        return ((WorldSession)type.GetProperty("Session")!.GetValue(result)!,
            (MonsterDamageResult)type.GetProperty("Damage")!.GetValue(result)!,
            (bool)type.GetProperty("NativeDeadHandled")!.GetValue(result)!);
    }

    private static async Task InvokeReceive8418EagleEye(
        Receive8418ContinuationScene scene, Func<double, bool> rate)
    {
        var method = typeof(WorldPacketHandlers).GetMethod("HandleLegacyEagleEyeMonsterAsync",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        var target = Activator.CreateInstance(method.GetParameters()[5].ParameterType,
            scene.Monster, scene.Family)!;
        var query = new MsgAttackQuery(KillerUid, scene.Monster.Uid, scene.Monster.X, scene.Monster.Y,
            MsgAttackType.Magic, 0, 0, 0, MsgAttackEffect.None, 0, SpellId: 11_030);
        var spell = new LegacyMagicRuntimeSpell(11_030, LegacyMagicSort.DirectAttack, "EagleEye",
            0, 0, 100, 1_000_000, 0, 9, 0, 0, 0, 0, 0);
        await (ValueTask)method.Invoke(null,
            [scene.Context, scene.Session, query,
             scene.Session.PlayerSpells.Spells.Single(value => value.Id == 11_030), spell, target,
             CancellationToken.None, rate,
             (Func<int, int, int>)((_, _) => throw new InvalidOperationException("Unexpected integer RNG"))])!;
    }

    private static IReadOnlyList<LegacySpellAnimationPacket> ParseReceive8418Animations(IEnumerable<byte[]> frames) =>
        frames.Select(ReadFrame).Where(frame => LegacySpellAnimationPacket.TryParse(frame, out _))
            .Select(frame => { LegacySpellAnimationPacket.TryParse(frame, out var packet); return packet; }).ToArray();

    private static MagicType CreateReceive8418MagicRow(int id, ushort type, byte level, int sort, int rate = 0)
    {
        var fields = Enumerable.Repeat("0", 48).ToArray();
        fields[1] = type.ToString(CultureInfo.InvariantCulture);
        fields[2] = sort.ToString(CultureInfo.InvariantCulture);
        fields[3] = type == 11_030 ? "EagleEye" : "AdrenalineRush";
        fields[8] = level.ToString(CultureInfo.InvariantCulture);
        fields[10] = "100";
        fields[12] = rate.ToString(CultureInfo.InvariantCulture);
        fields[15] = "9";
        fields[18] = "1000000";
        return new MagicType(id, type, fields[3], fields);
    }

    private static Receive8418ContinuationScene CreateReceive8418ContinuationScene(
        bool removeOnDead = false, int failPrefixSendNumber = 0,
        Action<int, MonsterRuntimeStateStore, LegacyMonsterInstance>? observePrefixSend = null)
    {
        var family = CreateBossFamily(8_418, "MapHuantMonster", boss: false);
        var spawn = CreateBossSpawn(family, 10_342);
        var world = CreateWorldDataCatalog(family, spawn, magic:
            [CreateReceive8418MagicRow(1, 11_030, 0, 48), CreateReceive8418MagicRow(2, 11_030, 1, 48),
             CreateReceive8418MagicRow(3, 11_130, 0, 59, rate: 100)]);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn, family, world.GetLegacyMonsterRuntimeUidBase(spawn)).Single()
            with { RemoveOnDead = removeOnDead };
        var session = WithAcceptedQuest(CreateSession(KillerUid, "LastHit", 10_342,
            attributes: new Dictionary<string, string>
            {
                ["DailySpiritBeadCount"] = "7", ["LegacyMonsterDropRoll"] = "40",
                ["LegacyMonsterDeathConquerPointRatePercent"] = "100",
                ["LegacyMonsterDeathConquerPointRoll"] = "1",
                ["LegacyEagleEyeCountDown"] = "1", ["LegacyEagleEyeResponseRoll"] = "0",
            }), 2_375) with
        {
            NativeGameClient = new LegacyNativeGameClientRuntime(),
            PlayerSpells = new PlayerSpellSnapshot([new(11_030, 0, 0, 0, 0, 0), new(11_130, 0, 0, 0, 0, 0)]),
        };
        var states = new MonsterRuntimeStateStore();
        var now = DateTimeOffset.UtcNow;
        Assert.True(states.TryMarkBlackSpotUntil(monster, now, now.AddMinutes(1)));
        Assert.True(states.ApplyTimedStatusFlag(monster, 4, TimeSpan.FromMinutes(1), now).Applied);
        var sent = new List<byte[]>();
        var sendNumber = 0;
        var owner = Receive8418SavedOwner.Create(session, world, (bytes, _) =>
        {
            if (failPrefixSendNumber != 0 || observePrefixSend is not null)
            {
                ++sendNumber;
                observePrefixSend?.Invoke(sendNumber, states, monster);
                if (sendNumber == failPrefixSendNumber) throw new IOException("prefix send fault");
            }
            sent.Add(bytes.ToArray());
            return ValueTask.CompletedTask;
        });
        // Kills is the scenario's native input, after the actual saved prefix
        // established the UID and physical constructor owner through admission.
        session = owner.Session;
        session.NativePlayer!.Kills = 7;
        var context = owner.CreateContext(world, states);
        return new(context, owner.Connection, session, owner.Sessions, monster, family, states, sent, owner.Log, owner);
    }

    private sealed record Receive8418ContinuationScene(
        WorldPacketHandlerContext Context, ConquerTcpConnection Connection, WorldSession Session,
        WorldSessionStore Sessions, LegacyMonsterInstance Monster, Monster Family,
        MonsterRuntimeStateStore States, List<byte[]> Sent, StringWriter Log, Receive8418SavedOwner Owner) : IDisposable
    {
        public void Dispose() => Owner.Dispose();
    }
}
