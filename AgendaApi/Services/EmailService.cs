using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace AgendaApi.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;

    public EmailService(IConfiguration configuration, HttpClient httpClient)
    {
        _configuration = configuration;
        _httpClient = httpClient;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
    {
        Console.WriteLine($"[EMAIL] Iniciando envio para {toEmail} via API Brevo");

        // Lê as credenciais da API
        var apiKey = _configuration["EmailSettings:ApiKey"] ?? _configuration["EmailSettings__ApiKey"] ?? "";
        var senderEmail = _configuration["EmailSettings:SenderEmail"] ?? _configuration["EmailSettings__SenderEmail"] ?? "";
        var senderName = _configuration["EmailSettings:SenderName"] ?? _configuration["EmailSettings__SenderName"] ?? "AgendaPro";

        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("[EMAIL] ERRO: Chave de API do Brevo não configurada!");
            throw new Exception("Chave de API do Brevo não configurada.");
        }

        if (string.IsNullOrEmpty(senderEmail))
        {
            Console.WriteLine("[EMAIL] ERRO: E-mail remetente não configurado!");
            throw new Exception("E-mail remetente não configurado.");
        }

        // Constrói o payload da API Brevo (v3)
        var payload = new
        {
            sender = new { email = senderEmail, name = senderName },
            to = new[] { new { email = toEmail } },
            subject = subject,
            htmlContent = htmlBody
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("api-key", apiKey);

        var url = "https://api.brevo.com/v3/smtp/email";

        Console.WriteLine("[EMAIL] Enviando requisição para API Brevo...");

        try
        {
            var response = await _httpClient.PostAsync(url, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[EMAIL] Erro na API Brevo: {response.StatusCode} - {responseBody}");
                throw new Exception($"Erro ao enviar e-mail: {response.StatusCode} - {responseBody}");
            }

            Console.WriteLine($"[EMAIL] E-mail enviado com sucesso para {toEmail}!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EMAIL] ERRO: {ex.Message}");
            throw;
        }
    }
}