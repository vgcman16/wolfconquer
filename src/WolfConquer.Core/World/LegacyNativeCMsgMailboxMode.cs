namespace WolfConquer.Core.World;

// Complete native byte enum prerequisite for Mail's primitive codecs only.
// Both whole native Process methods remain absent pending genuine inventory/warehouse/reward owners.
internal class LegacyNativeCMsgMailboxMode
{
    public enum Type : byte
    {
        CMsgMailContent = 1,
        Delete,
        ClaimMoney,
        ClaimItem,
        ClaimRecordType,
        DeleteClaim,
        ClaimAll
    }
}
