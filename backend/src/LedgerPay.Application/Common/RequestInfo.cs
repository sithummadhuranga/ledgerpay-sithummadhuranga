namespace LedgerPay.Application.Common;

// Where a request came from, for the audit log. The values come from the network, so they are cut to the
// size of the audit columns here. An oversized header must never be able to fail a transfer.
public sealed class RequestInfo(string? ipAddress, string? correlationId, string? userAgent = null)
{
    public const int IpAddressLength = 45;
    public const int CorrelationIdLength = 64;
    public const int UserAgentLength = 200;

    public string? IpAddress { get; } = Limit(ipAddress, IpAddressLength);

    public string? CorrelationId { get; } = Limit(correlationId, CorrelationIdLength);

    public string? UserAgent { get; } = Limit(userAgent, UserAgentLength);

    private static string? Limit(string? value, int length) =>
        value is not null && value.Length > length ? value[..length] : value;
}
