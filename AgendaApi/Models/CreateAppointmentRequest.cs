namespace AgendaApi.Models;

public class CreateAppointmentRequest
{
    public Guid EmployeeId { get; set; } // FUNCIONÁRIO
    public string ClientName { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public int DurationMinutes { get; set; }
}
