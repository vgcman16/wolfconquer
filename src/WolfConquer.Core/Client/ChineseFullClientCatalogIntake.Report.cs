using System.Globalization;
using System.Text;

namespace WolfConquer.Core.Client;

public static partial class ChineseFullClientCatalogIntake
{
    /// <summary>Metadata and supported row observations only; never exports proprietary row text.</summary>
    public static string WriteFindingsMarkdown(ChineseFullClientCatalogSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var output = new StringBuilder("# Selected full-client catalog findings\n\n");
        output.Append("Observed version: ").Append(snapshot.ClientVersion.ToString(CultureInfo.InvariantCulture))
            .Append("; version SHA256: ").Append(snapshot.VersionSha256).Append(".\n\n");
        output.Append("| Relative path | Captured bytes | SHA256 | Result | Item rows / fields | Item ID min..max |\n")
            .Append("| --- | ---: | --- | --- | --- | --- |\n");
        foreach (var row in snapshot.Findings)
        {
            output.Append('|').Append(row.RelativePath).Append('|').Append(row.Length.ToString(CultureInfo.InvariantCulture))
                .Append('|').Append(row.Sha256).Append('|').Append(row.State).Append('|');
            if (row.Items is { } items)
                output.Append(items.Rows.Count.ToString(CultureInfo.InvariantCulture)).Append(" / ")
                    .Append(ClientItemCatalogDocument.FieldCount.ToString(CultureInfo.InvariantCulture));
            else if (row.Chinese7937Items is { } chineseItems)
                output.Append(chineseItems.Rows.Count.ToString(CultureInfo.InvariantCulture)).Append(" / ")
                    .Append(Chinese7937ItemCatalogDocument.FieldCount.ToString(CultureInfo.InvariantCulture));
            else output.Append("Unknown");
            output.Append('|');
            if (row.Items is { } identities)
                output.Append(identities.Rows.Min(item => item.ItemId).ToString(CultureInfo.InvariantCulture)).Append("..")
                    .Append(identities.Rows.Max(item => item.ItemId).ToString(CultureInfo.InvariantCulture));
            else if (row.Chinese7937Items is { } chineseIdentities)
                output.Append(chineseIdentities.Rows.Min(item => item.ItemId).ToString(CultureInfo.InvariantCulture)).Append("..")
                    .Append(chineseIdentities.Rows.Max(item => item.ItemId).ToString(CultureInfo.InvariantCulture));
            else output.Append("Unknown");
            output.Append("|\n");
        }
        output.Append("\nKnown item results retain complete immutable byte documents; no server field mapping or encoding is inferred.\n")
            .Append("ParsedChinese7937ItemEnvelope means exact67-field/CRLF data only; unknown columns are opaque and historical65 text roles are not assumed.\n")
            .Append("Opaque Echo/configuration bytes are not decoded by item transforms.\n");
        if (snapshot.DecodedEchoEvidence is { } echo && snapshot.EchoEvidenceSource == ChineseFullClientEchoEvidenceSource.ExplicitKeyTransform)
            output.Append("Explicit-key Echo transform, captured encoded identity, decoded pin and complete schema verified: ")
                .Append(echo.Count.ToString(CultureInfo.InvariantCulture)).Append(" rows. Native key/cache ownership and activation remain unproved.\n");
        else if (snapshot.DecodedEchoEvidence is not null)
            output.Append("An explicitly pinned decoded Echo supplement parsed under its declared schema; raw derivation and native binding remain unproved.\n");
        output.Append("\nThese are selected saved-file observations. File/row counts and hashes are not gameplay completion, publisher attestation or runtime authority.\n")
            .Append("Open: current runtime/catalog ownership, Echo contributions/items/cost meanings/operations13-14, durable activation/save, profile startup, English rendering and actual client acceptance.\n")
            .Append("No game, Lua, executable, packet handler or runtime registration was executed or installed by this intake.\n");
        return output.ToString();
    }
}
