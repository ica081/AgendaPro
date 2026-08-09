using System;

namespace AgendaApi.Models
{
    public class Appointment
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid CompanyId { get; set; }
        public Company? Company { get; set; }

        public Guid? ServiceId { get; set; }
        public Service? Service { get; set; }

        public Guid? EmployeeId { get; set; }
        public Employee? Employee { get; set; }

        public string ClientName { get; set; } = "";
        public string ClientEmail { get; set; } = ""; // antes ClientPhone

        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        public string Status { get; set; } = "Pending"; // Pending, Active, Canceled

        public Guid? ClientUserId { get; set; }

        // Campos para confirmação
        public string? ConfirmationToken { get; set; }
        public DateTime? ConfirmationTokenExpiry { get; set; }
    }
}