using System.Text;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedExperienceContinuationDriverTests
{
    [Fact]
    public Task EarnedQueryPendingSurvivesActualStartupOutputLossAndResumesOnce() => Run(async (f, monster, catalog) =>
    {
        var before = f.Current();
        var oldBinding = Assert.IsType<LegacyOrdinarySpellBinding>(before.NativeGameClient!.OrdinarySpellBinding);
        var expected = f.Data.Files();
        var key = Path.GetRelativePath(f.Data.Root, f.CharacterPath());
        var expectedText = Encoding.UTF8.GetString(expected[key]);
        expectedText = Replace(expectedText, "Level=137", "Level=139");
        expectedText = Replace(expectedText, "Atributes=12", "Atributes=18");
        expectedText = Replace(expectedText, "MinHitPoints=5000", "MinHitPoints=11136");
        int interrupted = 0;
        f.BeforeSend = (_, bytes) =>
        {
            if (oldBinding.Player.Level != 138 || OwnedEquipmentBehaviorFixture.ReadFrame(bytes).Header.Opcode != LegacyPlayerStatusPacket.Opcode) return;
            interrupted++;
            throw new IOException("owned earned first-query interruption");
        };
        try { await ExpectHitFailure(f, monster); }
        finally { f.BeforeSend = null; }
        Assert.Equal(1, interrupted); Assert.True(oldBinding.IsRetired); Assert.False(f.Recovery.IsBlocked);
        var earned = LegacyExperienceContinuationCodec.ReadCharacterImage(File.ReadAllBytes(f.CharacterPath()),
            before.AccountId, checked((uint)before.Character.Uid));
        Assert.NotNull(earned);
        Assert.Equal(LegacyExperienceContinuationPhase.QueryPending, earned.Phase);
        Assert.Equal((ushort)1, earned.StepIndex);
        AssertEarnedFromHit(f.ActorFrames, monster, earned);
        var finalExperience = earned.RemainingExperience - 1;
        Assert.True(finalExperience < 1_000_000_000);
        expectedText = Replace(expectedText, "Experience=0", "Experience=" + finalExperience.ToString(System.Globalization.CultureInfo.InvariantCulture));
        expected[key] = Encoding.UTF8.GetBytes(expectedText);

        var interruptedStartup = await f.ReconnectWithStartupStatusFailureAsync();
        var answer = LegacyMsgMessagePacket.AnswerOk().ToFrameBytes();
        Assert.Contains(f.ActorFrames, row => row.AsSpan().SequenceEqual(answer));
        Assert.True(interruptedStartup.Binding.IsRetired);
        Assert.False(f.Store.TryGet(f.Connection(OwnedEquipmentBehaviorFixture.Actor), out _));
        Assert.False(f.Recovery.IsBlocked);
        f.Data.AssertFiles(interruptedStartup.Files);
        var retained = LegacyExperienceContinuationCodec.ReadCharacterImage(File.ReadAllBytes(f.CharacterPath()),
            before.AccountId, checked((uint)before.Character.Uid));
        Assert.Equal(earned, retained);

        await f.ReconnectContinuationAsync();
        var resumed = f.Current();
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(resumed.NativeGameClient!.OrdinarySpellBinding);
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
