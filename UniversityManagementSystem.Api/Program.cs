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
using UniversityManagementSystem.Infrastructure.Services.Deletion;
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
        builder.Services.AddDistributedMemoryCache();
    }
}
else
{
    // No Redis configured — fall back to in-memory distributed cache so that
    // controllers injecting IDistributedCache don't crash at startup.
    builder.Services.AddDistributedMemoryCache();
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
    x.AddConsumer<UniversityManagementSystem.Infrastructure.Consumers.NotificationConsumer>();

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

    // AI endpoints: 10 requests per minute per user to prevent abuse
    options.AddPolicy("AiPolicy", httpContext =>
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
    options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                context.Token = accessToken;
            return Task.CompletedTask;
        }
    };
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
    options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
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
builder.Services.AddSignalR(options =>
{
    options.ClientTimeoutInterval  = TimeSpan.FromSeconds(60);
    options.KeepAliveInterval      = TimeSpan.FromSeconds(15);
    options.HandshakeTimeout       = TimeSpan.FromSeconds(15);
    options.MaximumReceiveMessageSize = 32 * 1024; // 32 KB
});
builder.Services.AddSingleton<Microsoft.AspNetCore.SignalR.IUserIdProvider, UniversityManagementSystem.Api.Hubs.NameIdUserIdProvider>();
builder.Services.AddSingleton<IRealtimeNotifier, SignalRNotifier>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddSingleton<UniversityManagementSystem.Infrastructure.Services.IAiInputSanitizer, UniversityManagementSystem.Infrastructure.Services.AiInputSanitizer>();
builder.Services.AddScoped<IAcademicRiskJob, AcademicRiskJob>();
builder.Services.AddScoped<IExamReminderJob, ExamReminderJob>();
builder.Services.AddScoped<IAssignmentReminderJob, AssignmentReminderJob>();
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
builder.Services.AddScoped<IExamService, ExamService>();
builder.Services.AddScoped<IAcademicYearService, AcademicYearService>();
builder.Services.AddScoped<IAcademicYearDepartmentService, AcademicYearDepartmentService>();
builder.Services.AddScoped<ISemesterService, SemesterService>();
builder.Services.AddScoped<ISubjectOfferingService, SubjectOfferingService>();
builder.Services.AddScoped<IGradeService, GradeService>();
builder.Services.AddScoped<IMaterialService, MaterialService>();
builder.Services.AddScoped<IRagService, RagService>();
builder.Services.AddScoped<IRagIndexingJob, RagIndexingJob>();

builder.Services.AddHttpClient("FastApi", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["FastApiSettings:BaseUrl"] ?? "http://localhost:8000");
    client.Timeout = TimeSpan.FromSeconds(120);
});

builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IDeletionService, DeletionService>();
builder.Services.AddScoped<IAssignmentService, AssignmentService>();
builder.Services.AddScoped<IAcademicStatusService, AcademicStatusService>();
builder.Services.AddScoped<IRegistrationService, RegistrationService>();

// ── AI Companion Platform ────────────────────────────────────────────────────
builder.Services.AddScoped<IAiCompanionService, AiCompanionService>();
builder.Services.AddHostedService<AiFollowUpBackgroundService>();

