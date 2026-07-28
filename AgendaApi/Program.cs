using AgendaApi.Models;
using AgendaApi.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=agenda.db"));

builder.Services.AddCors(options =>
{
    options.AddPolicy("DevCors", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var jwtKey = "CHAVE_SUPER_SECRETA_AGENDA_API_123456";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddOpenApi();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseCors("DevCors");
app.UseAuthentication();
app.UseAuthorization();

// =======================
// AUTH
// =======================
app.MapPost("/auth/register", (RegisterRequest request, AppDbContext db) =>
{
    if (db.Users.Any(u => u.Email == request.Email))
        return Results.BadRequest("Email já existe");

    var userType = Enum.TryParse<UserType>(request.Type, true, out var type) ? type : UserType.Client;

    var user = new User
    {
        Email = request.Email,
        Password = BCrypt.Net.BCrypt.HashPassword(request.Password),
        ActiveCompanyId = null,
        Type = userType
    };
    db.Users.Add(user);
    db.SaveChanges();
    return Results.Ok("Registrado");
});

app.MapPost("/auth/login", (LoginRequest request, AppDbContext db) =>
{
    var user = db.Users.FirstOrDefault(u => u.Email == request.Email);
    if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
        return Results.BadRequest("Login inválido");

    var claims = new List<Claim>
    {
        new Claim("userId", user.Id.ToString()),
        new Claim("companyId", user.ActiveCompanyId?.ToString() ?? ""),
        new Claim("userType", user.Type.ToString())
    };

    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    var token = new JwtSecurityToken(claims: claims, expires: DateTime.UtcNow.AddHours(12), signingCredentials: creds);
    var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

    return Results.Ok(new { 
        token = tokenString, 
        companyId = user.ActiveCompanyId,
        userType = user.Type.ToString()
    });
});

// =======================
// EMPRESAS
// =======================
app.MapPost("/companies", async (CreateCompanyRequest request, AppDbContext db, ClaimsPrincipal user) =>
{
    var userIdClaim = user.FindFirst("userId")?.Value;
    if (!Guid.TryParse(userIdClaim, out var userId))
        return Results.BadRequest("Usuário inválido");

    var userType = user.FindFirst("userType")?.Value;
    if (userType != "Company")
        return Results.BadRequest("Apenas contas Empresa podem criar empresas.");

    var company = new Company
    {
        Name = request.Name,
        Category = request.Category
    };
    db.Companies.Add(company);
    await db.SaveChangesAsync();

    var userCompany = new UserCompany { UserId = userId, CompanyId = company.Id };
    db.UserCompanies.Add(userCompany);
    await db.SaveChangesAsync();

    var dbUser = await db.Users.FindAsync(userId);
    if (dbUser != null)
    {
        dbUser.ActiveCompanyId = company.Id;
        await db.SaveChangesAsync();
    }

    var claims = new List<Claim>
    {
        new Claim("userId", userId.ToString()),
        new Claim("companyId", company.Id.ToString()),
        new Claim("userType", "Company")
    };
    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    var newToken = new JwtSecurityToken(claims: claims, expires: DateTime.UtcNow.AddHours(12), signingCredentials: creds);
    var tokenString = new JwtSecurityTokenHandler().WriteToken(newToken);

    return Results.Ok(new { token = tokenString, companyId = company.Id, company });
}).RequireAuthorization();

app.MapGet("/companies", async (AppDbContext db, ClaimsPrincipal user) =>
{
    var userIdClaim = user.FindFirst("userId")?.Value;
    if (!Guid.TryParse(userIdClaim, out var userId))
        return Results.BadRequest("Usuário inválido");

    var companies = await db.UserCompanies
        .Where(uc => uc.UserId == userId)
        .Select(uc => uc.Company)
        .ToListAsync();

    var result = companies.Select(c => new
    {
        c.Id,
        c.Name,
        c.Category,
        WorkSchedule = c.WorkSchedule
    });

    return Results.Ok(result);
}).RequireAuthorization();

app.MapPost("/companies/select", async (SelectCompanyRequest request, AppDbContext db, ClaimsPrincipal user) =>
{
    var userIdClaim = user.FindFirst("userId")?.Value;
    if (!Guid.TryParse(userIdClaim, out var userId))
        return Results.BadRequest("Usuário inválido");

    var userRecord = await db.Users.FindAsync(userId);
    if (userRecord == null) return Results.NotFound();

    var hasAccess = await db.UserCompanies.AnyAsync(uc => uc.UserId == userId && uc.CompanyId == request.CompanyId);
    if (!hasAccess) return Results.Forbid();

    userRecord.ActiveCompanyId = request.CompanyId;
    await db.SaveChangesAsync();

    var claims = new List<Claim>
    {
        new Claim("userId", userId.ToString()),
        new Claim("companyId", request.CompanyId.ToString()),
        new Claim("userType", userRecord.Type.ToString())
    };
    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    var token = new JwtSecurityToken(claims: claims, expires: DateTime.UtcNow.AddHours(12), signingCredentials: creds);
    var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

    return Results.Ok(new { token = tokenString, companyId = request.CompanyId });
}).RequireAuthorization();

app.MapPut("/companies/settings", async (UpdateCompanySettingsRequest request, AppDbContext db, ClaimsPrincipal user) =>
{
    var companyId = GetCompanyId(user);
    if (companyId == null)
        return Results.BadRequest("Usuário sem empresa.");

    var company = await db.Companies.FindAsync(companyId);
    if (company == null)
        return Results.NotFound("Empresa não encontrada.");

    if (request.WorkSchedule != null)
        company.WorkSchedule = request.WorkSchedule;

    if (request.OpeningTime.HasValue) company.OpeningTime = request.OpeningTime;
    if (request.ClosingTime.HasValue) company.ClosingTime = request.ClosingTime;

    await db.SaveChangesAsync();
    return Results.Ok(company);
}).RequireAuthorization();

app.MapPut("/companies/{id}", async (Guid id, UpdateCompanyRequest request, AppDbContext db, ClaimsPrincipal user) =>
{
    var userIdClaim = user.FindFirst("userId")?.Value;
    if (!Guid.TryParse(userIdClaim, out var userId))
        return Results.BadRequest("Usuário inválido");

    var company = await db.Companies.FirstOrDefaultAsync(c => c.Id == id && db.UserCompanies.Any(uc => uc.UserId == userId && uc.CompanyId == id));
    if (company == null)
        return Results.NotFound("Empresa não encontrada ou você não tem permissão.");

    company.Name = request.Name ?? company.Name;
    company.Category = request.Category ?? company.Category;

    await db.SaveChangesAsync();
    return Results.Ok(company);
}).RequireAuthorization();

// =======================
// HELPERS
// =======================
Guid? GetCompanyId(ClaimsPrincipal user)
{
    var claim = user.FindFirst("companyId")?.Value;
    if (Guid.TryParse(claim, out var cid)) return cid;
    return null;
}

List<TimeRange> GetAvailablePeriods(Company company, DateTime date)
{
    var schedule = company.WorkSchedule;
    if (schedule == null) return new List<TimeRange>();

    var dateKey = date.Date.ToString("yyyy-MM-dd");
    if (schedule.Exceptions.TryGetValue(dateKey, out var exception))
    {
        if (exception.IsClosed) return new List<TimeRange>();
        return exception.Periods;
    }

    var dayOfWeek = (int)date.DayOfWeek;
    if (!schedule.Days.TryGetValue(dayOfWeek, out var daySchedule))
        return new List<TimeRange>();

    if (daySchedule.IsClosed) return new List<TimeRange>();
    return daySchedule.Periods;
}

List<DateTime> GenerateSlots(Company company, DateTime date)
{
    var periods = GetAvailablePeriods(company, date);
    if (periods.Count == 0) return new List<DateTime>();

    var step = TimeSpan.FromMinutes(company.WorkSchedule?.StepMinutes ?? 30);
    var slots = new List<DateTime>();

    foreach (var period in periods)
    {
        string startStr = period.Start?.ToString() ?? "";
        string endStr = period.End?.ToString() ?? "";

        if (string.IsNullOrWhiteSpace(startStr) || string.IsNullOrWhiteSpace(endStr))
            continue;

        if (!TimeSpan.TryParse(startStr, out var startTime) || !TimeSpan.TryParse(endStr, out var endTime))
            continue;

        var current = date.Date + startTime;
        var end = date.Date + endTime;

        while (current < end)
        {
            slots.Add(current);
            current = current.Add(step);
        }
    }
    return slots;
}

// =======================
// VALIDAÇÃO DE CLIENTE (por telefone)
// =======================
async Task<string?> ValidateClientAppointments(AppDbContext db, string clientPhone, Guid companyId, Guid? excludeAppointmentId = null, DateTime? newStartTime = null)
{
    if (string.IsNullOrWhiteSpace(clientPhone))
        return null;

    var query = db.Appointments
        .Where(a => a.CompanyId == companyId &&
                    a.Status == "Active" &&
                    a.ClientPhone == clientPhone);

    if (excludeAppointmentId.HasValue)
        query = query.Where(a => a.Id != excludeAppointmentId.Value);

    var activeAppointments = await query.ToListAsync();

    var company = await db.Companies.FindAsync(companyId);
    var schedule = company?.WorkSchedule;

    if (schedule == null)
        return null;

    if (!schedule.AllowMultiplePerClient && activeAppointments.Count > 0)
        return "Cliente já possui um agendamento ativo. Não é permitido múltiplos agendamentos.";

    if (schedule.MaxActiveAppointmentsPerClient > 0 &&
        activeAppointments.Count >= schedule.MaxActiveAppointmentsPerClient)
        return $"Cliente já possui {activeAppointments.Count} agendamentos ativos (limite: {schedule.MaxActiveAppointmentsPerClient}).";

    if (!schedule.AllowSameDayMultiple && newStartTime.HasValue)
    {
        var sameDay = activeAppointments.Any(a => a.StartTime.Date == newStartTime.Value.Date);
        if (sameDay)
            return "Cliente já possui um agendamento neste dia. Não é permitido múltiplos no mesmo dia.";
    }

    return null;
}

// =======================
// ENDPOINT PARA CLIENTE LISTAR SEUS AGENDAMENTOS (ativos)
// =======================
app.MapGet("/client/appointments", async (AppDbContext db, ClaimsPrincipal user) =>
{
    var userIdClaim = user.FindFirst("userId")?.Value;
    if (!Guid.TryParse(userIdClaim, out var userId))
        return Results.BadRequest("Usuário inválido");

    var appointments = await db.Appointments
        .Include(a => a.Service)
        .Include(a => a.Employee)
        .Include(a => a.Company)
        .Where(a => a.ClientUserId == userId && a.Status == "Active")
        .Select(a => new {
            a.Id,
            a.ClientName,
            a.ClientPhone,
            a.StartTime,
            a.EndTime,
            a.Status,
            ServiceName = a.Service != null ? a.Service.Name : "Serviço",
            EmployeeName = a.Employee != null ? a.Employee.Name : "Sem funcionário",
            CompanyName = a.Company != null ? a.Company.Name : "Empresa"
        })
        .OrderBy(a => a.StartTime)
        .ToListAsync();

    return Results.Ok(appointments);
}).RequireAuthorization();

// =======================
// PERFIL DO CLIENTE
// =======================
app.MapGet("/client/profile", async (AppDbContext db, ClaimsPrincipal user) =>
{
    var userIdClaim = user.FindFirst("userId")?.Value;
    if (!Guid.TryParse(userIdClaim, out var userId))
        return Results.BadRequest("Usuário inválido");

    var userRecord = await db.Users.FindAsync(userId);
    if (userRecord == null) return Results.NotFound();

    return Results.Ok(new
    {
        userRecord.Id,
        userRecord.Email,
        userRecord.Name,
        userRecord.Phone,
        userRecord.Preferences,
        userRecord.Type
    });
}).RequireAuthorization();

app.MapPut("/client/profile", async (UpdateClientProfileRequest request, AppDbContext db, ClaimsPrincipal user) =>
{
    var userIdClaim = user.FindFirst("userId")?.Value;
    if (!Guid.TryParse(userIdClaim, out var userId))
        return Results.BadRequest("Usuário inválido");

    var userRecord = await db.Users.FindAsync(userId);
    if (userRecord == null) return Results.NotFound();

    if (!string.IsNullOrWhiteSpace(request.Name))
        userRecord.Name = request.Name;
    if (!string.IsNullOrWhiteSpace(request.Phone))
        userRecord.Phone = request.Phone;
    if (request.Preferences != null)
        userRecord.Preferences = request.Preferences;

    await db.SaveChangesAsync();
    return Results.Ok(new { message = "Perfil atualizado com sucesso." });
}).RequireAuthorization();

// =======================
// HISTÓRICO DO CLIENTE COM FILTROS
// =======================
app.MapGet("/client/history", async (string? status, string? startDate, string? endDate, AppDbContext db, ClaimsPrincipal user) =>
{
    var userIdClaim = user.FindFirst("userId")?.Value;
    if (!Guid.TryParse(userIdClaim, out var userId))
        return Results.BadRequest("Usuário inválido");

    var query = db.Appointments
        .Include(a => a.Service)
        .Include(a => a.Employee)
        .Include(a => a.Company)
        .Where(a => a.ClientUserId == userId);

    if (!string.IsNullOrEmpty(status))
        query = query.Where(a => a.Status == status);

    if (!string.IsNullOrEmpty(startDate) && DateTime.TryParse(startDate, out var start))
        query = query.Where(a => a.StartTime >= start);

    if (!string.IsNullOrEmpty(endDate) && DateTime.TryParse(endDate, out var end))
        query = query.Where(a => a.StartTime <= end.AddDays(1));

    var appointments = await query
        .OrderByDescending(a => a.StartTime)
        .Select(a => new
        {
            a.Id,
            a.ClientName,
            a.ClientPhone,
            a.StartTime,
            a.EndTime,
            a.Status,
            ServiceName = a.Service != null ? a.Service.Name : "Serviço",
            EmployeeName = a.Employee != null ? a.Employee.Name : "Sem funcionário",
            CompanyName = a.Company != null ? a.Company.Name : "Empresa"
        })
        .ToListAsync();

    return Results.Ok(appointments);
}).RequireAuthorization();

// =======================
// SERVICES (ADMIN)
// =======================
app.MapGet("/companies/{companyId}/services", async (Guid companyId, AppDbContext db, ClaimsPrincipal user) =>
{
    var userCid = GetCompanyId(user);
    if (userCid == null) return Results.BadRequest("Usuário sem empresa.");
    if (userCid != companyId) return Results.Forbid();
    var services = await db.Services.Where(s => s.CompanyId == companyId).ToListAsync();
    return Results.Ok(services);
}).RequireAuthorization();

app.MapGet("/services", async (AppDbContext db, ClaimsPrincipal user) =>
{
    var companyId = GetCompanyId(user);
    if (companyId == null) 
        return Results.BadRequest("Usuário sem empresa.");
    var services = await db.Services.Where(s => s.CompanyId == companyId).ToListAsync();
    return Results.Ok(services);
}).RequireAuthorization();

app.MapPost("/services", async (CreateServiceRequest request, AppDbContext db, ClaimsPrincipal user) =>
{
    var companyId = GetCompanyId(user);
    if (companyId == null) 
        return Results.BadRequest("Usuário sem empresa.");

    var service = new Service
    {
        Name = request.Name,
        Price = request.Price,
        DurationMinutes = request.DurationMinutes,
        CompanyId = companyId.Value
    };

    db.Services.Add(service);
    await db.SaveChangesAsync();
    return Results.Ok(service);
}).RequireAuthorization();

app.MapDelete("/services/{id}", async (Guid id, AppDbContext db, ClaimsPrincipal user) =>
{
    var companyId = GetCompanyId(user);
    if (companyId == null) 
        return Results.BadRequest("Usuário sem empresa.");
    var service = await db.Services.FirstOrDefaultAsync(s => s.Id == id && s.CompanyId == companyId);
    if (service == null) return Results.NotFound();
    db.Services.Remove(service);
    await db.SaveChangesAsync();
    return Results.Ok();
}).RequireAuthorization();

// =======================
// EMPLOYEES (ADMIN)
// =======================
app.MapGet("/employees", async (AppDbContext db, ClaimsPrincipal user) =>
{
    var companyId = GetCompanyId(user);
    if (companyId == null) 
        return Results.BadRequest("Usuário sem empresa.");
    var employees = await db.Employees.Where(e => e.CompanyId == companyId).ToListAsync();
    return Results.Ok(employees);
}).RequireAuthorization();

app.MapPost("/employees", async (CreateEmployeeRequest request, AppDbContext db, ClaimsPrincipal user) =>
{
    var companyId = GetCompanyId(user);
    if (companyId == null) 
        return Results.BadRequest("Usuário sem empresa.");
    var employee = new Employee
    {
        Name = request.Name,
        Specialty = request.Specialty,
        PhotoUrl = request.PhotoUrl,
        CompanyId = companyId.Value
    };
    db.Employees.Add(employee);
    await db.SaveChangesAsync();
    return Results.Ok(employee);
}).RequireAuthorization();

app.MapPut("/employees/{id}", async (Guid id, UpdateEmployeeRequest request, AppDbContext db, ClaimsPrincipal user) =>
{
    var companyId = GetCompanyId(user);
    if (companyId == null)
        return Results.BadRequest("Usuário sem empresa.");

    var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == id && e.CompanyId == companyId);
    if (employee == null)
        return Results.NotFound("Funcionário não encontrado.");

    employee.Name = request.Name ?? employee.Name;
    employee.Specialty = request.Specialty ?? employee.Specialty;
    employee.PhotoUrl = request.PhotoUrl ?? employee.PhotoUrl;

    await db.SaveChangesAsync();
    return Results.Ok(employee);
}).RequireAuthorization();

app.MapDelete("/employees/{id}", async (Guid id, AppDbContext db, ClaimsPrincipal user) =>
{
    var companyId = GetCompanyId(user);
    if (companyId == null)
        return Results.BadRequest("Usuário sem empresa.");

    var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == id && e.CompanyId == companyId);
    if (employee == null)
        return Results.NotFound("Funcionário não encontrado.");

    db.Employees.Remove(employee);
    await db.SaveChangesAsync();
    return Results.Ok();
}).RequireAuthorization();

// =======================
// APPOINTMENTS (ADMIN)
// =======================
app.MapGet("/companies/{companyId}/schedules", async (Guid companyId, AppDbContext db, ClaimsPrincipal user) =>
{
    var userCid = GetCompanyId(user);
    if (userCid == null) return Results.BadRequest("Usuário sem empresa.");
    if (userCid != companyId) return Results.Forbid();

    var appointments = await db.Appointments
        .Where(a => a.CompanyId == companyId && a.Status == "Active")
        .Select(a => new {
            a.Id,
            a.ClientName,
            a.ClientPhone,
            a.StartTime,
            a.EndTime,
            a.Status,
            EmployeeName = a.Employee != null ? a.Employee.Name : "Sem funcionário",
            EmployeeId = a.EmployeeId,
            ServiceId = a.ServiceId
        })
        .ToListAsync();

    return Results.Ok(appointments);
}).RequireAuthorization();

// =======================
// AGENDAMENTOS DA SEMANA
// =======================
app.MapGet("/companies/{companyId}/schedules/week", async (Guid companyId, string start, string end, AppDbContext db, ClaimsPrincipal user) =>
{
    var userType = user.FindFirst("userType")?.Value;

    if (userType != "Client")
    {
        var userCid = GetCompanyId(user);
        if (userCid == null) 
            return Results.BadRequest("Usuário sem empresa ativa.");
        if (userCid != companyId) 
            return Results.BadRequest("Você não tem permissão para acessar esta empresa.");
    }

    if (!DateTime.TryParse(start, out var startDate) || !DateTime.TryParse(end, out var endDate))
        return Results.BadRequest("Datas inválidas.");

    var endDateInclusive = endDate.AddDays(1);

    var appointments = await db.Appointments
        .Include(a => a.Employee)
        .Where(a => a.CompanyId == companyId &&
                    a.Status == "Active" &&
                    a.StartTime >= startDate &&
                    a.StartTime < endDateInclusive)
        .Select(a => new {
            a.Id,
            a.ClientName,
            a.StartTime,
            a.EmployeeId,
            EmployeeName = a.Employee != null ? a.Employee.Name : "Sem funcionário"
        })
        .ToListAsync();

    return Results.Ok(appointments);
}).RequireAuthorization();

app.MapGet("/companies/{companyId}/slots", async (Guid companyId, string date, AppDbContext db, ClaimsPrincipal user) =>
{
    var userCid = GetCompanyId(user);
    if (userCid == null || userCid != companyId) return Results.Forbid();

    if (!DateTime.TryParse(date, out var day))
        return Results.BadRequest("Data inválida.");

    var company = await db.Companies.FindAsync(companyId);
    if (company == null) return Results.NotFound();

    var slots = GenerateSlots(company, day);
    return Results.Ok(slots.Select(s => s.ToString("HH:mm")));
}).RequireAuthorization();

// =======================
// EXCLUIR EMPRESA
// =======================
app.MapDelete("/companies/{id}", async (Guid id, AppDbContext db, ClaimsPrincipal user) =>
{
    var userIdClaim = user.FindFirst("userId")?.Value;
    if (!Guid.TryParse(userIdClaim, out var userId))
        return Results.BadRequest("Usuário inválido");

    var userCompany = await db.UserCompanies
        .FirstOrDefaultAsync(uc => uc.UserId == userId && uc.CompanyId == id);
    if (userCompany == null)
        return Results.NotFound("Empresa não encontrada ou você não tem permissão.");

    var company = await db.Companies
        .Include(c => c.Services)
        .Include(c => c.Employees)
        .Include(c => c.Appointments)
        .FirstOrDefaultAsync(c => c.Id == id);
    if (company == null)
        return Results.NotFound("Empresa não encontrada.");

    db.UserCompanies.Remove(userCompany);
    db.Appointments.RemoveRange(company.Appointments);
    db.Services.RemoveRange(company.Services);
    db.Employees.RemoveRange(company.Employees);
    db.Companies.Remove(company);

    await db.SaveChangesAsync();

    var dbUser = await db.Users.FindAsync(userId);
    if (dbUser != null && dbUser.ActiveCompanyId == id)
    {
        dbUser.ActiveCompanyId = null;
        await db.SaveChangesAsync();
    }

    return Results.Ok(new { message = "Empresa excluída com sucesso." });
}).RequireAuthorization();

// =======================
// APPOINTMENTS (CRIAR E EDITAR)
// =======================
app.MapPost("/schedules", async (CreateScheduleRequest request, AppDbContext db, ClaimsPrincipal user) =>
{
    var companyId = GetCompanyId(user);
    if (companyId == null) 
        return Results.BadRequest("Usuário sem empresa.");

    var company = await db.Companies.FindAsync(companyId);
    if (company == null)
        return Results.BadRequest("Empresa não encontrada.");

    var service = await db.Services.FirstOrDefaultAsync(s => s.Id == request.ServiceId && s.CompanyId == companyId);
    if (service == null) 
        return Results.BadRequest("Serviço inválido.");

    var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == request.EmployeeId && e.CompanyId == companyId);
    if (employee == null)
        return Results.BadRequest("Funcionário inválido.");

    DateTime startTime = request.StartTime;
    if (startTime.Kind == DateTimeKind.Utc)
        startTime = startTime.ToLocalTime();

    var endTime = startTime.AddMinutes(service.DurationMinutes);

    var slots = GenerateSlots(company, startTime);
    if (slots.Count == 0)
        return Results.BadRequest("Empresa fechada neste dia.");
    if (!slots.Any(s => s == startTime))
        return Results.BadRequest("Horário não disponível para agendamento.");

    var validationError = await ValidateClientAppointments(db, request.ClientPhone, companyId.Value, null, startTime);
    if (validationError != null)
        return Results.BadRequest(validationError);

    var hasConflict = await db.Appointments.AnyAsync(a =>
        a.CompanyId == companyId &&
        a.EmployeeId == request.EmployeeId &&
        a.Status != "Canceled" &&
        a.StartTime < endTime &&
        a.EndTime > startTime
    );

    if (hasConflict)
        return Results.BadRequest("Este funcionário já tem um agendamento neste horário.");

    var appointment = new Appointment
    {
        CompanyId = companyId.Value,
        ServiceId = request.ServiceId,
        EmployeeId = request.EmployeeId,
        ClientName = request.ClientName,
        ClientPhone = request.ClientPhone,
        StartTime = startTime,
        EndTime = endTime,
        Status = "Active"
    };

    db.Appointments.Add(appointment);
    await db.SaveChangesAsync();
    return Results.Ok(appointment);
}).RequireAuthorization();

