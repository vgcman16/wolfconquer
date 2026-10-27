using System.Buffers.Binary;
using System.Text;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeNinja
{
    private sealed partial class PreparedNinjaSequence : IDisposable
    {
        private readonly List<PacketGroup> groups = new();
        private readonly List<(PacketGroup Group, byte[] Frame)> outputs = new();

        internal PreparedNinjaSequence(LoadingInput input, LegacyNativeSpellStorage.PreparedNinjaSpells? spells, bool messages)
        {
            if (!messages && !input.BadgeChanges && (spells is null || spells.Added.Count + spells.Removed.Count == 0)) return;
            var seal = LegacyNativePreparedPacketFrames.CaptureSeal();
            if (spells is not null)
            {
                var frames = spells.Added.Select(row => LegacyNativeSpellPacket.PrepareSpellFrame(row, seal))
                    .Concat(spells.Removed.Select(id => LegacyNativeActionPacket.PrepareSpellRemovalFrame(input.Uid, id, seal))).ToArray();
                AddGroup(frames);
            }
            if (input.BadgeChanges) AddGroup([LegacyNativeMsgUpdate.PrepareNinjaBadgeFrame(input.Uid, input.Badge, seal)]);
            if (!messages) return;
            AddGroup([LegacyNativeMsgNinja.PrepareNinjaUserFrame(input.UserMessage, seal)]);
            var itemFrame = LegacyNativeMsgNinja.PrepareNinjaItemFrame(input.ItemMessage);
            var declared = BinaryPrimitives.ReadUInt16LittleEndian(itemFrame);
            byte[] sealBytes = seal is null ? [] : Encoding.UTF8.GetBytes(seal).AsSpan(0, 8).ToArray();
            if (itemFrame.Length != declared + sealBytes.Length || itemFrame.Length > 1032 ||
                !itemFrame.AsSpan(declared).SequenceEqual(sealBytes))
                throw new InvalidDataException("Ninja full item frame exceeds its send envelope or captured seal.");
            AddGroup([itemFrame]);
        }

        private void AddGroup(byte[][] frames)
        {
            if (frames.Length == 0) return;
            var group = new PacketGroup(frames.Max(frame => frame.Length), frames[0]);
            groups.Add(group);
            foreach (var frame in frames) outputs.Add((group, frame));
        }

        internal void RentStorage()
        {
            var seen = new HashSet<LegacyNativePacket>(ReferenceEqualityComparer.Instance);
            try
            {
                foreach (var group in groups)
                {
                    group.Rent();
                    if (!seen.Add(group.Packet)) throw UnavailableRanking();
                }
                // Every real rental/capacity/alias check precedes the first copy.
                foreach (var group in groups) group.Initialize();
            }
            catch { Dispose(); throw; }
        }

        internal void RequireStorage()
        {
            foreach (var group in groups) group.RequireCurrent();
        }

        internal void Publish(LoadingInput input, LegacyNativeSpellStorage.PreparedNinjaSpells? spells, bool badgeApplied)
        {
            try
            {
                foreach (var output in outputs)
                {
                    if (!input.HasCommitted(badgeApplied) || spells is not null && !spells.HasCurrentCommittedGraph()) return;
                    output.Group.Write(output.Frame);
                    if (!input.HasCommitted(badgeApplied) || spells is not null && !spells.HasCurrentCommittedGraph()) return;
                    input.PublishPacket(output.Group.Packet);
                }
            }
            catch (Exception error) { LegacyNativePlayerRuntime.ReportFlagAdmission("Committed Ninja sequence publication", error); }
        }

        public void Dispose()
        {
            for (var index = groups.Count - 1; index >= 0; index--) groups[index].Dispose();
        }
    }
}
