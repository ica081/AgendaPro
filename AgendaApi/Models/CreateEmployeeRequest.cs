namespace AgendaApi.Models;

public class CreateEmployeeRequest
{
    public string Name { get; set; } = string.Empty;
    public string Specialty { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }
}
