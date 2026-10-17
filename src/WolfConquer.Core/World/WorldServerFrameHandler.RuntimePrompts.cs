using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private const string RuntimePendingMessageBoxAttribute = LegacyRuntimeMessageBoxPrompt.PendingAttribute;
    private const string RuntimePendingMessageBoxMapIdAttribute = LegacyRuntimeMessageBoxPrompt.DestinationMapIdAttribute;
    private const string RuntimePendingMessageBoxXAttribute = LegacyRuntimeMessageBoxPrompt.DestinationXAttribute;
    private const string RuntimePendingMessageBoxYAttribute = LegacyRuntimeMessageBoxPrompt.DestinationYAttribute;
    private const string RuntimePendingMessageBoxDynamicIdAttribute = LegacyRuntimeMessageBoxPrompt.DestinationDynamicIdAttribute;
    private const string RuntimePendingMessageBoxSecondsAttribute = LegacyRuntimeMessageBoxPrompt.TimeoutSecondsAttribute;

    private async ValueTask SendRuntimeMessageBoxPromptAsync(
        ConquerTcpConnection connection,
        LegacyRuntimeMessageBoxPrompt prompt,
        CancellationToken cancellationToken)
    {
        if (!_sessions.TryGet(connection, out var session) || session is null)
        {
            await _log.WriteLineAsync(
                    $"WorldServer skipped runtime message box for {connection.RemoteEndPoint}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        var character = WithRuntimeMessageBoxPrompt(session.Character, prompt);
        _sessions.Set(connection, session with { Character = character });

        await connection.SendAsync(
                LegacyNpcReplyPacket.ForMessageBox(prompt.Text).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await _log.WriteLineAsync(
                "WorldServer sent runtime message box " +
                $"uid={character.Uid} key={prompt.PendingKey} map={prompt.MapId} x={prompt.X} y={prompt.Y}.")
            .ConfigureAwait(false);
    }

    private async ValueTask<int> SendRuntimeUpdateToActiveSessionsAsync(
        IReadOnlyList<WorldActiveSession> activeSessions,
        LegacyRuntimeUpdate update,
        CancellationToken cancellationToken)
    {
        var sentCount = 0;
        var promptedCount = 0;
        foreach (var active in activeSessions)
        {
            await active.Connection.SendAsync(update.FrameBytes, cancellationToken).ConfigureAwait(false);
            sentCount++;

            if (update.MessageBoxPrompt is not { } prompt)
            {
                continue;
            }

            var session = active.Session;
            if (_sessions.TryGet(active.Connection, out var currentSession) &&
                currentSession is not null)
            {
                session = currentSession;
            }

            var promptedSession = session with
            {
                Character = WithRuntimeMessageBoxPrompt(session.Character, prompt),
            };
            _sessions.Set(active.Connection, promptedSession);

            await active.Connection.SendAsync(
                    LegacyNpcReplyPacket.ForMessageBox(prompt.Text).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            promptedCount++;
        }

        return update.MessageBoxPrompt is null ? sentCount : promptedCount;
    }

    private static CharacterSnapshot WithRuntimeMessageBoxPrompt(
        CharacterSnapshot character,
        LegacyRuntimeMessageBoxPrompt prompt)
    {
        var attributes = new Dictionary<string, string>(
            character.Attributes,
            StringComparer.OrdinalIgnoreCase)
        {
            [RuntimePendingMessageBoxAttribute] = prompt.PendingKey,
            [RuntimePendingMessageBoxMapIdAttribute] = prompt.MapId.ToString(CultureInfo.InvariantCulture),
            [RuntimePendingMessageBoxXAttribute] = prompt.X.ToString(CultureInfo.InvariantCulture),
            [RuntimePendingMessageBoxYAttribute] = prompt.Y.ToString(CultureInfo.InvariantCulture),
            [RuntimePendingMessageBoxDynamicIdAttribute] = prompt.DynamicId.ToString(CultureInfo.InvariantCulture),
            [RuntimePendingMessageBoxSecondsAttribute] = prompt.Seconds.ToString(CultureInfo.InvariantCulture),
        };

        return character with { Attributes = attributes };
    }

    private static bool IsLegacyDeathRevivePromptVisible(CharacterSnapshot character)
    {
        return character.Attributes.TryGetValue(
                LegacyPlayerDeathState.RevivePromptVisibleAttribute,
                out var raw) &&
            !string.IsNullOrWhiteSpace(raw) &&
            !string.Equals(raw, "0", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(raw, "false", StringComparison.OrdinalIgnoreCase);
    }

    private static CharacterSnapshot WithLegacyDeathRevivePrompt(CharacterSnapshot character)
    {
        var attributes = new Dictionary<string, string>(
            character.Attributes,
            StringComparer.OrdinalIgnoreCase)
        {
            [LegacyPlayerDeathState.RevivePromptVisibleAttribute] = "1",
            [RuntimePendingMessageBoxAttribute] = LegacyPlayerDeathState.RevivePromptPendingKey,
        };
        attributes.Remove(RuntimePendingMessageBoxMapIdAttribute);
        attributes.Remove(RuntimePendingMessageBoxXAttribute);
        attributes.Remove(RuntimePendingMessageBoxYAttribute);
        attributes.Remove(RuntimePendingMessageBoxDynamicIdAttribute);
        attributes.Remove(RuntimePendingMessageBoxSecondsAttribute);
        return character with { Attributes = attributes };
    }

    private static CharacterSnapshot WithoutLegacyDeathRevivePrompt(CharacterSnapshot character)
    {
        var attributes = new Dictionary<string, string>(
            character.Attributes,
            StringComparer.OrdinalIgnoreCase);
        attributes.Remove(LegacyPlayerDeathState.RevivePromptVisibleAttribute);
        if (attributes.TryGetValue(RuntimePendingMessageBoxAttribute, out var pending) &&
            string.Equals(pending, LegacyPlayerDeathState.RevivePromptPendingKey, StringComparison.Ordinal))
        {
            attributes.Remove(RuntimePendingMessageBoxAttribute);
            attributes.Remove(RuntimePendingMessageBoxMapIdAttribute);
            attributes.Remove(RuntimePendingMessageBoxXAttribute);
            attributes.Remove(RuntimePendingMessageBoxYAttribute);
            attributes.Remove(RuntimePendingMessageBoxDynamicIdAttribute);
            attributes.Remove(RuntimePendingMessageBoxSecondsAttribute);
        }

        return character with { Attributes = attributes };
    }
}
