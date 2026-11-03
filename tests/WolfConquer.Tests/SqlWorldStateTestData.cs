using System.Globalization;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Data.SqlWorld;

namespace WolfConquer.Tests;

// Invented complete images. A repository image is not authenticated gameplay authority.
internal static class SqlWorldStateTestData
{
    internal const uint FirstAccount = 501, SecondAccount = 502;
    internal const uint FirstCharacter = 9001, SecondCharacter = 9002;
    internal const uint SashUid = 71001, ChildUid = 71002, FirstKept = 71003, OfferedUid = 72001, SecondKept = 72002;

    internal static byte[] Character(uint uid, long money = 1000, long cp = 100) =>
        Encoding.UTF8.GetBytes(string.Join("\r\n", new[]
        {
            "[Character]", "UID=" + uid.ToString(CultureInfo.InvariantCulture), "Name=SqlStateSynthetic",
            "Body=1003", "Face=0", "Haire=0", "Class=10", "FirstClass=10", "SecoundeClass=0", "Reborn=0",
            "Level=120", "Experience=0", "Map=1002", "X=430", "Y=380", "PMap=1002", "PMapX=430", "PMapY=380",
            "Strength=0", "Agility=0", "Vitaliti=0", "Spirit=0", "Atributes=0", "MinHitPoints=1", "MinMana=0",
            "Money=" + money.ToString(CultureInfo.InvariantCulture), "ConquerPoints=" + cp.ToString(CultureInfo.InvariantCulture),
            "BoundConquerPoints=3", "BankCps=7", "WHMoney=5", "PkPoints=0", "Online=1", "[Unrelated]", "Sentinel=owned-sql-state", ""
        }));

    internal static PlayerItemSnapshotItem Item(uint uid, uint id = 1088001) => new()
    { Uid = uid, ItemId = id, StackSize = 1, Durability = 10, MaximumDurability = 10, Color = 5 };

    internal static PlayerItemSnapshot FirstItems() => PlayerItemSnapshot.Empty with
    {
        InventoryItems = [Item(SashUid, 1100009).WithDepositedItems([Item(ChildUid)]), Item(FirstKept)]
    };

    internal static PlayerItemSnapshot SecondItems() => PlayerItemSnapshot.Empty with
    { InventoryItems = [Item(OfferedUid), Item(SecondKept)] };

    internal static (SqlWorldStateImage First, SqlWorldStateImage Second) Initial() =>
        (Image(FirstAccount, FirstCharacter, FirstItems()), Image(SecondAccount, SecondCharacter, SecondItems(), 2000, 200));

    internal static (SqlWorldStateImage First, SqlWorldStateImage Second) Exchanged()
    {
        var first = FirstItems(); var second = SecondItems();
        var sash = Assert.Single(first.InventoryItems, row => row.Uid == SashUid);
        var offered = Assert.Single(second.InventoryItems, row => row.Uid == OfferedUid);
        return (Image(FirstAccount, FirstCharacter, first with
        { InventoryItems = first.InventoryItems.Where(row => row.Uid != SashUid).Append(offered).ToArray() }, 1025, 103),
            Image(SecondAccount, SecondCharacter, second with
            { InventoryItems = second.InventoryItems.Where(row => row.Uid != OfferedUid).Append(sash).ToArray() }, 1975, 197));
    }

    internal static SqlWorldStateImage Image(long account, uint uid, PlayerItemSnapshot? items = null, long money = 1000, long cp = 100)
    {
        Assert.True(SqlWorldStateImage.TryCreate(account, uid, Character(uid, money, cp),
            PlayerItemSnapshotLoader.ToLegacyBytes(items ?? PlayerItemSnapshot.Empty), out var image, out var failure), failure.ToString());
        Assert.Equal(SqlWorldStateFailure.None, failure);
        return Assert.IsType<SqlWorldStateImage>(image);
    }

    internal static SqlWorldStateRequest Request(SqlWorldStateOperationKind kind, Guid id, params SqlWorldStateChange[] changes)
    {
        Assert.True(SqlWorldStateRequest.TryCreate(id, kind, changes, out var request, out var failure), failure.ToString());
        Assert.Equal(SqlWorldStateFailure.None, failure);
        return Assert.IsType<SqlWorldStateRequest>(request);
    }
}
