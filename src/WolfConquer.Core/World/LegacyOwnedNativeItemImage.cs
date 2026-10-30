using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;
using Placement = WolfConquer.Core.World.LegacyNativeItemLoadOwners.Placement;

namespace WolfConquer.Core.World;

// Data/provenance preparation, never a Store/holder/UID admission issuer.
internal sealed partial class LegacyOwnedNativeItemImage
{
    internal WorldDataCatalog? Catalog => metadata.Catalog;
    internal ConquerProtocolProfile Profile => metadata.Profile;
    internal PlayerItemSnapshot FinalSnapshot { get; }
    internal IReadOnlyList<Node> Roots { get; }
    internal Placement[] Placements { get; }
    private readonly Node[] roots;
    private readonly Placement[] placementWitness;
    private readonly CatalogEvidence metadata;
    private readonly SnapshotProof canonical;

    private LegacyOwnedNativeItemImage(SnapshotProof canonical, CatalogEvidence metadata, Node[] roots, Placement[] placements)
    {
        this.canonical = canonical; this.metadata = metadata; this.roots = roots;
        FinalSnapshot = canonical.Source; Roots = Array.AsReadOnly(roots); Placements = placements;
        placementWitness = (Placement[])placements.Clone();
    }

    internal sealed class Preparation
    {
        private readonly SnapshotProof proposal;
        internal LegacyOwnedNativeItemImage Image { get; }
        internal LegacyOwnedNativeItemImage? BaseImage { get; }
        internal PlayerItemSnapshot SourceSnapshot => proposal.Source;
        internal PlayerItemSnapshot FinalSnapshot => Image.FinalSnapshot;
        internal IReadOnlyList<Node> Roots => Image.Roots;
        internal Placement[] Placements => Image.Placements;
        internal Preparation(SnapshotProof proposal, LegacyOwnedNativeItemImage image, LegacyOwnedNativeItemImage? previous)
        { this.proposal = proposal; Image = image; BaseImage = previous; }
        internal void RequireCurrent()
        {
            proposal.RequireCurrent(); Image.RequireCurrent();
            if (!IsCurrentPlain()) throw Invalid("source-before-writer item evidence changed");
        }
        internal bool IsCurrentPlain() => proposal.IsCurrentPlain() && Image.IsCurrentPlain();
    }

    internal static Preparation Prepare(WorldDataCatalog? catalog, PlayerItemSnapshot snapshot,
        ConquerProtocolProfile profile, LegacyOwnedNativeItemImage? previousImage = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        previousImage?.RequireCurrent();
        var proposal = new SnapshotProof(snapshot);
        var ids = new List<uint>();
        foreach (var group in proposal.Rows) foreach (var root in group)
        { ids.Add(root.Record.ITEM_ID); foreach (var child in root.Children) ids.Add(child.Record.ITEM_ID); }
        CatalogEvidence evidence;
        if (previousImage is not null && ReferenceEquals(previousImage.Catalog, catalog) && previousImage.Profile == profile &&
            previousImage.metadata.HasRequirements(ids)) evidence = previousImage.metadata;
        else evidence = ValidateItemIds(catalog, ids, profile);
        evidence.RequireCurrent();
        LegacyOwnedNativeItemImage image;
        if (previousImage is not null && ReferenceEquals(evidence, previousImage.metadata) && proposal.SameValue(previousImage.canonical))
            image = previousImage;
        else
        {
            var final = new SnapshotProof(proposal.Canonical);
            var nodes = new List<Node>(); var placements = new List<Placement>();
            for (int group = 0; group < final.Rows.Length; group++) foreach (var row in final.Rows[group])
            {
                Node? old = null;
                if (previousImage is not null) foreach (var candidate in previousImage.roots)
                    if (candidate.Record.UID == row.Record.UID) { old = candidate; break; }
                var node = Node.Create(row, evidence, old); nodes.Add(node); Route(group, node, placements);
            }
            image = new(final, evidence, nodes.ToArray(), placements.ToArray());
        }
        var prepared = new Preparation(proposal, image, previousImage);
        prepared.RequireCurrent();
        // A callback during current metadata preparation must not invalidate
        // the actual old graph between B's before/after owner checks.
        if (previousImage is not null && !previousImage.IsCurrentPlain()) throw Invalid("prior native image changed");
        return prepared;
    }

    internal void RequireCurrent()
    {
        canonical.RequireCurrent(); metadata.RequireCurrent();
        if (!IsCurrentPlain()) throw Invalid("published canonical/native item evidence changed");
    }
    internal bool IsCurrentPlain()
    {
        if (!canonical.IsCurrentPlain() || !metadata.IsCurrentPlain() || Placements.Length != placementWitness.Length) return false;
        for (int index = 0; index < Placements.Length; index++)
        {
            var actual = Placements[index]; var expected = placementWitness[index];
            if (!ReferenceEquals(actual.Item, expected.Item) || actual.Destination != expected.Destination || actual.WarehouseId != expected.WarehouseId) return false;
        }
        foreach (var root in roots) if (!root.IsCurrentPlain()) return false;
        return true;
    }
    private static InvalidDataException Invalid(string reason) => new("Invalid owned native item image: " + reason);
}
