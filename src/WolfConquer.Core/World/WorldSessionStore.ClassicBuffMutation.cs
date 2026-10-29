namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    // A transient monster status mutation, not a second cost or file writer.
    // The actual accepted actor record remains pinned until this concrete status
    // operation completes; all output occurs after these scopes have ended.
    internal bool TryApplyClassicMonsterBuff(ProcParticipant actor, MonsterRuntimeStateStore.ClassicBuffTarget target,
        int flag, TimeSpan duration, DateTimeOffset now, bool refresh,
        out LegacyMonsterTimedStatusApplyResult result, out LegacyMonsterInstance visible,
        out ulong[] vector, out string reason)
    {
        result = default; visible = null!; vector = []; reason = "classicActorChanged";
        RefuseBoundSpellReentry(actor.Connection);
        using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, actor.Expected);
        if (!_connectionGates.TryGetValue(actor.Connection, out var gate)) return false;
        lock (gate)
        {
            using var scope = EnterNativeConnectionGate();
            if (!TryCurrentClassicParticipant(actor, out _) ||
                !LegacyMailboxPackageMutationFence.CanMutateSession(MailboxPackages, actor.Expected, actor.Expected)) return false;
            if (!target.TryEnter(out var lease, out reason)) return false;
            using (lease) target.ApplyHeld(flag, duration, now, refresh, out result, out visible, out vector);
            reason = string.Empty; return true;
        }
    }
}
