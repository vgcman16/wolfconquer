using System.Buffers.Binary;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyAuthenticatedOrdinaryCreationTests
{
    internal sealed record PhysicalDeathBefore(Dictionary<string, byte[]> Files, Dictionary<string, byte[]> After,
        Dictionary<string, byte[]> History, byte[] Ledger, byte[] Payload, byte[] Newborn,
        WorldSession Actor, WorldSession Target, LegacyOrdinarySpellBinding ActorBinding, LegacyOrdinarySpellBinding TargetBinding,
        object ActorImage, object TargetImage, object ActorSpells, object TargetSpells, DateTime Stamp, ulong Maximum);

    internal sealed partial class PhysicalDeathFixture
    {
        internal async Task EquipAndAttackAsync(LegacyJournalInterruption? fault = null)
        {
            bool triggered = false, inside = false;
            Owner.BeforeSend = (account, bytes) =>
            {
                if (!triggered && account == DeathTargetAccount)
                {
                    triggered = true;
                    var armor = Assert.Single(TargetSession.PlayerItems.EquipmentItems);
                    Assert.Equal(DeathArmorUid, armor.Uid); Assert.Equal((ushort)3, armor.Position);
                    Assert.False(TargetSession.PlayerSpells.ContainsSpell(20000));
                    Assert.Equal(200L, TargetSession.Character.MinHitPoints);
                    Assert.False(LegacyTradeJournal.HasPending(Owner.Data.Users));
                    Before = CapturePhysicalDeathBefore(this);
                    // Equip has released Exclusive and all Store/native gates.
                    // This OTHER real connection enters normal Shared +the real
                    // ordered two-owner physical publisher before Equip's query.
                    Assert.False(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
                    inside = true; Owner.Handler.PhysicalDeathJournalInterruption = fault;
                    try { Owner.SendOnAsync(Actor, DeathAttackRequest()).GetAwaiter().GetResult(); }
                    finally { Owner.Handler.PhysicalDeathJournalInterruption = null; inside = false; }
                    return;
                }
                if (!inside) return;
                PhysicalFrames.Add((account, bytes.ToArray()));
                BeforePhysicalSend?.Invoke(account, bytes);
            };
            try
            {
                await Owner.SendOnAsync(Target,
                    new MsgItemUsagePacket(DeathArmorUid, 3, MsgItemUsageAction.Equip, 1234, 0, 0, 0).ToFrameBytes());
            }
            finally { Owner.BeforeSend = null; }
            Assert.True(triggered, Owner.Log.ToString()); Assert.NotNull(Before);
        }
    }

    private static PhysicalDeathBefore CapturePhysicalDeathBefore(PhysicalDeathFixture f)
    {
        var actor = f.ActorSession; var target = f.TargetSession;
        var own = actor.NativeGameClient!.OrdinarySpellBinding!; var peer = target.NativeGameClient!.OrdinarySpellBinding!;
        Assert.True(f.Owner.Store.IsBoundSpellOwnerCurrent(own)); Assert.True(f.Owner.Store.IsBoundSpellOwnerCurrent(peer));
        Assert.Equal((long)DeathTargetAccount, target.AccountId); Assert.Equal((long)DeathTargetUid, target.Character.Uid);
        Assert.Empty(actor.PlayerItems.EquipmentItems); // Profs phase is genuinely neutral.
        var files = f.Owner.Data.Files();
        var after = files.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
        string charKey = Path.GetRelativePath(f.Owner.Data.Root, f.CharacterPath);
        string spellKey = Path.GetRelativePath(f.Owner.Data.Root, f.SpellsPath);
        after[charKey] = DeathHpZero(files[charKey], f.Changed);
        var spells = files[spellKey]; Assert.Equal(16, spells.Length); Assert.Equal((byte)0xC7, spells[15]);
        if (f.Changed)
        {
            var changed = new byte[28]; spells.CopyTo(changed, 0);
            BinaryPrimitives.WriteInt32LittleEndian(changed, 2);
            BinaryPrimitives.WriteUInt16LittleEndian(changed.AsSpan(16), 20000);
            BinaryPrimitives.WriteUInt16LittleEndian(changed.AsSpan(18), 5);
            after[spellKey] = changed;
        }
        var completed = CompletedIntent(f.Owner);
        Assert.NotEmpty(HistoryFiles(f.Owner));
        Assert.Equal((99_300L, "700"), (f.Owner.Current.Character.Money, f.Owner.Current.Character.Attributes["WHMoney"]));
        return new(files, after, HistoryFiles(f.Owner), File.ReadAllBytes(f.Owner.Data.State), completed.CopyPayload(),
            File.ReadAllBytes(f.Owner.CharacterPath), actor, target, own, peer, own.Items.Image, peer.Items.Image,
            own.Storage.ClientSpells, peer.Storage.ClientSpells, own.Player.AttackStamp, f.Maximum);
    }

    private static byte[] DeathHpZero(byte[] image, bool changed)
    {
        string text = CharacterSnapshotFileEncoding.Decode(image, out var encoding);
        const string token = "\r\nMinHitPoints=200\r\n";
        Assert.Equal(2, text.Split(token, StringSplitOptions.None).Length);
        text = text.Replace(token, "\r\nMinHitPoints=0\r\n", StringComparison.Ordinal);
        const string originKey = "ManagedDerivedSpellOrigins=";
        Assert.Equal(-1, text.IndexOf(originKey, StringComparison.OrdinalIgnoreCase));
        if (changed)
        {
            // Explicit Root policy: the real fresh20000 grant owns exactly
            // this marker in the SAME22 Character write, never a seed/grant.
            const string header = "[Character]\r\n";
            int section = text.IndexOf(header, StringComparison.Ordinal);
            Assert.True(section >= 0);
            int next = text.IndexOf("\r\n[", section + header.Length, StringComparison.Ordinal);
            const string line = "ManagedDerivedSpellOrigins=20000:5\r\n";
            if (next >= 0) text = text.Insert(next + 2, line);
            else { Assert.EndsWith("\r\n", text); text += line; }
        }
        return encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray();
    }

    private static byte[] DeathAttackRequest() => new MsgAttackPacket(new(CreationFixture.RuneActor, DeathTargetUid,
        831, 584, MsgAttackType.Physical, 0, 0, 17, MsgAttackEffect.None, 19)).ToFrameBytes();

    internal sealed record PhysicalDeathCapture(PhysicalDeathBefore Before, JsonNode Record, string TransactionId);

    internal static async Task<PhysicalDeathCapture> CapturePhysicalDeathAsync(PhysicalDeathFixture f, int boundary, bool flushed = false)
    {
        using (var fault = flushed ? LegacyJournalInterruption.StopAfterStageFlushed(boundary) : LegacyJournalInterruption.StopAfter(boundary))
        {
            await f.EquipAndAttackAsync(fault);
            Assert.True(fault.PendingObserved, f.Owner.Log.ToString());
            Assert.Equal(flushed ? new[] { 0, boundary } : Enumerable.Range(0, boundary + 1), fault.Visited);
        }
        var before = Assert.IsType<PhysicalDeathBefore>(f.Before);
        Assert.True(f.Owner.Runtime.Recovery.IsBlocked); Assert.Empty(f.PhysicalFrames);
        Assert.False(f.Owner.Store.TryGet(f.Actor, out _)); Assert.False(f.Owner.Store.TryGet(f.Target, out _));
        Assert.True(before.ActorBinding.IsRetired); Assert.True(before.TargetBinding.IsRetired);
        Assert.Equal(200, before.TargetBinding.Player.HitPoints); Assert.Equal(before.Stamp, before.ActorBinding.Player.AttackStamp);
        Assert.False(before.ActorBinding.Client.OnAutoAttack);
        Assert.Same(before.ActorImage, before.ActorBinding.Items.Image); Assert.Same(before.TargetImage, before.TargetBinding.Items.Image);
        Assert.Same(before.ActorSpells, before.ActorBinding.Storage.ClientSpells); Assert.Same(before.TargetSpells, before.TargetBinding.Storage.ClientSpells);
        Assert.False(before.TargetBinding.Storage.ClientSpells.ContainsKey(20000));
        var record = JsonNode.Parse(File.ReadAllBytes(f.Owner.PendingPath))!;
        string transaction = record["TransactionId"]!.GetValue<string>();
        AssertPhysicalDeathRecord(f, before, record);
        var targets = new[] { f.CharacterPath, f.SpellsPath };
        // The real engine stages ALL targets before its replacement loop.
        // StageFlushed2 therefore retains BOTH stages with BOTH physical files
        // still at Before; it is not a one-replacement prefix.
        int completed = flushed ? 0 : boundary;
        for (int index = 0; index < targets.Length; index++)
        {
            string key = Path.GetRelativePath(f.Owner.Data.Root, targets[index]);
            Assert.Equal(index < completed ? before.After[key] : before.Files[key], File.ReadAllBytes(targets[index]));
        }
        var expectedKeys = before.Files.Keys.Append(Path.GetRelativePath(f.Owner.Data.Root, f.Owner.PendingPath));
        if (flushed)
        {
            foreach (string target in targets.Take(boundary))
            {
                string stage = target + ".managed-trade-" + transaction + ".tmp";
                Assert.Equal(before.After[Path.GetRelativePath(f.Owner.Data.Root, target)], File.ReadAllBytes(stage));
                expectedKeys = expectedKeys.Append(Path.GetRelativePath(f.Owner.Data.Root, stage));
            }
        }
        Assert.Equal(expectedKeys.Order(StringComparer.OrdinalIgnoreCase), f.Owner.Data.Files().Keys.Order(StringComparer.OrdinalIgnoreCase));
        foreach (var row in before.Files)
            if (!targets.Contains(Path.Combine(f.Owner.Data.Root, row.Key), StringComparer.OrdinalIgnoreCase))
                Assert.Equal(row.Value, File.ReadAllBytes(Path.Combine(f.Owner.Data.Root, row.Key)));
        AssertDeathHistoryUnchanged(f, before);
        return new(before, record, transaction);
    }
}
