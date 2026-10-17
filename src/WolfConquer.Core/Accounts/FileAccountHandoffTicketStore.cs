using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.Accounts;

public sealed class FileAccountHandoffTicketStore : IAccountHandoffTicketStore, IAccountHandoffTicketRedeemer
{
    private const int TicketFileVersion = 1;
    private const string SignatureAlgorithm = "HMAC-SHA256";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };
    private readonly byte[] _signingKey;

    public FileAccountHandoffTicketStore(string rootPath, string signingSecret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(signingSecret);

        RootPath = Path.GetFullPath(rootPath);
        _signingKey = Encoding.UTF8.GetBytes(signingSecret);
        Directory.CreateDirectory(RootPath);
    }

    public string RootPath { get; }

    public void Save(AccountHandoffTicket ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        ValidateToken(ticket.Token);

        Directory.CreateDirectory(RootPath);

        using var ticketLock = AcquireTicketLock(ticket.Token);
        SaveUnlocked(ticket);
    }

    public bool TryGet(string token, out AccountHandoffTicket ticket)
    {
        ticket = null!;
        if (!IsValidToken(token))
        {
            return false;
        }

        return TryGetUnlocked(token, out ticket);
    }

    public AccountHandoffRedeemResult Redeem(string token, DateTimeOffset nowUtc)
    {
        if (!IsValidToken(token))
        {
            return new AccountHandoffRedeemResult(AccountHandoffRedeemStatus.UnknownTicket);
        }

        using var ticketLock = AcquireTicketLock(token);
        if (!TryGetUnlocked(token, out var ticket))
        {
            return new AccountHandoffRedeemResult(AccountHandoffRedeemStatus.UnknownTicket);
        }

        if (ticket.IsRedeemed)
        {
            return new AccountHandoffRedeemResult(AccountHandoffRedeemStatus.AlreadyRedeemed, ticket);
        }

        if (ticket.ExpiresAtUtc <= nowUtc)
        {
            return new AccountHandoffRedeemResult(AccountHandoffRedeemStatus.ExpiredTicket, ticket);
        }

        var redeemed = ticket with { RedeemedAtUtc = nowUtc };
        SaveUnlocked(redeemed);
        return new AccountHandoffRedeemResult(AccountHandoffRedeemStatus.Success, redeemed);
    }

    private void SaveUnlocked(AccountHandoffTicket ticket)
    {
        var finalPath = GetTicketPath(ticket.Token);
        var tempPath = Path.Combine(
            RootPath,
            Path.GetFileNameWithoutExtension(finalPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");

        try
        {
            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var ticketFile = TicketFile.FromTicket(ticket);
                JsonSerializer.Serialize(stream, ticketFile with { Signature = Sign(ticketFile) }, JsonOptions);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(finalPath))
            {
                File.Replace(tempPath, finalPath, destinationBackupFileName: null);
            }
            else
            {
                File.Move(tempPath, finalPath);
            }
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private bool TryGetUnlocked(string token, out AccountHandoffTicket ticket)
    {
        ticket = null!;
        var path = GetTicketPath(token);
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            var fileInfo = new FileInfo(path);
            if (fileInfo.Length is <= 0 or > 16 * 1024)
            {
                return false;
            }

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var persisted = JsonSerializer.Deserialize<TicketFile>(stream, JsonOptions);
            if (persisted is null ||
                persisted.Version != TicketFileVersion ||
                persisted.SignatureAlgorithm != SignatureAlgorithm ||
                !string.Equals(persisted.TokenHash, GetTokenHash(token), StringComparison.Ordinal) ||
                persisted.AccountId <= 0 ||
                string.IsNullOrWhiteSpace(persisted.Username) ||
                string.IsNullOrWhiteSpace(persisted.WorldAddress) ||
                persisted.WorldPort is < 0 or > ushort.MaxValue ||
                persisted.ExpiresAtUtc <= persisted.IssuedAtUtc ||
                !IsValidSignature(persisted))
            {
                return false;
            }

            ticket = persisted.ToTicket(token);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            return false;
        }
    }

    private FileStream AcquireTicketLock(string token)
    {
        Directory.CreateDirectory(RootPath);
        var lockPath = Path.Combine(RootPath, Path.GetFileNameWithoutExtension(GetTicketPath(token)) + ".lock");
        IOException? lastError = null;
        for (var attempt = 0; attempt < 50; attempt++)
        {
            try
            {
                return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException ex)
            {
                lastError = ex;
                Thread.Sleep(10);
            }
        }

        throw new IOException($"Could not acquire account handoff ticket lock for {GetTokenHash(token)}.", lastError);
    }

    private string GetTicketPath(string token)
    {
        return Path.Combine(RootPath, GetFileName(token));
    }

    private static string GetFileName(string token)
    {
        return GetTokenHash(token) + ".json";
    }

    private static string GetTokenHash(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private string Sign(TicketFile ticket)
    {
        using var hmac = new HMACSHA256(_signingKey);
        var payload = Encoding.UTF8.GetBytes(GetSignaturePayload(ticket));
        return Convert.ToHexString(hmac.ComputeHash(payload)).ToLowerInvariant();
    }

    private bool IsValidSignature(TicketFile ticket)
    {
        if (string.IsNullOrWhiteSpace(ticket.Signature))
        {
            return false;
        }

        var expected = Sign(ticket);
        var expectedBytes = Encoding.ASCII.GetBytes(expected);
        var actualBytes = Encoding.ASCII.GetBytes(ticket.Signature);
        return expectedBytes.Length == actualBytes.Length &&
            CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    private static string GetSignaturePayload(TicketFile ticket)
    {
        var builder = new StringBuilder();
        AppendSignedField(builder, ticket.Version.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AppendSignedField(builder, ticket.SignatureAlgorithm);
        AppendSignedField(builder, ticket.TokenHash);
        AppendSignedField(builder, ticket.AccountId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AppendSignedField(builder, ticket.Username);
        AppendSignedField(builder, ticket.WorldAddress);
        AppendSignedField(builder, ticket.WorldPort.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AppendSignedField(builder, ticket.IssuedAtUtc.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        AppendSignedField(builder, ticket.ExpiresAtUtc.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        AppendSignedField(
            builder,
            ticket.RedeemedAtUtc?.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture) ?? "<null>");
        return builder.ToString();
    }

    private static void AppendSignedField(StringBuilder builder, string value)
    {
        builder
            .Append(value.Length.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Append(':')
            .Append(value)
            .Append('\n');
    }

    private static void ValidateToken(string token)
    {
        if (!IsValidToken(token))
        {
            throw new ArgumentException("Account handoff token must be a 32-character lowercase hex value.", nameof(token));
        }
    }

    private static bool IsValidToken(string token)
    {
        return token.Length == 32 && token.All(static c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    }

    private sealed record TicketFile(
        int Version,
        string SignatureAlgorithm,
        string Signature,
        string TokenHash,
        long AccountId,
        string Username,
        string WorldAddress,
        int WorldPort,
        DateTimeOffset IssuedAtUtc,
        DateTimeOffset ExpiresAtUtc,
        DateTimeOffset? RedeemedAtUtc)
    {
        public static TicketFile FromTicket(AccountHandoffTicket ticket)
        {
            return new TicketFile(
                TicketFileVersion,
                FileAccountHandoffTicketStore.SignatureAlgorithm,
                string.Empty,
                GetTokenHash(ticket.Token),
                ticket.AccountId,
                ticket.Username,
                ticket.WorldEndpoint.Address,
                ticket.WorldEndpoint.Port,
                ticket.IssuedAtUtc,
                ticket.ExpiresAtUtc,
                ticket.RedeemedAtUtc);
        }

        public AccountHandoffTicket ToTicket(string token)
        {
            return new AccountHandoffTicket(
                token,
                AccountId,
                Username,
                new ConquerEndpoint("WorldServer", WorldAddress, WorldPort),
                IssuedAtUtc,
                ExpiresAtUtc,
                RedeemedAtUtc);
        }
    }
}
