using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Core.Client;

public sealed partial class Chinese7937EchoRuntimeCatalog
{
    /// <summary>Prepares observed operation6 request data and its exact catalog quote; does not grant activation.</summary>
    public bool TryPrepareActivationRequest(uint uiSelection, out Chinese7937EchoActivationRequest? request)
    {
        request = null;
        if (uiSelection >= Requirements.CatalogMaximumHoleNumber) return false;
        var holeNumber = checked(uiSelection + 1);
        if (!Requirements.TryGetCost(holeNumber, out var cost) || !TryFindItem(cost.ItemTypeField, out var item)) return false;
        request = new(cost, item);
        return true;
    }
}

/// <summary>Outbound request data plus raw catalog fields. This object is not an operation permit or response.</summary>
public sealed class Chinese7937EchoActivationRequest
{
    internal Chinese7937EchoActivationRequest(Chinese7937EchoCostRow cost, Chinese7937ItemCatalogReference item)
    { Cost = cost; Item = item; }

    public Chinese7937EchoCostRow Cost { get; }
    public Chinese7937ItemCatalogReference Item { get; }

    public Chinese7935XuanbaoOperationPayload CreatePayload() => new(6, new(Field4: Cost.HoleNumber));

    public byte[] ToFrameBytes()
    {
        var payload = CreatePayload().ToPayloadBytes();
        var writer = new PacketWriter(Chinese7935XuanbaoOperationPayload.Opcode, payload.Length);
        payload.CopyTo(writer.Payload);
        return writer.ToArray();
    }
}
