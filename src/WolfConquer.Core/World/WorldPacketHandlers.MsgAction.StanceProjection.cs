using System.Globalization;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // A frozen projection, not an owner or permission. The real Store still
    // validates its actual Frame, current holder, physical player and lifetime.
    internal sealed class StanceProjection
    {
        private readonly LegacyActionState.NativeValueProof before;
        internal WorldSession Expected { get; }
        internal WorldSession Proposed { get; }
        internal uint Action { get; }
        internal bool RemovedActivePick { get; }
        internal uint Skin { get; }
        internal byte[] SkinFrame { get; }
        internal byte[] ActionFrame { get; }
        internal MsgActionPacket Reply { get; }
        internal byte[]? PickFrame { get; }

        private StanceProjection(WorldSession expected, MsgActionPacket request)
        {
            Expected = expected;
            before = new(expected.Character);
            Action = request.DwParam;
            var attributes = CopyCharacterAttributes(expected.Character.Attributes);
            RemovedActivePick = HasOwnedActivePick(attributes);
            LegacyCharacterAttributeMap.Set(attributes, "Action", Action.ToString(CultureInfo.InvariantCulture));
            if (RemovedActivePick) LegacyCharacterAttributeMap.Remove(attributes, "ActivePick");
            Proposed = expected with { Character = expected.Character with { Attributes = attributes } };
            uint uid = CheckedUInt32(expected.Character.Uid, nameof(expected.Character.Uid));
            Skin = LegacyDragonSkinState.FromSnapshot(expected.Character).EquippedSkin;
            SkinFrame = new LegacyDragonSkinPacket(LegacyDragonSkinType.Effect, uid, Skin, OK: false).ToFrameBytes();
            // The input and stored value remain full UInt32. Only the native
            // source-shaped echo value is projected to its low sixteen bits.
            Reply = request with { ObjId = uid, DwParam = unchecked((ushort)Action) };
            ActionFrame = Reply.ToFrameBytes();
            if (RemovedActivePick)
                PickFrame = new MsgActionPacket
                {
                    ObjId = uid, DwParam = LegacyActionPickDwParam,
                    Timestamp = unchecked((uint)Environment.TickCount), Type = MsgActionType.ActionPick,
                    Facing = 2, RemainTime = 0, Strings = ["Interrupted"],
                }.ToFrameBytes();
            if (!before.IsCurrentPlain()) throw new InvalidOperationException("stanceInputsChangedDuringPreparation");
        }

        private static bool HasOwnedActivePick(Dictionary<string, string> attributes)
        {
            if (!LegacyCharacterAttributeMap.TryRead(attributes, "ActivePick", out var raw)) return false;
            return raw.Equals("true", StringComparison.OrdinalIgnoreCase) || raw.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                ulong.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value != 0;
        }

        internal static StanceProjection Prepare(WorldSession expected, MsgActionPacket request) => new(expected, request);
        internal bool IsCurrentPlain() => before.IsCurrentPlain();
    }
}