// ── AI Teaching Intelligence Platform ───────────────────────────────────────
builder.Services.AddScoped<ITeachingIntelligenceService, TeachingIntelligenceService>();
builder.Services.AddHostedService<TeachingIntelligenceBackgroundService>();
builder.Services.AddScoped<IBulkUploadJob, BulkUploadJob>();
builder.Services.AddScoped<IExcelImportService, ExcelImportService>();
builder.Services.AddScoped<IStudentFileService, StudentFileService>();
builder.Services.AddScoped<IEnrollmentUploadService, EnrollmentUploadService>();
builder.Services.AddScoped<IScheduleService, ScheduleService>();
builder.Services.AddScoped<IProctoringService, ProctoringService>();



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
app.MapHub<NotificationHub>("/hubs/notifications", options =>
{
    // Prefer WebSockets; fall back to SSE then long-polling.
    // Long-polling POST 404s after connection DELETE are expected and harmless —
    // the client reconnects automatically.
    options.Transports =
        Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets |
        Microsoft.AspNetCore.Http.Connections.HttpTransportType.ServerSentEvents |
        Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
});

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

        // Ensure citext extension exists before migrations (required by InitialUlidSchema)
        try { db.Database.ExecuteSqlRaw("CREATE EXTENSION IF NOT EXISTS citext;"); }
        catch (Exception) { /* may lack superuser — migration will try again */ }

        // 1. Apply any pending EF Core migrations automatically
        db.Database.Migrate();

        // Patch: AddStorageKeyToUploadedFiles migration was empty — add column manually
        try
        {
            db.Database.ExecuteSqlRaw(@"
                ALTER TABLE ""UploadedFiles"" ADD COLUMN IF NOT EXISTS ""StorageKey"" text NOT NULL DEFAULT '';
                ALTER TABLE ""UploadedFiles"" ADD COLUMN IF NOT EXISTS ""StoredFileName"" text NOT NULL DEFAULT '';
            ");
        }
        catch (Exception) { /* column may already exist */ }

        // Patch: backfill empty Exam codes
        try
        {
            db.Database.ExecuteSqlRaw(@"
                DO $$
                DECLARE
                    r RECORD;
                    seq INT := 1;
                    yr TEXT := EXTRACT(YEAR FROM NOW())::TEXT;
                    new_code TEXT;
                BEGIN
                    FOR r IN
                        SELECT ""Id"" FROM ""Exams""
                        WHERE ""Code"" IS NULL OR ""Code"" = ''
                        ORDER BY ""CreatedAt""
                    LOOP
                        LOOP
                            new_code := 'EXAM-' || yr || '-' || LPAD(seq::TEXT, 4, '0');
                            EXIT WHEN NOT EXISTS (SELECT 1 FROM ""Exams"" WHERE ""Code"" = new_code);
                            seq := seq + 1;
                        END LOOP;
                        UPDATE ""Exams"" SET ""Code"" = new_code WHERE ""Id"" = r.""Id"";
                        seq := seq + 1;
                    END LOOP;
                END $$;
            ");
        }
        catch (Exception) { /* safe to ignore if already patched */ }

        // 1a. Ensure AddRegistrationAndAcademicFeatures migration is applied
        // This migration renames AcademicYear→AcademicYears and Semester→Semesters and creates
        // new tables. It can fail if FK names differ in production, so we apply it idempotently.
        try
        {
            db.Database.ExecuteSqlRaw(@"
DO $$
BEGIN

    -- Step 1: Drop FKs that block the rename (by name — ignore if already gone)
    IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_AcademicYear_Colleges_CollegeId') THEN
        ALTER TABLE ""AcademicYear"" DROP CONSTRAINT ""FK_AcademicYear_Colleges_CollegeId"";
    END IF;
    IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_AcademicYearDepartments_AcademicYear_AcademicYearId') THEN
        ALTER TABLE ""AcademicYearDepartments"" DROP CONSTRAINT ""FK_AcademicYearDepartments_AcademicYear_AcademicYearId"";
    END IF;
    IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Semester_AcademicYear_AcademicYearId') THEN
        ALTER TABLE ""Semester"" DROP CONSTRAINT ""FK_Semester_AcademicYear_AcademicYearId"";
    END IF;
    IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_SubjectOfferings_Semester_SemesterId') THEN
        ALTER TABLE ""SubjectOfferings"" DROP CONSTRAINT ""FK_SubjectOfferings_Semester_SemesterId"";
    END IF;

    -- Step 2: Drop PKs that block the rename
    IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'PK_Semester') THEN
        ALTER TABLE ""Semester"" DROP CONSTRAINT ""PK_Semester"";
    END IF;
    IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'PK_AcademicYear') THEN
        ALTER TABLE ""AcademicYear"" DROP CONSTRAINT ""PK_AcademicYear"";
    END IF;

    -- Step 3: Rename tables (only if old name exists and new name doesn't)
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Semester')
       AND NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Semesters') THEN
        ALTER TABLE ""Semester"" RENAME TO ""Semesters"";
    END IF;
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'AcademicYear')
       AND NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'AcademicYears') THEN
        ALTER TABLE ""AcademicYear"" RENAME TO ""AcademicYears"";
    END IF;

    -- Step 4: Rename indexes on Semesters
    IF EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'IX_Semester_Name_AcademicYearId') THEN
        ALTER INDEX ""IX_Semester_Name_AcademicYearId"" RENAME TO ""IX_Semesters_Name_AcademicYearId"";
    END IF;
    IF EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'IX_Semester_AcademicYearId') THEN
        ALTER INDEX ""IX_Semester_AcademicYearId"" RENAME TO ""IX_Semesters_AcademicYearId"";
    END IF;

    -- Step 5: Re-add PKs
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'PK_Semesters') THEN
        ALTER TABLE ""Semesters"" ADD CONSTRAINT ""PK_Semesters"" PRIMARY KEY (""Id"");
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'PK_AcademicYears') THEN
        ALTER TABLE ""AcademicYears"" ADD CONSTRAINT ""PK_AcademicYears"" PRIMARY KEY (""Id"");
    END IF;

    -- Step 6: Create new tables
    CREATE TABLE IF NOT EXISTS ""AcademicPolicies"" (
        ""Id""                      character varying(26) NOT NULL,
        ""DepartmentId""            character varying(26) NULL,
        ""DefaultMaxHours""         integer NOT NULL DEFAULT 18,
        ""HonorMaxHours""           integer NOT NULL DEFAULT 21,
        ""WarningMaxHours""         integer NOT NULL DEFAULT 12,
        ""ProbationMaxHours""       integer NOT NULL DEFAULT 9,
        ""WarningGpaThreshold""     double precision NOT NULL DEFAULT 2.0,
        ""ProbationGpaThreshold""   double precision NOT NULL DEFAULT 1.5,
        ""HonorGpaThreshold""       double precision NOT NULL DEFAULT 3.5,
        ""GraduationMinGpa""        double precision NOT NULL DEFAULT 2.0,
        ""Code""                    text NOT NULL DEFAULT '',
        ""CreatedAt""               timestamp with time zone NOT NULL DEFAULT now(),
        ""DeletedAt""               timestamp with time zone NULL,
        CONSTRAINT ""PK_AcademicPolicies"" PRIMARY KEY (""Id"")
    );

    CREATE TABLE IF NOT EXISTS ""StudentAcademicStatuses"" (
        ""Id""                      character varying(26) NOT NULL,
        ""StudentId""               character varying(26) NOT NULL,
        ""GPA""                     double precision NOT NULL DEFAULT 0,
        ""CGPA""                    double precision NOT NULL DEFAULT 0,
        ""LastSemesterGPA""         double precision NOT NULL DEFAULT 0,
        ""LastCalculatedAt""        timestamp with time zone NULL,
        ""EarnedCreditHours""       integer NOT NULL DEFAULT 0,
        ""RemainingCreditHours""    integer NOT NULL DEFAULT 0,
        ""TotalRequiredHours""      integer NOT NULL DEFAULT 0,
        ""Standing""                integer NOT NULL DEFAULT 0,
        ""WarningCount""            integer NOT NULL DEFAULT 0,
        ""CurrentLevel""            integer NOT NULL DEFAULT 0,
        ""Code""                    text NOT NULL DEFAULT '',
        ""CreatedAt""               timestamp with time zone NOT NULL DEFAULT now(),
        ""DeletedAt""               timestamp with time zone NULL,
        CONSTRAINT ""PK_StudentAcademicStatuses"" PRIMARY KEY (""Id""),
        CONSTRAINT ""FK_StudentAcademicStatuses_Students_StudentId""
            FOREIGN KEY (""StudentId"") REFERENCES ""Students""(""Id"") ON DELETE CASCADE
    );

    CREATE TABLE IF NOT EXISTS ""SubjectOfferingWaitlists"" (
        ""Id""                  character varying(26) NOT NULL,
        ""StudentId""           character varying(26) NOT NULL,
        ""SubjectOfferingId""   character varying(26) NOT NULL,
        ""Position""            integer NOT NULL DEFAULT 0,
        ""AddedAt""             timestamp with time zone NOT NULL DEFAULT now(),
        ""Code""                text NOT NULL DEFAULT '',
        ""CreatedAt""           timestamp with time zone NOT NULL DEFAULT now(),
        ""DeletedAt""           timestamp with time zone NULL,
        CONSTRAINT ""PK_SubjectOfferingWaitlists"" PRIMARY KEY (""Id""),
        CONSTRAINT ""FK_SubjectOfferingWaitlists_Students_StudentId""
            FOREIGN KEY (""StudentId"") REFERENCES ""Students""(""Id"") ON DELETE CASCADE,
        CONSTRAINT ""FK_SubjectOfferingWaitlists_SubjectOfferings_SubjectOfferingId""
            FOREIGN KEY (""SubjectOfferingId"") REFERENCES ""SubjectOfferings""(""Id"") ON DELETE CASCADE
    );

    CREATE TABLE IF NOT EXISTS ""SubjectPrerequisites"" (
        ""Id""                      character varying(26) NOT NULL,
        ""SubjectId""               character varying(26) NOT NULL,
        ""PrerequisiteSubjectId""   character varying(26) NOT NULL,
        ""MinimumGrade""            double precision NULL,
        ""Code""                    text NOT NULL DEFAULT '',
        ""CreatedAt""               timestamp with time zone NOT NULL DEFAULT now(),
        ""DeletedAt""               timestamp with time zone NULL,
        CONSTRAINT ""PK_SubjectPrerequisites"" PRIMARY KEY (""Id""),
        CONSTRAINT ""FK_SubjectPrerequisites_Subjects_SubjectId""
            FOREIGN KEY (""SubjectId"") REFERENCES ""Subjects""(""Id"") ON DELETE CASCADE,
        CONSTRAINT ""FK_SubjectPrerequisites_Subjects_PrerequisiteSubjectId""
            FOREIGN KEY (""PrerequisiteSubjectId"") REFERENCES ""Subjects""(""Id"") ON DELETE RESTRICT
    );

    -- Step 7: Create indexes
    CREATE INDEX IF NOT EXISTS ""IX_AcademicPolicies_DepartmentId""
        ON ""AcademicPolicies""(""DepartmentId"");
    CREATE UNIQUE INDEX IF NOT EXISTS ""IX_StudentAcademicStatuses_StudentId""
        ON ""StudentAcademicStatuses""(""StudentId"");
    CREATE INDEX IF NOT EXISTS ""IX_SubjectOfferingWaitlist_OfferingId""
        ON ""SubjectOfferingWaitlists""(""SubjectOfferingId"");
    CREATE UNIQUE INDEX IF NOT EXISTS ""IX_SubjectOfferingWaitlist_Student_Offering""
        ON ""SubjectOfferingWaitlists""(""StudentId"", ""SubjectOfferingId"");
    CREATE INDEX IF NOT EXISTS ""IX_SubjectPrerequisites_PrerequisiteSubjectId""
        ON ""SubjectPrerequisites""(""PrerequisiteSubjectId"");
    CREATE UNIQUE INDEX IF NOT EXISTS ""IX_SubjectPrerequisites_Subject_Prereq""
        ON ""SubjectPrerequisites""(""SubjectId"", ""PrerequisiteSubjectId"");
    CREATE INDEX IF NOT EXISTS ""IX_SubjectPrerequisites_SubjectId""
        ON ""SubjectPrerequisites""(""SubjectId"");

    -- Step 8: Re-add FKs on renamed tables (only if missing)
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_AcademicYearDepartments_AcademicYears_AcademicYearId') THEN
        ALTER TABLE ""AcademicYearDepartments""
            ADD CONSTRAINT ""FK_AcademicYearDepartments_AcademicYears_AcademicYearId""
            FOREIGN KEY (""AcademicYearId"") REFERENCES ""AcademicYears""(""Id"") ON DELETE CASCADE;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_AcademicYears_Colleges_CollegeId') THEN
        ALTER TABLE ""AcademicYears""
            ADD CONSTRAINT ""FK_AcademicYears_Colleges_CollegeId""
            FOREIGN KEY (""CollegeId"") REFERENCES ""Colleges""(""Id"") ON DELETE RESTRICT;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Semesters_AcademicYears_AcademicYearId') THEN
        ALTER TABLE ""Semesters""
            ADD CONSTRAINT ""FK_Semesters_AcademicYears_AcademicYearId""
            FOREIGN KEY (""AcademicYearId"") REFERENCES ""AcademicYears""(""Id"") ON DELETE CASCADE;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_SubjectOfferings_Semesters_SemesterId') THEN
        ALTER TABLE ""SubjectOfferings""
            ADD CONSTRAINT ""FK_SubjectOfferings_Semesters_SemesterId""
            FOREIGN KEY (""SemesterId"") REFERENCES ""Semesters""(""Id"") ON DELETE RESTRICT;
    END IF;

    -- Step 9: Mark migration as applied
    INSERT INTO ""__EFMigrationsHistory""(""MigrationId"", ""ProductVersion"")
    VALUES ('20260518015621_AddRegistrationAndAcademicFeatures', '9.0.0')
    ON CONFLICT DO NOTHING;

