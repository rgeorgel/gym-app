using GymApp.Domain.Entities;
using GymApp.Domain.Enums;
using Xunit;

namespace GymApp.Tests.Entities;

public class TenantTests
{
    private static Tenant CreateTenant(
        bool isActive = true,
        SubscriptionStatus status = SubscriptionStatus.Trial,
        int trialDays = 14,
        DateTime? createdAt = null,
        DateTime? periodEnd = null)
    {
        return new Tenant
        {
            Id = Guid.NewGuid(),
            IsActive = isActive,
            SubscriptionStatus = status,
            TrialDays = trialDays,
            CreatedAt = createdAt ?? DateTime.UtcNow.AddDays(-7),
            SubscriptionCurrentPeriodEnd = periodEnd
        };
    }

    #region HasStudentAccess

    [Fact]
    public void HasStudentAccess_InactiveTenant_ReturnsFalse()
    {
        var tenant = CreateTenant(isActive: false);
        Assert.False(tenant.HasStudentAccess);
    }

    [Fact]
    public void HasStudentAccess_TrialWithinDays_ReturnsTrue()
    {
        var tenant = CreateTenant(
            isActive: true,
            status: SubscriptionStatus.Trial,
            trialDays: 14,
            createdAt: DateTime.UtcNow.AddDays(-7));
        Assert.True(tenant.HasStudentAccess);
    }

    [Fact]
    public void HasStudentAccess_TrialExpired_ReturnsFalse()
    {
        var tenant = CreateTenant(
            isActive: true,
            status: SubscriptionStatus.Trial,
            trialDays: 14,
            createdAt: DateTime.UtcNow.AddDays(-20));
        Assert.False(tenant.HasStudentAccess);
    }

    [Fact]
    public void HasStudentAccess_ActiveSubscription_ReturnsTrue()
    {
        var tenant = CreateTenant(
            isActive: true,
            status: SubscriptionStatus.Active);
        Assert.True(tenant.HasStudentAccess);
    }

    [Fact]
    public void HasStudentAccess_CanceledWithFuturePeriod_ReturnsTrue()
    {
        var tenant = CreateTenant(
            isActive: true,
            status: SubscriptionStatus.Canceled,
            periodEnd: DateTime.UtcNow.AddDays(10));
        Assert.True(tenant.HasStudentAccess);
    }

    [Fact]
    public void HasStudentAccess_CanceledWithPastPeriod_ReturnsFalse()
    {
        var tenant = CreateTenant(
            isActive: true,
            status: SubscriptionStatus.Canceled,
            periodEnd: DateTime.UtcNow.AddDays(-5));
        Assert.False(tenant.HasStudentAccess);
    }

    [Fact]
    public void HasStudentAccess_CanceledWithNoPeriodEnd_ReturnsFalse()
    {
        var tenant = CreateTenant(
            isActive: true,
            status: SubscriptionStatus.Canceled,
            periodEnd: null);
        Assert.False(tenant.HasStudentAccess);
    }

    [Fact]
    public void HasStudentAccess_PastDueStatus_ReturnsFalse()
    {
        var tenant = CreateTenant(
            isActive: true,
            status: SubscriptionStatus.PastDue);
        Assert.False(tenant.HasStudentAccess);
    }

    [Fact]
    public void HasStudentAccess_SuspendedStatus_ReturnsFalse()
    {
        var tenant = CreateTenant(
            isActive: true,
            status: SubscriptionStatus.Suspended);
        Assert.False(tenant.HasStudentAccess);
    }

    #endregion

    #region IsInTrial

    [Fact]
    public void IsInTrial_ActiveTrialWithinDays_ReturnsTrue()
    {
        var tenant = CreateTenant(
            isActive: true,
            status: SubscriptionStatus.Trial,
            trialDays: 14,
            createdAt: DateTime.UtcNow.AddDays(-5));
        Assert.True(tenant.IsInTrial);
    }

    [Fact]
    public void IsInTrial_ExpiredTrial_ReturnsFalse()
    {
        var tenant = CreateTenant(
            isActive: true,
            status: SubscriptionStatus.Trial,
            trialDays: 14,
            createdAt: DateTime.UtcNow.AddDays(-20));
        Assert.False(tenant.IsInTrial);
    }

    [Fact]
    public void IsInTrial_ActiveSubscription_ReturnsFalse()
    {
        var tenant = CreateTenant(
            isActive: true,
            status: SubscriptionStatus.Active);
        Assert.False(tenant.IsInTrial);
    }

    [Fact]
    public void IsInTrial_LastDayOfTrial_ReturnsTrue()
    {
        var tenant = CreateTenant(
            isActive: true,
            status: SubscriptionStatus.Trial,
            trialDays: 14,
            createdAt: DateTime.UtcNow.AddDays(-13).AddHours(-12));
        Assert.True(tenant.IsInTrial);
    }

    #endregion

    #region TrialDaysRemaining

    [Fact]
    public void TrialDaysRemaining_InTrial_ReturnsPositiveDays()
    {
        var tenant = CreateTenant(
            isActive: true,
            status: SubscriptionStatus.Trial,
            trialDays: 14,
            createdAt: DateTime.UtcNow.AddDays(-7));
        Assert.True(tenant.TrialDaysRemaining > 0);
    }

    [Fact]
    public void TrialDaysRemaining_NotInTrial_ReturnsZero()
    {
        var tenant = CreateTenant(
            isActive: true,
            status: SubscriptionStatus.Active);
        Assert.Equal(0, tenant.TrialDaysRemaining);
    }

    [Fact]
    public void TrialDaysRemaining_ExpiredTrial_ReturnsZero()
    {
        var tenant = CreateTenant(
            isActive: true,
            status: SubscriptionStatus.Trial,
            trialDays: 14,
            createdAt: DateTime.UtcNow.AddDays(-30));
        Assert.Equal(0, tenant.TrialDaysRemaining);
    }

    [Fact]
    public void TrialDaysRemaining_ActiveSubscription_ReturnsZero()
    {
        var tenant = CreateTenant(
            isActive: true,
            status: SubscriptionStatus.Active);
        Assert.Equal(0, tenant.TrialDaysRemaining);
    }

    #endregion

    #region PaymentsActive

    [Fact]
    public void PaymentsActive_BothEnabled_ReturnsTrue()
    {
        var tenant = new Tenant { PaymentsEnabled = true, PaymentsAllowedBySuperAdmin = true };
        Assert.True(tenant.PaymentsActive);
    }

    [Fact]
    public void PaymentsActive_PaymentsDisabled_ReturnsFalse()
    {
        var tenant = new Tenant { PaymentsEnabled = false, PaymentsAllowedBySuperAdmin = true };
        Assert.False(tenant.PaymentsActive);
    }

    [Fact]
    public void PaymentsActive_SuperAdminBlocked_ReturnsFalse()
    {
        var tenant = new Tenant { PaymentsEnabled = true, PaymentsAllowedBySuperAdmin = false };
        Assert.False(tenant.PaymentsActive);
    }

    [Fact]
    public void PaymentsActive_BothDisabled_ReturnsFalse()
    {
        var tenant = new Tenant { PaymentsEnabled = false, PaymentsAllowedBySuperAdmin = false };
        Assert.False(tenant.PaymentsActive);
    }

    #endregion
}