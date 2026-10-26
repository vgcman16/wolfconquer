using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    // Existing account/snapshot admission precedes this path; complete native
    // authentication is a separate open boundary. Pending work pins its four
    // before-images, so normal startup normalization and
    // persistence must wait for reconciliation rather than silently stranding it.
    private async ValueTask<bool> TryStartMailboxDebtRecoverySessionAsync(
        ConquerTcpConnection connection, long accountId, string username,
        CharacterSnapshot snapshot, LegacyNativeGameClientRuntime? nativeLoadedClient,
        CancellationToken cancellationToken, bool answerLegacyLogin = false)
    {
        if (_mailboxPackages is not { } producer) return false;
        if (!producer.HasPendingAccountWork(accountId, out var pending, out var reason))
        {
            await _log.WriteLineAsync($"WorldServer mailbox recovery startup refused: {reason}.").ConfigureAwait(false);
            return true;
        }
        if (!pending) return false;
        if (_snapshotLoader is not { } loader || _tradeRecovery.IsBlocked)
        {
            await _log.WriteLineAsync("WorldServer mailbox recovery startup requires its real snapshot loader and ready recovery owner.")
                .ConfigureAwait(false);
            return true;
        }
        LegacyNativePlayerRuntime.MailboxRecoveryLoad? coldLoad = null;
        if (connection.NativeGameClient is { } coldClient &&
            (coldClient.Player is not { } coldPlayer ||
             !coldPlayer.TryPrepareMailboxRecoveryLoad(coldClient, snapshot, out coldLoad)))
        {
            await _log.WriteLineAsync("WorldServer mailbox recovery requires a genuine coherent cold native load.")
                .ConfigureAwait(false);
            return true;
        }
        var candidate = new WorldSession(accountId, username, snapshot,
            loader.LoadPlayerItemsByCharacterUid(snapshot.Uid),
            loader.LoadPlayerSpellsByCharacterUid(snapshot.Uid),
            loader.LoadPlayerProficienciesByCharacterUid(snapshot.Uid),
            CombatGear: loader.LoadCombatGearByCharacterUid(snapshot.Uid), UsersRoot: loader.UsersRoot)
        { NativeGameClient = connection.NativeGameClient };
        if (!LegacySecondaryPasswordState.TryRead(candidate.Character.Attributes, out _, out reason) ||
            !LegacySecondaryPasswordPersistence.TryValidateBaseline(candidate, out reason))
        {
            await _log.WriteLineAsync($"WorldServer mailbox recovery saved PIN admission refused: {reason}.").ConfigureAwait(false);
            return true;
        }
        if (!_sessions.TrySetMailboxRecoveryOwner(connection, candidate, producer, out var admitted, out reason) || admitted is null)
        {
            await _log.WriteLineAsync($"WorldServer mailbox recovery startup owner refused: {reason}.").ConfigureAwait(false);
            return true;
        }
        if (coldLoad is not null && !coldLoad.TryPublish(_sessions, connection, admitted))
        {
            _sessions.TryRemove(connection, out _);
            await _log.WriteLineAsync("WorldServer mailbox recovery cold native publication refused before startup.")
                .ConfigureAwait(false);
            return true;
        }
        if (connection.NativeGameClient is { } native &&
            (!ReferenceEquals(admitted.NativeGameClient, native) || native.Player is not { } player ||
             !ReferenceEquals(player.Owner, native) || player.UID != snapshot.Uid || player.Class != snapshot.Class ||
             !snapshot.Attributes.TryGetValue("ClassExperience", out var rawExperience) ||
             !uint.TryParse(rawExperience, System.Globalization.NumberStyles.None,
                 System.Globalization.CultureInfo.InvariantCulture, out var experience) || player.ClassExperience != experience))
        {
            _sessions.TryRemove(connection, out _);
            await _log.WriteLineAsync("WorldServer mailbox recovery requires its coherent genuine loaded native Player before startup metadata.")
                .ConfigureAwait(false);
            return true;
        }
        if (!TryInitializeMailboxRecoveryPin(connection, admitted))
        {
            _sessions.TryRemove(connection, out _);
            return true;
        }
        if (answerLegacyLogin)
            await connection.SendAsync(LegacyMsgMessagePacket.AnswerOk().ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        await SendLegacyWorldEntryStartupAsync(connection, admitted.Character, admitted.PlayerItems,
            admitted.PlayerSpells, admitted.CombatGear!, cancellationToken, nativeLoadedClient,
            preserveMailboxRecoveryState: true).ConfigureAwait(false);
        // No normal startup callback, normalization, runtime loop or autosave is
        // started while a durable pending claim still owns these saved images.
        return true;
    }

    private bool TryInitializeMailboxRecoveryPin(ConquerTcpConnection connection, WorldSession session)
    {
        lock (LegacySecondaryPasswordRuntime.SyncRoot)
        {
            LegacySecondaryPasswordRuntime.Invalidate(connection, session);
            var reason = "secondaryPasswordRecoveryOwnerChanged";
            var initialized = false;
            _sessions.WithCurrentNativeClientIdentity(connection, session.Character.Uid, _ =>
            {
                if (!_sessions.TryGet(connection, out var current) || !ReferenceEquals(current, session) ||
                    !LegacySecondaryPasswordState.TryRead(session.Character.Attributes, out var saved, out reason) ||
                    !LegacySecondaryPasswordPersistence.TryValidateBaseline(session, out reason)) return;
                // Do not expire/reset the persisted PIN during recovery loading.
                initialized = connection.NativeGameClient is null && session.NativeGameClient is null && saved.Password == 0 ||
                    LegacySecondaryPasswordRuntime.TryInitialize(connection, session, saved);
            });
            if (!initialized)
                _log.WriteLine($"WorldServer mailbox recovery PIN retained fail-closed state: {reason}.");
            return initialized;
        }
    }
}
