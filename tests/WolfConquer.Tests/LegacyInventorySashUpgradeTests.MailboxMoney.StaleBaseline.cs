using System.Globalization;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Fact]
    public Task ActualD13FrameMailboxMoneyStaleCharacterImageRefusesThenExactBaselineRestorationCanRetry() =>
        RunAsync(async fixture =>
        {
            await fixture.EnterAtomicMailFrameAsync(mixed: true, flag: 1);
            var before = fixture.CaptureTransferFrame();
            var relative = Path.GetRelativePath(fixture.Root, fixture.CharacterPath);
            var original = before.Files[relative];
            var changed = ProjectSashFields(original, new Dictionary<string, string>
            {
                ["Money"] = checked(before.Owner.Character.Money + 1).ToString(CultureInfo.InvariantCulture),
            });
            Assert.False(original.AsSpan().SequenceEqual(changed));
            File.WriteAllBytes(fixture.CharacterPath, changed); // Owned adversarial disk input, after genuine login.
            var injected = new Dictionary<string, byte[]>(before.Files, StringComparer.OrdinalIgnoreCase)
            {
                [relative] = changed,
            };

            await fixture.FrameAsync(MailboxMoneyRequest());

            fixture.AssertFiles(injected);
            Assert.Equal(before.FrameCount, fixture.Sent.Count);
            // Restore only the owned disk fault. The real Frame Store/cache remain untouched.
            File.WriteAllBytes(fixture.CharacterPath, original);
            await fixture.FrameAsync(MailboxMoneyRequest());
            AssertMailboxMoneyCommitted(fixture, before);
            AssertMailboxMoneyFrames(fixture, before, before.FrameCount);
            await AssertMailboxMoneyReplayAsync(fixture, before);
        });
}
