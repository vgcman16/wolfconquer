namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private sealed class BasicPhysicalReadScope : IDisposable
    {
        private readonly LegacyNativeGameClientRuntime client;
        private readonly LegacyNativePlayerRuntime player;
        internal LegacyNativeStatusFlagsBigVector32 Vector { get; }
        private BasicPhysicalReadScope(LegacyOrdinarySpellBinding binding)
        {
            client = binding.Client; player = binding.Player;
            Monitor.Enter(client.SpellAdmissionSyncRoot);
            try
            {
                Monitor.Enter(player.SpellAdmissionSyncRoot);
                try
                {
                    Vector = player.BitVector ?? throw new InvalidOperationException("basicPhysicalFlagsMissing");
                    Monitor.Enter(Vector.SyncRoot);
                }
                catch { Monitor.Exit(player.SpellAdmissionSyncRoot); throw; }
            }
            catch { Monitor.Exit(client.SpellAdmissionSyncRoot); throw; }
        }
        internal static BasicPhysicalReadScope Enter(LegacyOrdinarySpellBinding binding) => new(binding);
        public void Dispose()
        {
            Monitor.Exit(Vector.SyncRoot);
            Monitor.Exit(player.SpellAdmissionSyncRoot);
            Monitor.Exit(client.SpellAdmissionSyncRoot);
        }
    }

    private static bool TryReadBasicPhysicalOrdinaryFlags(LegacyOrdinarySpellBinding binding, WorldSession expected,
        out LegacyNativeStatusFlagsBigVector32.StaminaFlags proof)
    {
        proof = null!;
        var vector = binding.Player.BitVector;
        if (!Monitor.IsEntered(binding.Client.SpellAdmissionSyncRoot) || !Monitor.IsEntered(binding.Player.SpellAdmissionSyncRoot) ||
            vector is null || !Monitor.IsEntered(vector.SyncRoot) || !vector.TryCaptureStaminaFlags(out var captured) || captured is null)
            return false;
        var words = LegacyWindWalkerStatusProjection.Read(expected.Character);
        foreach (int flag in new[] { (int)LegacyNativeMsgUpdate.Flags.Cyclone,
            (int)LegacyNativeMsgUpdate.Flags.FatalStrike, (int)LegacyNativeMsgUpdate.Flags.Dizzy })
        {
            // Applicable unjoined special/status branches cannot silently
            // receive the ordinary cadence. Neither a saved alias nor a bare
            // physical flag is a producer for their missing lifetime effects.
            if (captured.Has(flag) || (words[flag / 32] & (1UL << (flag % 32))) != 0) return false;
        }
        proof = captured;
        return proof.IsCurrentPlain();
    }
}