app.MapPut("/appointments/{id}", async (Guid id, UpdateAppointmentRequest request, AppDbContext db, ClaimsPrincipal user) =>
{
    var companyId = GetCompanyId(user);
    if (companyId == null)
        return Results.BadRequest("Usuário sem empresa.");

    var appointment = await db.Appointments
        .Include(a => a.Service)
        .FirstOrDefaultAsync(a => a.Id == id && a.CompanyId == companyId);

    if (appointment == null)
        return Results.NotFound("Agendamento não encontrado.");
    if (appointment.Status == "Canceled")
        return Results.BadRequest("Não é possível editar cancelado.");

    var company = await db.Companies.FindAsync(companyId);
    if (company == null)
        return Results.BadRequest("Empresa não encontrada.");

    if (request.ServiceId.HasValue && request.ServiceId != appointment.ServiceId)
    {
        var service = await db.Services.FirstOrDefaultAsync(s => s.Id == request.ServiceId && s.CompanyId == companyId);
        if (service == null)
            return Results.BadRequest("Serviço inválido.");
        appointment.ServiceId = service.Id;
        appointment.Service = service;
    }

    if (request.EmployeeId.HasValue && request.EmployeeId != appointment.EmployeeId)
    {
        var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == request.EmployeeId && e.CompanyId == companyId);
        if (employee == null)
            return Results.BadRequest("Funcionário inválido.");
        appointment.EmployeeId = request.EmployeeId.Value;
    }

    DateTime startTime = request.StartTime ?? appointment.StartTime;
    if (startTime.Kind == DateTimeKind.Utc)
        startTime = startTime.ToLocalTime();

    int durationMinutes = appointment.Service?.DurationMinutes ?? 30;
    var endTime = startTime.AddMinutes(durationMinutes);

    var slots = GenerateSlots(company, startTime);
    if (!slots.Any(s => s == startTime))
        return Results.BadRequest("Horário não disponível para agendamento.");

    var clientPhone = request.ClientPhone ?? appointment.ClientPhone;
    var validationError = await ValidateClientAppointments(db, clientPhone, companyId.Value, id, startTime);
    if (validationError != null)
        return Results.BadRequest(validationError);

    var hasConflict = await db.Appointments.AnyAsync(a =>
        a.CompanyId == companyId &&
        a.Id != id &&
        a.EmployeeId == appointment.EmployeeId &&
        a.Status != "Canceled" &&
        a.StartTime < endTime &&
        a.EndTime > startTime
    );
    if (hasConflict)
        return Results.BadRequest("Conflito de horário com outro agendamento.");

    appointment.ClientName = request.ClientName ?? appointment.ClientName;
    appointment.ClientPhone = request.ClientPhone ?? appointment.ClientPhone;
    appointment.StartTime = startTime;
    appointment.EndTime = endTime;

    await db.SaveChangesAsync();
    return Results.Ok(appointment);
}).RequireAuthorization();

