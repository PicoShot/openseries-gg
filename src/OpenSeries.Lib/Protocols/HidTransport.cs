using System.Buffers;
using HidSharp;

namespace OpenSeries.Protocols;

internal sealed class HidTransport(HidDevice endpoint, int timeoutMilliseconds) : IDisposable
{
    private readonly object syncRoot = new();
    private HidStream? stream;
    private bool disposed;

    internal void WriteOutput(ReadOnlySpan<byte> command, int commandOffset = 0, int minimumReportLength = 0)
    {
        EnsureNotDisposed();
        byte[] report = CreateReport(command, commandOffset, endpoint.GetMaxOutputReportLength(), minimumReportLength, "Output");
        Execute(activeStream => activeStream.Write(report));
    }

    internal void WriteFeature(ReadOnlySpan<byte> command, int commandOffset = 0, int minimumReportLength = 0)
    {
        EnsureNotDisposed();
        byte[] report = CreateReport(command, commandOffset, endpoint.GetMaxFeatureReportLength(), minimumReportLength, "Feature");
        Execute(activeStream => activeStream.SetFeature(report));
    }

    internal byte[] WriteOutputAndRead(
        ReadOnlySpan<byte> command,
        int responseBufferLength,
        int commandOffset = 0,
        int minimumReportLength = 0,
        byte? normalizeLeadingZeroReportIdFor = null)
    {
        EnsureNotDisposed();
        byte[] report = CreateReport(command, commandOffset, endpoint.GetMaxOutputReportLength(), minimumReportLength, "Output");
        return Execute(activeStream =>
        {
            activeStream.Write(report);
            int bufferLength = Math.Max(responseBufferLength, endpoint.GetMaxInputReportLength());
            byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferLength);
            try
            {
                int bytesRead = activeStream.Read(buffer, 0, bufferLength);
                int responseOffset =
                    normalizeLeadingZeroReportIdFor is byte commandByte &&
                    bytesRead >= 2 &&
                    buffer[0] == 0x00 &&
                    buffer[1] == commandByte
                        ? 1
                        : 0;
                return buffer.AsSpan(responseOffset, bytesRead - responseOffset).ToArray();
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        });
    }

    public void Dispose()
    {
        lock (syncRoot)
        {
            if (disposed)
                return;

            disposed = true;
            InvalidateStream();
        }
    }

    private void Execute(Action<HidStream> operation) =>
        Execute(activeStream =>
        {
            operation(activeStream);
            return true;
        });

    private T Execute<T>(Func<HidStream, T> operation)
    {
        lock (syncRoot)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            try
            {
                return operation(GetOrOpenStream());
            }
            catch (Exception exception) when (exception is IOException or TimeoutException)
            {
                InvalidateStream();
                throw;
            }
        }
    }

    private HidStream GetOrOpenStream()
    {
        if (stream is not null)
            return stream;

        HidStream openedStream = endpoint.Open();
        try
        {
            openedStream.ReadTimeout = timeoutMilliseconds;
            openedStream.WriteTimeout = timeoutMilliseconds;
            stream = openedStream;
            return openedStream;
        }
        catch
        {
            openedStream.Dispose();
            throw;
        }
    }

    private void InvalidateStream()
    {
        HidStream? invalidStream = stream;
        stream = null;
        invalidStream?.Dispose();
    }

    private void EnsureNotDisposed()
    {
        lock (syncRoot)
            ObjectDisposedException.ThrowIf(disposed, this);
    }

    private static byte[] CreateReport(
        ReadOnlySpan<byte> command,
        int commandOffset,
        int reportLength,
        int minimumReportLength,
        string reportKind)
    {
        if (commandOffset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(commandOffset));
        }

        if (reportLength < command.Length + commandOffset || reportLength < minimumReportLength)
        {
            throw new InvalidDataException($"{reportKind} report length {reportLength} cannot carry this command.");
        }

        var report = new byte[reportLength];
        command.CopyTo(report.AsSpan(commandOffset));
        return report;
    }
}
