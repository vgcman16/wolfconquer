using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class MsgItemUsageNonEquipmentItemUseTests
{
    private static void AssertRuneUntouchedSessionFields(WorldSession before, WorldSession after)
    {
        // Prepared transactions own frozen copies. Compare every untouched value,
        // while separate independent item/spell byte goldens prove the mutation.
        Assert.Equal(before with { Character = after.Character, PlayerItems = after.PlayerItems,
            PlayerSpells = after.PlayerSpells }, after);
        Assert.Equal(before.Character with { Attributes = after.Character.Attributes,
            PaidNobility = after.Character.PaidNobility }, after.Character);
        Assert.Equal(before.Character.Attributes.OrderBy(p => p.Key, StringComparer.Ordinal),
            after.Character.Attributes.OrderBy(p => p.Key, StringComparer.Ordinal));
        Assert.Equal(before.Character.PaidNobility.OrderBy(p => p.Key, StringComparer.Ordinal),
            after.Character.PaidNobility.OrderBy(p => p.Key, StringComparer.Ordinal));
    }
}