// =======================
// DELETE (soft) - cancelamento (PARA EMPRESA E CLIENTE)
// =======================
app.MapDelete("/appointments/{id}", async (Guid id, AppDbContext db, ClaimsPrincipal user) =>
{
    var userIdClaim = user.FindFirst("userId")?.Value;
    if (!Guid.TryParse(userIdClaim, out var userId))
        return Results.BadRequest("Usuário inválido");

    var userType = user.FindFirst("userType")?.Value;

    Appointment? ap = null;

    if (userType == "Company")
    {
        var companyId = GetCompanyId(user);
        if (companyId == null)
            return Results.BadRequest("Usuário sem empresa.");
        ap = await db.Appointments.FirstOrDefaultAsync(a => a.Id == id && a.CompanyId == companyId);
        if (ap == null)
            return Results.NotFound("Agendamento não encontrado ou você não tem permissão.");
    }
    else // Client (ou fallback)
    {
        ap = await db.Appointments.FirstOrDefaultAsync(a => a.Id == id && a.ClientUserId == userId);
        if (ap == null)
            return Results.NotFound("Agendamento não encontrado ou você não tem permissão.");
    }

    if (ap.Status == "Canceled")
        return Results.BadRequest("Este agendamento já foi cancelado.");

    ap.Status = "Canceled";
    await db.SaveChangesAsync();
    return Results.Ok("Cancelado");
}).RequireAuthorization();

