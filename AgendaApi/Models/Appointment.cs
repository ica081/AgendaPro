namespace AgendaApi.Models;

public class Appointment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompanyId { get; set; }

    public Guid? EmployeeId { get; set; }   // FUNCIONÁRIO
    public Employee? Employee { get; set; }
    public Company? Company { get; set; } // <-- adicionar
    public Guid ServiceId { get; set; }
    public string ClientPhone { get; set; } = string.Empty;
    public Service? Service { get; set; }
    public Guid? ClientUserId { get; set; } // associar ao cliente logado (opcional)
    public string ClientName { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    
    
    
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }

    public string Status { get; set; } = "Active";
}
