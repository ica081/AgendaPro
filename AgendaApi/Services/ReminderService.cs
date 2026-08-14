using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using AgendaApi.Data;
using AgendaApi.Models;

namespace AgendaApi.Services;

public class ReminderService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ReminderService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(1); // verifica a cada 1 minuto

    public ReminderService(IServiceProvider serviceProvider, ILogger<ReminderService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ReminderService iniciado.");

        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessReminders(stoppingToken);
            await Task.Delay(_interval, stoppingToken);
        }

        _logger.LogInformation("ReminderService parado.");
    }

    private async Task ProcessReminders(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

            // Buscar agendamentos ativos com lembrete não enviado
            var now = DateTime.Now;

            // Carregar os agendamentos com suas empresas (para pegar as configurações)
            var appointments = await db.Appointments
                .Include(a => a.Company)
                .Include(a => a.Service)
                .Where(a => a.Status == "Active" && a.ReminderSent == false && a.Company != null)
                .ToListAsync(cancellationToken);

            foreach (var appointment in appointments)
            {
                var company = appointment.Company;
                if (company == null) continue;

                var settings = company.ReminderSettings;
                if (settings == null || !settings.Enabled) continue;

                // Calcular quando o lembrete deve ser enviado
                DateTime reminderTime;
                if (settings.SendTime == "DayOfAppointment")
                {
                    // Meia-noite do dia do serviço
                    reminderTime = appointment.StartTime.Date;
                }
                else // "BeforeAppointment"
                {
                    // Subtrair o offset do horário do agendamento
                    int offset = settings.OffsetValue;
                    if (settings.OffsetUnit == "Hours")
                        reminderTime = appointment.StartTime.AddHours(-offset);
                    else if (settings.OffsetUnit == "Days")
                        reminderTime = appointment.StartTime.AddDays(-offset);
                    else
                        continue; // unidade inválida
                }

                // Se já passou do horário de enviar o lembrete, envia
                if (reminderTime <= now)
                {
                    await SendReminderEmail(appointment, company, emailService);
                    appointment.ReminderSent = true;
                    await db.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation($"Lembrete enviado para agendamento {appointment.Id} - {appointment.ClientEmail}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar lembretes.");
        }
    }

    private async Task SendReminderEmail(Appointment appointment, Company company, IEmailService emailService)
    {
        string serviceName = appointment.Service?.Name ?? "Serviço";
        string subject = $"Lembrete: {serviceName} - {company.Name}";

        string body = $@"
            <h2>Lembrete do seu agendamento</h2>
            <p>Olá {appointment.ClientName},</p>
            <p>Este é um lembrete do seu agendamento de <strong>'{serviceName}'</strong> na empresa <strong>'{company.Name}'</strong>.</p>
            <p><strong>Data/Hora:</strong> {appointment.StartTime:dd/MM/yyyy HH:mm}</p>
            <p>Se precisar cancelar ou remarcar, entre em contato com a empresa.</p>
            <p>Atenciosamente,<br/>{company.Name}</p>
        ";

        try
        {
            await emailService.SendEmailAsync(appointment.ClientEmail, subject, body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Falha ao enviar lembrete para {appointment.ClientEmail}");
        }
    }
}