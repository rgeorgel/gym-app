namespace GymApp.Domain.Interfaces;

public interface IEmailService
{
    Task SendPasswordResetAsync(string toEmail, string toName, string resetUrl);
}
