using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyPreparedSpellFileWrite
{
    private WorldPacketHandlers.OwnedFullAccountClassSpellRetirement? preparedClassRetirement, committedClassRetirement;

    internal void PrepareClassRetirementImage(PlayerSpellSnapshot proposed, WorldPacketHandlers.OwnedFullAccountClassSpellRetirement retirement)
    {
        retirement.RequireImage(this, expected, proposed);
        PrepareImageCore(proposed, retirement);
    }

    internal bool HasCommittedClassRetirement(WorldPacketHandlers.OwnedFullAccountClassSpellRetirement retirement)
    {
        if (!committed || !ReferenceEquals(committedClassRetirement, retirement) || Recovery.IsBlocked ||
            binding is not null && !ReferenceEquals(binding.SavedSpells, prepared)) return false;
        try
        {
            return SnapshotFileLockCoordinator.ExecuteLocked(new[] { CharacterPath, SpellsPath }, () =>
            {
                var changes = retirement.CopyCapturedChanges();
                return retirement.IsCurrentPlain() && !LegacyTradeJournal.HasPending(UsersRoot) &&
                    CharacterSnapshotFileBaseline.TryReadBytes(CharacterPath, "fullAccountClassRetirement", out var character, out _) &&
                    character.AsSpan().SequenceEqual(changes[0].After) && ReadImage(SpellsPath) is { } spells &&
                    spells.AsSpan().SequenceEqual(changes[1].After) && prepared!.Matches(spells);
            });
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or
            NotSupportedException or FormatException or OverflowException or System.Security.SecurityException) { return false; }
    }
}
