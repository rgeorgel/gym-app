using GymApp.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Resend;

namespace GymApp.Infra.Services;

public class ResendEmailService(IResend resend, IConfiguration config) : IEmailService
{
    public async Task SendPasswordResetAsync(string toEmail, string toName, string resetUrl)
    {
        var fromEmail = config["Resend:FromEmail"] ?? throw new InvalidOperationException("Resend:FromEmail not configured.");
        var fromName = config["Resend:FromName"] ?? "Gym App";

        var msg = new EmailMessage
        {
            From = $"{fromName} <{fromEmail}>",
            Subject = "Redefinição de senha",
            HtmlBody = $"""
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
        msg.To.Add(toEmail);

        await resend.EmailSendAsync(msg);
    }
}
