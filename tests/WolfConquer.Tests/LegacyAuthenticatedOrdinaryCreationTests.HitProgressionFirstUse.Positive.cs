using System.Buffers.Binary;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyAuthenticatedOrdinaryCreationTests
{
    [Theory]
    [InlineData(false, true, false)]
    [InlineData(true, true, true)]
    [InlineData(true, false, true)]
    public Task GenuineMissingBooksAreCreatedByTheFirstValidatedJointHitBeforeOutput(bool lethal, bool missingProfs, bool missingSpells) =>
        RunFirstUseHitAsync(lethal, missingProfs, missingSpells, async f =>
        {
            var before = f.Owner.Data.Files(); var after = FirstUseAfterImages(f, before);
            var original = f.Actor.NativeGameClient!.OrdinarySpellBinding!.SavedProficiencies;
            var observations = 0;
            f.Owner.BeforeSend = (_, _) =>
            {
                observations++;
                Assert.False(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
                f.Owner.Data.AssertFiles(after); AssertFirstUseNativeBook(f);
                Assert.False(f.Actor.NativeGameClient!.OnAutoAttack);
            };
            try { await f.AttackAsync(); }
            finally { f.Owner.BeforeSend = null; }
            Assert.True(observations > 0); f.Owner.Data.AssertFiles(after); AssertFirstUseNativeBook(f);
            Assert.NotSame(original, f.Actor.NativeGameClient!.OrdinarySpellBinding!.SavedProficiencies);
            var wire = Assert.Single(f.Owner.Frames(CreationFixture.RuneActor), bytes => LegacyProficiencyPacket.TryParse(
                CreationFixture.ReadFrame(bytes), out _));
            Assert.Equal(FirstUseProficiencyWireGolden(), wire);
            Assert.False(f.Owner.Runtime.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(f.Owner.Data.Users));
            var ledger = File.ReadAllBytes(f.Owner.Data.State); var output = f.Owner.Frames(CreationFixture.RuneActor).Count + f.Owner.Frames(DeathTargetAccount).Count;
            await f.Owner.CloseAsync();
            for (var restart = 0; restart < 2; restart++)
            {
                using var recovered = f.Owner.Open();
                f.Owner.Data.AssertFiles(after); Assert.Equal(ledger, File.ReadAllBytes(f.Owner.Data.State));
                Assert.Equal(output, f.Owner.Frames(CreationFixture.RuneActor).Count + f.Owner.Frames(DeathTargetAccount).Count);
                Assert.False(LegacyTradeJournal.HasPending(f.Owner.Data.Users));
            }
        });

    [Fact]
    public Task TheFirstCreatedBookRemainsTheActualSavedOwnerForTheNextPhysicalHit() =>
        RunFirstUseHitAsync(false, true, false, async f =>
        {
            await f.AttackAsync(); AssertFirstUseNativeBook(f);
            var first = f.Actor.NativeGameClient!.OrdinarySpellBinding!.SavedProficiencies;
            var stamp = f.Actor.NativeGameClient.Player.AttackStamp;
            f.Clock.Advance(); f.Owner.Frames(CreationFixture.RuneActor).Clear(); f.Owner.Frames(DeathTargetAccount).Clear();
            await f.AttackAsync();
            Assert.Equal(1400L, f.Victim.Character.MinHitPoints); Assert.Equal(1400, f.Victim.NativeGameClient!.Player.HitPoints);
            var bytes = File.ReadAllBytes(f.ProfsPath);
            Assert.True(f.Actor.NativeGameClient.OrdinarySpellBinding!.SavedProficiencies.Matches(bytes));
            Assert.NotSame(first, f.Actor.NativeGameClient.OrdinarySpellBinding.SavedProficiencies);
            Assert.NotEqual(stamp, f.Actor.NativeGameClient.Player.AttackStamp);
            var row = Assert.Single(PlayerProficiencySnapshotLoader.Parse(bytes).Proficiencies);
            Assert.Equal(410U, row.Id); Assert.Equal(8U, row.Level);
            Assert.Equal(13_440_800U, row.Experience); Assert.Equal(22_000_000U, row.NeededExperience);
            var expected = FirstUseProfsGolden(); BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(8), 8);
            BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(16), 13_440_800);
            BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(20), 22_000_000);
            Assert.Equal(expected, bytes);
            Assert.False(f.Owner.Runtime.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(f.Owner.Data.Users));
            Assert.True(f.Owner.Store.IsBoundSpellOwnerCurrent(f.Actor.NativeGameClient.OrdinarySpellBinding));
        });
}
