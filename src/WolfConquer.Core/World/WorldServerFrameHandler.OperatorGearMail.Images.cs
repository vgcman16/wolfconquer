using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private sealed record OperatorMailBaseline(CharacterSnapshotFileBaseline.Snapshot Character,
        byte[] Items, LegacyItemSaveGraph Graph, byte[] Mailbox, LegacyMailboxStore.DeliveryCapture Mail);

    private bool TryReadOperatorMailBaseline(LegacyOwnedAdmissionRuntime runtime, OperatorMailOwner owner,
        out OperatorMailBaseline? baseline, out string reason)
    {
        baseline = null;
        reason = "operatorMailBaselineStale";
        if (!OperatorMailOwnerCurrent(runtime, owner, owner.Expected) ||
            !LegacyItemSaveGraph.TryCapture(owner.Expected.PlayerItems, out var graph) ||
            !CharacterSnapshotFileBaseline.TryRead(owner.CharacterPath, "operatorMail", out var character, out reason) ||
            !OperatorMailCharacterMatches(owner, character!) ||
            !CharacterSnapshotFileBaseline.TryReadBytes(owner.ItemsPath, "operatorMail", out var items, out reason) ||
            !graph.HasBytes(items) || !graph.IsCurrent(owner.Expected.PlayerItems)) return false;
        byte[]? mail;
        try
        {
            _ = File.GetAttributes(owner.MailPath);
            if (!CharacterSnapshotFileBaseline.TryReadBytes(owner.MailPath, "operatorMail", out var present, out reason)) return false;
            mail = present;
        }
        catch (FileNotFoundException) { mail = null; }
        if (!_mailboxes!.TryCaptureOperatorDelivery(checked((uint)owner.Expected.Character.Uid), mail, out var capture, out reason))
            return false;
        if (mail is null)
        {
            // v4 requires an existing before-image. This separately creates only
            // a genuine empty mailbox, before any UID reservation or reward.
            if (!capture.IsCurrent() || !OperatorMailOwnerCurrent(runtime, owner, owner.Expected)) return false;
            _mailboxes.Save(checked((uint)owner.Expected.Character.Uid));
            if (!CharacterSnapshotFileBaseline.TryReadBytes(owner.MailPath, "operatorMail", out mail, out reason) ||
                mail.Length != sizeof(int) || !mail.AsSpan().SequenceEqual(new byte[sizeof(int)]) ||
                !_mailboxes.TryCaptureOperatorDelivery(checked((uint)owner.Expected.Character.Uid), mail, out capture, out reason))
                return false;
        }
        if (LegacyTradeJournal.HasPending(owner.Routes.UsersRoot) || !capture.IsCurrent() ||
            !OperatorMailOwnerCurrent(runtime, owner, owner.Expected)) return false;
        baseline = new(character!, items, graph, mail, capture);
        reason = string.Empty;
        return true;
    }

    private static bool OperatorMailCharacterMatches(OperatorMailOwner owner, CharacterSnapshotFileBaseline.Snapshot file)
    {
        var text = CharacterSnapshotFileEncoding.Decode(file.Bytes, out var encoding);
        if (!encoding.GetPreamble().Concat(encoding.GetBytes(text)).SequenceEqual(file.Bytes)) return false;
        var parsed = CharacterSnapshotParser.ParseText(text, owner.Expected.Character.SourceFile);
        var current = owner.Expected.Character;
        return parsed.Uid == current.Uid && parsed.Money == current.Money &&
            parsed.ConquerPoints == current.ConquerPoints && parsed.BoundConquerPoints == current.BoundConquerPoints &&
            parsed.BankConquerPoints == current.BankConquerPoints &&
            LegacyTradeEscrowClaims.TryRead(parsed.Attributes, out var escrow, out _) && escrow is null &&
            LegacySecondaryPasswordState.TryRead(parsed.Attributes, out var pin, out _) && pin == owner.Pin &&
            parsed.Attributes.TryGetValue("SecurityPass", out var diskPin) ==
                current.Attributes.TryGetValue("SecurityPass", out var livePin) &&
            string.Equals(diskPin, livePin, StringComparison.Ordinal);
    }

    private static bool OperatorMailFilesMatch(OperatorMailOwner owner, byte[] character, byte[] items, byte[] mailbox) =>
        CharacterSnapshotFileBaseline.TryReadBytes(owner.CharacterPath, "operatorMail", out var c, out _) &&
        c.AsSpan().SequenceEqual(character) &&
        CharacterSnapshotFileBaseline.TryReadBytes(owner.ItemsPath, "operatorMail", out var i, out _) &&
        i.AsSpan().SequenceEqual(items) &&
        CharacterSnapshotFileBaseline.TryReadBytes(owner.MailPath, "operatorMail", out var m, out _) &&
        m.AsSpan().SequenceEqual(mailbox);
}
