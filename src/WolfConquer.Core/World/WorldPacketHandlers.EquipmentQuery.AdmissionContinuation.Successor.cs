using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal sealed partial class PreparedAdmissionContinuationQuery
    {
        internal bool CoversPreQueryHolder(WorldSession expected) => ReferenceEquals(admitted, expected);

        internal static bool TryPrepareSuccessor(WorldPacketHandlerContext context, CompletedAdmissionContinuationQuery previous,
            out PreparedAdmissionContinuationQuery? plan) => previous.PrepareSuccessor(context, out plan);

        internal bool PrepareFollowingQuery(WorldPacketHandlerContext actual, CompletedAdmissionContinuationQuery previous,
            out PreparedAdmissionContinuationQuery? plan)
        {
            plan = null;
            if (startupOrdinal != 0 || !previous.TryCurrent(out var current) || !ReferenceEquals(current, completedSession) ||
                !ReferenceEquals(previous.Pending, pending) || !IsQueryCurrent(actual, current, current) ||
                !binding.Store.IsAdmissionContinuationQueryOwnerHeld(binding, current, actual)) return false;
            try
            {
                var following = new PreparedAdmissionContinuationQuery(actual, origin, current, previous);
                if (!following.IsCurrentPlain()) return false;
                plan = following; return true;
            }
            catch (Exception error) when (error is InvalidDataException or InvalidOperationException or ArgumentException or
                OverflowException or FormatException or IOException or UnauthorizedAccessException or NotSupportedException) { return false; }
        }

        private bool AdmissionSourceIsCurrent() => predecessor is null ? origin.IsCurrentPlain() :
            predecessor.TryCurrent(out var current) && ReferenceEquals(current, admitted);

        private string AdmissionCharacterPath => Path.Combine(binding.UsersRoot,
            binding.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");

        private byte[] CaptureAdmissionQueryImage()
        {
            if (predecessor is null) return origin.CopyAdmittedImage();
            return SnapshotFileLockCoordinator.ExecuteLocked(new[] { AdmissionCharacterPath }, () =>
            {
                if (!AdmissionSourceIsCurrent() ||
                    !CharacterSnapshotFileBaseline.TryReadBytes(AdmissionCharacterPath, "continuationStartupSuccessor", out var bytes, out _))
                    throw InvalidContinuationQueryMutation();
                var saved = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(bytes, out _));
                if (!LegacyCombatCharacterImageInputs.Matches(saved, admitted.Character)) throw InvalidContinuationQueryMutation();
                LegacyDerivedSpellOrigins.RequireUnchanged(saved, admitted.PlayerSpells, admitted.Character, admitted.PlayerSpells);
                return bytes.ToArray();
            });
        }

        private bool AdmissionImageIsCurrent() => SnapshotFileLockCoordinator.ExecuteLocked(new[] { AdmissionCharacterPath }, () =>
            CharacterSnapshotFileBaseline.TryReadBytes(AdmissionCharacterPath, "continuationStartupBeforeQuery", out var bytes, out _) &&
            bytes.AsSpan().SequenceEqual(admittedImage));
    }

    internal sealed partial class CompletedAdmissionContinuationQuery
    {
        private int successorPrepared;
        internal bool CoversPreQueryHolder(WorldSession expected) => owner.CoversPreQueryHolder(expected) && TryCurrent(out _);
        internal bool PrepareSuccessor(WorldPacketHandlerContext context, out PreparedAdmissionContinuationQuery? plan)
        {
            plan = null;
            if (Volatile.Read(ref successorPrepared) != 0 || !owner.PrepareFollowingQuery(context, this, out var prepared) ||
                prepared is null || Interlocked.CompareExchange(ref successorPrepared, 1, 0) != 0) return false;
            plan = prepared; return true;
        }
    }
}
