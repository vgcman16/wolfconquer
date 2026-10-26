using ProtoBuf;
using ProtoBuf.Meta;
using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal partial class LegacyNativeCombatGearTable
{
    private sealed record MailboxWireContract(Type Type, string[] Names, int[]? Tags = null)
    {
        internal int Tag(int index) => Tags is null ? index + 1 : Tags[index];
    }

    private static readonly MailboxWireContract[] MailboxWireContracts =
    [
        new(typeof(LegacyNativeMsgCombatGear), ["Type", "UID", "Items"]),
        new(typeof(LegacyNativeMsgCombatGear.Item), ["ID", "Level", "Exp", "Equip", "Transform", "Data1", "Data2", "Data3", "Data4", "Data5", "Data6", "MasteryScore"]),
        new(typeof(LegacyNativeMsgCombatGearOpt), ["Type", "ItemID", "Transform", "UID", "Exp", "U", "SelectMaterial"]),
        new(typeof(LegacyNativeMsgCombatGearOpt.Items), ["UID", "Size"]),
        new(typeof(LegacyNativeMsgCombatGearSkin), ["Type", "Weapon"]),
        new(typeof(LegacyNativeMsgCombatGearSkin.WeaponInfo), ["ID", "Type", "Data1", "Data2", "Data3", "Data4", "Data5", "Data6", "Data7"]),
        // Literal source insertion order: tags8/9/10 precede tag7.
        new(typeof(LegacyNativeMsgDaoQi), ["Type", "ItemID", "TaoistReiki", "LOTTERY", "Data5", "Data6", "Data8", "PirateExp", "Data10", "MasteryPoints", "Position", "Done", "Items", "Positions"],
            [1, 2, 3, 4, 5, 6, 8, 9, 10, 7, 11, 12, 13, 14]),
        new(typeof(LegacyNativeMsgDaoQi.Item), ["Id"]),
        new(typeof(LegacyNativeMsgAirPowerUp), ["Type", "UserId", "PowerType", "Powers"]),
        new(typeof(LegacyNativeMsgAirPowerUp.Power), ["Id", "SenseLevel", "SenseAipPower", "ArmedLevel", "ArmedAipPower", "OverLoadLevel", "OverLoadAipPower", "Points"]),
        new(typeof(LegacyNativeMsgDictLottery), ["Type", "Done", "Items2"]),
        new(typeof(LegacyNativeMsgDictLottery.Lottery), ["Type", "Exp"]),
    ];
    private static MetaType[]? mailboxOwnedWireContracts;

    // Exact idempotent registration. A conflicting/frozen incomplete model is a
    // real refusal; no duplicate fields or blanket model reset can bless it.
    private static void RegisterMessages()
    {
        lock (MailboxLoadGate)
        {
            if (mailboxOwnedWireContracts is not null)
            {
                if (!MailboxContractsCurrent()) throw new InvalidDataException("Owned native gear wire schema changed.");
                return;
            }
            // Factory/surrogate configuration has no public observation API in
            // the pinned model. Foreign known owners cannot be blessed by field
            // names alone. Unrelated registered types are deliberately allowed.
            if (MailboxWireContracts.Any(contract => FindMailboxMeta(contract.Type) is not null) ||
                MailboxWireEnumTypes.Any(type => FindMailboxMeta(type) is not null))
                throw new InvalidDataException("Foreign native gear wire schema has unproved configuration.");
            var enums = RegisterMailboxWireEnums();
            var owned = new List<MetaType>();
            foreach (var contract in MailboxWireContracts)
            {
                var owner = RuntimeTypeModel.Default.Add(contract.Type, applyDefaultBehaviour: false);
                owned.Add(owner);
                for (var index = 0; index < contract.Names.Length; index++)
                    owner.AddField(contract.Tag(index), contract.Names[index]).IsRequired = true;
            }
            foreach (var contract in MailboxWireContracts) ValidateMailboxContract(contract, requireComplete: true);
            // Actual serializer preparation freezes all twelve message owners;
            // no private flag or inferred "ready" marker stands in for this.
            ProbeMailboxSerializer();
            foreach (var contract in MailboxWireContracts) ValidateMailboxContract(contract, requireComplete: true);
            if (!MailboxWireEnumsCurrent(enums)) throw new InvalidDataException("Native gear wire enum options changed.");
            mailboxOwnedWireEnums = enums;
            mailboxOwnedWireContracts = owned.ToArray();
        }
    }

    private static MetaType? FindMailboxMeta(Type type) => RuntimeTypeModel.Default.GetTypes().Cast<MetaType>()
        .SingleOrDefault(owner => owner.Type == type);

    private static void ValidateMailboxContract(MailboxWireContract contract, bool requireComplete)
    {
        var owner = FindMailboxMeta(contract.Type);
        if (owner is null)
        {
            if (requireComplete) throw new InvalidDataException("Native gear wire contract is absent.");
            return;
        }
        var fields = owner.GetFields();
        if (owner.BaseType is not null || owner.HasCallbacks || owner.HasSubtypes || owner.AsReferenceDefault ||
            !owner.UseConstructor || owner.ConstructType is not null || owner.IgnoreListHandling)
            throw new InvalidDataException("Native gear wire owner options conflict with the source schema.");
        var seen = new HashSet<int>();
        foreach (var field in fields)
        {
            var index = Enumerable.Range(0, contract.Names.Length).FirstOrDefault(index => contract.Tag(index) == field.FieldNumber, -1);
            if (index < 0 || !seen.Add(field.FieldNumber) || field.Member.Name != contract.Names[index] || !field.IsRequired ||
                field.IsPacked || field.DataFormat != DataFormat.Default || field.AsReference || field.DynamicType ||
                field.IsStrict || field.OverwriteList || field.SupportNull)
                throw new InvalidDataException("Native gear wire contract conflicts with the exact schema.");
        }
        if (requireComplete && fields.Length != contract.Names.Length)
            throw new InvalidDataException("Native gear wire contract is incomplete.");
    }

    private static bool MailboxContractsCurrent()
    {
        try
        {
            if (mailboxOwnedWireContracts is not { Length: 12 } || !MailboxWireEnumsCurrent(mailboxOwnedWireEnums)) return false;
            for (var index = 0; index < MailboxWireContracts.Length; index++)
            {
                if (!ReferenceEquals(mailboxOwnedWireContracts[index], FindMailboxMeta(MailboxWireContracts[index].Type))) return false;
                ValidateMailboxContract(MailboxWireContracts[index], requireComplete: true);
            }
            return true;
        }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException or InvalidDataException) { return false; }
    }

    private static void ProbeMailboxSerializer()
    {
        foreach (var contract in MailboxWireContracts)
        {
            object value = Activator.CreateInstance(contract.Type) ?? throw new InvalidDataException("Native gear wire value is unavailable.");
            using var output = new MemoryStream();
            Serializer.Serialize((Stream)output, value); // Actual pinned statically-object serializer, detached bytes only.
            if (output.Length == 0) throw new InvalidDataException("Required native gear fields were not serialized.");
        }
        object points = new LegacyNativeMsgAirPowerUp
        {
            Type = LegacyNativeMsgAirPowerUp.Types.ADDPOINTS, UserId = 1,
            Powers = [new LegacyNativeMsgAirPowerUp.Power { Id = 7, Points = 1 }],
        };
        using var probe = new MemoryStream();
        Serializer.Serialize((Stream)probe, points);
        if (!probe.ToArray().AsSpan().SequenceEqual(Convert.FromHexString("080510011800221008071000180020002800300038004001")))
            throw new InvalidDataException("Native Pirate points serializer differs from the required wire.");
    }
}
