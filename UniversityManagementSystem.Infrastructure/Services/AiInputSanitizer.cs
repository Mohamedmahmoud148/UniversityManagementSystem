using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace UniversityManagementSystem.Infrastructure.Services
{
    public interface IAiInputSanitizer
    {
        (bool isSafe, string? rejectionReason) Validate(string input);
        string Sanitize(string input);
    }

    public class AiInputSanitizer(ILogger<AiInputSanitizer> logger) : IAiInputSanitizer
    {
        private static readonly string[] _dangerousPatterns =
        [
            @"ignore\s+(all\s+)?(previous|prior|above)\s+instructions",
            @"forget\s+(everything|all|previous|your\s+instructions)",
            @"reveal\s+(your\s+)?(system\s+prompt|instructions|prompt)",
            @"print\s+(your\s+)?(system\s+prompt|instructions)",
            @"what\s+(are|is)\s+your\s+(system\s+prompt|instructions|initial\s+prompt)",
            @"you\s+are\s+now\s+(a|an)\s+\w+",
            @"act\s+as\s+(if\s+you\s+are\s+)?(a|an)\s+\w+\s+(without|with\s+no)\s+restriction",
            @"bypass\s+(your\s+)?(safety|restriction|filter|limit|guideline)",
            @"jailbreak",
            @"DAN\s+mode",
            @"developer\s+mode",
            @"override\s+(your\s+)?(instruction|rule|guideline|safety)",
            @"disregard\s+(your\s+)?(previous|prior|all)\s+(instruction|rule)",
            @"you\s+must\s+(now\s+)?obey",
            @"new\s+instruction[s]?\s*:",
            @"system\s*:\s*you\s+are",
        ];

        private static readonly Regex[] _compiled = _dangerousPatterns
            .Select(p => new Regex(p, RegexOptions.IgnoreCase | RegexOptions.Compiled))
            .ToArray();

        public (bool isSafe, string? rejectionReason) Validate(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return (true, null);

            if (input.Length > 4000)
                return (false, "Message exceeds maximum allowed length.");

            foreach (var regex in _compiled)
            {
                if (regex.IsMatch(input))
                {
                    logger.LogWarning("[AiSecurity] Suspicious prompt detected. Pattern: {Pattern} | Input snippet: {Snippet}",
                        regex.ToString(), input[..Math.Min(100, input.Length)]);
                    return (false, "Your message contains content that cannot be processed by the AI assistant.");
                }
            }

            return (true, null);
        }

        public string Sanitize(string input)
        {
            // Strip null bytes and control characters that could confuse tokenizers
            return Regex.Replace(input, @"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]", string.Empty).Trim();
        }
    }
}
