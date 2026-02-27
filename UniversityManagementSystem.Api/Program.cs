using Hangfire;
using Hangfire.PostgreSql;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using StackExchange.Redis;
using System.Text.Json;
using System;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using UniversityManagementSystem.Api.Filters;
using UniversityManagementSystem.Api.Middleware;
using UniversityManagementSystem.Infrastructure.Consumers;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;
using UniversityManagementSystem.Infrastructure.Services;
using UniversityManagementSystem.Infrastructure.Jobs;

var builder = WebApplication.CreateBuilder(args);

// 1. Serilog
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

// 2. Database
var connectionString =
    Environment.GetEnvironmentVariable("CONNECTION_STRING")
    ?? Environment.GetEnvironmentVariable("DATABASE_URL");

Console.WriteLine("Hangfire connection string: " + connectionString);

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new Exception("Database connection string is missing.");
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// 3. Redis
var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConnectionString ?? "localhost"));
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnectionString ?? "localhost";
    options.InstanceName = "UMS_";
});

// 4. Hangfire
builder.Services.AddHangfire(config =>
{
#pragma warning disable CS0618
    config.UsePostgreSqlStorage(connectionString);
#pragma warning restore CS0618
});

builder.Services.AddHangfireServer();

// 5. MassTransit
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<AttendanceConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        var rabbitUrl =
            Environment.GetEnvironmentVariable("RABBITMQ_URL") ??
            Environment.GetEnvironmentVariable("AMQP_URL");

        if (!string.IsNullOrEmpty(rabbitUrl))
        {
            cfg.Host(new Uri(rabbitUrl));
        }
        else
        {
            // Local fallback
            cfg.Host("localhost", "/", h =>
            {
                h.Username("guest");
                h.Password("guest");
            });
        }

        cfg.ConfigureEndpoints(context);
    });
});

// 6. Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var host = context?.Request?.Host.Value
              ?? Environment.GetEnvironmentVariable("APP_URL")
              ?? "localhost";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context?.User?.Identity?.Name ?? host,
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 1000,
                QueueLimit = 100,
                Window = TimeSpan.FromMinutes(1)
            });
    });

    // Task 2: Strict Login Rate Limiting (5 per min per IP)
    options.AddPolicy("LoginPolicy", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1)
            }));
});

// 7. Security (JWT) - Read from Env Var for Production
var secretKey = Environment.GetEnvironmentVariable("JWT_SECRET");

// Allow appsettings fallback ONLY in Development for DX
if (string.IsNullOrEmpty(secretKey) && builder.Environment.IsDevelopment())
{
    secretKey = "DevelopmentOnlyFallbackKey_ChangeInProduction!";
}

if (string.IsNullOrEmpty(secretKey))
    throw new InvalidOperationException("JWT_SECRET environment variable is missing.");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
        ValidAudience = builder.Configuration["JwtSettings:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        RoleClaimType = "role",
        NameClaimType = "nameid"
    };
    options.MapInboundClaims = false;
});

builder.Services.AddAuthorization();

// 8. Controllers & Swagger
builder.Services.AddControllers(options =>
{
    options.Filters.Add<ResponseWrapperFilter>();
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "University Management System API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            []
        }
    });
});

builder.Services.AddHttpClient();

// Task 5: Health Checks
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString)
    .AddRedis(redisConnectionString ?? "localhost")
    .AddHangfire(options => { options.MinimumAvailableServers = 1; });

// 9. Dependency Injection Registrations
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IStudentService, StudentService>();
builder.Services.AddScoped<IDoctorService, DoctorService>();
builder.Services.AddScoped<ISubjectService, SubjectService>();
builder.Services.AddScoped<IEnrollmentService, EnrollmentService>();
builder.Services.AddScoped<IUniversityService, UniversityService>();
builder.Services.AddScoped<ICollegeService, CollegeService>();
builder.Services.AddScoped<IDepartmentService, DepartmentService>();
builder.Services.AddScoped<IBatchService, BatchService>();
builder.Services.AddScoped<IGroupService, GroupService>();

builder.Services.AddScoped<IFileService, FileService>();
builder.Services.AddScoped<IExcelService, ExcelService>();
builder.Services.AddScoped<IIdentityProvisioningService, IdentityProvisioningService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IRegulationService, RegulationService>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IAiService, AiService>();
builder.Services.AddScoped<IAttendanceService, AttendanceService>();
builder.Services.AddScoped<IExamService, ExamService>();
builder.Services.AddScoped<IAcademicYearService, AcademicYearService>();
builder.Services.AddScoped<ISemesterService, SemesterService>();
builder.Services.AddScoped<ISubjectOfferingService, SubjectOfferingService>();
builder.Services.AddScoped<IGradeService, GradeService>();
builder.Services.AddScoped<IMaterialService, MaterialService>();

builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IBulkUploadJob, BulkUploadJob>();


var app = builder.Build();

// 10. Middleware Pipeline
app.UseMiddleware<ExceptionMiddleware>();

// Enable Swagger in all environments (including Production)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "University Management System API v1");
    c.RoutePrefix = "swagger"; // Available at /swagger
});

// Root URL ("/") automatically redirects to /swagger
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

app.UseSerilogRequestLogging();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new HangfireAuthorizationFilter()]
});

app.MapControllers();

// Task 5: Health Check Endpoint
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var response = new
        {
            Status = report.Status.ToString(),
            Checks = report.Entries.Select(e => new
            {
                Component = e.Key,
                e.Value.Status,
                e.Value.Description
            }),
            report.TotalDuration
        };
        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
});

// Seed Database
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        await DbInitializer.SeedAsync(services);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "An error occurred while seeding the database.");
    }
}

app.Run();
