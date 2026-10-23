namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    internal void AddSpellFlag(LegacyNativeMsgUpdate.Flags Flag, int Secounds, bool RemoveOnDead, int StampSecounds = 0)
        => TryAdmitFlag(Flag, Secounds, RemoveOnDead, StampSecounds, 0, 0, replace: true);

    internal bool AddFlag(LegacyNativeMsgUpdate.Flags Flag, int Secounds, bool RemoveOnDead,
        int StampSecounds = 0, uint showamount = 0, uint amount = 0)
        => TryAdmitFlag(Flag, Secounds, RemoveOnDead, StampSecounds, showamount, amount, replace: false);

    private bool TryAdmitFlag(LegacyNativeMsgUpdate.Flags flag, int seconds, bool removeOnDeath,
        int invoker, uint shown, uint amount, bool replace)
    {
        var client = Owner;
        lock (client.SpellAdmissionSyncRoot)
        lock (SpellAdmissionSyncRoot)
        {
            var vector = BitVector;
            var view = View;
            uint uid = UID;
            if (vector is null || view is null || !ReferenceEquals(client.Player, this) ||
                !ReferenceEquals(view.Owner, client)) return false;
            lock (vector.SyncRoot)
            {
                LegacyNativePreparedFlagPublication? publication = null;
                bool committed = false;
                try
                {
                    // Duplicate admission occurs before optional Rune lookup, timestamps,
                    // pool access, XP resets or any published registration mutation.
                    if (!vector.TryPrepareAdd((int)flag, seconds, removeOnDeath, invoker, replace, out var change)) return false;
                    bool xp = IsXpAdmissionFlag(flag);
                    byte booster = 0;
                    if (xp) client.Rune?.IsEquipped("XPBooster", ref booster);
                    if (!vector.TryGetPreparedWords(change, out var preparedWords)) return false;
                    publication = LegacyNativePreparedFlagPublication.Create(uid, preparedWords,
                        flag, seconds, shown, amount);
                    if (!ReferenceEquals(client.Player, this) || !ReferenceEquals(BitVector, vector) ||
                        !ReferenceEquals(View, view) || !ReferenceEquals(view.Owner, client) || UID != uid ||
                        !change!.TryCommit(vector, this, client, view, uid, publication)) return false;

                    // Non-throwing physical commit continuation under all three gates.
                    committed = true;
                    if (xp) { XPBooster = booster; XPKiller = 0; }
                    publication.Publish(this, client, view, uid);
                    return true;
                }
                catch (Exception error)
                {
                    ReportFlagAdmission(committed ? "publication after commit" : "preparation", error);
                    return committed;
                }
                finally { publication?.Dispose(); }
            }
        }
    }

    private static bool IsXpAdmissionFlag(LegacyNativeMsgUpdate.Flags flag) => flag is
        LegacyNativeMsgUpdate.Flags.Superman or LegacyNativeMsgUpdate.Flags.Cyclone or
        LegacyNativeMsgUpdate.Flags.ShurikenVortex or LegacyNativeMsgUpdate.Flags.FatalStrike or
        LegacyNativeMsgUpdate.Flags.ChaintBolt or LegacyNativeMsgUpdate.Flags.Oblivion or
        LegacyNativeMsgUpdate.Flags.CannonBarrage or LegacyNativeMsgUpdate.Flags.BlackbeardsRage or
        LegacyNativeMsgUpdate.Flags.BladeFlurry or LegacyNativeMsgUpdate.Flags.SuperCyclone or
        LegacyNativeMsgUpdate.Flags.DragonCyclone or LegacyNativeMsgUpdate.Flags.Omnipotence or
        LegacyNativeMsgUpdate.Flags.ThunderRampage;

    internal static void ReportFlagAdmission(string phase, Exception error)
    {
        try { Console.WriteLine($"Flag admission {phase}: {error.GetType().Name}"); }
        catch { /* Diagnostics cannot change committed state or refusal classification. */ }
    }
}
