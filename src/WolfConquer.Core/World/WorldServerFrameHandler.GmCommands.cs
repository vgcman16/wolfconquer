using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private async ValueTask<bool> TryHandleGmCommandAsync(ConquerTcpConnection connection, PacketFrame frame,
        CancellationToken cancellationToken)
    {
        if (_protocolProfile != ConquerProtocolProfile.VisibleDx8D13 ||
            frame.Header.Opcode != WorldPacketOpcodes.LegacyMsgMessage ||
            !LegacyMsgMessagePacket.TryParse(frame, out var packet)) return false;
        var parsed = LegacyGmCommandParser.Parse(packet.Content[3]);
        if (parsed.Status == LegacyGmCommandParseStatus.NotReserved) return false;
        RegisterTradeDisconnect(connection, cancellationToken);
        if (_gmPolicy is null || _ownedAdmissionRuntime is not { } runtime ||
            packet.ChatType != LegacyMsgMessagePacket.TalkChatType ||
            parsed.Status != LegacyGmCommandParseStatus.Parsed || parsed.Command is not { } command)
        {
            await SendGmRefusalAsync(connection, cancellationToken).ConfigureAwait(false);
            return true;
        }

        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, runtime.ShutdownToken);
        using var worker = runtime.EnterWorker();
        var actor = CaptureGmActor(connection);
        if (actor is null)
        {
            await SendGmRefusalAsync(connection, lifetime.Token).ConfigureAwait(false);
            return true;
        }
        string[]? lines = null;
        OperatorMailPlan? delivery = null;
        LegacyOperatorGearMailResult? mail = null;
        using (await _worldOperations.EnterExclusiveAsync(lifetime.Token).ConfigureAwait(false))
        lock (_tradeStates.SyncRoot)
        {
            lifetime.Token.ThrowIfCancellationRequested();
            if (GmActorCurrent(actor, requireOriginal: true, out _) &&
                (_mailboxPackages is null || LegacyMailboxPacketAdmission.TryRequireCurrent(
                    _mailboxPackages, _sessions, connection, frame, _protocolProfile, out _)) &&
                GmActorCurrent(actor, requireOriginal: true, out _))
            {
                switch (command.Kind)
                {
                    case LegacyGmCommandKind.Help:
                        lines = GmHelpLines(actor.Expected);
                        break;
                    case LegacyGmCommandKind.Inspect when GmAllows(actor.Expected, LegacyGmCapability.InspectSelf):
                        var request = new LegacyRuntimeInspectionRequest(1, Guid.NewGuid(), actor.Routes.ExpectedDomain,
                            checked((uint)actor.Expected.AccountId), checked((uint)actor.Expected.Character.Uid));
                        var inspection = InspectOwnedRuntimeUnderLease(runtime, request, actor.Routes, lifetime.Token);
                        if (inspection.RuntimeObserved && inspection.Snapshot is { } snapshot &&
                            GmActorCurrent(actor, requireOriginal: true, out _))
                        {
                            lines = CreateGmInspectionLines(command.Inspection!.Value, snapshot, actor.Expected);
                            if (!GmActorCurrent(actor, requireOriginal: true, out _)) lines = null;
                        }
                        break;
                    case LegacyGmCommandKind.Kit when GmAllows(actor.Expected, LegacyGmCapability.MailTestKitSelf):
                        if (_mailboxes is null || _snapshotLoader is null || _worldData is null) break;
                        var kit = LegacyOperatorTestKits.Create(command.OperationId!.Value, actor.Routes.ExpectedDomain,
                            checked((uint)actor.Expected.AccountId), checked((uint)actor.Expected.Character.Uid), command.KitName!);
                        if (!TryPrepareOperatorMailRequest(kit, out var prepared, out _)) break;
                        lock (_mailboxes.SyncRoot)
                        {
                            if (!GmActorCurrent(actor, requireOriginal: true, out _)) break;
                            mail = PrepareAndCommitOperatorMail(runtime, prepared!, lifetime.Token, out delivery, actor);
                        }
                        break;
                }
            }
        }
        if (mail is not null)
        {
            if (delivery is not null)
                mail = await NotifyOperatorGearMailAsync(runtime, delivery, lifetime.Token).ConfigureAwait(false);
            lines = GmMailOutcomeLines(mail);
        }
        if (lines is null) await SendGmRefusalAsync(connection, lifetime.Token).ConfigureAwait(false);
        else await SendGmLinesAsync(actor, lines, lifetime.Token).ConfigureAwait(false);
        return true;
    }

    private string[] GmHelpLines(WorldSession session)
    {
        var lines = new List<string> { "WolfConquer GM test tools: self only; explicit server policy and current native owner required." };
        if (GmAllows(session, LegacyGmCapability.InspectSelf))
        {
            lines.Add("@wolf inspect [overview|position|vitals|inventory|equipment|spells|proficiencies|quests|mail]");
            lines.Add("Inspection is a bounded diagnostic; it does not certify saved-file equality or complete native parity.");
        }
        if (GmAllows(session, LegacyGmCapability.MailTestKitSelf))
        {
            lines.Add("@wolf kit <basic-trojan|socket-materials|consumables> <operationGuidN>");
            lines.Add("Gear arrives in the real mailbox. Retain one nonzero 32-hex operation ID per request and reuse it after uncertain transport.");
        }
        return lines.ToArray();
    }
}
