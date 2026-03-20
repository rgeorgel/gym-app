using GymApp.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Resend;

namespace GymApp.Infra.Services;

public class ResendEmailService(IResend resend, IConfiguration config) : IEmailService
{
    private string FromEmail => config["Resend:FromEmail"] ?? throw new InvalidOperationException("Resend:FromEmail not configured.");
    private string FromName => config["Resend:FromName"] ?? "Agendofy";

    public async Task SendWelcomeAsync(string toEmail, string toName, string academyName, string panelUrl)
    {
        var msg = new EmailMessage
        {
            From = $"{FromName} <{FromEmail}>",
            Subject = $"Sua academia está pronta no Agendofy 🎉",
            HtmlBody = $"""
                <!DOCTYPE html>
                <html lang="pt-BR">
                <head><meta charset="UTF-8"><meta name="viewport" content="width=device-width,initial-scale=1"></head>
                <body style="margin:0;padding:0;background:#F4F6FB;font-family:'Helvetica Neue',Arial,sans-serif">
                  <table width="100%" cellpadding="0" cellspacing="0" style="background:#F4F6FB;padding:40px 16px">
                    <tr><td align="center">
                      <table width="560" cellpadding="0" cellspacing="0" style="background:#ffffff;border-radius:16px;overflow:hidden;box-shadow:0 4px 24px rgba(0,0,0,0.07)">

                        <!-- Header -->
                        <tr>
                          <td style="background:#0D1525;padding:32px 40px;text-align:center">
                            <p style="margin:0;font-size:24px;font-weight:800;color:#ffffff;letter-spacing:-0.5px">
                              agendo<span style="color:#3563E9">fy</span>
                            </p>
                          </td>
                        </tr>

                        <!-- Body -->
                        <tr>
                          <td style="padding:40px 40px 24px">
                            <h1 style="margin:0 0 8px;font-size:24px;font-weight:800;color:#060A14;letter-spacing:-0.5px">
                              Bem-vindo, {toName}! 🎉
                            </h1>
                            <p style="margin:0 0 24px;font-size:15px;color:#64748B;line-height:1.6">
                              Sua academia <strong style="color:#060A14">{academyName}</strong> foi criada com sucesso.
                              Acesse o painel de administração e configure tudo em minutos.
                            </p>

                            <!-- CTA -->
                            <table width="100%" cellpadding="0" cellspacing="0" style="margin-bottom:20px">
                              <tr>
                                <td align="center">
                                  <a href="{panelUrl}"
                                     style="display:inline-block;background:#3563E9;color:#ffffff;font-size:15px;font-weight:600;
                                            padding:14px 36px;border-radius:10px;text-decoration:none;letter-spacing:-0.2px">
                                    Acessar meu painel →
                                  </a>
                                </td>
                              </tr>
                            </table>
                            <p style="margin:0 0 32px;font-size:12px;color:#94A3B8;text-align:center;line-height:1.6">
                              Ou copie o endereço abaixo no navegador:<br>
                              <a href="{panelUrl}" style="color:#3563E9;text-decoration:none;font-weight:500;word-break:break-all">
                                {panelUrl}
                              </a>
                            </p>

                            <!-- Divider -->
                            <hr style="border:none;border-top:1px solid #E8ECF4;margin:0 0 28px">

                            <!-- Steps -->
                            <p style="margin:0 0 16px;font-size:13px;font-weight:700;color:#94A3B8;text-transform:uppercase;letter-spacing:1px">
                              Por onde começar
                            </p>

                            {Step("1", "Criar modalidades", "Cadastre os tipos de aula da sua academia: Boxe em Grupo, Individual, Sparring, etc.")}
                            {Step("2", "Montar a grade de horários", "Defina os dias e horários de cada modalidade e a capacidade máxima de alunos.")}
                            {Step("3", "Criar planos e pacotes", "Configure os pacotes de crédito que seus alunos poderão comprar (ex: 8 aulas em grupo + 4 individuais).")}
                            {Step("4", "Cadastrar seus alunos", "Adicione os alunos e atribua os planos a cada um. Eles receberão acesso para se agendar.")}
                            {Step("5", "Compartilhar o link", "Envie o link do app para seus alunos. Eles se agendarão sozinhos nas aulas disponíveis.")}
                          </td>
                        </tr>

                        <!-- Footer -->
                        <tr>
                          <td style="background:#F8FAFF;border-top:1px solid #E8ECF4;padding:24px 40px;text-align:center">
                            <p style="margin:0;font-size:12px;color:#94A3B8;line-height:1.6">
                              Qualquer dúvida, responda este e-mail — estamos aqui para ajudar.<br>
                              <a href="https://agendofy.com" style="color:#3563E9;text-decoration:none">agendofy.com</a>
                            </p>
                          </td>
                        </tr>

                      </table>
                    </td></tr>
                  </table>
                </body>
                </html>
                """
        };
        msg.To.Add(toEmail);
        await resend.EmailSendAsync(msg);
    }

