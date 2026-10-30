namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Sanitized native Player1003/1005/1007. Ordinary named access cooperates
    // with claiming; full producers must install the pair/deadline together.
    private readonly object messageBoxGate = new();
    private Action<LegacyNativeGameClientRuntime>? messageOk;
    private Action<LegacyNativeGameClientRuntime>? messageCancel;
    private DateTime messageBoxDeadline;

    public Action<LegacyNativeGameClientRuntime>? MessageOK
    {
        get { lock (messageBoxGate) return messageOk; }
        set { lock (messageBoxGate) messageOk = value; }
    }

    public Action<LegacyNativeGameClientRuntime>? MessageCancel
    {
        get { lock (messageBoxGate) return messageCancel; }
        set { lock (messageBoxGate) messageCancel = value; }
    }

    public DateTime StartMessageBox
    {
        get { lock (messageBoxGate) return messageBoxDeadline; }
        set { lock (messageBoxGate) messageBoxDeadline = value; }
    }

    internal void SetMessageBox(Action<LegacyNativeGameClientRuntime>? ok,
        Action<LegacyNativeGameClientRuntime>? cancel, DateTime deadline)
    {
        lock (messageBoxGate)
        {
            messageOk = ok;
            messageCancel = cancel;
            messageBoxDeadline = deadline;
        }
    }

    internal Action<LegacyNativeGameClientRuntime>? ClaimMessageBoxResponse(LegacyNativeGameClientRuntime user)
    {
        ArgumentNullException.ThrowIfNull(user);
        lock (messageBoxGate)
        {
            if (!ReferenceEquals(Owner, user) || !ReferenceEquals(user.Player, this))
                throw new InvalidOperationException("Message-box player does not belong to the responding client.");
            if (LegacyNativeConstants.BlockTeleportMap.Contains(Map)) return null;
            var callback = messageBoxDeadline > DateTime.Now ? messageOk ?? messageCancel : null;
            // Claim/remove the exact current pair before any callback can throw,
            // reenter or install a replacement. Never clear again after invoking.
            messageOk = null;
            messageCancel = null;
            return callback;
        }
    }
}
