namespace LedgerPay.Application.Abstractions;

public interface IPasswordService
{
    string Hash(string password);

    bool Verify(string passwordHash, string password);

    // Does the same work as Verify against a hash of a password nobody knows, and always answers false.
    // Used when the email is not registered, so the response time does not give that away.
    bool VerifyUnknownAccount(string password);
}