// =======================
// ENDPOINT PARA CLIENTE LOGADO CRIAR AGENDAMENTO (NOVO)
// =======================
app.MapPost("/client/schedules/{companyId}", async (Guid companyId, CreateScheduleRequest request, AppDbContext db, ClaimsPrincipal user) =>
{
    var userIdClaim = user.FindFirst("userId")?.Value;
    if (!Guid.TryParse(userIdClaim, out var userId))
        return Results.BadRequest("Usuário inválido");

    var company = await db.Companies.FindAsync(companyId);
    if (company == null) return Results.BadRequest("Empresa não encontrada.");

    var service = await db.Services.FirstOrDefaultAsync(s => s.Id == request.ServiceId && s.CompanyId == companyId);
    if (service == null) return Results.BadRequest("Serviço inválido.");

    var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == request.EmployeeId && e.CompanyId == companyId);
    if (employee == null) return Results.BadRequest("Funcionário inválido.");

    DateTime startTime = request.StartTime;
    if (startTime.Kind == DateTimeKind.Utc)
        startTime = startTime.ToLocalTime();

    var endTime = startTime.AddMinutes(service.DurationMinutes);

    var slots = GenerateSlots(company, startTime);
    if (slots.Count == 0 || !slots.Any(s => s == startTime))
        return Results.BadRequest("Horário não disponível.");

    var validationError = await ValidateClientAppointments(db, request.ClientPhone, companyId, null, startTime);
    if (validationError != null)
        return Results.BadRequest(validationError);

    var hasConflict = await db.Appointments.AnyAsync(a =>
        a.CompanyId == companyId &&
        a.EmployeeId == request.EmployeeId &&
        a.Status != "Canceled" &&
        a.StartTime < endTime &&
        a.EndTime > startTime
    );
    if (hasConflict)
        return Results.BadRequest("Este funcionário já tem um agendamento neste horário.");

    var appointment = new Appointment
    {
        CompanyId = companyId,
        ServiceId = request.ServiceId,
        EmployeeId = request.EmployeeId,
        ClientName = request.ClientName,
        ClientPhone = request.ClientPhone,
        StartTime = startTime,
        EndTime = endTime,
        Status = "Active",
        ClientUserId = userId // associa ao cliente logado
    };

    db.Appointments.Add(appointment);
    await db.SaveChangesAsync();
    return Results.Ok(appointment);
}).RequireAuthorization();

