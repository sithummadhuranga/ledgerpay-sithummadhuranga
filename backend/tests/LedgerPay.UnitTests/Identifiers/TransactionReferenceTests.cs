using System.Text.RegularExpressions;
using LedgerPay.Domain.Identifiers;

namespace LedgerPay.UnitTests.Identifiers;

public class TransactionReferenceTests
{
    [Fact]
    public void Reference_is_readable_and_has_a_fixed_shape()
    {
        for (var i = 0; i < 500; i++)
        {
            Assert.Matches(new Regex("^TX[0-9A-HJKMNP-TV-Z]{14}$"), TransactionReference.Next());
        }
    }

    [Fact]
    public void References_do_not_repeat()
    {
        var references = Enumerable.Range(0, 2000).Select(_ => TransactionReference.Next()).ToList();

        Assert.Equal(references.Count, references.Distinct().Count());
    }
}
