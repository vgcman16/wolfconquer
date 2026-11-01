using WolfConquer.Core.Packets;
using WolfConquer.Core.World;
using System.Text;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedExperienceContinuationDriverTests
{
    [Fact]
    public Task ActualCreditFileLeaseRefusesBeforeAnyAwardOrLevelSuccess() => Run(async (f, monster, catalog) =>
    {
        var before = f.Current();
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(before.NativeGameClient!.OrdinarySpellBinding);
        var files = f.Data.Files();
        f.ActorFrames.Clear();
        using (new FileStream(f.CharacterPath(), FileMode.Open, FileAccess.Read, FileShare.Read))
            await Hit(f, monster);
        Assert.Same(before, f.Current());
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
        Assert.Equal((ushort)138, binding.Player.Level); Assert.Equal(0UL, binding.Player.Experience);
        f.Data.AssertFiles(files);
        Assert.Null(LegacyExperienceContinuationCodec.ReadCharacterImage(File.ReadAllBytes(f.CharacterPath()),
            before.AccountId, checked((uint)before.Character.Uid)));
        Assert.DoesNotContain(f.ActorFrames, row => row.AsSpan().SequenceEqual(
            Update(OwnedEquipmentBehaviorFixture.Actor, LegacyMsgUpdateDataType.Level, 139)));
        Assert.Contains("WorldServer refused combat experience continuation reason=experienceCreditReplace:", f.Log.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("WorldServer sent legacy monster experience ", f.Log.ToString(), StringComparison.Ordinal);
    });

    [Fact]
    public Task ActualSavedFileLeaseAfterFirstLevelKeepsTheEarnedPendingImage() => Run(async (f, monster, catalog) =>
    {
        var before = f.Current();
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(before.NativeGameClient!.OrdinarySpellBinding);
        var expected = f.Data.Files();
        var characterKey = Path.GetRelativePath(f.Data.Root, f.CharacterPath());
        var text = Encoding.UTF8.GetString(expected[characterKey]);
        text = Replace(text, "Level=138", "Level=140");
        text = Replace(text, "Atributes=12", "Atributes=18");
        text = Replace(text, "MinHitPoints=5000", "MinHitPoints=11136");
        expected[characterKey] = Encoding.UTF8.GetBytes(text);
        f.ActorFrames.Clear();
        FileStream? lease = null;
        Dictionary<string, byte[]>? atLease = null;
        f.BeforeSend = (_, bytes) =>
        {
            if (binding.Player.Level != 139 || lease is not null ||
                OwnedEquipmentBehaviorFixture.ReadFrame(bytes).Header.Opcode != LegacyPlayerStatusPacket.Opcode) return;
            atLease = f.Data.Files();
            lease = new FileStream(f.CharacterPath(), FileMode.Open, FileAccess.Read, FileShare.Read);
        };
        // The real Frame registry catches this internal persistence refusal;
        // unlike the transport-fault fixture, no send callback throws to the test.
        try { await Hit(f, monster); }
        finally { f.BeforeSend = null; lease?.Dispose(); }
        Assert.NotNull(lease); Assert.NotNull(atLease);
        Assert.False(f.Store.TryGet(f.Connection(OwnedEquipmentBehaviorFixture.Actor), out _));
        Assert.True(binding.IsRetired);
        Assert.False(f.Recovery.IsBlocked);
        Assert.False(binding.Client.OnAutoAttack);
        f.Data.AssertFiles(atLease);
        var pending = LegacyExperienceContinuationCodec.ReadCharacterImage(File.ReadAllBytes(f.CharacterPath()),
            before.AccountId, checked((uint)before.Character.Uid));
        Assert.NotNull(pending);
        Assert.Equal((ushort)139, pending.CurrentLevel);
        Assert.Equal((ushort)1, pending.StepIndex);
        Assert.Equal(LegacyExperienceContinuationPhase.QueryPending, pending.Phase);
        Assert.True(pending.CreditedExperience >= 2);
        AssertEarnedFromHit(f.ActorFrames, monster, pending);
        Assert.Equal(pending.InitialExperience + pending.CreditedExperience,
            pending.RemainingExperience + pending.ConsumedExperience + pending.CappedExperience);
        Assert.Contains("experienceContinuationQueryCheckpointRefused:experienceContinuationQueryReplace:capturedImageUnchanged:IOException",
            f.Log.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("WorldServer sent legacy monster experience ", f.Log.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(f.ActorFrames, row => LegacyMsgUpdatePacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(row), out var update) &&
            update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Experience));
        Assert.DoesNotContain(f.ActorFrames, row => row.AsSpan().SequenceEqual(
            Update(OwnedEquipmentBehaviorFixture.Actor, LegacyMsgUpdateDataType.Level, 140)));

        // The actual lease is released by the finally above. Resume the
        // already-earned marker through a fresh real connection, without a hit.
        await f.ReconnectContinuationAsync();
        var resumed = f.Current();
        var resumedBinding = Assert.IsType<LegacyOrdinarySpellBinding>(resumed.NativeGameClient!.OrdinarySpellBinding);
        Assert.Equal(140, resumed.Character.Level); Assert.Equal(0, resumed.Character.Experience);
        Assert.Equal(18, resumed.Character.AttributePoints); Assert.Equal(11136, resumed.Character.MinHitPoints);
        Assert.Equal((ushort)140, resumedBinding.Player.Level); Assert.Equal(0UL, resumedBinding.Player.Experience);
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(resumedBinding));
        Assert.Null(LegacyExperienceContinuationCodec.ReadCharacterImage(File.ReadAllBytes(f.CharacterPath()),
            resumed.AccountId, checked((uint)resumed.Character.Uid)));
        f.Data.AssertFiles(expected);
        Assert.Equal(Update(OwnedEquipmentBehaviorFixture.Actor, LegacyMsgUpdateDataType.Experience, 0), f.ActorFrames[^1]);
    });
}
