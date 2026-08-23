using MailKit.Net.Smtp;
using MimeKit;
using Microsoft.Extensions.Configuration;

namespace AgendaApi.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;

    public EmailService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
    {
        Console.WriteLine($"[EMAIL] Iniciando envio para {toEmail}");

        // Lê as configurações com fallback
        var smtpHost = _configuration["EmailSettings:SmtpHost"] ?? _configuration["EmailSettings__SmtpHost"] ?? "smtp.gmail.com";
        var smtpPortStr = _configuration["EmailSettings:SmtpPort"] ?? _configuration["EmailSettings__SmtpPort"] ?? "587";
        var smtpUser = _configuration["EmailSettings:Username"] ?? _configuration["EmailSettings__Username"] ?? "";
        var smtpPass = _configuration["EmailSettings:Password"] ?? _configuration["EmailSettings__Password"] ?? "";

        if (!int.TryParse(smtpPortStr, out var smtpPort))
            smtpPort = 587;

        Console.WriteLine($"[EMAIL] Host={smtpHost}, Porta={smtpPort}, User={smtpUser}, Pass={string.IsNullOrEmpty(smtpPass) ? "NÃO DEFINIDA" : "***"}");

        if (string.IsNullOrEmpty(smtpUser) || string.IsNullOrEmpty(smtpPass))
        {
            Console.WriteLine("[EMAIL] ERRO: Credenciais não configuradas!");
            throw new Exception("Credenciais de e-mail não configuradas.");
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("AgendaPro", smtpUser));
        message.To.Add(new MailboxAddress("Cliente", toEmail));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = htmlBody };

        using (var client = new SmtpClient())
        {
            // Aumenta o timeout para 15 segundos
            client.Timeout = 15000;

            Console.WriteLine("[EMAIL] Conectando ao SMTP...");
            try
            {
                await client.ConnectAsync(smtpHost, smtpPort, MailKit.Security.SecureSocketOptions.StartTls);
                Console.WriteLine("[EMAIL] Conectado. Autenticando...");
                await client.AuthenticateAsync(smtpUser, smtpPass);
                Console.WriteLine("[EMAIL] Autenticado. Enviando mensagem...");
                await client.SendAsync(message);
                await client.DisconnectAsync(true);
                Console.WriteLine("[EMAIL] Mensagem enviada com sucesso!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EMAIL] ERRO: {ex.Message}");
                throw; // Re-lança para o chamador lidar
            }
        }
    }
}