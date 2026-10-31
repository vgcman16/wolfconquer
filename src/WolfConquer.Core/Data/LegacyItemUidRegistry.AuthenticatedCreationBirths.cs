using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal sealed partial class LegacyItemUidRegistry
{
    internal sealed partial class OwnedStartupCustody
    {
        // Raw-scan evidence only. It cannot allocate, publish or admit a player.
        internal sealed class AuthenticatedCreationBirths
        {
            private readonly OwnedStartupCustody custody;
            private readonly uint first;
            private readonly int count;
            internal AuthenticatedCreationBirths(OwnedStartupCustody custody, LegacyItemUidReservationReceipt receipt)
            { this.custody = custody; first = receipt.First; count = receipt.Count; }

            internal void RequireRoots(string items, string mailbox, string redeem, string claim)
            {
                custody.RequireCurrent();
                var configured = custody.Configuration;
                if (!PathComparer.Equals(items, configured.ItemsRoot) || !PathComparer.Equals(mailbox, configured.MailboxRoot) ||
                    !PathComparer.Equals(redeem, configured.RedeemFile) || !PathComparer.Equals(claim, configured.ClaimFile))
                    throw new InvalidDataException("Creation birth scan differs from its actual startup namespace.");
            }

            internal void RequireUnoccupiedPhysicalUid(uint uid)
            {
                if (uid >= first && (ulong)uid < (ulong)first + (uint)count)
                    throw new InvalidDataException("A physical input already owns a reserved startup creation UID.");
            }
        }

        internal AuthenticatedCreationBirths CaptureAuthenticatedCreationBirths(LegacyAuthenticatedOrdinaryCreationOperation operation)
        {
            RequireAuthenticatedCreationAllocation(operation);
            return new(this, operation.Allocation);
        }
    }
}
