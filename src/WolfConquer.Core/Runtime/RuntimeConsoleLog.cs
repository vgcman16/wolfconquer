using System.Text;

namespace WolfConquer.Core.Runtime;

public static class RuntimeConsoleLog
{
    public const string LogDirectoryEnvironmentVariable = "WOLFCONQUER_LOG_DIR";
    public const string LogFileEnvironmentVariable = "WOLFCONQUER_LOG_FILE";

    private static readonly object SyncRoot = new();
    private static TextWriter? outputWriter;
    private static TextWriter? errorWriter;

    public static RuntimeConsoleLogPaths? ConfigureFromEnvironment(string role)
    {
        var paths = ResolveFromEnvironment(role);
        if (paths is null)
        {
            return null;
        }

        lock (SyncRoot)
        {
            outputWriter?.Dispose();
            errorWriter?.Dispose();

            var output = CreateWriter(paths.OutputPath);
            var error = CreateWriter(paths.ErrorPath);

            outputWriter = TextWriter.Synchronized(output);
            errorWriter = TextWriter.Synchronized(error);

            Console.SetOut(outputWriter);
            Console.SetError(errorWriter);
        }

        Console.Out.WriteLine($"Runtime log file: {paths.OutputPath}");
        Console.Error.WriteLine($"Runtime error log file: {paths.ErrorPath}");
        return paths;
    }

    public static RuntimeConsoleLogPaths? ResolveFromEnvironment(string role)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(role);

        var logFile = Environment.GetEnvironmentVariable(LogFileEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(logFile))
        {
            var outputPath = Path.GetFullPath(logFile);
            var directory = Path.GetDirectoryName(outputPath) ?? Directory.GetCurrentDirectory();
            var fileName = Path.GetFileNameWithoutExtension(outputPath);
            var extension = Path.GetExtension(outputPath);
            var errorPath = Path.Combine(
                directory,
                string.IsNullOrEmpty(extension)
                    ? fileName + ".err"
                    : fileName + ".err" + extension);
            return new RuntimeConsoleLogPaths(outputPath, Path.GetFullPath(errorPath));
        }

        var logDirectory = Environment.GetEnvironmentVariable(LogDirectoryEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(logDirectory))
        {
            return null;
        }

        var root = Path.GetFullPath(logDirectory);
        var safeRole = SanitizeFileName(role);
        return new RuntimeConsoleLogPaths(
            Path.Combine(root, safeRole + ".out.log"),
            Path.Combine(root, safeRole + ".err.log"));
    }

    private static StreamWriter CreateWriter(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        return new StreamWriter(
            new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
        {
            AutoFlush = true,
        };
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(invalid.Contains(character) ? '_' : character);
        }

        return builder.Length == 0 ? "WolfConquer" : builder.ToString();
    }
}

public sealed record RuntimeConsoleLogPaths(string OutputPath, string ErrorPath);
