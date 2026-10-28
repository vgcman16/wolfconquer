using System.Reflection;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyBossHarmRankMailRewardTests
{
    private sealed partial class Receive8418SavedOwner
    {
        private void RegisterRealCloseCleanup()
        {
            // Invoke the real Frame lifecycle component. DisconnectAsync has an
            // actual transport-close cleanup route, including owner retirement.
            typeof(WorldServerFrameHandler).GetMethod("RegisterTradeDisconnect", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(handler, [Connection, CancellationToken.None]);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            var errors = new List<Exception>();
            try { if (Connection is not null) Connection.CompleteTransportCloseAsync().GetAwaiter().GetResult(); }
            catch (Exception error) { errors.Add(error); }
            try { registry?.Dispose(); }
            catch (Exception error) { errors.Add(error); }
            try { beasts?.Dispose(); }
            catch (Exception error) { errors.Add(error); }
            try { Log.Dispose(); }
            catch (Exception error) { errors.Add(error); }
            try
            {
                if (ownsRoot)
                {
                    ValidateRoot();
                    Directory.Delete(root, recursive: true);
                }
            }
            catch (Exception error) { errors.Add(error); }
            if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
            if (errors.Count > 1) throw new AggregateException("Receive8418 owned fixture cleanup failed.", errors);
        }

        private void ValidateRoot()
        {
            const string prefix = "wolf-receive8418-owner-";
            var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
            var name = Path.GetFileName(full);
            if (!StringComparer.OrdinalIgnoreCase.Equals(Path.GetDirectoryName(full),
                    Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()))) ||
                !name.StartsWith(prefix, StringComparison.Ordinal) || !Guid.TryParseExact(name[prefix.Length..], "N", out _))
                throw new InvalidOperationException("Receive8418 fixture requires its actual GUID-owned Temp root.");
        }
    }
}
