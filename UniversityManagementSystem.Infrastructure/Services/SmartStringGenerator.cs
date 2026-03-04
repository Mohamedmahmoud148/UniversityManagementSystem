using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;

namespace UniversityManagementSystem.Infrastructure.Services
{
    public class SmartStringGenerator(AppDbContext context) : ISmartStringGenerator
    {
        private readonly AppDbContext _context = context;

        public async Task<string> GenerateUniqueAsync<TEntity>(string baseValue, Expression<Func<TEntity, string>> selector) where TEntity : class
        {
            if (string.IsNullOrWhiteSpace(baseValue))
                return baseValue ?? string.Empty;

            var trimmedBase = baseValue.Trim().ToUpper();

            // Fetch only the string column from the database matching the prefix
            var existingValues = await _context.Set<TEntity>()
                .Select(selector)
                .Where(v => v != null && v.ToUpper().StartsWith(trimmedBase))
                .ToListAsync();

            // Check if exact base value exists
            bool exactMatchExists = existingValues.Any(v => v.Equals(trimmedBase, StringComparison.OrdinalIgnoreCase));

            if (!exactMatchExists)
            {
                // No exact match, safe to return base
                return trimmedBase;
            }

            int maxSuffix = 0;
            string prefixWithDash = trimmedBase + "-";

            // Find the maximum existing numeric suffix e.g., "ENGINEERING-01" -> 1
            foreach (var val in existingValues)
            {
                string upperVal = val.ToUpper();
                if (upperVal.StartsWith(prefixWithDash))
                {
                    var suffixPart = upperVal.Substring(prefixWithDash.Length);
                    if (int.TryParse(suffixPart, out int suffixValue))
                    {
                        if (suffixValue > maxSuffix)
                        {
                            maxSuffix = suffixValue;
                        }
                    }
                }
            }

            // Increment max suffix
            int newSuffix = maxSuffix + 1;
            // Pad left to ensure minimum 2 digits (e.g., -01, -02, ... -10, -11)
            return $"{trimmedBase}-{newSuffix:D2}";
        }
    }
}
