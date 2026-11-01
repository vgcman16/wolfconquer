using System.Text;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedExperienceContinuationDriverTests
{
    [Fact]
    public Task ActualCommittedFirstLevelOutputLossReconnectsWithoutRecredit() => Run(async (f, monster, catalog) =>
    {
        var before = f.Current();
        var oldBinding = Assert.IsType<LegacyOrdinarySpellBinding>(before.NativeGameClient!.OrdinarySpellBinding);
        var expected = f.Data.Files();
        var key = Path.GetRelativePath(f.Data.Root, f.CharacterPath());
        var text = Encoding.UTF8.GetString(expected[key]);
        text = Replace(text, "Level=137", "Level=139");
        text = Replace(text, "Atributes=12", "Atributes=18");
        text = Replace(text, "MinHitPoints=5000", "MinHitPoints=11136");
        expected[key] = Encoding.UTF8.GetBytes(text);
        int interrupted = 0;
        f.BeforeSend = (_, bytes) =>
        {
            if (oldBinding.Player.Level != 138 || OwnedEquipmentBehaviorFixture.ReadFrame(bytes).Header.Opcode != LegacyPlayerStatusPacket.Opcode) return;
            interrupted++;
            throw new IOException("owned exact-checkpoint output interruption");
        };
        try { await ExpectHitFailure(f, monster); }
        finally { f.BeforeSend = null; }
        Assert.Equal(1, interrupted);
        Assert.False(f.Store.TryGet(f.Connection(OwnedEquipmentBehaviorFixture.Actor), out _));
        Assert.True(oldBinding.IsRetired);
        Assert.False(f.Recovery.IsBlocked);
        var pending = LegacyExperienceContinuationCodec.ReadCharacterImage(File.ReadAllBytes(f.CharacterPath()),
            before.AccountId, checked((uint)before.Character.Uid));
        Assert.NotNull(pending);
        Assert.Equal(LegacyExperienceContinuationPhase.QueryPending, pending.Phase);
        Assert.Equal((ushort)1, pending.StepIndex);
        Assert.True(pending.CreditedExperience >= 2);
        AssertEarnedFromHit(f.ActorFrames, monster, pending);
        Assert.Contains(f.ActorFrames, row => row.AsSpan().SequenceEqual(Update(OwnedEquipmentBehaviorFixture.Actor, LegacyMsgUpdateDataType.Level, 138)));
        Assert.Equal((ushort)138, pending.CurrentLevel);
        var finalExperience = pending.RemainingExperience - 1;
        Assert.True(finalExperience < 1_000_000_000);
        text = Replace(text, "Experience=0", "Experience=" + finalExperience.ToString(System.Globalization.CultureInfo.InvariantCulture));
        expected[key] = Encoding.UTF8.GetBytes(text);

        await f.ReconnectContinuationAsync();
        var resumed = f.Current();
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(resumed.NativeGameClient!.OrdinarySpellBinding);
        Assert.NotSame(oldBinding, binding);
        Assert.Equal(139, resumed.Character.Level); Assert.Equal(checked((long)finalExperience), resumed.Character.Experience);
        Assert.Equal(18, resumed.Character.AttributePoints); Assert.Equal(11136, resumed.Character.MinHitPoints);
        Assert.Equal((ushort)139, binding.Player.Level); Assert.Equal(finalExperience, binding.Player.Experience);
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
        Assert.Null(LegacyExperienceContinuationCodec.ReadCharacterImage(File.ReadAllBytes(f.CharacterPath()),
            resumed.AccountId, checked((uint)resumed.Character.Uid)));
        f.Data.AssertFiles(expected);
        AssertResumeWire(f.ActorFrames);
        Assert.Equal(Update(OwnedEquipmentBehaviorFixture.Actor, LegacyMsgUpdateDataType.Experience, finalExperience), f.ActorFrames[^1]);
    }, initialLevel: 137);
}
