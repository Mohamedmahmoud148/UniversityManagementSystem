using System.Threading.Tasks;
using Hangfire;
using Microsoft.Extensions.Logging;
using UniversityManagementSystem.Core.Interfaces;

namespace UniversityManagementSystem.Infrastructure.Jobs
{
    public interface IEmailJob
    {
        Task SendEmailAsync(string to, string subject, string body);
    }

    /// <summary>
    /// Hangfire background job for sending emails.
    /// Enqueue with: _jobClient.Enqueue&lt;IEmailJob&gt;(j => j.SendEmailAsync(to, subject, body));
    /// </summary>
    public class EmailJob : IEmailJob
    {
        private readonly ILogger<EmailJob> _logger;

        public EmailJob(ILogger<EmailJob> logger)
        {
            _logger = logger;
        }

        [AutomaticRetry(Attempts = 3)]
        public async Task SendEmailAsync(string to, string subject, string body)
        {
            _logger.LogInformation("Sending email to {To} — subject: {Subject}", to, subject);

            // ── Pluggable implementation ──────────────────────────────────────────
            // Replace with your email provider (SendGrid, SMTP, Resend, etc.)
            // Example with SmtpClient:
            //
            // using var client = new SmtpClient(_smtpHost, _smtpPort);
            // client.Credentials = new NetworkCredential(_user, _password);
            // await client.SendMailAsync(new MailMessage(_from, to, subject, body));
            //
            // For now we log so the job completes successfully and can be replaced later.

            await Task.Delay(50); // Simulate async work
            _logger.LogInformation("Email queued for {To}", to);
        }
    }
}
