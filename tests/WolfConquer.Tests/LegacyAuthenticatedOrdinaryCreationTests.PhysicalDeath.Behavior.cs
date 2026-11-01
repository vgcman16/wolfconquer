using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyAuthenticatedOrdinaryCreationTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task ARealEquipDeliveryDeathCommitsItsActualDerivedSpellOutcomeAndPreservesSignedCreationHistory(bool changed) =>
        RunPhysicalDeathAsync(async f =>
        {
            f.BeforePhysicalSend = (_, _) => AssertPhysicalDeathPublished(f);
            using (var observation = LegacyJournalInterruption.Observe())
            {
                await f.EquipAndAttackAsync(observation);
                Assert.True(observation.PendingObserved, f.Owner.Log.ToString());
                Assert.Equal(new[] { 0, 1, 2 }, observation.Visited);
            }
            f.BeforePhysicalSend = null;
            AssertPhysicalDeathPublished(f);
            var interactions = f.PhysicalFrames.Where(row => MsgAttackPacket.TryParse(CreationFixture.ReadFrame(row.Bytes), out var packet) &&
                packet.Query.AttackType == MsgAttackType.Physical).ToArray();
            Assert.Equal(new[] { CreationFixture.RuneActor, DeathTargetAccount }, interactions.Select(row => row.Recipient));
            Assert.All(interactions, row => Assert.Equal(DeathInteractionGolden(), row.Bytes));
            var grants = f.PhysicalFrames.Where(row => LegacySpellPacket.TryParse(CreationFixture.ReadFrame(row.Bytes), out var spell) &&
                spell.Id == 20000).ToArray();
            Assert.Equal(changed ? 1 : 0, grants.Length);
            if (changed)
            {
                Assert.Equal(DeathTargetAccount, grants[0].Recipient); Assert.Equal(DeathGrantedGolden(), grants[0].Bytes);
                Assert.True(f.PhysicalFrames.IndexOf(grants[0]) > f.PhysicalFrames.FindLastIndex(row => interactions.Contains(row)));
            }
            // The cap is observed from a real prior login packet, not the
            // unjoined native Status.MaxHP field or a fabricated calculator.
            var maxima = f.PhysicalFrames.Select(row => LegacyMsgUpdatePacket.TryParse(CreationFixture.ReadFrame(row.Bytes), out var update) ? update : null)
                .Where(update => update is not null).SelectMany(update => update!.Updates)
                .Where(row => row.Type == LegacyMsgUpdateDataType.MaxHitpoints).ToArray();
            Assert.NotEmpty(maxima); Assert.All(maxima, row => Assert.Equal(f.Maximum, row.Value));
            Assert.Empty(f.Owner.Sent); Assert.False(f.Owner.Runtime.Recovery.IsBlocked);
            var before = Assert.IsType<PhysicalDeathBefore>(f.Before);
            Assert.Equal(before.Payload, CompletedIntent(f.Owner).CopyPayload());
            int count = f.PhysicalFrames.Count;
            await f.Owner.Handler.SendDueBasicPhysicalAttacksAsync(CancellationToken.None);
            Assert.Equal(count, f.PhysicalFrames.Count); AssertPhysicalDeathPublished(f);
            await RestartPhysicalDeathAsync(f, before);
        }, changed);
}
