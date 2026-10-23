using System.Security.Cryptography;
using System.Text.Json.Nodes;
using WolfConquer.Updates.ClientLayout;
using WolfConquer.Updates.Publishing;

namespace WolfConquer.Updates.Tests;

public sealed partial class UpdaterPipelineTests
{
    private const string LayoutPreset = "legacy-dx8-v1";
    private static string LayoutEngine(Fixture f) =>
        "Env_DX8/WolfPipelineLayout-" + Path.GetFileName(f.Root)["wolf-updater-pipeline-".Length..] + ".exe";
    private static readonly string[] LayoutAssets = ["ani", "c3", "data", "data2", "Font", "Help", "ini", "map", "Script", "sound", "video"];
    private static string LayoutAliasPath(Fixture f, string name) => Path.Combine(f.Client, "Env_DX8", name);
    private static string LayoutTargetPath(Fixture f, string name) => Path.Combine(f.Client, name);

    private static void PublishLayout(Fixture f, ECDsa key, string? aliasPayload = null)
    {
        var plan = new List<PublishEntry>();
        foreach (string name in LayoutAssets)
        {
            string path = name + "/asset.bin";
            Directory.CreateDirectory(Path.Combine(f.Source, name));
            File.WriteAllText(Path.Combine(f.Source, path), "owned " + name);
            plan.Add(new(path, null));
        }
        Directory.CreateDirectory(Path.Combine(f.Source, "Env_DX8"));
        File.WriteAllBytes(Path.Combine(f.Source, LayoutEngine(f)), f.Executable);
        plan.Add(new(LayoutEngine(f), null));
        if (aliasPayload is not null)
        {
            string path = Path.Combine(f.Source, aliasPayload);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "signed but locally forbidden alias payload");
            plan.Add(new(aliasPayload, null));
        }
        var now = DateTimeOffset.UtcNow;
        ReleasePublisher.Publish(new(f.Source, f.Published, "fixture-client", "preview", 1, f.Host.BaseAddress,
            now.AddMinutes(-1), now.AddHours(1), includeBundle: true, allowLoopbackHttp: true), plan, key, "pipeline-test-key");
    }

    private static string WriteLayoutConfig(Fixture f, ECDsa key, string? preset = LayoutPreset, string? cwd = "Env_DX8")
    {
        string config = f.WriteConfig(f.Client);
        var json = JsonNode.Parse(File.ReadAllBytes(config))!.AsObject();
        json["publicKeySpkiPem"] = key.ExportSubjectPublicKeyInfoPem();
        json["launchRelativePath"] = LayoutEngine(f);
        json["protectedExecutables"] = new JsonArray(LayoutEngine(f));
        if (preset is not null) json["clientAssetLayoutPreset"] = preset;
        if (cwd is not null) json["launchWorkingDirectoryRelativePath"] = cwd;
        File.WriteAllText(config, json.ToJsonString());
        return config;
    }

    private static void DeleteLayoutJunctions(Fixture f)
    {
        var failures = new List<Exception>();
        foreach (string name in LayoutAssets)
        {
            string alias = LayoutAliasPath(f, name);
            try
            {
                FileAttributes attributes;
                try { attributes = File.GetAttributes(alias); }
                catch (FileNotFoundException) { continue; }
                catch (DirectoryNotFoundException) { continue; }
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    ClientDirectoryJunction.DeleteExact(alias, LayoutTargetPath(f, name));
            }
            catch (Exception error)
            { failures.Add(new IOException("Exact owned junction cleanup failed for " + name + ".", error)); }
        }
        if (failures.Count != 0) throw new AggregateException("One or more exact owned junction removals failed.", failures);
    }

    private static async Task WithAssetLayoutFixture(Func<Fixture, Task> body)
    {
        var fixture = await Fixture.CreateAsync();
        var failures = new List<Exception>();
        try { await body(fixture); }
        catch (Exception error) { failures.Add(new InvalidOperationException("Asset-layout test body failed.", error)); }
        try { DeleteLayoutJunctions(fixture); }
        catch (Exception error) { failures.Add(new IOException("Asset-layout exact-link cleanup failed.", error)); }
        try { await fixture.DisposeAsync(); }
        catch (Exception error) { failures.Add(new IOException("Asset-layout fixture disposal failed.", error)); }
        if (failures.Count != 0)
            throw new AggregateException("Asset-layout test did not complete cleanly; all observed phase failures are retained.", failures);
    }
}
