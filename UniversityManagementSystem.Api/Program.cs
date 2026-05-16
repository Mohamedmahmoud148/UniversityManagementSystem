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
using UniversityManagementSystem.Api.Filters;
using UniversityManagementSystem.Api.Hubs;
using UniversityManagementSystem.Api.Middleware;
using UniversityManagementSystem.Api.Services;
using UniversityManagementSystem.Infrastructure.Consumers;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;
using UniversityManagementSystem.Infrastructure.Services;
using UniversityManagementSystem.Infrastructure.Jobs;
using UniversityManagementSystem.Infrastructure.Storage;


var builder = WebApplication.CreateBuilder(args);

// 1. Serilog
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

// 2. Database
var connectionString =
    Environment.GetEnvironmentVariable("CONNECTION_STRING")
    ?? Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new Exception("Database connection string is missing.");
}

// Convert Railway's postgresql:// URI format to standard Npgsql format
if (connectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
    connectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
{
    var uri = new Uri(connectionString);
    var userInfo = uri.UserInfo.Split(':');

    var builderConn = new Npgsql.NpgsqlConnectionStringBuilder
    {
        Host = uri.Host,
        Port = uri.Port > 0 ? uri.Port : 5432,
        Username = userInfo[0],
        Password = userInfo.Length > 1 ? userInfo[1] : "",
        Database = uri.LocalPath.TrimStart('/'),
        SslMode = Npgsql.SslMode.Require
    };

    connectionString = builderConn.ToString();
}


builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(connectionString);
    options.ConfigureWarnings(warnings => 
        warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
});

// 3. Redis
var redisConnectionString = builder.Configuration.GetConnectionString("Redis");

if (!string.IsNullOrWhiteSpace(redisConnectionString))
{
    try
    {
        var configurationOptions = ConfigurationOptions.Parse(redisConnectionString);
        configurationOptions.AbortOnConnectFail = false;
        configurationOptions.ConnectRetry = 5;
        configurationOptions.ReconnectRetryPolicy = new ExponentialRetry(5000);

        var redis = ConnectionMultiplexer.Connect(configurationOptions);
        builder.Services.AddSingleton<IConnectionMultiplexer>(redis);

        builder.Services.AddStackExchangeRedisCache(options =>
        {
            options.ConfigurationOptions = configurationOptions;
            options.InstanceName = "UMS_";
        });
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Failed to connect to Redis at startup. Application will continue without Redis cache.");
    }
}

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

    // Strict Login Rate Limiting (5 per min per IP)
    options.AddPolicy("LoginPolicy", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1)
            }));

    // Sensitive auth endpoints (refresh-token, change-password): 10 per min per user
    options.AddPolicy("SensitiveAuthPolicy", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User?.Identity?.Name
                          ?? httpContext.Connection.RemoteIpAddress?.ToString()
                          ?? "unknown",
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 10,
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

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
                "http://localhost:3000",
                "http://localhost:5173",
                "https://bsnu.web.app")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddAuthorization();

// 8. Controllers & Swagger
builder.Services.AddControllers(options =>
{
    options.Filters.Add<ResponseWrapperFilter>();
})
.AddJsonOptions(options =>
{
    // Factory handles both Ulid (non-nullable) and Ulid? (nullable) fields.
    // A factory is required because System.Text.Json does NOT automatically
    // wrap a JsonConverter<T> to also cover T? — without this, nullable ULID
    // fields and any Ulid stored behind `object` emit {Time, Random} objects
    // instead of plain "01JTNQ..." strings.
    options.JsonSerializerOptions.Converters.Add(new UniversityManagementSystem.Api.Converters.UlidJsonConverterFactory());
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "University Management System API", Version = "v1" });

    // Map Ulid → string so Swagger renders all ULID fields as plain strings instead of {}
    c.MapType<NUlid.Ulid>(() => new Microsoft.OpenApi.Models.OpenApiSchema
    {
        Type    = "string",
        Example = new Microsoft.OpenApi.Any.OpenApiString("01JSCX1234ABC56DEFGH789JKM")
    });

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
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));

builder.Services.AddScoped<IUserContextService, UserContextService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAdminService, AdminService>();