    public async Task SendPasswordResetAsync(string toEmail, string toName, string resetUrl)
    {
        var msg = new EmailMessage
        {
            From = $"{FromName} <{FromEmail}>",
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

    public async Task SendSubscriptionReminderAsync(string toEmail, string toName, string academyName, int daysRemaining, string paymentUrl)
    {
        var urgencyColor = daysRemaining == 1 ? "#EF4444" : daysRemaining <= 3 ? "#F59E0B" : "#3563E9";
        var urgencyLabel = daysRemaining == 1
            ? "⚠️ Último dia!"
            : $"{daysRemaining} dias restantes";

        var msg = new EmailMessage
        {
            From = $"{FromName} <{FromEmail}>",
            Subject = $"[Agendofy] Sua assinatura vence em {daysRemaining} dia(s) — {academyName}",
            HtmlBody = $"""
                <!DOCTYPE html>
                <html lang="pt-BR">
                <head><meta charset="UTF-8"><meta name="viewport" content="width=device-width,initial-scale=1"></head>
                <body style="margin:0;padding:0;background:#F4F6FB;font-family:'Helvetica Neue',Arial,sans-serif">
                  <table width="100%" cellpadding="0" cellspacing="0" style="background:#F4F6FB;padding:40px 16px">
                    <tr><td align="center">
                      <table width="560" cellpadding="0" cellspacing="0" style="background:#ffffff;border-radius:16px;overflow:hidden;box-shadow:0 4px 24px rgba(0,0,0,0.07)">

                        <tr>
                          <td style="background:#0D1525;padding:32px 40px;text-align:center">
                            <p style="margin:0;font-size:24px;font-weight:800;color:#ffffff;letter-spacing:-0.5px">
                              agendo<span style="color:#3563E9">fy</span>
                            </p>
                          </td>
                        </tr>

                        <tr>
                          <td style="padding:40px 40px 32px">
                            <div style="display:inline-block;background:{urgencyColor}1A;border:1px solid {urgencyColor}40;border-radius:8px;padding:8px 16px;margin-bottom:24px">
                              <span style="font-size:13px;font-weight:700;color:{urgencyColor}">{urgencyLabel}</span>
                            </div>

                            <h1 style="margin:0 0 8px;font-size:22px;font-weight:800;color:#060A14;letter-spacing:-0.5px">
                              Renove sua assinatura
                            </h1>
                            <p style="margin:0 0 24px;font-size:15px;color:#64748B;line-height:1.6">
                              Olá, <strong style="color:#060A14">{toName}</strong>! A assinatura da academia
                              <strong style="color:#060A14">{academyName}</strong> vence em
                              <strong style="color:{urgencyColor}">{daysRemaining} dia(s)</strong>.
                              Após o vencimento, seus alunos perderão acesso ao sistema de agendamentos.
                            </p>

                            <table width="100%" cellpadding="0" cellspacing="0" style="margin-bottom:20px">
                              <tr>
                                <td align="center">
                                  <a href="{paymentUrl}"
                                     style="display:inline-block;background:{urgencyColor};color:#ffffff;font-size:15px;font-weight:600;
                                            padding:14px 36px;border-radius:10px;text-decoration:none;letter-spacing:-0.2px">
                                    Pagar agora via PIX →
                                  </a>
                                </td>
                              </tr>
                            </table>
                            <p style="margin:0 0 0;font-size:12px;color:#94A3B8;text-align:center;line-height:1.6">
                              Ou copie o link abaixo:<br>
                              <a href="{paymentUrl}" style="color:#3563E9;text-decoration:none;font-weight:500;word-break:break-all">
                                {paymentUrl}
                              </a>
                            </p>
                          </td>
                        </tr>

                        <tr>
                          <td style="background:#F8FAFF;border-top:1px solid #E8ECF4;padding:24px 40px;text-align:center">
                            <p style="margin:0;font-size:12px;color:#94A3B8;line-height:1.6">
                              Qualquer dúvida, responda este e-mail.<br>
                              <a href="https://agendofy.com" style="color:#3563E9;text-decoration:none">agendofy.com</a>
                            </p>
                          </td>
                        </tr>

                      </table>
                    </td></tr>
                  </table>
                </body>
                </html>
                """
        };
        msg.To.Add(toEmail);
        await resend.EmailSendAsync(msg);
    }

    public async Task SendBookingConfirmationAsync(string toEmail, string toName, string serviceName, string academyName, DateTime sessionDateTime)
    {
        var dateStr = sessionDateTime.ToString("dddd, dd 'de' MMMM", new System.Globalization.CultureInfo("pt-BR"));
        var timeStr = sessionDateTime.ToString("HH:mm");

        var msg = new EmailMessage
        {
            From = $"{FromName} <{FromEmail}>",
            Subject = $"✅ Agendamento confirmado — {serviceName}",
            HtmlBody = $"""
                <!DOCTYPE html>
                <html lang="pt-BR">
                <head><meta charset="UTF-8"><meta name="viewport" content="width=device-width,initial-scale=1"></head>
                <body style="margin:0;padding:0;background:#F4F6FB;font-family:'Helvetica Neue',Arial,sans-serif">
                  <table width="100%" cellpadding="0" cellspacing="0" style="background:#F4F6FB;padding:40px 16px">
                    <tr><td align="center">
                      <table width="560" cellpadding="0" cellspacing="0" style="background:#ffffff;border-radius:16px;overflow:hidden;box-shadow:0 4px 24px rgba(0,0,0,0.07)">
                        <tr>
                          <td style="background:#0D1525;padding:28px 40px;text-align:center">
                            <p style="margin:0;font-size:22px;font-weight:800;color:#ffffff;letter-spacing:-0.5px">
                              agendo<span style="color:#3563E9">fy</span>
                            </p>
                          </td>
                        </tr>
                        <tr>
                          <td style="padding:40px 40px 32px">
                            <div style="text-align:center;margin-bottom:24px">
                              <div style="display:inline-block;background:#D1FAE5;border-radius:50%;width:56px;height:56px;line-height:56px;font-size:28px">✅</div>
                            </div>
                            <h1 style="margin:0 0 8px;font-size:22px;font-weight:800;color:#060A14;text-align:center">
                              Agendamento confirmado!
                            </h1>
                            <p style="margin:0 0 28px;font-size:15px;color:#64748B;text-align:center">
                              Olá, <strong style="color:#060A14">{toName}</strong>! Seu agendamento foi confirmado.
                            </p>
                            <table width="100%" cellpadding="0" cellspacing="0" style="background:#F8FAFF;border-radius:12px;padding:20px;margin-bottom:24px">
                              <tr>
                                <td style="padding:8px 20px">
                                  <table width="100%" cellpadding="0" cellspacing="0">
                                    <tr>
                                      <td style="padding:8px 0;border-bottom:1px solid #E8ECF4">
                                        <span style="font-size:13px;color:#64748B">Serviço</span>
                                        <span style="float:right;font-size:14px;font-weight:600;color:#060A14">{serviceName}</span>
                                      </td>
                                    </tr>
                                    <tr>
                                      <td style="padding:8px 0;border-bottom:1px solid #E8ECF4">
                                        <span style="font-size:13px;color:#64748B">Data</span>
                                        <span style="float:right;font-size:14px;font-weight:600;color:#060A14">{dateStr}</span>
                                      </td>
                                    </tr>
                                    <tr>
                                      <td style="padding:8px 0">
                                        <span style="font-size:13px;color:#64748B">Horário</span>
                                        <span style="float:right;font-size:14px;font-weight:600;color:#3563E9">{timeStr}</span>
                                      </td>
                                    </tr>
                                  </table>
                                </td>
                              </tr>
                            </table>
                            <p style="margin:0;font-size:13px;color:#94A3B8;text-align:center">
                              📍 <strong style="color:#060A14">{academyName}</strong> — pagamento no local após o atendimento.
                            </p>
                          </td>
                        </tr>
                        <tr>
                          <td style="background:#F8FAFF;border-top:1px solid #E8ECF4;padding:20px 40px;text-align:center">
                            <p style="margin:0;font-size:12px;color:#94A3B8">
                              Para cancelar, acesse o aplicativo.<br>
                              <a href="https://agendofy.com" style="color:#3563E9;text-decoration:none">agendofy.com</a>
                            </p>
                          </td>
                        </tr>
                      </table>
                    </td></tr>
                  </table>
                </body>
                </html>
                """
        };
        msg.To.Add(toEmail);
        await resend.EmailSendAsync(msg);
    }

    public async Task SendNewBookingNotificationAsync(string toEmail, string adminName, string clientName, string? clientPhone, string serviceName, DateTime sessionDateTime)
    {
        var dateStr = sessionDateTime.ToString("dd/MM/yyyy");
        var timeStr = sessionDateTime.ToString("HH:mm");
        var phoneDisplay = clientPhone is not null ? $"📱 {clientPhone}" : "Telefone não informado";

        var msg = new EmailMessage
        {
            From = $"{FromName} <{FromEmail}>",
            Subject = $"📅 Novo agendamento: {clientName} — {timeStr}",
            HtmlBody = $"""
                <!DOCTYPE html>
                <html lang="pt-BR">
                <head><meta charset="UTF-8"><meta name="viewport" content="width=device-width,initial-scale=1"></head>
                <body style="margin:0;padding:0;background:#F4F6FB;font-family:'Helvetica Neue',Arial,sans-serif">
                  <table width="100%" cellpadding="0" cellspacing="0" style="background:#F4F6FB;padding:40px 16px">
                    <tr><td align="center">
                      <table width="560" cellpadding="0" cellspacing="0" style="background:#ffffff;border-radius:16px;overflow:hidden;box-shadow:0 4px 24px rgba(0,0,0,0.07)">
                        <tr>
                          <td style="background:#0D1525;padding:28px 40px;text-align:center">
                            <p style="margin:0;font-size:22px;font-weight:800;color:#ffffff;letter-spacing:-0.5px">
                              agendo<span style="color:#3563E9">fy</span>
                            </p>
                          </td>
                        </tr>
                        <tr>
                          <td style="padding:36px 40px 32px">
                            <h1 style="margin:0 0 8px;font-size:20px;font-weight:800;color:#060A14">
                              📅 Novo agendamento recebido
                            </h1>
                            <p style="margin:0 0 24px;font-size:14px;color:#64748B">
                              Olá, <strong style="color:#060A14">{adminName}</strong>! Uma nova reserva foi feita no seu sistema.
                            </p>
                            <table width="100%" cellpadding="0" cellspacing="0" style="background:#F8FAFF;border-radius:12px;margin-bottom:20px">
                              <tr>
                                <td style="padding:8px 20px">
                                  <table width="100%" cellpadding="0" cellspacing="0">
                                    <tr>
                                      <td style="padding:10px 0;border-bottom:1px solid #E8ECF4">
                                        <span style="font-size:13px;color:#64748B">Cliente</span>
                                        <span style="float:right;font-size:14px;font-weight:700;color:#060A14">{clientName}</span>
                                      </td>
                                    </tr>
                                    <tr>
                                      <td style="padding:10px 0;border-bottom:1px solid #E8ECF4">
                                        <span style="font-size:13px;color:#64748B">Contato</span>
                                        <span style="float:right;font-size:14px;color:#060A14">{phoneDisplay}</span>
                                      </td>
                                    </tr>
                                    <tr>
                                      <td style="padding:10px 0;border-bottom:1px solid #E8ECF4">
                                        <span style="font-size:13px;color:#64748B">Serviço</span>
                                        <span style="float:right;font-size:14px;font-weight:600;color:#060A14">{serviceName}</span>
                                      </td>
                                    </tr>
                                    <tr>
                                      <td style="padding:10px 0;border-bottom:1px solid #E8ECF4">
                                        <span style="font-size:13px;color:#64748B">Data</span>
                                        <span style="float:right;font-size:14px;font-weight:600;color:#060A14">{dateStr}</span>
                                      </td>
                                    </tr>
                                    <tr>
                                      <td style="padding:10px 0">
                                        <span style="font-size:13px;color:#64748B">Horário</span>
                                        <span style="float:right;font-size:16px;font-weight:800;color:#3563E9">{timeStr}</span>
                                      </td>
                                    </tr>
                                  </table>
                                </td>
                              </tr>
                            </table>
                          </td>
                        </tr>
                        <tr>
                          <td style="background:#F8FAFF;border-top:1px solid #E8ECF4;padding:20px 40px;text-align:center">
                            <p style="margin:0;font-size:12px;color:#94A3B8">
                              Acesse o painel para fazer check-in após o atendimento.<br>
                              <a href="https://agendofy.com" style="color:#3563E9;text-decoration:none">agendofy.com</a>
                            </p>
                          </td>
                        </tr>
                      </table>
                    </td></tr>
                  </table>
                </body>
                </html>
                """
        };
        msg.To.Add(toEmail);
        await resend.EmailSendAsync(msg);
    }

    private static string Step(string num, string title, string desc) => $"""
        <table width="100%" cellpadding="0" cellspacing="0" style="margin-bottom:16px">
          <tr>
            <td width="36" valign="top">
              <div style="width:28px;height:28px;border-radius:8px;background:#EEF3FF;
                          text-align:center;line-height:28px;font-size:12px;font-weight:800;color:#3563E9">
                {num}
              </div>
            </td>
            <td valign="top" style="padding-left:12px">
              <p style="margin:0 0 2px;font-size:14px;font-weight:700;color:#060A14">{title}</p>
              <p style="margin:0;font-size:13px;color:#64748B;line-height:1.5">{desc}</p>
            </td>
          </tr>
        </table>
        """;
}