// =======================
// ENDPOINTS PÚBLICOS (sem autenticação para agendamento anônimo)
// =======================
app.MapGet("/public/companies", async (AppDbContext db) =>
{
    var companies = await db.Companies
        .Where(c => c.UserCompanies.Any())
        .ToListAsync();
    return Results.Ok(companies);
});

app.MapGet("/public/company/{id}", async (Guid id, AppDbContext db) =>
{
    var company = await db.Companies.FindAsync(id);
    if (company == null) return Results.NotFound();
    return Results.Ok(new { company.Id, company.Name, company.Category, company.WorkSchedule });
});

app.MapGet("/public/services", async (Guid companyId, AppDbContext db) =>
{
    var services = await db.Services.Where(s => s.CompanyId == companyId).ToListAsync();
    return Results.Ok(services);
});

app.MapGet("/public/employees", async (Guid companyId, AppDbContext db) =>
{
    var employees = await db.Employees.Where(e => e.CompanyId == companyId).ToListAsync();
    return Results.Ok(employees);
});

app.MapGet("/public/slots", async (Guid companyId, string date, AppDbContext db) =>
{
    if (!DateTime.TryParse(date, out var day))
        return Results.BadRequest("Data inválida.");
    var company = await db.Companies.FindAsync(companyId);
    if (company == null) return Results.NotFound();
    var slots = GenerateSlots(company, day);
    return Results.Ok(slots.Select(s => s.ToString("HH:mm")));
});

