namespace LedgerPay.Domain.Rules;

// An email and a mobile number cut down for a list, so a list of customers is not a list of contact details.
// The whole values are on the page of one customer, which is written to the audit log.
public static class ContactMask
{
    // The first letter of the part before the @, then stars, then the domain: n***@example.com.
    public static string Email(string email)
    {
        var at = email.LastIndexOf('@');
        return at <= 0 ? "***" : email[..1] + "***" + email[at..];
    }

    // The country code and the first digits of the number, then stars, then the last four: +9477***4635.
    public static string Phone(string phone) =>
        phone.Length <= 9 ? "***" : phone[..5] + "***" + phone[^4..];
}
