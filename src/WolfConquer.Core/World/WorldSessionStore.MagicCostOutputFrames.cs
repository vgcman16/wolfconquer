using System.Buffers;
using System.Globalization;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal sealed partial class MagicCostOutputReceipt
    {
        [Flags]
        private enum CostFields { None = 0, Stamina = 1, Mana = 2 }

        private CostFields[] DescribeFrames(IReadOnlyList<byte[]> frames)
        {
            if (frames.Count > 3) throw InvalidCostFrame();
            var result = new CostFields[frames.Count];
            var seen = CostFields.None;
            var seenCooldown = false;
            for (var index = 0; index < frames.Count; index++)
            {
                var bytes = frames[index];
                if (!PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _) ||
                    frame.Frame.Length != bytes.Length) throw InvalidCostFrame();
                var field = CostFields.None;
                if (LegacyMsgUpdatePacket.TryParse(frame, out var update))
                {
                    if (update.Uid != Published.Character.Uid || update.Updates.Count != 1) throw InvalidCostFrame();
                    var value = update.Updates[0];
                    if (value.Type == LegacyMsgUpdateDataType.Mana && mana >= 0 && value.Value == (ulong)mana)
                        field = CostFields.Mana;
                    else if (value.Type == LegacyMsgUpdateDataType.Stamina && hasStamina &&
                        ulong.TryParse(stamina, NumberStyles.Integer, CultureInfo.InvariantCulture, out var expected) && value.Value == expected)
                        field = CostFields.Stamina;
                    else throw InvalidCostFrame();
                    if ((seen & field) != 0) throw InvalidCostFrame();
                    seen |= field;
                }
                else if (LegacyMagicColdTimePacket.TryParse(frame, out var cold))
                {
                    if (seenCooldown || cold.Spells.Count != 1 ||
                        !cooldowns.Any(entry => entry.Present && entry.Id == cold.Spells[0].SpellId)) throw InvalidCostFrame();
                    seenCooldown = true;
                }
                else throw InvalidCostFrame();
                result[index] = field;
            }
            return result;
        }

        private static InvalidDataException InvalidCostFrame() => new("committedMagicCostFrameDoesNotMatchPublication");
    }
}
