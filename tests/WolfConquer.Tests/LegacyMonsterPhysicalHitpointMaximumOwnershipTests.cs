using System.Buffers.Binary;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public async Task MonsterPhysicalPositiveSetterCapturesPriorCompatibilityMaximumAndKeepsItAcrossHits()
    {
        var armed = false;
        var incoming = 0;
        long expectedBefore = 1_000;
        // Deliberate compatibility-adapter input: current HP1000 exceeds raw
        // equipped item HP500. A real status query exposes effective Max1000;
        // no MaxHitpoints attribute, owned cache, query reset or oracle is seeded.
        using var fixture = new MonsterPostInteractionFixture(1_000, maximum: 500,
            onSend: (current, _, bytes) =>
            {
                if (armed && MonsterPostIncoming(bytes))
                {
                    incoming++;
                    Assert.Equal(expectedBefore, current.Owner.Character.MinHitPoints);
                    MonsterPostAssertNoDeath(current.Owner);
                }
                return ValueTask.CompletedTask;
            });
        await fixture.PrimeAsync(1_000); // Public status response, not a private max calculator.
        Assert.Null(fixture.Owner.EquipmentHitpointsStatus);
        await fixture.JoinAsync();
        Assert.Null(fixture.Owner.EquipmentHitpointsStatus); // Membership does not install a desired cache.
        Assert.False(fixture.Owner.Character.Attributes.ContainsKey("MaxHitpoints"));

        // Both assignments remain positive; a fix restricted to negative HP
        // cannot satisfy this source Status.Max ownership regression.
        foreach (var expectedAfter in new uint[] { 600, 200 })
        {
            var start = fixture.Order.Count;
            armed = true;
            await fixture.HitAsync();
            armed = false;
            Assert.Equal((long)expectedAfter, fixture.Owner.Character.MinHitPoints);
            var frames = fixture.Order.ToArray().Skip(start).ToArray();
            var owner = fixture.OwnerFramesSince(start);
            Assert.Equal(400, AssertSinglePhysicalAttack(owner, TargetPlayerUid).Query.Damage);
            var responses = frames.Where(frame => MonsterPostIncoming(frame.Bytes)).ToArray();
            Assert.Equal(2, responses.Length);
            Assert.Equal(new[] { PlayerUid, TargetPlayerUid }.Order(), responses.Select(frame => frame.Recipient).Order());
            Assert.Equal(responses[0].Bytes, responses[1].Bytes);
            Assert.All(responses, frame => Assert.Equal((ushort)2_135,
                BinaryPrimitives.ReadUInt16LittleEndian(frame.Bytes.AsSpan(2, 2))));

            // Source HitPoints setter changes CURRENT only. Its team projection
            // and both self packets still use the previous independent Max1000.
            MonsterPostResponseAssertOneSetter(frames, maximum: 1_000, hitpoints: expectedAfter);
            Assert.All(owner.Where(DirectNormalDeathIsHp), bytes => Assert.Equal((ushort)2_176,
                BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(2, 2))));
            var status = await fixture.StatusAsync();
            Assert.Equal(1_000U, status.MaxHP);
            Assert.True(LegacyEquipmentHitpointsStatus.TryGetForOwner(fixture.Owner, out var captured));
            Assert.Equal(1_000U, captured);
            MonsterPostAssertNoDeath(fixture.Owner);
            Assert.Empty(ParseDeathAttacks(owner));
            Assert.Empty(ParseSpellAnimations(owner));
            Assert.DoesNotContain(owner, bytes => LegacyShowEquipmentPacket.TryParse(ReadFrame(bytes), out _));
            expectedBefore = expectedAfter;
        }
        Assert.Equal(4, incoming);
        Assert.Equal(0, fixture.ScheduledTimers);
    }
}
