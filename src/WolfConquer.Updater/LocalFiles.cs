using System.Text.Json;
using System.Text.Json.Serialization;

namespace WolfConquer.Updater;

internal static class LocalFiles
{
    internal static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true, MaxDepth = 16
    };
    internal static T Read<T>(string path, int maximum) where T : class => Parse<T>(Bytes(path, maximum));

    internal static T Parse<T>(ReadOnlySpan<byte> bytes) where T : class
    {
        var reader = new Utf8JsonReader(bytes, new JsonReaderOptions { MaxDepth = 16 });
        var fields = new Stack<HashSet<string>>();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.StartObject) fields.Push(new(StringComparer.Ordinal));
            else if (reader.TokenType == JsonTokenType.EndObject) fields.Pop();
            else if (reader.TokenType == JsonTokenType.PropertyName && !fields.Peek().Add(reader.GetString()!))
                throw new InvalidDataException("Duplicate local configuration property.");
        }
        return JsonSerializer.Deserialize<T>(bytes, Json) ?? throw new InvalidDataException("Empty local configuration.");
    }
    internal static byte[] Bytes(string path, int maximum)
    {
        Check(path);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > maximum) throw new InvalidDataException("Local configuration/cache exceeds its bound.");
        var bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        return bytes;
    }
    internal static void Check(string path)
    {
        for (string? current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current))
        {
            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Reparse points are not allowed in updater paths.");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
    }
    internal static void AtomicWrite(string destination, byte[] bytes)
    {
        Check(destination);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        Check(destination);
        var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.WriteThrough))
        { stream.Write(bytes); stream.Flush(true); }
        File.Move(temporary, destination, overwrite: true);
    }
}
