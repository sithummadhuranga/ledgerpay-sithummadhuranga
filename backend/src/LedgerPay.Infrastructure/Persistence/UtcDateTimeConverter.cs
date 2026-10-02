using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LedgerPay.Infrastructure.Persistence;

// datetime2 has no time zone. A local time is converted before it is written, and a value read back is
// marked UTC so JSON keeps the Z. An Unspecified value is taken as UTC already.
internal sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    value => value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value,
    value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
