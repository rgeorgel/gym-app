namespace GymApp.Domain.Interfaces;

public interface IEmailService
{
    Task SendPasswordResetAsync(string toEmail, string toName, string resetUrl);
    Task SendWelcomeAsync(string toEmail, string toName, string businessName, string panelUrl, bool isSalon = false);
    Task SendSubscriptionReminderAsync(string toEmail, string toName, string academyName, int daysRemaining, string paymentUrl);
    Task SendBookingConfirmationAsync(string toEmail, string toName, string serviceName, string academyName, DateTime sessionDateTime);
    Task SendNewBookingNotificationAsync(string toEmail, string adminName, string clientName, string? clientPhone, string serviceName, DateTime sessionDateTime);
    Task SendAffiliateCommissionEarnedAsync(string toEmail, string toName, string tenantName, decimal commissionAmount, decimal newBalance);
    Task SendAffiliateWithdrawalStatusAsync(string toEmail, string toName, decimal amount, string status, string? adminNotes);
}