// Background Jobs (Hangfire)
builder.Services.AddScoped<IEmailJob, EmailJob>();
builder.Services.AddScoped<INotificationJob, NotificationJob>();
builder.Services.AddScoped<IAiBackgroundJob, AiBackgroundJob>();
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
builder.Services.AddSignalR();
builder.Services.AddSingleton<IRealtimeNotifier, SignalRNotifier>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IAcademicRiskJob, AcademicRiskJob>();
builder.Services.AddScoped<IExamReminderJob, ExamReminderJob>();
builder.Services.AddScoped<IRegulationService, RegulationService>();
builder.Services.AddScoped<ISmartStringGenerator, SmartStringGenerator>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<ISystemUserResolver, SystemUserResolver>();
builder.Services.AddScoped<IComplaintService, ComplaintService>();
builder.Services.AddScoped<IComplaintIntelligenceJob, ComplaintIntelligenceJob>();
// AiToolRegistry and IAiTool registrations removed — tool execution
// is handled entirely inside the FastAPI AI service. The .NET backend
// no longer re-executes tools; it only calls AI and saves the response.
builder.Services.AddHttpClient<IAiService, AiService>(client =>
{
    var baseUrl = Environment.GetEnvironmentVariable("AI_SERVICE_URL")
                  ?? "https://ai-orchestration-service-production.up.railway.app";
    client.BaseAddress = new Uri(baseUrl);
    // Polly pipeline inside AiService adds its own timeout; HttpClient timeout is
    // a final backstop set slightly above Polly's 65 s pipeline timeout.
    client.Timeout = TimeSpan.FromSeconds(90);
});
builder.Services.AddScoped<IAttendanceService, AttendanceService>();
builder.Services.AddScoped<IExamService, ExamService>();
builder.Services.AddScoped<IAcademicYearService, AcademicYearService>();
builder.Services.AddScoped<IAcademicYearDepartmentService, AcademicYearDepartmentService>();
builder.Services.AddScoped<ISemesterService, SemesterService>();
builder.Services.AddScoped<ISubjectOfferingService, SubjectOfferingService>();
builder.Services.AddScoped<IGradeService, GradeService>();
builder.Services.AddScoped<IMaterialService, MaterialService>();

builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IBulkUploadJob, BulkUploadJob>();
builder.Services.AddScoped<IExcelImportService, ExcelImportService>();
builder.Services.AddScoped<IStudentFileService, StudentFileService>();
builder.Services.AddScoped<IEnrollmentUploadService, EnrollmentUploadService>();
builder.Services.AddScoped<IScheduleService, ScheduleService>();



// Cloudflare R2 Storage
builder.Services.Configure<R2Settings>(builder.Configuration.GetSection("R2"));
builder.Services.Configure<UniversityManagementSystem.Core.Settings.UniversitySettings>(
    builder.Configuration.GetSection("UniversitySettings"));

// Allow DEFAULT_PASSWORD env var to override appsettings at runtime
builder.Services.PostConfigure<UniversityManagementSystem.Core.Settings.UniversitySettings>(settings =>
{
    var envPassword = Environment.GetEnvironmentVariable("DEFAULT_PASSWORD");
    if (!string.IsNullOrWhiteSpace(envPassword))
        settings.DefaultPassword = envPassword;
});
builder.Services.AddSingleton<IStorageService, R2StorageService>();


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
app.UseCors();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new HangfireAuthorizationFilter()]
});

app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications");

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

