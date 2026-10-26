using System.Text;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Fact]
    public Task NativePendingRecoveryRejectsMalformedSavedPinBeforeColdMutationOrAnySuccessfulLoginOutput() =>
        RunPackageJoinAsync(async (fixture, join) =>
        {
            var text = Encoding.UTF8.GetString(File.ReadAllBytes(fixture.CharacterPath));
            Assert.DoesNotContain("SecurityPass=", text, StringComparison.Ordinal);
            text = text.Replace("[Unrelated]", "SecurityPass=invalid" + Environment.NewLine + "[Unrelated]", StringComparison.Ordinal);
            File.WriteAllBytes(fixture.CharacterPath, new UTF8Encoding(false).GetBytes(text));
            await join.RestartAsync();
            var initial = CapturePackageJoin(fixture, join);
            var intent = PrepareDebtIntent(fixture, join, initial);
            var files = fixture.PackageFiles();
            var first = fixture.Sent.Count;

            var native = await join.RestartNativeDebtRecoveryAsync();

            Assert.False(join.SessionStore.TryGet(join.Connection!, out _));
            Assert.Equal(0U, native.Player.UID);
            Assert.Equal(0U, native.Player.Class);
            Assert.Equal(0U, native.Player.ClassExperience);
            Assert.False(native.FullLoading);
            Assert.False(native.Player.IsCheckedPass);
            Assert.Equal(first, fixture.Sent.Count);
            AssertPackageJoinFiles(fixture, files);
            Assert.Contains("mailbox recovery saved PIN admission refused", fixture.Log.ToString(), StringComparison.Ordinal);
            Assert.True(join.Registry.TryReadProducerIntent(join.Domain, intent.Request.OperationId, out var retained, out var reason), reason);
            Assert.Equal(intent.Request, retained!.Request);
            Assert.Equal(intent.Allocation, retained.Allocation);
            Assert.Equal(intent.CopyPayload(), retained.CopyPayload());
        });
}
