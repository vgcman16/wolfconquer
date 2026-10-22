#nullable disable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

// Complete native component used by the single physical Player.BitVector owner.
// Player flag wrappers are separate; RemoveFlag/ClearFlags remain unfinished work.
internal class LegacyNativeStatusFlagsBigVector32 : LegacyNativeBitVector32
{
    public class Flag
    {
        public int Secounds = 0;

        public int InvokerSecouds = 0;

        public int Key;

        public DateTime TimerInvoker = default(DateTime);

        public DateTime Timer = default(DateTime);

        public bool RemoveOnDead = false;

        public Flag(int _flag, int _Secounds, bool _removeondead, int _InvokerSecouds)
        {
            Secounds = _Secounds;
            Key = _flag;
            Timer = DateTime.Now;
            RemoveOnDead = _removeondead;
            InvokerSecouds = _InvokerSecouds;
            if (InvokerSecouds != 0)
            {
                TimerInvoker = DateTime.Now;
            }
        }

        public bool Expire(DateTime Now)
        {
            if (Now >= Timer.AddSeconds(Secounds))
            {
                return true;
            }
            return false;
        }

        public bool CheckInvoke(DateTime Now)
        {
            if (InvokerSecouds == 0)
            {
                return true;
            }
            if (Now >= TimerInvoker.AddSeconds(InvokerSecouds))
            {
                TimerInvoker = DateTime.Now;
                return true;
            }
            return false;
        }
    }

    public const int PermanentFlag = 2592000;

    public const int PermanentFlag2 = 604800;

    public const int PermanentFlag3 = 86400;

    public ConcurrentDictionary<int, Flag> ArrayFlags;

    private Flag[] Array = new Flag[0];

    private bool Update = false;

    public LegacyNativeStatusFlagsBigVector32(int Size)
        : base(Size)
    {
        ArrayFlags = new ConcurrentDictionary<int, Flag>();
    }

    public bool TryAdd(int flag, int Secounds, bool RemoveOnDead, int InvokerSecouds)
    {
        if (!ArrayFlags.ContainsKey(flag))
        {
            ArrayFlags.TryAdd(flag, new Flag(flag, Secounds, RemoveOnDead, InvokerSecouds));
            Add(flag);
            Update = true;
            return true;
        }
        return false;
    }

    public bool UpdateFlag(int flag, int Secounds, bool SetNewTimer, int MaxSecounds)
    {
        if (ArrayFlags.TryGetValue(flag, out var value))
        {
            if (SetNewTimer)
            {
                value.Timer = DateTime.Now;
                value.Secounds = Secounds;
            }
            else if (value.Timer.AddSeconds(value.Secounds) > DateTime.Now.AddSeconds(MaxSecounds))
            {
                value.Timer = DateTime.Now;
                value.Secounds = MaxSecounds;
            }
            else
            {
                value.Secounds += Secounds;
            }
            return true;
        }
        return false;
    }

    public bool TryRemove(int flag)
    {
        if (ArrayFlags.TryRemove(flag, out var _))
        {
            Remove(flag);
            Update = true;
            return true;
        }
        return false;
    }

    public bool InLife(int flag, DateTime Now64)
    {
        if (ArrayFlags.TryGetValue(flag, out var value))
        {
            return value.Expire(Now64);
        }
        return false;
    }

    public bool CheckInvoke(int flag, DateTime Now64)
    {
        if (ArrayFlags.TryGetValue(flag, out var value))
        {
            return value.CheckInvoke(Now64);
        }
        return false;
    }

    public bool ContainFlag(int flag)
    {
        return ArrayFlags.ContainsKey(flag);
    }

    public void GetClear()
    {
        List<int> list = new List<int>();
        Flag[] flags = GetFlags();
        foreach (Flag flag in flags)
        {
            if (flag.RemoveOnDead)
            {
                list.Add(flag.Key);
            }
        }
        foreach (int item in list)
        {
            TryRemove(item);
        }
    }

    public Flag[] GetFlags()
    {
        try
        {
            if (Update)
            {
                Array = ArrayFlags.Values.ToArray();
                Update = false;
            }
            return Array;
        }
        catch (Exception e)
        {
            LegacyNativeConsole.WriteException(e);
            return new Flag[0];
        }
    }
}
