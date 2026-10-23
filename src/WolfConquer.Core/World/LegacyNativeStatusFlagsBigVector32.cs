using System.Collections.Concurrent;

namespace WolfConquer.Core.World;

// Sanitized cooperating-method owner. Public raw fields and escaped Flag values
// are legacy compatibility surfaces, not an atomic read/write transaction API.
internal partial class LegacyNativeStatusFlagsBigVector32 : LegacyNativeBitVector32
{
    public const int PermanentFlag = 2592000;
    public const int PermanentFlag2 = 604800;
    public const int PermanentFlag3 = 86400;

    public ConcurrentDictionary<int, Flag> ArrayFlags;
    private Flag[] Array = new Flag[0];
    internal object SyncRoot { get; } = new();

    public LegacyNativeStatusFlagsBigVector32(int Size) : base(NonNegativeSize(Size))
        => ArrayFlags = new();

    private static int NonNegativeSize(int size)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(size);
        return size;
    }

    public class Flag
    {
        public int Secounds;
        public int InvokerSecouds;
        public int Key;
        public DateTime TimerInvoker;
        public DateTime Timer;
        public bool RemoveOnDead;

        public Flag(int _flag, int _Secounds, bool _removeondead, int _InvokerSecouds)
        {
            Secounds = _Secounds;
            Key = _flag;
            Timer = DateTime.Now;
            RemoveOnDead = _removeondead;
            InvokerSecouds = _InvokerSecouds;
            if (InvokerSecouds != 0) TimerInvoker = DateTime.Now;
        }

        public bool Expire(DateTime Now) =>
            TryDeadline(Timer, Secounds, out var deadline) && Now >= deadline;

        public bool CheckInvoke(DateTime Now)
        {
            if (InvokerSecouds == 0) return true;
            if (!TryDeadline(TimerInvoker, InvokerSecouds, out var deadline) || Now < deadline) return false;
            TimerInvoker = DateTime.Now;
            return true;
        }
    }

    private static bool TryDeadline(DateTime timer, int seconds, out DateTime deadline)
    {
        try { deadline = timer.AddSeconds(seconds); return true; }
        catch (ArgumentOutOfRangeException) { deadline = default; return false; }
    }
}
