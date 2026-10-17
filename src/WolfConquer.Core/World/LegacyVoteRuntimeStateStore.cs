using System.Globalization;
using System.Text;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed class LegacyVoteRuntimeStateStore
{
    private const long CooldownTicks = 12L * TimeSpan.TicksPerHour;
    private static readonly Encoding VoteFileEncoding = new UTF8Encoding(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    private readonly object _gate = new();
    private readonly List<LegacyVoteRuntimeRecord> _records;
    private bool _claimInProgress;

    public LegacyVoteRuntimeStateStore(string votesFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(votesFilePath);

        VotesFilePath = Path.GetFullPath(votesFilePath);
        (_records, LoadResult) = LoadRecords(VotesFilePath);
    }

    public string VotesFilePath { get; }

    public LegacyVoteRuntimeLoadResult LoadResult { get; }

    public IReadOnlyList<LegacyVoteRuntimeRecord> Snapshot
    {
        get
        {
            lock (_gate)
            {
                return Array.AsReadOnly(_records.ToArray());
            }
        }
    }

    public static LegacyVoteRuntimeStateStore Load(string votesFilePath)
    {
        return new LegacyVoteRuntimeStateStore(votesFilePath);
    }

    public bool CanVote(uint uid, string ipAddress, DateTime now)
    {
        if (!IsSerializableIpAddress(ipAddress))
        {
            return false;
        }

        lock (_gate)
        {
            if (!LoadResult.Success)
            {
                return false;
            }

            var record = FindFirstMatch(_records, uid, ipAddress);
            if (record is null)
            {
                return true;
            }

            if (record.Timer.Ticks > DateTime.MaxValue.Ticks - CooldownTicks)
            {
                return false;
            }

            return record.Timer.Ticks + CooldownTicks < now.Ticks;
        }
    }

    public LegacyVoteClaimResult TryCommitDueClaim(
        uint uid,
        string ipAddress,
        DateTime now,
        Func<LegacyVotePersistencePlan, bool> persist)
    {
        ArgumentNullException.ThrowIfNull(persist);

        lock (_gate)
        {
            if (!LoadResult.Success)
            {
                return LegacyVoteClaimResult.Failed(
                    $"Votes.txt store is unavailable: {LoadResult.FailureReason}");
            }

            if (!IsSerializableIpAddress(ipAddress))
            {
                return LegacyVoteClaimResult.Failed("The vote IP address is empty or is not safely serializable.");
            }

            if (_claimInProgress)
            {
                return LegacyVoteClaimResult.Failed("A vote claim persistence callback is already in progress.");
            }

            var candidateRecords = new List<LegacyVoteRuntimeRecord>(_records);
            var matchIndex = FindFirstMatchIndex(candidateRecords, uid, ipAddress);
            LegacyVoteRuntimeRecord effectiveRecord;

            if (matchIndex >= 0)
            {
                effectiveRecord = candidateRecords[matchIndex] with { Timer = now };
                candidateRecords[matchIndex] = effectiveRecord;
            }
            else
            {
                effectiveRecord = new LegacyVoteRuntimeRecord(uid, ipAddress, now);
                candidateRecords.Add(effectiveRecord);
            }

            var plan = new LegacyVotePersistencePlan(
                VotesFilePath,
                Serialize(candidateRecords));

            bool persisted;
            _claimInProgress = true;
            try
            {
                persisted = persist(plan);
            }
            catch (Exception ex)
            {
                return LegacyVoteClaimResult.Failed(
                    $"Vote persistence callback threw {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                _claimInProgress = false;
            }

            if (!persisted)
            {
                return LegacyVoteClaimResult.Failed("Vote persistence callback reported failure.");
            }

            _records.Clear();
            _records.AddRange(candidateRecords);
            return LegacyVoteClaimResult.Succeeded(effectiveRecord);
        }
    }

    private static (List<LegacyVoteRuntimeRecord> Records, LegacyVoteRuntimeLoadResult Result) LoadRecords(
        string votesFilePath)
    {
        byte[] fileBytes;
        try
        {
            fileBytes = File.ReadAllBytes(votesFilePath);
        }
        catch (FileNotFoundException)
        {
            return ([], LegacyVoteRuntimeLoadResult.MissingFile());
        }
        catch (DirectoryNotFoundException)
        {
            return ([], LegacyVoteRuntimeLoadResult.MissingFile());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ([], LegacyVoteRuntimeLoadResult.Failed(votesFilePath, ex));
        }

        try
        {
            var text = VoteFileEncoding.GetString(fileBytes);
            var sourceFile = Path.GetFileName(votesFilePath);
            var sourceLines = ReadSourceLines(text);
            var parsedRecords = ParseVotesPreservingUnsignedUids(text, sourceFile, sourceLines);
            var records = new List<LegacyVoteRuntimeRecord>(parsedRecords.Count);

            foreach (var parsedRecord in parsedRecords)
            {
                var fields = RankingSnapshotParser.SplitSlashRow(sourceLines[parsedRecord.LineNumber - 1]);
                if (!uint.TryParse(
                        fields[0],
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out var uid))
                {
                    throw new FormatException(
                        $"{parsedRecord.SourceFile} line {parsedRecord.LineNumber} has invalid vote UID '{fields[0]}'.");
                }

                var ipAddress = fields[1];
                if (!IsSerializableIpAddress(ipAddress))
                {
                    throw new FormatException(
                        $"{parsedRecord.SourceFile} line {parsedRecord.LineNumber} has an invalid vote IP address.");
                }

                if (!long.TryParse(
                        fields[2],
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out var ticks))
                {
                    throw new FormatException(
                        $"{parsedRecord.SourceFile} line {parsedRecord.LineNumber} has invalid DateTime ticks '{fields[2]}'.");
                }

                DateTime timer;
                try
                {
                    timer = new DateTime(ticks, DateTimeKind.Unspecified);
                }
                catch (ArgumentOutOfRangeException ex)
                {
                    throw new FormatException(
                        $"{parsedRecord.SourceFile} line {parsedRecord.LineNumber} has out-of-range DateTime ticks '{fields[2]}'.",
                        ex);
                }

                records.Add(new LegacyVoteRuntimeRecord(
                    uid,
                    ipAddress,
                    timer));
            }

            return (records, LegacyVoteRuntimeLoadResult.Loaded(records.Count));
        }
        catch (Exception ex) when (ex is FormatException or DecoderFallbackException)
        {
            return ([], LegacyVoteRuntimeLoadResult.Failed(votesFilePath, ex));
        }
    }

    private static IReadOnlyList<VoteRecord> ParseVotesPreservingUnsignedUids(
        string text,
        string sourceFile,
        IReadOnlyList<string> sourceLines)
    {
        try
        {
            return RankingSnapshotParser.ParseVotes(text, sourceFile);
        }
        catch (FormatException)
        {
            return RankingSnapshotParser.ParseVotes(
                CreateParserCompatibleVoteText(sourceLines),
                sourceFile);
        }
    }

    private static IReadOnlyList<string> ReadSourceLines(string text)
    {
        var lines = new List<string>();
        using var reader = new StringReader(text);
        while (reader.ReadLine() is { } line)
        {
            lines.Add(line);
        }

        return lines;
    }

    private static string CreateParserCompatibleVoteText(IReadOnlyList<string> sourceLines)
    {
        var text = new StringBuilder();
        var foundHeader = false;

        for (var index = 0; index < sourceLines.Count; index++)
        {
            if (index > 0)
            {
                text.Append('\n');
            }

            var line = sourceLines[index];
            if (line.Trim().Length == 0)
            {
                text.Append(line);
                continue;
            }

            if (!foundHeader)
            {
                foundHeader = true;
                text.Append(line);
                continue;
            }

            var separator = line.IndexOf('/');
            if (separator < 0)
            {
                text.Append(line);
                continue;
            }

            text.Append('0').Append(line.AsSpan(separator));
        }

        return text.ToString();
    }

    private static LegacyVoteRuntimeRecord? FindFirstMatch(
        IReadOnlyList<LegacyVoteRuntimeRecord> records,
        uint uid,
        string ipAddress)
    {
        var index = FindFirstMatchIndex(records, uid, ipAddress);
        return index >= 0 ? records[index] : null;
    }

    private static int FindFirstMatchIndex(
        IReadOnlyList<LegacyVoteRuntimeRecord> records,
        uint uid,
        string ipAddress)
    {
        for (var index = 0; index < records.Count; index++)
        {
            var record = records[index];
            if (record.Uid == uid || string.Equals(record.IpAddress, ipAddress, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private static byte[] Serialize(IReadOnlyList<LegacyVoteRuntimeRecord> records)
    {
        var text = new StringBuilder();
        text.Append("Count=")
            .Append(records.Count.ToString(CultureInfo.InvariantCulture))
            .Append("\r\n");

        foreach (var record in records)
        {
            text.Append(record.Uid.ToString(CultureInfo.InvariantCulture))
                .Append('/')
                .Append(record.IpAddress)
                .Append('/')
                .Append(record.Timer.Ticks.ToString(CultureInfo.InvariantCulture))
                .Append("/\r\n");
        }

        return Encoding.ASCII.GetBytes(text.ToString());
    }

    private static bool IsSerializableIpAddress(string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            return false;
        }

        foreach (var character in ipAddress)
        {
            if (character < '!' || character > '~' || character == '/')
            {
                return false;
            }
        }

        return true;
    }
}

public sealed record LegacyVoteRuntimeRecord(
    uint Uid,
    string IpAddress,
    DateTime Timer);

public sealed record LegacyVoteRuntimeLoadResult(
    bool Success,
    bool FileExisted,
    int RecordCount,
    string? FailureReason)
{
    internal static LegacyVoteRuntimeLoadResult MissingFile()
    {
        return new LegacyVoteRuntimeLoadResult(
            Success: true,
            FileExisted: false,
            RecordCount: 0,
            FailureReason: null);
    }

    internal static LegacyVoteRuntimeLoadResult Loaded(int recordCount)
    {
        return new LegacyVoteRuntimeLoadResult(
            Success: true,
            FileExisted: true,
            RecordCount: recordCount,
            FailureReason: null);
    }

    internal static LegacyVoteRuntimeLoadResult Failed(string votesFilePath, Exception exception)
    {
        return new LegacyVoteRuntimeLoadResult(
            Success: false,
            FileExisted: true,
            RecordCount: 0,
            FailureReason: $"Failed to load '{votesFilePath}': {exception.Message}");
    }
}

public sealed class LegacyVotePersistencePlan
{
    private readonly byte[] _votesFileBytes;

    internal LegacyVotePersistencePlan(string votesFilePath, byte[] votesFileBytes)
    {
        VotesFilePath = votesFilePath;
        _votesFileBytes = votesFileBytes;
    }

    public string VotesFilePath { get; }

    public ReadOnlyMemory<byte> VotesFileBytes => _votesFileBytes;
}

public sealed record LegacyVoteClaimResult(
    bool Success,
    LegacyVoteRuntimeRecord? EffectiveRecord,
    string? FailureReason)
{
    internal static LegacyVoteClaimResult Succeeded(LegacyVoteRuntimeRecord effectiveRecord)
    {
        return new LegacyVoteClaimResult(
            Success: true,
            EffectiveRecord: effectiveRecord,
            FailureReason: null);
    }

    internal static LegacyVoteClaimResult Failed(string failureReason)
    {
        return new LegacyVoteClaimResult(
            Success: false,
            EffectiveRecord: null,
            FailureReason: failureReason);
    }
}
