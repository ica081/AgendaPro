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
        Console.WriteLine($"[EMAIL] Iniciando envio para {toEmail} via API Mailjet");

        // Lê as credenciais da API (chave pública e secreta)
        var apiKey = _configuration["EmailSettings:ApiKey"] ?? _configuration["EmailSettings__ApiKey"] ?? "";
        var apiSecret = _configuration["EmailSettings:ApiSecret"] ?? _configuration["EmailSettings__ApiSecret"] ?? "";
        var fromEmail = _configuration["EmailSettings:FromEmail"] ?? _configuration["EmailSettings__FromEmail"] ?? "seu-email-verificado@dominio.com";

        if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiSecret))
        {
            Console.WriteLine("[EMAIL] ERRO: Credenciais da API Mailjet não configuradas!");
            throw new Exception("Credenciais da API Mailjet não configuradas.");
        }

        // Construir o payload da API Mailjet
        var payload = new
        {
            Messages = new[]
            {
                new
                {
                    From = new { Email = fromEmail, Name = "AgendaPro" },
                    To = new[] { new { Email = toEmail, Name = "Cliente" } },
                    Subject = subject,
                    HTMLPart = htmlBody
                }
            }
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Autenticação Basic (chave pública + chave secreta)
        var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{apiKey}:{apiSecret}"));
        _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", auth);

        // URL da API Mailjet (versão 3.1)
        var url = "https://api.mailjet.com/v3.1/send";

        Console.WriteLine("[EMAIL] Enviando requisição para API Mailjet...");

        try
        {
            var response = await _httpClient.PostAsync(url, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[EMAIL] Erro na API: {response.StatusCode} - {responseBody}");
                throw new Exception($"Erro ao enviar e-mail: {response.StatusCode} - {responseBody}");
            }

            Console.WriteLine($"[EMAIL] E-mail enviado com sucesso! Resposta: {responseBody}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EMAIL] ERRO: {ex.Message}");
            throw;
        }
    }
}