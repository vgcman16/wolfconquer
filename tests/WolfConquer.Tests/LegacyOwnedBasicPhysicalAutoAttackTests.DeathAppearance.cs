using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedBasicPhysicalAutoAttackTests
{
    [Theory]
    [InlineData("Sit", 250U)]
    [InlineData("65786", 65786U)]
    public Task LethalDeathAppearanceUsesValidatedFullActionAndNarrowsOnlyItsWireValue(string savedAction, uint fullAction) =>
        Run(async f =>
        {
            await f.SitAsync();
            var before = f.Files();
            var after = HpImage(f, before, 200, 0);
            var stamp = f.Clock.GetLocalNow().DateTime;
            var binding = f.Binding(Target);
            Assert.Equal(fullAction, (uint)binding.Player.Action);
            Assert.Equal(fullAction, LegacyActionState.RequireCurrent(f.Current(Target).Character));
            int appearances = 0;
            f.BeforeSend = (recipient, bytes) =>
            {
                var frame = OwnedEquipmentBehaviorFixture.ReadFrame(bytes);
                if (frame.Header.Opcode != LegacyPlayerSpawnPacket.Opcode) return;
                Assert.True(LegacyPlayerSpawnPacket.TryParse(frame, out var spawn));
                if (spawn.GetNumericField(2) != Target) return;
                Assert.Equal(Actor, recipient);
                f.AssertFiles(after); f.AssertCoherent(0); AssertCommittedActor(f, stamp);
                Assert.Equal(fullAction, (uint)binding.Player.Action);
                Assert.Equal(fullAction, LegacyActionState.RequireCurrent(f.Current(Target).Character));
                Assert.Equal(250UL, spawn.GetNumericField(31));
                // Independent protobuf field31 tag (248) and varint250 bytes.
                Assert.True(bytes.AsSpan().IndexOf(new byte[] { 0xF8, 0x01, 0xFA, 0x01 }) >= 0);
                appearances++;
            };
            try { await f.AttackAsync(); }
            finally { f.BeforeSend = null; }
            Assert.Equal(1, appearances);
            f.AssertFiles(after); f.AssertCoherent(0); AssertCommittedActor(f, stamp);
            Assert.Equal(fullAction, (uint)binding.Player.Action);
        }, targetHp: 200, targetAction: savedAction);
}
