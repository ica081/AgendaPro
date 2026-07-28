using System.Text.Json;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AgendaApi.Models;

public enum UserType
{
    Client,
    Company
}

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public UserType Type { get; set; } = UserType.Client;
    public Guid? ActiveCompanyId { get; set; }

    // ===== NOVOS CAMPOS DO PERFIL =====
    public string? Name { get; set; }           // Nome do cliente
    public string? Phone { get; set; }          // Telefone do cliente
    public string? PreferencesJson { get; set; } // Preferências em JSON

    [NotMapped]
    public Dictionary<string, object>? Preferences
    {
        get => PreferencesJson == null ? null : JsonSerializer.Deserialize<Dictionary<string, object>>(PreferencesJson);
        set => PreferencesJson = JsonSerializer.Serialize(value);
    }

    // Relacionamento com UserCompanies (para usuários do tipo Company)
    public ICollection<UserCompany> UserCompanies { get; set; } = new List<UserCompany>();
}