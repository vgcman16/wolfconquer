namespace WolfConquer.Core.Data;

internal partial class LegacyNativePrestigeRanking
{
    private static PrestigeSaveOutcome ReconcilePrestigeSave(
        PrestigeSavePaths paths, FileStream? before, byte[] image, bool operationReturned,
        out Exception? observationError)
    {
        observationError = null;
        try
        {
            RequirePrestigeSaveRegularPath(paths.Destination);
            var matchesCandidate = PrestigeSaveMatchesImage(paths.Destination, image);
            if (matchesCandidate && (operationReturned || PrestigeSaveFileAbsent(paths.Stage)))
            {
                return PrestigeSaveOutcome.Committed;
            }

            if (!operationReturned && PrestigeSaveMatchesPrior(paths.Destination, before))
            {
                return PrestigeSaveOutcome.Refused;
            }

            // A missing-target create may lose a race. A different destination
            // plus the unchanged private source is a refused create, not license
            // to overwrite the newly appeared file or to retry as Replace.
            if (!operationReturned && before is null && !matchesCandidate &&
                PrestigeSaveMatchesImage(paths.Stage, image))
            {
                return PrestigeSaveOutcome.Refused;
            }
        }
        catch (Exception error) when (IsOrdinaryPrestigeSaveFailure(error))
        {
            observationError = error;
        }

        return PrestigeSaveOutcome.Indeterminate;
    }

    private static bool PrestigeSaveMatchesImage(string path, byte[] image)
    {
        using var file = OpenPrestigeSaveRead(path);
        if (file is null || file.Length != image.Length)
        {
            return false;
        }

        Span<byte> buffer = stackalloc byte[4096];
        for (var offset = 0; offset < image.Length;)
        {
            var count = Math.Min(buffer.Length, image.Length - offset);
            file.ReadExactly(buffer[..count]);
            if (!buffer[..count].SequenceEqual(image.AsSpan(offset, count)))
            {
                return false;
            }

            offset += count;
        }

        return file.ReadByte() == -1;
    }

    private static bool PrestigeSaveMatchesPrior(string path, FileStream? before)
    {
        using var actual = OpenPrestigeSaveRead(path);
        if (before is null || actual is null)
        {
            return before is null && actual is null;
        }

        if (before.Length != actual.Length)
        {
            return false;
        }

        before.Position = 0;
        Span<byte> priorBuffer = stackalloc byte[4096];
        Span<byte> actualBuffer = stackalloc byte[4096];
        var remaining = before.Length;
        while (remaining > 0)
        {
            var count = (int)Math.Min(remaining, priorBuffer.Length);
            before.ReadExactly(priorBuffer[..count]);
            actual.ReadExactly(actualBuffer[..count]);
            if (!priorBuffer[..count].SequenceEqual(actualBuffer[..count]))
            {
                return false;
            }

            remaining -= count;
        }

        return before.ReadByte() == -1 && actual.ReadByte() == -1;
    }

}
