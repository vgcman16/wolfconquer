namespace WolfConquer.Core.World;

// Request recipes only: no item UID allocation, account authority, save or send.
// Current seed provenance and item-name evidence are recorded in the kit spec.
public static class LegacyOperatorTestKits
{
    public static LegacyOperatorGearMailRequest Create(Guid operationId, Guid domain, uint accountId,
        uint characterUid, string kit)
    {
        if (operationId == Guid.Empty) throw new ArgumentException("A retained operation ID is required.", nameof(operationId));
        if (domain == Guid.Empty) throw new ArgumentException("An explicit configured domain is required.", nameof(domain));
        if (accountId == 0) throw new ArgumentOutOfRangeException(nameof(accountId));
        if (characterUid == 0) throw new ArgumentOutOfRangeException(nameof(characterUid));
        uint[] ids = kit switch
        {
            "basic-trojan" => [118003, 120003, 130003, 150003, 160013, 410003, 420003, 1088000, 1088001],
            "socket-materials" => [700011, 700031, 1200005, 1200006, 1088000, 1088001],
            _ => throw new ArgumentException("Unknown operator test kit.", nameof(kit))
        };
        var title = kit == "basic-trojan" ? "Basic Trojan test kit" : "Socket material test kit";
        return new(operationId, domain, accountId, characterUid, title, "WolfConquer Operator",
            "Disposable local test items. Claim through the mailbox. Request generation does not deliver items.",
            ids.Select(id => new LegacyOperatorGearMailItem(id, StackSize: 1)).ToArray());
    }
}
