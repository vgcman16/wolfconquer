using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeCanUseSpell
{
    private sealed class PreparedUpdates : IDisposable
    {
        private LegacyNativeRecycledPacket? stamina;
        private LegacyNativeRecycledPacket? mana;

        internal static PreparedUpdates Create(uint uid, AdmissionPlan plan)
        {
            var updates = new PreparedUpdates();
            try
            {
                if (plan.SendStamina)
                {
                    updates.stamina = new LegacyNativeRecycledPacket();
                    Serialize(updates.stamina.GetStream(), uid,
                        LegacyNativeMsgUpdate.DataType.Stamina, plan.Commit.Stamina);
                }
                if (plan.SendMana)
                {
                    updates.mana = new LegacyNativeRecycledPacket();
                    var packet = updates.mana.GetStream();
                    if (updates.stamina is not null && ReferenceEquals(packet, updates.stamina.GetStream()))
                        throw new InvalidOperationException("Spell update rentals must not alias the same packet.");
                    Serialize(packet, uid,
                        LegacyNativeMsgUpdate.DataType.Mana, plan.Commit.Mana);
                }
                return updates;
            }
            catch
            {
                updates.Dispose();
                throw;
            }
        }

        private static unsafe void Serialize(LegacyNativePacket packet, uint uid,
            LegacyNativeMsgUpdate.DataType type, ushort value)
        {
            // A single UID + one ushort scalar update is below64B even at maximum
            // varint widths. Include the actual optional seal before unsafe writes.
            if (packet is null || packet.Memory == null || packet.Capacity < 64 + LegacyNativePacket.SealSize)
                throw new InvalidOperationException("Spell update packet has no live, bounded storage.");
            var update = new LegacyNativeMsgUpdate(packet, uid);
            update.Append(packet, type, value);
            update.GetArray(packet); // Genuine typed native contract and opcode2176.
        }

        internal void Publish(LegacyNativeGameClientRuntime client)
        {
            // Publish independently: failure of the first update does not suppress
            // the second accepted-state update, and neither can reject the cast.
            PublishOne(client, stamina);
            PublishOne(client, mana);
        }

        private static void PublishOne(LegacyNativeGameClientRuntime client,
            LegacyNativeRecycledPacket? rental)
        {
            if (rental is null)
                return;
            try { client.Send(rental.GetStream()); }
            catch (Exception error) { Report("publication after commit", error); }
        }

        public void Dispose()
        {
            DisposeOne(ref stamina);
            DisposeOne(ref mana);
        }

        private static void DisposeOne(ref LegacyNativeRecycledPacket? rental)
        {
            var owned = rental;
            rental = null;
            if (owned is null)
                return;
            try { owned.Dispose(); }
            catch (Exception error) { Report("packet cleanup", error); }
        }
    }
}
