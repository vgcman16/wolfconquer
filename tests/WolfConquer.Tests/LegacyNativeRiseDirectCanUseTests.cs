using System.Reflection;
using System.Text.Json;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public async Task NativeRiseDirectCanUseFailedManaLeavesCompleteOwnerAndOutputUnchanged()
    {
        using var fixture = new NativeRiseGlobalEntryFixture();
        await fixture.QueryAsync();
        fixture.DeclareInsufficientDirectRiseMana();

        await fixture.AssertDirectRiseRejectedUnchangedAsync("mana=36 requiredMana=37");
    }

    [Fact]
    public async Task NativeRiseDirectCanUseEarnedCooldownRejectsUnspentAndPublicCastAdmitsExactDeadline()
    {
        using var fixture = new NativeRiseGlobalEntryFixture();
        await fixture.QueryAsync();
        var start = fixture.Order.Count;
        await fixture.CastRiseAsync(); // Earn the real deadline, status, and successful publication.
        fixture.AssertCompletedSince(start, 89, 463, dead: false);
        Assert.Equal(new[]
        {
            (LegacyMsgUpdateDataType.Stamina, 89UL),
            (LegacyMsgUpdateDataType.Mana, 463UL),
        }, fixture.CostsSince(start));

        fixture.Advance(TimeSpan.FromMilliseconds(3_999));
        await fixture.AssertDirectRiseRejectedUnchangedAsync("cooldownRemainingMs=1");

        fixture.Advance(TimeSpan.FromMilliseconds(1));
        start = fixture.Order.Count;
        await fixture.CastRiseAsync();
        fixture.AssertCompletedSince(start, 78, 426, dead: false);
        Assert.Equal(new[]
        {
            (LegacyMsgUpdateDataType.Stamina, 78UL),
            (LegacyMsgUpdateDataType.Mana, 426UL),
        }, fixture.CostsSince(start));
    }

    private sealed partial class NativeRiseGlobalEntryFixture
    {
        internal void DeclareInsufficientDirectRiseMana()
        {
            var owner = Owner;
            Assert.Equal(500L, owner.Character.MinMana);
            // Declared component input on the existing real Store owner, not a
            // claimed damage/consumption producer or a fabricated native client.
            sessions.Set(ownerConnection, owner with { Character = owner.Character with { MinMana = 36 } });
        }

        internal async Task AssertDirectRiseRejectedUnchangedAsync(string reason)
        {
            var before = Owner;
            var peerBefore = Peer;
            var ownerImage = JsonSerializer.SerializeToUtf8Bytes(before);
            var peerImage = JsonSerializer.SerializeToUtf8Bytes(peerBefore);
            var start = Order.Count;
            var logStart = log.GetStringBuilder().Length;
            Assert.Null(context.RootFirstFamilyInvocation);
            Assert.True(LegacyNativeClientPublication.TryCaptureIdentity(
                sessions, ownerConnection, PlayerUid, out var identity));
            Assert.True(LegacyMagicRuntime.TryResolveNativeLearnedSpell(
                before.PlayerSpells, context.WorldData!, Rise, out var book, out var spell, out _));
            Assert.Equal(LegacyMagicSort.AttachStatus, spell.Sort);
            Assert.Equal((byte)11, spell.UseStamina);
            Assert.Equal((ushort)37, spell.UseMana);
            Assert.Equal(4_000, spell.ColdTime);
            var query = new MsgAttackQuery(Uid: PlayerUid, OpponentUid: PlayerUid,
                X: 832, Y: 584, AttackType: MsgAttackType.Magic, Damage: 0, KilledMonster: 0,
                ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0, SpellId: Rise);
            var method = typeof(WorldPacketHandlers).GetMethod(
                "ExecuteLegacyNativeRiseOfTaoismQueryCastAsync", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);
            // Actual direct consumer and actual returned ValueTask; no prepared
            // invocation, accepting publisher, synthesized completion, or root prefix.
            var operation = Assert.IsType<ValueTask<bool>>(method.Invoke(null,
                [context with { Session = before }, query, book, spell, before, CancellationToken.None]));
            Assert.True(await operation);

            Assert.Same(before, Owner);
            Assert.Same(before.Character, Owner.Character);
            Assert.Same(before.Character.Attributes, Owner.Character.Attributes);
            Assert.Same(before.MagicCooldowns, Owner.MagicCooldowns);
            Assert.Equal(ownerImage, JsonSerializer.SerializeToUtf8Bytes(Owner));
            Assert.Same(peerBefore, Peer);
            Assert.Equal(peerImage, JsonSerializer.SerializeToUtf8Bytes(Peer));
            Assert.True(LegacyNativeClientPublication.TryCaptureIdentity(
                sessions, ownerConnection, PlayerUid, out var afterIdentity));
            Assert.Same(identity, afterIdentity);
            Assert.Equal(start, Order.Count); // No cost, interaction, flag, animation, or equipment output to either owner.
            var diagnostic = log.ToString()[logStart..];
            Assert.Contains("WorldServer skipped legacy player magic", diagnostic);
            Assert.Contains("spellId=15440 " + reason + ".", diagnostic);
            Assert.DoesNotContain("WorldServer handled legacy RiseofTaoism", diagnostic);
        }
    }
}
