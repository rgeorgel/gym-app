namespace GymApp.Domain.Interfaces;

public interface IEmailService
{
    Task SendPasswordResetAsync(string toEmail, string toName, string resetUrl);
    Task SendWelcomeAsync(string toEmail, string toName, string academyName, string panelUrl);
    Task SendSubscriptionReminderAsync(string toEmail, string toName, string academyName, int daysRemaining, string paymentUrl);
}
