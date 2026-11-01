using System.Globalization;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyAuthenticatedOrdinaryCreationTests
{
    [Fact]
    public Task TheActualConfiguredMissingProficiencyDirectoryIsCreatedOnlyByTheValidatedFirstUseJournal()
    {
        var clock = new FirstUseHitClock();
        return Run(async f =>
        {
            Assert.False(Directory.Exists(f.Data.Proficiencies));
            await f.ConnectRuneActorAsync();
            var target = f.NewConnection(DeathTargetAccount);
            await f.SendOnAsync(target, new LegacyMsgLoginGamePacket(DeathTargetAccount).ToFrameBytes());
            Assert.False(Directory.Exists(f.Data.Proficiencies));
            var input = new FirstUseHitFixture(f, target, clock, false, true, false);
            var binding = input.Actor.NativeGameClient!.OrdinarySpellBinding!;
            Assert.Empty(binding.Proficiencies.ClientProf); Assert.True(binding.SavedProficiencies.Matches(null));
            f.Sent.Clear(); f.Frames(CreationFixture.RuneActor).Clear(); f.Frames(DeathTargetAccount).Clear();
            var before = f.Data.Files(); var after = FirstUseAfterImages(input, before);
            await input.AttackAsync();
            Assert.True(Directory.Exists(f.Data.Proficiencies)); AssertFirstUseNativeBook(input);
            Assert.Equal(FirstUseProfsGolden(), File.ReadAllBytes(input.ProfsPath)); f.Data.AssertFiles(after);
            Assert.False(f.Runtime.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
            var ledger = File.ReadAllBytes(f.Data.State);
            var outputs = f.Frames(CreationFixture.RuneActor).Count + f.Frames(DeathTargetAccount).Count;
            await f.CloseAsync();
            for (var restart = 0; restart < 2; restart++)
            {
                using var recovered = f.Open();
                f.Data.AssertFiles(after); Assert.Equal(ledger, File.ReadAllBytes(f.Data.State));
                Assert.Equal(outputs, f.Frames(CreationFixture.RuneActor).Count + f.Frames(DeathTargetAccount).Count);
            }
        }, beforeBootstrap: f =>
        {
            SeedHitRecovery(f, lethal: false, changedDeath: false);
            File.Delete(Path.Combine(f.Data.Proficiencies, CreationFixture.RuneActor.ToString(CultureInfo.InvariantCulture) + ".bin"));
            File.Delete(Path.Combine(f.Data.Proficiencies, DeathTargetUid.ToString(CultureInfo.InvariantCulture) + ".bin"));
            Assert.Empty(Directory.EnumerateFileSystemEntries(f.Data.Proficiencies));
            // Empty owned fixture directory only, BEFORE Bootstrap/admission.
            // No live directory or configured route is changed after binding.
            Directory.Delete(f.Data.Proficiencies);
            f.RuntimeOptions = new LegacyRuntimeUpdateLoopOptions(false, TimeSpan.FromSeconds(1), clock,
                LegacySignedMillisecondClock.FromTimeProvider(clock, 10_000));
        }, enter: false);
    }
}
