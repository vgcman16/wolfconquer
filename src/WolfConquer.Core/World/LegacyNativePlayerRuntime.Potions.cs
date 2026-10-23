namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    public void ActiveAttackPotion(int Timer) => ActivatePotion(Timer, defense: false);

    public void ActiveDefensePotion(int Timer) => ActivatePotion(Timer, defense: true);

    private void ActivatePotion(int minutes, bool defense)
    {
        if (minutes <= 0) return;
        var client = Owner;
        lock (client.SpellAdmissionSyncRoot)
        lock (SpellAdmissionSyncRoot)
        {
            var vector = BitVector;
            var view = View;
            uint uid = UID;
            if (vector is null || view is null || !ReferenceEquals(client.Player, this) ||
                !ReferenceEquals(view.Owner, client)) return;
            lock (vector.SyncRoot)
            {
                LegacyNativePreparedFlagPublication? status = null;
                LegacyNativePreparedPotionMessage? message = null;
                bool committed = false;
                try
                {
                    DateTime deadline = DateTime.Now.AddMinutes(minutes);
                    var flag = defense ? LegacyNativeMsgUpdate.Flags.MagicShield : LegacyNativeMsgUpdate.Flags.Stigma;
                    if (!vector.TryContainFlag((int)flag, out bool present)) return;
                    LegacyNativeStatusFlagsBigVector32.PreparedChange? change = null;
                    if (!present)
                    {
                        if (!vector.TryPrepareAdd((int)flag, 60, true, 0, false, out change) ||
                            !vector.TryGetPreparedWords(change, out var words)) return;
                        status = LegacyNativePreparedFlagPublication.Create(uid, words, flag, 60, 0, 0);
                    }
                    message = LegacyNativePreparedPotionMessage.Create(defense, minutes, status);
                    if (present && (!vector.TryContainFlag((int)flag, out bool stillPresent) || !stillPresent)) return;
                    if (!ReferenceEquals(client.Player, this) || !ReferenceEquals(BitVector, vector) ||
                        !ReferenceEquals(View, view) || !ReferenceEquals(view.Owner, client) || UID != uid ||
                        (change is null ? !message.HasPreparedStorage() :
                            !change.TryCommit(vector, this, client, view, uid, status, message))) return;

                    // All fallible preparation is complete. Cooperating potion writers use
                    // these same gates; raw legacy readers/writers are not tuple-atomic.
                    if (defense) { OnDefensePotionStamp = deadline; OnDefensePotion = true; }
                    else { OnAttackPotionStamp = deadline; OnAttackPotion = true; }
                    committed = true;
                    status?.Publish(this, client, view, uid);
                    if (ReferenceEquals(client.Player, this) && ReferenceEquals(View, view) &&
                        ReferenceEquals(view.Owner, client) && UID == uid) message.Publish(client);
                }
                catch (Exception error)
                {
                    ReportFlagAdmission(committed ? "potion publication after commit" : "potion preparation", error);
                }
                finally { status?.Dispose(); message?.Dispose(); }
            }
        }
    }
}