END $$;
            ");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Migration workaround 20260518015621 encountered an issue (may be safe to ignore if already applied).");
        }

        // 1b. Ensure AddStudentProfileFields migration columns exist (idempotent)
        try
        {
            db.Database.ExecuteSqlRaw(@"
ALTER TABLE ""Students"" ADD COLUMN IF NOT EXISTS ""Address""      text NOT NULL DEFAULT '';
ALTER TABLE ""Students"" ADD COLUMN IF NOT EXISTS ""DateOfBirth""  timestamp with time zone NULL;
ALTER TABLE ""Students"" ADD COLUMN IF NOT EXISTS ""Gender""       integer NULL;
ALTER TABLE ""Students"" ADD COLUMN IF NOT EXISTS ""Governorate""  text NOT NULL DEFAULT '';
ALTER TABLE ""Students"" ADD COLUMN IF NOT EXISTS ""NationalId""   text NOT NULL DEFAULT '';
ALTER TABLE ""Students"" ADD COLUMN IF NOT EXISTS ""Religion""     text NOT NULL DEFAULT '';
ALTER TABLE ""Students"" ADD COLUMN IF NOT EXISTS ""StudentType""  integer NOT NULL DEFAULT 0;
INSERT INTO ""__EFMigrationsHistory""(""MigrationId"", ""ProductVersion"")
VALUES ('20260518220621_AddStudentProfileFields', '9.0.0')
ON CONFLICT DO NOTHING;
            ");
        }
        catch (Exception) { /* safe to ignore — columns already exist */ }

        // 1c. Ensure legacy databases have the Code column in SystemUsers
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

        // 1e. Ensure all new Materials columns exist
        try
        {
            db.Database.ExecuteSqlRaw(@"
ALTER TABLE ""Materials""
ADD COLUMN IF NOT EXISTS ""Title"" text NOT NULL DEFAULT '';
ALTER TABLE ""Materials""
ADD COLUMN IF NOT EXISTS ""Description"" text NULL;
ALTER TABLE ""Materials""
ADD COLUMN IF NOT EXISTS ""StorageKey"" text NOT NULL DEFAULT '';
ALTER TABLE ""Materials""
ADD COLUMN IF NOT EXISTS ""StoredFileName"" text NOT NULL DEFAULT '';
ALTER TABLE ""Materials""
ADD COLUMN IF NOT EXISTS ""ContentType"" text NOT NULL DEFAULT '';
ALTER TABLE ""Materials""
ADD COLUMN IF NOT EXISTS ""FileSize"" bigint NOT NULL DEFAULT 0;
ALTER TABLE ""Materials""
ADD COLUMN IF NOT EXISTS ""UploadedAt"" timestamp with time zone NOT NULL DEFAULT now();
ALTER TABLE ""Materials""
ADD COLUMN IF NOT EXISTS ""FileId"" character varying(26) NULL;
            ");
        }
        catch (Exception) { /* safe to ignore */ }

        // 1f. Ensure ScheduleEntries table exists (Type/WeekType stored as varchar per EF HasConversion<string>)
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
    ""Type""               character varying(20) NOT NULL DEFAULT 'Lecture',
    ""Location""           text NOT NULL DEFAULT '',
    ""WeekType""           character varying(10) NOT NULL DEFAULT 'All',
    ""IsActive""           boolean NOT NULL DEFAULT true,
    ""CreatedAt""          timestamp with time zone NOT NULL DEFAULT now(),
    ""DeletedAt""          timestamp with time zone NULL,
    CONSTRAINT ""PK_ScheduleEntries"" PRIMARY KEY (""Id""),
    CONSTRAINT ""FK_ScheduleEntries_SubjectOfferings_SubjectOfferingId""
        FOREIGN KEY (""SubjectOfferingId"") REFERENCES ""SubjectOfferings""(""Id"") ON DELETE CASCADE,
    CONSTRAINT ""FK_ScheduleEntries_Batches_BatchId""
        FOREIGN KEY (""BatchId"") REFERENCES ""Batches""(""Id"") ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS ""IX_ScheduleEntries_Batch_Day""
    ON ""ScheduleEntries""(""BatchId"", ""DayOfWeek"");
CREATE INDEX IF NOT EXISTS ""IX_ScheduleEntries_OfferingId""
    ON ""ScheduleEntries""(""SubjectOfferingId"");
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
        "daily-academic-risk-analysis",
        job => job.RunDailyRiskAnalysisAsync(),
        "0 6 * * *"); // every day at 06:00 UTC

    recurringJobManager.AddOrUpdate<IExamReminderJob>(
        "exam-reminders",
        job => job.RunAsync(),
        "*/30 * * * *"); // every 30 minutes

    recurringJobManager.AddOrUpdate<IAssignmentReminderJob>(
        "assignment-reminders",
        job => job.RunAsync(),
        "*/30 * * * *"); // every 30 minutes — mirrors exam reminders

    recurringJobManager.AddOrUpdate<IRagIndexingJob>(
        "rag-index-unindexed-materials",
        job => job.IndexAllUnindexedMaterialsAsync(),
        Cron.Daily);
}

app.Run();
