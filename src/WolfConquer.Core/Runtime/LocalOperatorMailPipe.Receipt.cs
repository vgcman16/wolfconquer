namespace WolfConquer.Core.Runtime;

public sealed partial class LocalOperatorMailPipe
{
    public const int ResponseReceiptLength = 20;

    // Local operator transport revision2, not a gameplay/client packet. The
    // marker is followed by Guid.TryWriteBytes' ordinary little-endian layout.
    // Empty operation IDs also correlate malformed-request rejection receipts.
    public static byte[] CreateResponseReceipt(Guid operationId)
    {
        var receipt = new byte[ResponseReceiptLength];
        "WCM2"u8.CopyTo(receipt);
        if (!operationId.TryWriteBytes(receipt.AsSpan(4)))
            throw new InvalidOperationException("Operator response receipt could not be prepared.");
        return receipt;
    }
}
