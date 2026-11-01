using System.Buffers.Binary;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyAuthenticatedOrdinaryCreationTests
{
    [Fact]
    public Task TheRealFrameProficiencyWriterAdvancesMissingOriginForTheFollowingJointHit() =>
        RunFirstUseHitAsync(false, true, false, async f =>
        {
            var before = f.Actor; var binding = before.NativeGameClient!.OrdinarySpellBinding!;
            var origin = binding.SavedProficiencies; Assert.True(origin.Matches(null));
            var proposed = before with { PlayerProficiencies = new PlayerProficiencySnapshot([new(410, 5, 0, 100, 1_600_000)]) };
            // Proposed book DATA goes through the real generic native/holder
            // publication and concrete Frame file postlude, without a callback.
            f.Owner.Store.Set(f.Owner.RuneConnection!, proposed, out var final);
            Assert.Same(final, f.Actor); Assert.True(f.Owner.Store.IsBoundSpellOwnerCurrent(binding));
            var expected = FirstUseProfsGolden(); BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(8), 5);
            BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(16), 100);
            BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(20), 1_600_000);
            Assert.Equal(expected, File.ReadAllBytes(f.ProfsPath));
            Assert.NotSame(origin, binding.SavedProficiencies); Assert.True(binding.SavedProficiencies.Matches(expected));
            var frameWritten = binding.SavedProficiencies;
            Assert.Empty(f.Owner.Frames(CreationFixture.RuneActor)); Assert.Empty(f.Owner.Frames(DeathTargetAccount));
            await f.AttackAsync();
            Assert.Equal(1700L, f.Victim.Character.MinHitPoints); Assert.Equal(1700, f.Victim.NativeGameClient!.Player.HitPoints);
            BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(16), 50_100);
            Assert.Equal(expected, File.ReadAllBytes(f.ProfsPath));
            Assert.NotSame(frameWritten, binding.SavedProficiencies); Assert.True(binding.SavedProficiencies.Matches(expected));
            Assert.Equal(50_100U, binding.Proficiencies.ClientProf[410].Experience);
            Assert.False(f.Owner.Runtime.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(f.Owner.Data.Users));
        });
}
