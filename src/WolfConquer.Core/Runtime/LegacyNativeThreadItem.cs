#nullable disable

using System;
using System.Threading;

namespace WolfConquer.Core.Runtime;

// Complete native System.ThreadGroup.ThreadItem. The original unused Mutex and
// Abort-before-warning Close order are deliberate. On modern .NET, Abort throws
// PlatformNotSupportedException; this owner does not silently replace its behavior.
internal class LegacyNativeThreadItem
{
    private int Max_ProcessInterval;

    private Mutex ThreadMutex;

    private Action Event;

    private string Name;

    public Action<int> EventSleep;

    private Thread _thread;

    private bool Alive = false;

    public LegacyNativeThreadItem(int interval, string MutexName, Action _Event, Action<int> Sleep = null)
    {
        if (LegacyNativeConsole.AllowSource())
        {
            _thread = new Thread(ThreadProc);
            Max_ProcessInterval = interval;
            Name = MutexName;
            ThreadMutex = new Mutex(initiallyOwned: false, MutexName);
            Event = _Event;
            EventSleep = Sleep;
        }
    }

    public void Open()
    {
        if (LegacyNativeConsole.AllowSource() && !Alive)
        {
            Alive = true;
            _thread.Start();
        }
    }

    public void ThreadProc()
    {
        while (Alive)
        {
            try
            {
                if (!OnProcess())
                {
                    Close();
                    break;
                }
            }
            catch
            {
            }
        }
    }

    public void Close()
    {
        if (Alive)
        {
            Alive = false;
            _thread.Abort();
            LegacyNativeConsole.WriteLine("[WARNING] Thread " + Name + " has been aported .", ConsoleColor.DarkYellow);
        }
    }

    protected bool OnProcess()
    {
        if (!LegacyNativeConsole.AllowSource())
        {
            return false;
        }
        try
        {
            DateTime now = DateTime.Now;
            try
            {
                if (Event != null)
                {
                    Event();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            finally
            {
                DateTime now2 = DateTime.Now;
                int num = Max_ProcessInterval - (int)(now2 - now).TotalMilliseconds;
                if (num > 0 && num <= Max_ProcessInterval)
                {
                    if (EventSleep != null)
                    {
                        EventSleep(num);
                    }
                    else
                    {
                        if (num > 10000)
                        {
                            LegacyNativeConsole.WriteLine("[WARNING] Thread " + Name + " is gonna sleep for " + num / 1000 + " secoonds .", ConsoleColor.DarkYellow);
                        }
                        Thread.Sleep(num);
                    }
                }
            }
        }
        catch (Exception ex2)
        {
            Console.WriteLine(ex2.ToString());
        }
        return true;
    }
}
