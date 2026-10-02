namespace LedgerPay.Infrastructure.Seeding;

public static class SeedData
{
    public const string AdminEmail = "chamara.rajapaksa@example.com";
    public const string OperatorEmail = "dilani.senanayake@example.com";

    public static readonly SeedUser Admin = new(AdminEmail, "+94719846203", "Chamara Rajapaksa");
    public static readonly SeedUser Operator = new(OperatorEmail, "+94762751894", "Dilani Senanayake");

    public static readonly IReadOnlyList<SeedUser> Customers =
    [
        new("nimali.perera@example.com", "+94771284635", "Nimali Perera"),
        new("kasun.jayawardena@example.com", "+94712390581", "Kasun Jayawardena"),
        new("tharindu.fernando@example.com", "+94754106872", "Tharindu Fernando")
    ];
}
