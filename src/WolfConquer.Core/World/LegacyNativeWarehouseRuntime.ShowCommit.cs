using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeWarehouseRuntime
{
    private bool IsCurrentShow(ShowPlan plan)
    {
        ShowContext context = plan.Context;
        return ShowOwnersAreCurrent(context) && ShowMembershipIsCurrent(context) &&
            plan.Outputs.All(output => output.IsUsable) && plan.Items.All(image => image.IsCurrent()) &&
            plan.Locks.All(itemLock => itemLock.IsCurrent()) &&
            (context.BankId == context.Player.UID ? context.Player.InventorySashCount : WHMaxSpace()) == context.Capacity;
    }

    private bool ShowOwnersAreCurrent(ShowContext context) =>
        ReferenceEquals(User, context.Client) && ReferenceEquals(context.Client.Player, context.Player) &&
        ReferenceEquals(context.Player.Owner, context.Client) && ReferenceEquals(context.Client.Warehouse, this) &&
        context.Player.UID == context.PlayerUid && ReferenceEquals(ClientItems, context.Banks) &&
        ReferenceEquals(IsShow, context.Shown) && context.Shown.SequenceEqual(context.ShownEntries);

    private static bool ShowMembershipIsCurrent(ShowContext context)
    {
        if (!context.Banks.TryGetValue(context.BankId, out var bank) || !ReferenceEquals(bank, context.Bank) ||
            bank.Count != context.Entries.Length) return false;
        foreach (var entry in context.Entries)
            if (!bank.TryGetValue(entry.Key, out var item) || !ReferenceEquals(item, entry.Value) ||
                item.Uid != entry.Key || item.WH_ID != context.BankId) return false;
        return true;
    }

    private void CommitShow(ShowPlan plan, LegacyNativePacket target)
    {
        if (!IsCurrentShow(plan)) throw new InvalidOperationException("Warehouse ownership changed before show commitment.");
        // Reserve any List allocation before publishing packet or model state.
        plan.Context.Shown.EnsureCapacity(checked(plan.Context.Shown.Count + 1));
        if (!IsCurrentShow(plan)) throw new InvalidOperationException("Warehouse ownership changed before output publication.");
        plan.Outputs[^1].PublishTo(target);
        // All ordinary validation/encoding/allocation has finished. Cooperating
        // Show/SendLocked calls hold actual owner/item monitors; public writes,
        // process crashes and post-commit delivery are outside this guarantee.
        foreach (var itemLock in plan.Locks) itemLock.Commit();
        foreach (var extra in plan.Extras)
        {
            extra.Item.Refinary = extra.Refinery;
            extra.Item.Purification = extra.Purification;
        }
        plan.Context.Shown.Add(plan.Context.BankId);
        foreach (var output in plan.Outputs) output.Send(plan.Context.Client);
    }
}