app.MapPost("/public/schedules", async (Guid companyId, CreateScheduleRequest request, AppDbContext db) =>
{
    var company = await db.Companies.FindAsync(companyId);
    if (company == null) return Results.BadRequest("Empresa não encontrada.");

    var service = await db.Services.FirstOrDefaultAsync(s => s.Id == request.ServiceId && s.CompanyId == companyId);
    if (service == null) return Results.BadRequest("Serviço inválido.");

    var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == request.EmployeeId && e.CompanyId == companyId);
    if (employee == null) return Results.BadRequest("Funcionário inválido.");

    DateTime startTime = request.StartTime;
    if (startTime.Kind == DateTimeKind.Utc)
        startTime = startTime.ToLocalTime();

    var endTime = startTime.AddMinutes(service.DurationMinutes);

    var slots = GenerateSlots(company, startTime);
    if (slots.Count == 0) return Results.BadRequest("Empresa fechada neste dia.");
    if (!slots.Any(s => s == startTime))
        return Results.BadRequest("Horário não disponível.");

    var validationError = await ValidateClientAppointments(db, request.ClientPhone, companyId, null, startTime);
    if (validationError != null)
        return Results.BadRequest(validationError);

    var hasConflict = await db.Appointments.AnyAsync(a =>
        a.CompanyId == companyId &&
        a.EmployeeId == request.EmployeeId &&
        a.Status != "Canceled" &&
        a.StartTime < endTime &&
        a.EndTime > startTime
    );
    if (hasConflict)
        return Results.BadRequest("Este funcionário já tem um agendamento neste horário.");

    var appointment = new Appointment
    {
        CompanyId = companyId,
        ServiceId = request.ServiceId,
        EmployeeId = request.EmployeeId,
        ClientName = request.ClientName,
        ClientPhone = request.ClientPhone,
        StartTime = startTime,
        EndTime = endTime,
        Status = "Active"
        // Não associa ClientUserId (agendamento anônimo)
    };

    db.Appointments.Add(appointment);
    await db.SaveChangesAsync();
    return Results.Ok(appointment);
});

