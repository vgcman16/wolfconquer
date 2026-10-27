using System.Security.AccessControl;
using System.Security.Principal;
using System.Reflection;

namespace WolfConquer.Tests;

public sealed partial class UnknownPacketAndFieldLoggerTests
{
    [Fact]
    public void OutputHasAPrivateOwnedDirectoryAndNoBom()
    {
        WorldWireDiagnosticTestFiles.Run(files =>
        {
            var logger = files.Open();
            var directory = Path.GetDirectoryName(logger.OutputPath!)!;
            if (OperatingSystem.IsWindows())
            {
                using var identity = WindowsIdentity.GetCurrent();
                var sid = identity.User;
                Assert.NotNull(sid);
                var security = FileSystemAclExtensions.GetAccessControl(new DirectoryInfo(directory));
                Assert.True(security.AreAccessRulesProtected);
                Assert.Equal(sid, security.GetOwner(typeof(SecurityIdentifier)));
                var rules = security.GetAccessRules(true, true, typeof(SecurityIdentifier));
                Assert.Single(rules.Cast<FileSystemAccessRule>());
                foreach (var rule in rules.Cast<FileSystemAccessRule>())
                {
                    Assert.Equal(sid, rule.IdentityReference);
                    Assert.Equal(AccessControlType.Allow, rule.AccessControlType);
                }
            }
            else
            {
                var directoryAllowed = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
                var fileAllowed = UnixFileMode.UserRead | UnixFileMode.UserWrite;
                Assert.Equal((UnixFileMode)0, File.GetUnixFileMode(directory) & ~directoryAllowed);
                Assert.Equal((UnixFileMode)0, File.GetUnixFileMode(logger.OutputPath!) & ~fileAllowed);
            }
            Observe(logger, 4999, [0xAA]);
            Assert.Single(files.Read(logger));
        });
    }

    [Fact]
    public void FailureOfTheActualOwnedOutputDisablesDiagnosticsWithoutThrowing()
    {
        WorldWireDiagnosticTestFiles.Run(files =>
        {
            var logger = files.Open();
            var field = typeof(WolfConquer.Core.World.UnknownPacketAndFieldLogger).GetField("output", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field);
            var stream = Assert.IsType<FileStream>(field.GetValue(logger));
            stream.Dispose(); // actual resource closure, not a replacement sink/callback
            Observe(logger, 4999, [0xAA]);
            Assert.False(logger.IsEnabled);
            Assert.Empty(files.Read(logger));
        });
    }
}
