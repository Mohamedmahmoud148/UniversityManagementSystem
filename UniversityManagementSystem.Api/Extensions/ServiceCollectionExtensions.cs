using Hangfire;
using Hangfire.PostgreSql;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using StackExchange.Redis;
using System;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using UniversityManagementSystem.Api.Filters;
using UniversityManagementSystem.Api.Hubs;
using UniversityManagementSystem.Api.Services;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Consumers;
using UniversityManagementSystem.Infrastructure.Data;
using UniversityManagementSystem.Infrastructure.Jobs;
using UniversityManagementSystem.Infrastructure.Services;
using UniversityManagementSystem.Infrastructure.Services.Deletion;
using UniversityManagementSystem.Infrastructure.Storage;

namespace UniversityManagementSystem.Api.Extensions
{
    /// <summary>
    /// Service registration extension methods.
    /// Extracted from Program.cs to improve maintainability.
    /// Each method registers a cohesive group of services.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        // ── Database ──────────────────────────────────────────────────────────
        public static IServiceCollection AddDatabase(
            this IServiceCollection services, IConfiguration config)
        {
            var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
                ?? config.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Database connection string not configured.");

            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(connectionString,
                    npgsql => npgsql.EnableRetryOnFailure(3)));

            return services;
        }

        // ── Core Application Services ─────────────────────────────────────────
        public static IServiceCollection AddApplicationServices(
            this IServiceCollection services, IConfiguration config)
        {
            // Auth
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IUserContextService, UserContextService>();
            services.AddScoped<ISystemUserResolver, SystemUserResolver>();
            services.AddScoped<IUniversityEmailGenerator, UniversityEmailGenerator>();

            // Academic
            services.AddScoped<IStudentService, StudentService>();
            services.AddScoped<IDoctorService, DoctorService>();
            services.AddScoped<IAdminService, AdminService>();
            services.AddScoped<IScheduleService, ScheduleService>();
            services.AddScoped<ISubjectService, SubjectService>();
            services.AddScoped<ISubjectOfferingService, SubjectOfferingService>();
            services.AddScoped<IEnrollmentService, EnrollmentService>();
            services.AddScoped<IGradeService, GradeService>();
            services.AddScoped<IMaterialService, MaterialService>();
            services.AddScoped<IExamService, ExamService>();
            services.AddScoped<IAssignmentService, AssignmentService>();
            services.AddScoped<IRegistrationService, RegistrationService>();
            services.AddScoped<INotificationService, NotificationService>();
            services.AddScoped<IComplaintService, ComplaintService>();
            services.AddScoped<IAcademicStatusService, AcademicStatusService>();
            services.AddScoped<IDeletionService, DeletionService>();
            services.AddScoped<ISmartStringGenerator, SmartStringGenerator>();

            // Infrastructure
            services.AddScoped<IAuditService, AuditService>();
            services.AddScoped<SuspiciousActivityDetector>();
            services.AddScoped<IExcelImportService, ExcelImportService>();
            services.AddScoped<IExcelService, ExcelService>();
            services.AddScoped<IIdentityProvisioningService, IdentityProvisioningService>();
            services.AddScoped<IBulkUploadJob, BulkUploadJob>();
            services.AddScoped<IAiInputSanitizer, AiInputSanitizer>();
            services.AddScoped<IRealtimeNotifier, SignalRNotifier>();
            services.AddSingleton<IRealtimeNotifier, SignalRNotifier>();

            return services;
        }

        // ── AI Services ───────────────────────────────────────────────────────
        public static IServiceCollection AddAiServices(
            this IServiceCollection services, IConfiguration config)
        {
            var aiUrl = Environment.GetEnvironmentVariable("AI_SERVICE_URL")
                        ?? "https://ai-orchestration-service-production.up.railway.app";

            // Main AI service (chat, exams, RAG, etc.)
            services.AddHttpClient<IAiService, AiService>(client =>
            {
                client.BaseAddress = new Uri(aiUrl);
                client.Timeout = TimeSpan.FromSeconds(90);
            });

            // Named client for other FastAPI calls
            services.AddHttpClient("FastApi", client =>
            {
                client.BaseAddress = new Uri(config["FastApiSettings:BaseUrl"] ?? aiUrl);
                client.Timeout = TimeSpan.FromSeconds(120);
            });

            // Lecture Intelligence services
            services.AddHttpClient<ISpeechToTextService, WhisperSpeechToTextService>(client =>
            {
                client.BaseAddress = new Uri(aiUrl);
                client.Timeout = TimeSpan.FromMinutes(10);
            });

            services.AddHttpClient<ILectureIntelligenceService, LectureIntelligenceService>(client =>
            {
                client.BaseAddress = new Uri(aiUrl);
                client.Timeout = TimeSpan.FromMinutes(5);
            });

            // AI Chat
            services.AddScoped<IChatService, ChatService>();
            services.AddScoped<IChatStreamingService, ChatStreamingService>();

            // AI Companion
            services.AddScoped<IAiCompanionService, AiCompanionService>();
            services.AddHostedService<AiFollowUpBackgroundService>();

            // Teaching Intelligence
            services.AddScoped<ITeachingIntelligenceService, TeachingIntelligenceService>();
            services.AddHostedService<TeachingIntelligenceBackgroundService>();

            // Lecture Recording
            services.AddScoped<LectureProcessingJob>();

            // RAG
            services.AddScoped<IRagService, RagService>();
            services.AddScoped<IRagIndexingJob, RagIndexingJob>();

            return services;
        }

        // ── Storage ───────────────────────────────────────────────────────────
        public static IServiceCollection AddStorageServices(
            this IServiceCollection services, IConfiguration config)
        {
            services.AddSingleton<IStorageService, CloudflareR2StorageService>();
            services.AddScoped<IFileService, FileService>();
            services.AddScoped<IStudentFileService, StudentFileService>();
            return services;
        }

        // ── Background Jobs ───────────────────────────────────────────────────
        public static IServiceCollection AddBackgroundJobs(
            this IServiceCollection services, IConfiguration config)
        {
            var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
                ?? config.GetConnectionString("DefaultConnection")!;

            services.AddHangfire(cfg =>
                cfg.UsePostgreSqlStorage(c =>
                    c.UseNpgsqlConnection(connectionString)));

            services.AddHangfireServer();
            services.AddScoped<IAiBackgroundJob, AiBackgroundJob>();
            return services;
        }
    }
}
