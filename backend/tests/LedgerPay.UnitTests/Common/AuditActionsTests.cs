using System.Reflection;
using LedgerPay.Domain.Constants;

namespace LedgerPay.UnitTests.Common;

public class AuditActionsTests
{
    [Fact]
    public void The_list_has_every_action_constant_once_and_nothing_else()
    {
        var constants = typeof(AuditActions).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral)
            .Select(field => (string)field.GetRawConstantValue()!)
            .Order()
            .ToList();

        Assert.NotEmpty(constants);
        Assert.Equal(constants, AuditActions.All.Order().ToList());
        Assert.Equal(AuditActions.All.Count, AuditActions.All.Distinct().Count());
    }
}