// Apply migrations and seed initial data in a single scope
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var db = services.GetRequiredService<AppDbContext>();

        // 1. Apply any pending EF Core migrations automatically
        db.Database.Migrate();

        // 1b. Ensure legacy databases have the Code column in SystemUsers
        try
        {
            db.Database.ExecuteSqlRaw(@"
    ALTER TABLE ""SystemUsers""
    ADD COLUMN IF NOT EXISTS ""Code"" text;
    ");
        }
        catch (Exception)
        {
            // Ignore errors if column already exists
        }

        // 1c. Ensure MustChangePassword column exists (guards against migration history mismatch)
        try
        {
            db.Database.ExecuteSqlRaw(@"
    ALTER TABLE ""SystemUsers""
    ADD COLUMN IF NOT EXISTS ""MustChangePassword"" boolean NOT NULL DEFAULT false;
    ");
        }
        catch (Exception)
        {
            // Column already exists — safe to ignore
        }

        // 1d. Ensure randomized-exam columns exist (AddRandomizedExamSupport migration)
        try
        {
            db.Database.ExecuteSqlRaw(@"
    ALTER TABLE ""Exams""
    ADD COLUMN IF NOT EXISTS ""IsRandomized"" boolean NOT NULL DEFAULT false;

    ALTER TABLE ""Exams""
    ADD COLUMN IF NOT EXISTS ""QuestionsPerStudent"" integer NOT NULL DEFAULT 0;

    ALTER TABLE ""ExamQuestions""
    ADD COLUMN IF NOT EXISTS ""OptionsJson"" text NULL;

    ALTER TABLE ""ExamQuestions""
    ADD COLUMN IF NOT EXISTS ""QuestionType"" integer NOT NULL DEFAULT 0;

    CREATE TABLE IF NOT EXISTS ""StudentExamVariants"" (
        ""Id""              character varying(26) NOT NULL,
        ""Code""            text NOT NULL DEFAULT '',
        ""ExamId""          character varying(26) NOT NULL,
        ""StudentId""       character varying(26) NOT NULL,
        ""QuestionIdsJson"" text NOT NULL DEFAULT '[]',
        ""CreatedAt""       timestamp with time zone NOT NULL DEFAULT now(),
        ""DeletedAt""       timestamp with time zone NULL,
        CONSTRAINT ""PK_StudentExamVariants"" PRIMARY KEY (""Id""),
        CONSTRAINT ""FK_StudentExamVariants_Exams_ExamId""
            FOREIGN KEY (""ExamId"") REFERENCES ""Exams""(""Id"") ON DELETE CASCADE,
        CONSTRAINT ""FK_StudentExamVariants_Students_StudentId""
            FOREIGN KEY (""StudentId"") REFERENCES ""Students""(""Id"") ON DELETE RESTRICT
    );

    CREATE UNIQUE INDEX IF NOT EXISTS ""IX_StudentExamVariants_ExamId_StudentId""
        ON ""StudentExamVariants""(""ExamId"", ""StudentId"");

    CREATE INDEX IF NOT EXISTS ""IX_StudentExamVariants_StudentId""
        ON ""StudentExamVariants""(""StudentId"");

    INSERT INTO ""__EFMigrationsHistory""(""MigrationId"", ""ProductVersion"")
    VALUES ('20260516025341_AddRandomizedExamSupport', '9.0.0')
    ON CONFLICT DO NOTHING;
    ");
        }
        catch (Exception)
        {
            // Safe to ignore — columns/table already exist
        }

        // 1e. Ensure Materials.Title and Materials.Description columns exist
        try
        {
            db.Database.ExecuteSqlRaw(@"
ALTER TABLE ""Materials""
ADD COLUMN IF NOT EXISTS ""Title"" text NOT NULL DEFAULT '';
ALTER TABLE ""Materials""
ADD COLUMN IF NOT EXISTS ""Description"" text NULL;
            ");
        }
        catch (Exception) { /* safe to ignore */ }

        // 1f. Ensure ScheduleEntries table exists
        try
        {
            db.Database.ExecuteSqlRaw(@"
CREATE TABLE IF NOT EXISTS ""ScheduleEntries"" (
    ""Id""                 character varying(26) NOT NULL,
    ""Code""               text NOT NULL DEFAULT '',
    ""SubjectOfferingId""  character varying(26) NOT NULL,
    ""BatchId""            character varying(26) NOT NULL,
    ""GroupId""            character varying(26) NULL,
    ""DayOfWeek""          integer NOT NULL DEFAULT 0,
    ""StartTime""          interval NOT NULL DEFAULT '00:00:00',
    ""EndTime""            interval NOT NULL DEFAULT '00:00:00',
    ""Type""               integer NOT NULL DEFAULT 0,
    ""Location""           text NOT NULL DEFAULT '',
    ""WeekType""           integer NOT NULL DEFAULT 0,
    ""IsActive""           boolean NOT NULL DEFAULT true,
    ""CreatedAt""          timestamp with time zone NOT NULL DEFAULT now(),
    ""DeletedAt""          timestamp with time zone NULL,
    CONSTRAINT ""PK_ScheduleEntries"" PRIMARY KEY (""Id""),
    CONSTRAINT ""FK_ScheduleEntries_SubjectOfferings_SubjectOfferingId""
        FOREIGN KEY (""SubjectOfferingId"") REFERENCES ""SubjectOfferings""(""Id"") ON DELETE CASCADE,
    CONSTRAINT ""FK_ScheduleEntries_Batches_BatchId""
        FOREIGN KEY (""BatchId"") REFERENCES ""Batches""(""Id"") ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS ""IX_ScheduleEntries_SubjectOfferingId""
    ON ""ScheduleEntries""(""SubjectOfferingId"");
CREATE INDEX IF NOT EXISTS ""IX_ScheduleEntries_BatchId""
    ON ""ScheduleEntries""(""BatchId"");
            ");
        }
        catch (Exception) { /* safe to ignore */ }

        // 2. Seed initial data (SuperAdmin, lookup tables, etc.)
        await DbInitializer.SeedAsync(services);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "An error occurred while migrating or seeding the database.");
    }
}

// 10. Schedule Hangfire Jobs
using (var scope = app.Services.CreateScope())
{
    var recurringJobManager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
    
    recurringJobManager.AddOrUpdate<IComplaintIntelligenceJob>(
        "daily-complaint-report",
        job => job.GenerateDailyReportAsync(),
        Cron.Daily);
        
    recurringJobManager.AddOrUpdate<IComplaintIntelligenceJob>(
        "weekly-complaint-report",
        job => job.GenerateWeeklyReportAsync(),
        Cron.Weekly);
        
    recurringJobManager.AddOrUpdate<IComplaintIntelligenceJob>(
        "monthly-complaint-report",
        job => job.GenerateMonthlyReportAsync(),
        Cron.Monthly);

    recurringJobManager.AddOrUpdate<IAcademicRiskJob>(
        "academic-risk-alerts",
        job => job.RunAsync(),
        Cron.Daily);

    recurringJobManager.AddOrUpdate<IExamReminderJob>(
        "exam-reminders",
        job => job.RunAsync(),
        "*/30 * * * *"); // every 30 minutes
}

app.Run();
