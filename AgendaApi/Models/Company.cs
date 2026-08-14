using System.Text.Json;
using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations.Schema;

namespace AgendaApi.Models;

public class Company
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public TimeSpan? OpeningTime { get; set; }
    public TimeSpan? ClosingTime { get; set; }
    public string? WorkScheduleJson { get; set; }

    [NotMapped]
    public WorkSchedule? WorkSchedule
    {
        get => WorkScheduleJson == null ? null : JsonSerializer.Deserialize<WorkSchedule>(WorkScheduleJson);
        set => WorkScheduleJson = JsonSerializer.Serialize(value);
    }

    // NOVO: Configurações de lembrete
    public string? ReminderSettingsJson { get; set; }

    [NotMapped]
    public ReminderSettings? ReminderSettings
    {
        get => ReminderSettingsJson == null ? null : JsonSerializer.Deserialize<ReminderSettings>(ReminderSettingsJson);
        set => ReminderSettingsJson = JsonSerializer.Serialize(value);
    }

    public ICollection<UserCompany> UserCompanies { get; set; } = new List<UserCompany>();
    public ICollection<Service> Services { get; set; } = new List<Service>();
    public ICollection<Employee> Employees { get; set; } = new List<Employee>();
    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}

public class WorkSchedule
{
    [JsonPropertyName("stepMinutes")]
    public int StepMinutes { get; set; } = 30;

    public Dictionary<int, DaySchedule> Days { get; set; } = new();
    public Dictionary<string, DaySchedule> Exceptions { get; set; } = new();

    [JsonPropertyName("allowMultiplePerClient")]
    public bool AllowMultiplePerClient { get; set; } = true;

    [JsonPropertyName("maxActiveAppointmentsPerClient")]
    public int MaxActiveAppointmentsPerClient { get; set; } = 0;

    [JsonPropertyName("allowSameDayMultiple")]
    public bool AllowSameDayMultiple { get; set; } = true;
}

public class DaySchedule
{
    public bool IsClosed { get; set; }
    public List<TimeRange> Periods { get; set; } = new();
}

public class TimeRange
{
    [JsonPropertyName("start")]
    public string Start { get; set; } = "";

    [JsonPropertyName("end")]
    public string End { get; set; } = "";
}