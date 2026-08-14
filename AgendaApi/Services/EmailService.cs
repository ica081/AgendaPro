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

        var smtpHost = _configuration["EmailSettings:SmtpHost"] ?? "smtp.gmail.com";
        var smtpPort = int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "587");
        var smtpUser = _configuration["EmailSettings:Username"] ?? "";
        var smtpPass = _configuration["EmailSettings:Password"] ?? "";

        Console.WriteLine($"[EMAIL] Host={smtpHost}, Porta={smtpPort}, User={smtpUser}");

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
            Console.WriteLine("[EMAIL] Conectando ao SMTP...");
            await client.ConnectAsync(smtpHost, smtpPort, MailKit.Security.SecureSocketOptions.StartTls);
            Console.WriteLine("[EMAIL] Conectado. Autenticando...");
            await client.AuthenticateAsync(smtpUser, smtpPass);
            Console.WriteLine("[EMAIL] Autenticado. Enviando mensagem...");
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
            Console.WriteLine("[EMAIL] Mensagem enviada com sucesso!");
        }
    }
}