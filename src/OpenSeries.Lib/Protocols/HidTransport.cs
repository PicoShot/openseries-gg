using HidSharp;

namespace OpenSeries.Protocols;

internal sealed class HidTransport(HidDevice endpoint, int timeoutMilliseconds)
{
    internal HidStream OpenStream()
    {
        HidStream stream = endpoint.Open();
        stream.ReadTimeout = timeoutMilliseconds;
        stream.WriteTimeout = timeoutMilliseconds;
        return stream;
    }

    internal void WriteOutput(ReadOnlySpan<byte> command, int commandOffset = 0, int minimumReportLength = 0)
    {
        using HidStream stream = OpenStream();
        byte[] report = CreateReport(
            command,
            commandOffset,
            endpoint.GetMaxOutputReportLength(),
            minimumReportLength,
            "Output");
        stream.Write(report);
    }

    internal void WriteFeature(ReadOnlySpan<byte> command, int commandOffset = 0, int minimumReportLength = 0)
    {
        using HidStream stream = OpenStream();
        byte[] report = CreateReport(
            command,
            commandOffset,
            endpoint.GetMaxFeatureReportLength(),
            minimumReportLength,
            "Feature");
        stream.SetFeature(report);
    }

    internal byte[] WriteOutputAndRead(
        ReadOnlySpan<byte> command,
        int responseBufferLength,
        int commandOffset = 0,
        int minimumReportLength = 0)
    {
        using HidStream stream = OpenStream();
        byte[] report = CreateReport(
            command,
            commandOffset,
            endpoint.GetMaxOutputReportLength(),
            minimumReportLength,
            "Output");
        stream.Write(report);

        var response = new byte[Math.Max(responseBufferLength, endpoint.GetMaxInputReportLength())];
        int bytesRead = stream.Read(response);
        return response[..bytesRead];
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
