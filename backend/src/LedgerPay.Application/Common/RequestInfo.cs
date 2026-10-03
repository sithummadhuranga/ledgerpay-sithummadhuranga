namespace LedgerPay.Application.Common;

// Where a request came from, for the audit log. The values come from the network, so they are cut to the
// size of the audit columns here. An oversized header must never be able to fail a transfer.
public sealed class RequestInfo(string? ipAddress, string? correlationId)
{
    public const int IpAddressLength = 45;
    public const int CorrelationIdLength = 64;

    public string? IpAddress { get; } = Limit(ipAddress, IpAddressLength);

    public string? CorrelationId { get; } = Limit(correlationId, CorrelationIdLength);

    private static string? Limit(string? value, int length) =>
        value is not null && value.Length > length ? value[..length] : value;
}
