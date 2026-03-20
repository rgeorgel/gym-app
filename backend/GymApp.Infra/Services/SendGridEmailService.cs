using GymApp.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace GymApp.Infra.Services;

public class SendGridEmailService(IConfiguration config) : IEmailService
{
    public async Task SendPasswordResetAsync(string toEmail, string toName, string resetUrl)
    {
        var apiKey = config["SendGrid:ApiKey"] ?? throw new InvalidOperationException("SendGrid:ApiKey not configured.");
        var fromEmail = config["SendGrid:FromEmail"] ?? throw new InvalidOperationException("SendGrid:FromEmail not configured.");
        var fromName = config["SendGrid:FromName"] ?? "Gym App";

        var client = new SendGridClient(apiKey);
        var msg = new SendGridMessage
        {
            From = new EmailAddress(fromEmail, fromName),
            Subject = "Redefinição de senha",
            HtmlContent = $"""
                <div style="font-family:sans-serif;max-width:480px;margin:auto;padding:2rem">
                  <h2 style="color:#1a1a2e">Redefinição de senha</h2>
                  <p>Olá, <strong>{toName}</strong>!</p>
                  <p>Recebemos uma solicitação para redefinir a senha da sua conta.
                     Clique no botão abaixo para criar uma nova senha. O link é válido por <strong>2 horas</strong>.</p>
                  <p style="text-align:center;margin:2rem 0">
                    <a href="{resetUrl}"
                       style="background:#6c63ff;color:#fff;padding:0.75rem 1.5rem;border-radius:8px;text-decoration:none;font-weight:600">
                      Redefinir senha
                    </a>
                  </p>
                  <p style="font-size:0.85rem;color:#666">
                    Se você não solicitou a redefinição de senha, ignore este e-mail.<br>
                    O link expira em 2 horas.
                  </p>
                </div>
                """
        };
        msg.AddTo(new EmailAddress(toEmail, toName));

        await client.SendEmailAsync(msg);
    }

    public Task SendWelcomeAsync(string toEmail, string toName, string academyName, string panelUrl)
        => Task.CompletedTask; // SendGrid not in use — no-op

    public Task SendSubscriptionReminderAsync(string toEmail, string toName, string academyName, int daysRemaining, string paymentUrl)
        => Task.CompletedTask; // SendGrid not in use — no-op

    public Task SendBookingConfirmationAsync(string toEmail, string toName, string serviceName, string academyName, DateTime sessionDateTime)
        => Task.CompletedTask; // SendGrid not in use — no-op

    public Task SendNewBookingNotificationAsync(string toEmail, string adminName, string clientName, string? clientPhone, string serviceName, DateTime sessionDateTime)
        => Task.CompletedTask; // SendGrid not in use — no-op
}
