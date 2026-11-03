using System.Runtime.ExceptionServices;

namespace WolfConquer.Core.Data.SqlWorld;

public sealed partial class SqlWorldStateRepository
{
    // Only the provider resources created by this repository are passed here.
    private static async ValueTask<T> UseResourceAsync<TResource, T>(TResource resource, Func<TResource, ValueTask<T>> action)
        where TResource : IAsyncDisposable
    {
        T result = default!;
        Exception? primary = null;
        try { result = await action(resource).ConfigureAwait(false); }
        catch (Exception error) { primary = error; }
        Exception? cleanup = null;
        try { await resource.DisposeAsync().ConfigureAwait(false); }
        catch (Exception error) { cleanup = error; }
        if (primary is not null)
        {
            if (cleanup is not null) throw new AggregateException(primary, cleanup);
            ExceptionDispatchInfo.Capture(primary).Throw();
        }
        if (cleanup is not null) ExceptionDispatchInfo.Capture(cleanup).Throw();
        return result;
    }

    private static async ValueTask UseResourceAsync<TResource>(TResource resource, Func<TResource, ValueTask> action)
        where TResource : IAsyncDisposable
    {
        await UseResourceAsync<TResource, bool>(resource, async actual =>
        {
            await action(actual).ConfigureAwait(false);
            return true;
        }).ConfigureAwait(false);
    }
}
