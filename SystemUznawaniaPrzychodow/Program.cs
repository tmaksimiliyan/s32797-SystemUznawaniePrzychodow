using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SystemUznawaniaPrzychodow.Data;
using SystemUznawaniaPrzychodow.Models;
using SystemUznawaniaPrzychodow.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(opt =>
{
    opt.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Wpisz access token (bez słowa 'Bearer')"
    });
    opt.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddScoped<IPasswordService, PasswordService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IClientService, ClientService>();
builder.Services.AddScoped<IContractService, ContractService>();
builder.Services.AddScoped<IContractPaymentService, ContractPaymentService>();
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
builder.Services.AddScoped<IRevenueService, RevenueService>();
builder.Services.AddHttpClient("NBP");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt => opt.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["JWT:Issuer"],
        ValidAudience = builder.Configuration["JWT:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["JWT:Secret"]!))
    });

builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var passwordService = scope.ServiceProvider.GetRequiredService<IPasswordService>();
    
    await db.Database.MigrateAsync();

    if (!await db.Employees.AnyAsync())
    {
        db.Employees.Add(new Employee
        {
            Login = "admin",
            PasswordHash = passwordService.HashPassword("admin123"),
            Role = EmployeeRole.Admin
        });
        db.Employees.Add(new Employee
        {
            Login = "user",
            PasswordHash = passwordService.HashPassword("user123"),
            Role = EmployeeRole.Standard
        });
        await db.SaveChangesAsync();
    }

   
    if (!await db.Softwares.AnyAsync())
    {
        var today = DateTime.UtcNow.Date;

        var finance = new Software
        {
            Name = "FinanceManager Pro",
            Description = "System do zarządzania finansami i księgowością.",
            CurrentVersion = "3.2",
            Category = "Finanse",
            YearlyLicensePrice = 5000m,
            Discounts = new List<Discount>
            {
                new() { Name = "Black Friday", DiscountType = DiscountType.Contract, Value = 15m,
                        DateFrom = today.AddMonths(-1), DateTo = today.AddMonths(1) },
                new() { Name = "Promocja SaaS", DiscountType = DiscountType.Subscription, Value = 10m,
                        DateFrom = today.AddMonths(-1), DateTo = today.AddMonths(1) }
            }
        };

        var edu = new Software
        {
            Name = "EduLearn Platform",
            Description = "Platforma e-learningowa dla szkół i uczelni.",
            CurrentVersion = "1.5",
            Category = "Edukacja",
            YearlyLicensePrice = 1200m,
            Discounts = new List<Discount>
            {
                new() { Name = "Rabat Edukacyjny", DiscountType = DiscountType.Subscription, Value = 20m,
                        DateFrom = today.AddMonths(-1), DateTo = today.AddMonths(1) },
                new() { Name = "Stara Promocja", DiscountType = DiscountType.Contract, Value = 25m,
                        DateFrom = today.AddMonths(-6), DateTo = today.AddMonths(-3) }
            }
        };

        var health = new Software
        {
            Name = "HealthTrack System",
            Description = "System do zarządzania placówką medyczną.",
            CurrentVersion = "2.0",
            Category = "Medycyna",
            YearlyLicensePrice = 3600m
        };

        db.Softwares.AddRange(finance, edu, health);
        await db.SaveChangesAsync();
    }
    
    if (!await db.Clients.AnyAsync())
    {
        db.Add(new IndividualClient
        {
            FirstName = "Jan", LastName = "Kowalski", Address = "ul. Testowa 1, Warszawa",
            Email = "jan.kowalski@example.com", Phone = "500100200", Pesel = "90010112345"
        });
        db.Add(new CompanyClient
        {
            CompanyName = "ABC Sp. z o.o.", Address = "ul. Firmowa 10, Kraków",
            Email = "biuro@abc.pl", Phone = "224004000", Krs = "0000123456"
        });
        await db.SaveChangesAsync();
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
