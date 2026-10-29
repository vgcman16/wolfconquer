using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    // Issued only by this Store's actual CAS pipeline or checked no-change
    // admission. No caller supplies a saver, validator or acceptance callback.
    internal sealed partial class MagicCostOutputReceipt
    {
        private readonly WorldSessionStore store;
        private readonly ConquerTcpConnection connection;
        private readonly object identity;
        private readonly LegacyOrdinarySpellBinding? binding;
        private readonly long mana;
        private readonly bool hasStamina;
        private readonly string? stamina;
        private readonly ushort authorization;
        private readonly (ushort Id, bool Present, DateTimeOffset Value)[] cooldowns;
        internal WorldSession Published { get; }
        private bool HasFailureOwner => binding is not null || store.framePersistenceOwner is not null;

        internal MagicCostOutputReceipt(WorldSessionStore store, ConquerTcpConnection connection,
            object identity, LegacyOrdinarySpellBinding? binding, WorldSession before, WorldSession published)
        {
            this.store = store; this.connection = connection; this.identity = identity; this.binding = binding;
            Published = published; mana = published.Character.MinMana; authorization = published.LegacyRandomSpell;
            hasStamina = published.Character.Attributes.TryGetValue("Stamina", out var rawStamina);
            stamina = rawStamina;
            var changed = new List<(ushort, bool, DateTimeOffset)>();
            if (published.MagicCooldowns is { } after)
                foreach (var pair in after)
                    if (before.MagicCooldowns is not { } previous ||
                        !previous.TryGetValue(pair.Key, out var value) || value != pair.Value)
                        changed.Add((pair.Key, true, pair.Value));
            if (before.MagicCooldowns is { } original)
                foreach (var pair in original)
                    if (published.MagicCooldowns is not { } afterValues || !afterValues.ContainsKey(pair.Key))
                        changed.Add((pair.Key, false, default));
            cooldowns = changed.ToArray();
        }

        internal async ValueTask<WorldSession?> SendAsync(IReadOnlyList<byte[]> frames, CancellationToken cancellationToken)
        {
            CostFields[] fields;
            try { fields = DescribeFrames(frames); }
            catch (Exception error) when (HasFailureOwner) { throw Failure(error); }
            var pending = CostFields.None;
            foreach (var field in fields) pending |= field;
            for (var index = 0; index < frames.Count; index++)
            {
                if (!TryRefresh(pending, out _)) return null;
                try { await connection.SendAsync(frames[index], cancellationToken).ConfigureAwait(false); }
                catch (Exception error) when (HasFailureOwner) { throw Failure(error); }
                // That field's reply has completed. A later independent tick
                // must not invalidate an unrelated still-pending resource reply.
                // This is not proof of ordering inside the completed Send.
                pending &= ~fields[index];
                if (!TryRefresh(pending, out _)) return null;
            }
            return TryRefresh(CostFields.None, out var current) ? current : null;
        }

        private bool TryRefresh(CostFields pending, out WorldSession current)
        {
            try { if (TryReadCurrent(pending, out current)) return true; }
            catch (Exception error) when (HasFailureOwner) { throw Failure(error); }
            current = null!;
            if (HasFailureOwner)
            {
                var failure = Failure(new InvalidOperationException("committedMagicCostOutputStateChangedOrOwnerLost"));
                if (failure.FencingFailure is not null) throw failure;
            }
            return false;
        }

        private bool TryReadCurrent(CostFields pending, out WorldSession current)
        {
            current = null!;
            using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(store.MailboxPackages,
                store.PeekMailboxSession(connection));
            if (!store._connectionGates.TryGetValue(connection, out var gate)) return false;
            lock (gate)
            {
                using var scope = EnterNativeConnectionGate();
                if (!store._sessions.TryGetValue(connection, out var holder) ||
                    !ReferenceEquals(holder.NativeClientIdentity, identity) || holder.Read() is not { } observed ||
                    observed.AccountId != Published.AccountId || observed.Character.Uid != Published.Character.Uid ||
                    !ReferenceEquals(holder.SpellBinding, binding)) return false;
                if (binding is not null
                    ? !store.BindingIsCurrentPlain(binding, holder, observed)
                    : connection.NativeGameClient?.OrdinarySpellBinding is not null ||
                      connection.NativeGameClient?.Player?.HasOwnedVitalsAttachment == true) return false;
                if (observed.LegacyRandomSpell != authorization ||
                    (pending & CostFields.Mana) != 0 && observed.Character.MinMana != mana) return false;
                if ((pending & CostFields.Stamina) != 0 &&
                    (observed.Character.Attributes.TryGetValue("Stamina", out var raw) != hasStamina ||
                     !string.Equals(raw, stamina, StringComparison.Ordinal))) return false;
                foreach (var entry in cooldowns)
                {
                    DateTimeOffset value = default;
                    var present = observed.MagicCooldowns is { } values && values.TryGetValue(entry.Id, out value);
                    if (present != entry.Present || present && value != entry.Value) return false;
                }
                current = observed;
                return true;
            }
        }

        private WorldSessionPublicationException Failure(Exception cause)
        {
            var failure = new WorldSessionPublicationException(store, connection, identity, binding, Published,
                WorldSessionPublicationStage.MagicCostOutput, cause);
            store.FenceObserverFailure(failure);
            return failure;
        }
    }
}
