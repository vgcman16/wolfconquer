using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    private bool TryRetireContinuationObserverFailure(WorldSessionPublicationException failure) =>
        ReferenceEquals(failure.Store, this) && failure.Binding is { } binding &&
        ReferenceEquals(failure.NativeIdentity, binding.NativeIdentity) &&
        continuationRetirements.TryGetValue(binding, out var slot) && slot.Latest is { } checkpoint &&
        ReferenceEquals(checkpoint.Session, failure.PublishedSession) &&
        TryRetireRecoverableContinuation(checkpoint.Context, binding, failure);

    // This method never clears a recovery block. It retires only a checkpoint's
    // exact actor after proving that a new admission can recover the saved award.
    private bool TryRetireRecoverableContinuation(WorldPacketHandlerContext context, LegacyOrdinarySpellBinding binding, Exception failure)
    {
        if (!ReferenceEquals(binding.Store, this) || framePersistenceOwner is not { } frame ||
            IsCurrentThreadInsideAnyConnectionGate || Monitor.IsEntered(binding.Client.SpellAdmissionSyncRoot) ||
            Monitor.IsEntered(binding.Player.SpellAdmissionSyncRoot)) return false;
        try
        {
            lock (frame.RequireActionLifecycleGate(this))
            {
                if (!continuationRetirements.TryGetValue(binding, out var slot) || slot.Latest is not { } checkpoint ||
                    !SameRetirementOperation(checkpoint.Context, context) ||
                    !IsContinuationCleanupLifecycleCurrent(frame, context, binding)) return false;
                // Only nested catches of this same propagated failure may
                // reuse its historical cleanup. A later distinct failure is
                // not proof that the images are still recoverable.
                if (checkpoint.RetiredFailure is { } retiredFailure) return ReferenceEquals(retiredFailure, failure);
                using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, checkpoint.Session);
                if (!_connectionGates.TryGetValue(binding.Connection, out var gate)) return false;
                lock (gate)
                {
                    using var native = EnterNativeConnectionGate();
                    using var actors = new SpellActorScope(binding);
                    if (!_sessions.TryGetValue(binding.Connection, out var holder) ||
                        !RetirementNativeCurrent(binding, holder, checkpoint)) return false;
                    var characterPath = RetirementCharacterPath(binding);
                    var spellsPath = RetirementSpellsPath(binding);
                    string[] paths = checkpoint.ItemsPath is { } itemsPath
                        ? [characterPath, spellsPath, itemsPath] : [characterPath, spellsPath];
                    return SnapshotFileLockCoordinator.ExecuteLocked(paths, () =>
                    {
                        if (!ReferenceEquals(slot.Latest, checkpoint) || !RetirementNativeCurrent(binding, holder, checkpoint) ||
                            !binding.Recovery.IsReadyForUsers(binding.UsersRoot) || !binding.Recovery.TryRequireLiveJournalClear(out _) ||
                            !CharacterSnapshotFileBaseline.TryReadBytes(characterPath, "experienceContinuationRetirement", out var image, out _) ||
                            !image.AsSpan().SequenceEqual(checkpoint.Character) ||
                            !Equals(checkpoint.Pending, LegacyExperienceContinuationCodec.ReadCharacterImage(image, binding.AccountId, checked((uint)binding.Uid))) ||
                            !checkpoint.Spells.Matches(ReadRetirementSidecar(spellsPath)) ||
                            checkpoint.ItemsPath is { } itemPath && !SameRetirementBytes(checkpoint.Items, ReadRetirementSidecar(itemPath)) ||
                            !RetirementNativeCurrent(binding, holder, checkpoint) ||
                            !IsContinuationCleanupLifecycleCurrent(frame, context, binding) ||
                            !binding.Recovery.TryRequireLiveJournalClear(out _)) return false;
                        holder.Clear();
                        checkpoint.RetiredFailure = failure;
                        return true;
                    });
                }
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or
            InvalidOperationException or ArgumentException or OverflowException or NotSupportedException or System.Security.SecurityException)
        { return false; }
    }

    private bool RetirementNativeCurrent(LegacyOrdinarySpellBinding binding, SessionHolder holder,
        ContinuationRetirementCheckpoint checkpoint)
    {
        var session = checkpoint.Session; var character = session.Character; var player = binding.Player;
        return BindingIsCurrentPlain(binding, holder, session) && ReferenceEquals(binding.SavedSpells, checkpoint.Spells) &&
            checkpoint.Attributes.IsCurrentPlain() && checkpoint.Paid.IsCurrentPlain() &&
            checkpoint.Scalars.IsCurrentPlain() && checkpoint.Action.IsCurrentPlain() &&
            player.Reborn == checkpoint.Scalars.Value.Reborn && player.X == checkpoint.Scalars.Value.X &&
            player.Y == checkpoint.Scalars.Value.Y && player.Map == checkpoint.Scalars.Value.Map &&
            player.DynamicID == checkpoint.Scalars.Value.DynamicID && (uint)player.Action == checkpoint.Action.Value &&
            player.Class == character.Class &&
            Equals(checkpoint.Pending, LegacyExperienceContinuationCodec.ReadCharacter(character, binding.AccountId)) &&
            player.Level == character.Level && character.Experience >= 0 && player.Experience == (ulong)character.Experience &&
            player.Strength == character.Strength && player.Agility == character.Agility && player.Vitality == character.Vitality &&
            player.Spirit == character.Spirit && player.Atributes == character.AttributePoints &&
            player.HitPoints == character.MinHitPoints && player.Mana == character.MinMana;
    }

    private static byte[]? ReadRetirementSidecar(string path)
    {
        var file = new FileInfo(Path.GetFullPath(path)); file.Refresh();
        if (file.Exists && (file.Attributes & FileAttributes.ReparsePoint) != 0) throw InvalidRetirementCheckpoint();
        for (var parent = file.Directory; parent is not null; parent = parent.Parent)
        { parent.Refresh(); if (parent.Exists && (parent.Attributes & FileAttributes.ReparsePoint) != 0) throw InvalidRetirementCheckpoint(); }
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > LegacyTradeJournal.DerivedSpellQueryMaximumReferenceBytes) throw InvalidRetirementCheckpoint();
            var bytes = new byte[checked((int)stream.Length)]; stream.ReadExactly(bytes);
            if (stream.ReadByte() != -1) throw InvalidRetirementCheckpoint();
            return bytes;
        }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }
    private static bool SameRetirementBytes(byte[]? first, byte[]? second) => first is null ? second is null :
        second is not null && first.AsSpan().SequenceEqual(second);
}
