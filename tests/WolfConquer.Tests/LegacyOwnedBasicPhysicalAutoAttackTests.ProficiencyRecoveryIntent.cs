using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedBasicPhysicalAutoAttackTests
{
    // Source rebase, UNRUN: requires the actual durable hit+proficiency
    // intent and configured startup recovery. No positive record is seeded.
    [Fact]
    public Task InteractionSendFailureAfterRealHitRecoversExactlyOneProficiencyAdvanceAcrossRestarts() =>
        RunProficiencyAsync(existing: true, async f =>
        {
            var before = f.Files();
            var expected = HpImage(f, before, 2000, 1700);
            string profPath = ProficiencyPath(f);
            string profKey = Path.GetRelativePath(f.Owner.Data.Root, profPath);
            byte[] originalProfs = File.ReadAllBytes(profPath);
            byte[] expectedProfs = ProficiencyAfterImage(originalProfs, existing: true);
            expected[profKey] = expectedProfs;
            int interrupted = 0;
            Exception? observationFailure = null;
            f.BeforeSend = (recipient, bytes) =>
            {
                if (recipient != Actor || !IsPhysical(bytes)) return;
                try
                {
                    Assert.Equal(InteractionGolden(), bytes);
                    var target = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(
                        File.ReadAllBytes(f.Owner.CharacterPath(Target)), out _));
                    Assert.Equal(1700L, target.MinHitPoints); // Genuine hit committed before this send.
                    interrupted++;
                }
                catch (Exception error) { observationFailure ??= error; throw; }
                // Keep expected transport loss separate from observation errors.
                throw new SocketException((int)SocketError.ConnectionReset);
            };
            try
            {
                // Frame handles the real failed transport; do not turn it into
                // an accepting callback or manufacture a completed journal.
                await f.Owner.Handler.HandleFrameAsync(f.Owner.Connection(Actor),
                    OwnedEquipmentBehaviorFixture.ReadFrame(Request().ToFrameBytes()));
            }
            finally
            {
                f.BeforeSend = null;
                if (observationFailure is { } error) ExceptionDispatchInfo.Capture(error).Throw();
            }
            Assert.Equal(1, interrupted);
            int outputCount = f.Order.Count;
            await f.Owner.Runtime.DisposeAsync();

            for (int restart = 0; restart < 2; restart++)
            {
                Assert.True(LegacyOwnedAdmissionRuntime.TryOpen(f.Owner.Data.Loader, f.Owner.Data.Options(),
                    out var candidate, out var reason), reason);
                await using var recovered = Assert.IsType<LegacyOwnedAdmissionRuntime>(candidate);
                f.AssertFiles(expected);
                var saved = PlayerProficiencySnapshotLoader.Parse(File.ReadAllBytes(profPath));
                var sword = Assert.Single(saved.Proficiencies, row => row.Id == 410);
                Assert.Equal(5U, sword.Level); Assert.Equal(50_100U, sword.Experience);
                Assert.Equal(new byte[] { 0xA5, 0xC7, 0xD9 }, File.ReadAllBytes(profPath).AsSpan(13, 3).ToArray());
                Assert.False(LegacyTradeJournal.HasPending(f.Owner.Data.Users));
                Assert.Equal(outputCount, f.Order.Count);
            }
        });
}
