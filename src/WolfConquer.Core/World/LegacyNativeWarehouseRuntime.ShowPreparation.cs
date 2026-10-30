using System.Collections.Concurrent;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeWarehouseRuntime
{
    private sealed record ShowContext(uint BankId, LegacyNativeMsgWarehouse.DepositActionID Action,
        LegacyNativeGameClientRuntime Client, LegacyNativePlayerRuntime Player,
        ConcurrentDictionary<uint, ConcurrentDictionary<uint, LegacyNativeGameItemRuntime>> Banks,
        ConcurrentDictionary<uint, LegacyNativeGameItemRuntime> Bank,
        KeyValuePair<uint, LegacyNativeGameItemRuntime>[] Entries, List<uint> Shown, uint[] ShownEntries, uint PlayerUid, int Capacity);

    private sealed record ExtraChange(LegacyNativeGameItemRuntime Item,
        LegacyNativeItemExtra.Refinery Refinery, LegacyNativeItemExtra.Purification Purification);

    private sealed class ShowPlan(ShowContext context, LegacyNativeItemPacketSnapshot[] items) : IDisposable
    {
        internal ShowContext Context { get; } = context;
        internal LegacyNativeItemPacketSnapshot[] Items { get; } = items;
        internal List<LegacyNativePreparedItemOutput> Outputs { get; } = [];
        internal List<LegacyNativeGameItemRuntime.LockedSendPlan> Locks { get; } = [];
        internal List<ExtraChange> Extras { get; } = [];
        public void Dispose()
        {
            foreach (var output in Outputs) output.Dispose();
            foreach (var itemLock in Locks) itemLock.Dispose(); // Native packet disposal is idempotent.
        }
    }

    private ShowContext CaptureShow(uint bankId, LegacyNativeMsgWarehouse.DepositActionID action,
        ConcurrentDictionary<uint, ConcurrentDictionary<uint, LegacyNativeGameItemRuntime>> banks,
        ConcurrentDictionary<uint, LegacyNativeGameItemRuntime> bank, List<uint> shown)
    {
        var client = User ?? throw new InvalidOperationException("Warehouse has no actual user owner.");
        var player = client.Player ?? throw new InvalidOperationException("Warehouse has no actual player owner.");
        if (!ReferenceEquals(player.Owner, client) || !ReferenceEquals(client.Warehouse, this))
            throw new InvalidOperationException("Warehouse user/player binding is foreign.");
        uint playerUid = player.UID;
        var entries = bank.ToArray();
        foreach (var entry in entries)
            if (entry.Value is null || entry.Key == 0 || entry.Key != entry.Value.Uid || entry.Value.WH_ID != bankId)
                throw new InvalidOperationException("Warehouse contains an invalid actual item membership.");
        return new(bankId, action, client, player, banks, bank, entries, shown, shown.ToArray(), playerUid,
            bankId == playerUid ? player.InventorySashCount : WHMaxSpace());
    }

    private static ShowPlan PrepareShow(ShowContext context, LegacyNativePacket target)
    {
        var images = context.Entries.Select(entry => new LegacyNativeItemPacketSnapshot(entry.Value)).ToArray();
        var plan = new ShowPlan(context, images);
        try
        {
            for (int first = 0; first < Math.Max(1, images.Length); first += 4)
                PrepareShowBatch(plan, images.Skip(first).Take(4).ToArray(), target);
            return plan;
        }
        catch { plan.Dispose(); throw; }
    }

    private static void PrepareShowBatch(ShowPlan plan, LegacyNativeItemPacketSnapshot[] images, LegacyNativePacket target)
    {
        ShowContext context = plan.Context;
        AddShowOutput(plan, LegacyNativePreparedItemOutput.Prepare(target.Capacity, prepared =>
        {
            prepared.WarehouseCreate(context.BankId, context.Action, 0, context.Capacity, images.Length);
            foreach (var image in images) prepared.AddCapturedWarehouse(image);
            prepared.FinalizeWarehouse();
        }));

        var extras = new LegacyNativeItemExtra();
        foreach (var image in images)
        {
            var refinery = image.Refinary;
            if (refinery.InLife)
            {
                refinery.Typ = refinery.EffectDuration == 0
                    ? LegacyNativeItemExtra.Typing.PermanentRefinery : LegacyNativeItemExtra.Typing.RefinaryAdding;
                extras.Refinerys.Add(refinery);
            }
            var purification = image.Purification;
            if (purification.InLife)
            {
                purification.Typ = LegacyNativeItemExtra.Typing.PurificationAdding;
                extras.Purifications.Add(purification);
            }
            plan.Extras.Add(new(image.Source, refinery, purification));
        }
        foreach (var image in images)
        {
            var itemLock = image.Source.PrepareLockedSend(context.Client, target, image);
            if (itemLock is null) continue;
            try
            {
                plan.Locks.Add(itemLock);
                AddShowOutput(plan, itemLock.Output);
            }
            catch { itemLock.Dispose(); throw; }
        }
        if (extras.Refinerys.Count != 0 || extras.Purifications.Count != 0)
            AddShowOutput(plan, LegacyNativePreparedItemOutput.Prepare(target.Capacity, prepared => extras.CreateArray(prepared)));
    }

    private static void AddShowOutput(ShowPlan plan, LegacyNativePreparedItemOutput output)
    {
        try { plan.Outputs.Add(output); }
        catch { output.Dispose(); throw; }
    }
}