// =======================
// SERVE FRONT-END
// =======================
app.UseDefaultFiles();
app.UseStaticFiles();

app.Run();

// =======================
// DTOs
// =======================
public class RegisterRequest
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public string Type { get; set; } = "Client";
}

public class LoginRequest
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
}

public class CreateCompanyRequest
{
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
}

public class SelectCompanyRequest
{
    public Guid CompanyId { get; set; }
}

public class CreateServiceRequest
{
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public int DurationMinutes { get; set; }
}

public class CreateScheduleRequest
{
    public Guid ServiceId { get; set; }
    public Guid EmployeeId { get; set; }
    public string ClientName { get; set; } = "";
    public string ClientPhone { get; set; } = "";
    public DateTime StartTime { get; set; }
}

public class UpdateEmployeeRequest
{
    public string? Name { get; set; }
    public string? Specialty { get; set; }
    public string? PhotoUrl { get; set; }
}

public class UpdateAppointmentRequest
{
    public Guid? ServiceId { get; set; }
    public Guid? EmployeeId { get; set; }
    public string? ClientName { get; set; }
    public string? ClientPhone { get; set; }
    public DateTime? StartTime { get; set; }
}

public class UpdateCompanySettingsRequest
{
    public TimeSpan? OpeningTime { get; set; }
    public TimeSpan? ClosingTime { get; set; }
    public WorkSchedule? WorkSchedule { get; set; }
}

public class UpdateCompanyRequest
{
    public string? Name { get; set; }
    public string? Category { get; set; }
}

public class UpdateClientProfileRequest
{
    public string? Name { get; set; }
    public string? Phone { get; set; }
    public Dictionary<string, object>? Preferences { get; set; }
}