#nullable disable

using System;
using System.Collections.Concurrent;
using System.Threading;
using WolfConquer.Core.World;

namespace WolfConquer.Core.Runtime;

// Complete native MrRayzo.ConcurrentSmartThreadQueue<T>. Construction alone
// does not start a worker; the real Start/ThreadItem lifecycle is preserved.
internal abstract class LegacyNativeConcurrentSmartThreadQueue<T>
{
    public ConcurrentQueue<T> Queues;

    public LegacyNativeThreadItem thread;

    public int Count => Queues.Count;

    public LegacyNativeConcurrentSmartThreadQueue(int Processors)
    {
        Queues = new ConcurrentQueue<T>();
    }

    protected abstract void OnDequeue(T Value, int time);

    public bool Finish()
    {
        while (Queues.Count > 0)
        {
            Thread.Sleep(1);
        }
        Thread.Sleep(1000);
        return true;
    }

    public void Start(int period)
    {
        if (LegacyNativeConsole.AllowSource())
        {
            thread = new LegacyNativeThreadItem(period, "ConquerThreadQueue", Work);
            thread.Open();
        }
    }

    public void Work()
    {
        try
        {
            DateTime now = DateTime.Now;
            T result;
            while (Queues.TryDequeue(out result))
            {
                OnDequeue(result, now.AllMilliseconds());
            }
        }
        catch (Exception e)
        {
            LegacyNativeConsole.WriteException(e);
        }
    }

    public virtual void Enqueue(T obj)
    {
        if (LegacyNativeConsole.AllowSource())
        {
            Queues.Enqueue(obj);
        }
    }
}
