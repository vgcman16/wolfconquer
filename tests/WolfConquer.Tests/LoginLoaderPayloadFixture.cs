using System.Diagnostics;
using WolfConquer.Core.Web;

namespace WolfConquer.Tests;

// Authored test data, independent of any launcher or captured response.
internal sealed class LoginLoaderPayloadFixture : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "wolf-loader-fixture-" + Guid.NewGuid());
    private readonly Dictionary<string, string?> _previous = new();
    private readonly Dictionary<string, string> _files = new();

    public LoginLoaderPayloadFixture(bool configureCurrentProcess = true)
    {
        Directory.CreateDirectory(_root);
        foreach (var route in LoginLoaderHttpResponder.Routes)
        {
            var path = Path.Combine(_root, route.EnvironmentVariable + ".fixture");
            File.WriteAllBytes(path, PayloadFor(route.Path));
            _files.Add(route.EnvironmentVariable, path);
            if (configureCurrentProcess)
            {
                _previous.Add(route.EnvironmentVariable, Environment.GetEnvironmentVariable(route.EnvironmentVariable));
                Environment.SetEnvironmentVariable(route.EnvironmentVariable, path);
            }
        }

        ExpectedVersionFile = Path.Combine(_root, "expected-version.fixture");
        ExpectedManifestFile = Path.Combine(_root, "expected-manifest.fixture");
        File.WriteAllBytes(ExpectedVersionFile, Version);
        File.WriteAllBytes(ExpectedManifestFile, Manifest);
    }

    public static byte[] Version => "fixture-version-v2\n"u8.ToArray();
    public static byte[] Manifest => [0x00, 0xff, 0x80, 0x41, 0x00, 0x0d, 0x0a];
    public string ExpectedVersionFile { get; }
    public string ExpectedManifestFile { get; }

    public static byte[] PayloadFor(string path)
    {
        if (path == LoginLoaderHttpResponder.VersionPath)
        {
            return Version;
        }

        if (path == LoginLoaderHttpResponder.ManifestPath)
        {
            return Manifest;
        }

        return System.Text.Encoding.UTF8.GetBytes("authored fixture for " + path + "\n");
    }

    public string FileFor(string environmentVariable) => _files[environmentVariable];

    public void ConfigureProcess(Process process)
    {
        foreach (var entry in _files)
        {
            process.StartInfo.Environment[entry.Key] = entry.Value;
        }
    }

    public void ConfigureReadinessProcess(Process process)
    {
        process.StartInfo.Environment[LoginWebLoaderReadinessOptions.ExpectedVersionFileEnvironmentVariable] = ExpectedVersionFile;
        process.StartInfo.Environment[LoginWebLoaderReadinessOptions.ExpectedManifestFileEnvironmentVariable] = ExpectedManifestFile;
    }

    public void Dispose()
    {
        foreach (var entry in _previous)
        {
            Environment.SetEnvironmentVariable(entry.Key, entry.Value);
        }

        Directory.Delete(_root, recursive: true);
    }
}

[CollectionDefinition("Login loader configuration", DisableParallelization = true)]
public sealed class LoginLoaderConfigurationCollection
{
    public const string Name = "Login loader configuration";
}
