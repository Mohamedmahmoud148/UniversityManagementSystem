using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using UniversityManagementSystem.Core.Interfaces;
using UniversityManagementSystem.Infrastructure.Data;

namespace UniversityManagementSystem.Infrastructure.Services
{
    public class UniversityEmailGenerator(AppDbContext context) : IUniversityEmailGenerator
    {
        private const string Domain = "benisuefnationaluniversity.edu";

        public async Task<string> GenerateStudentEmailAsync(string fullName)
        {
            var normalized = NormalizeName(fullName);
            if (string.IsNullOrEmpty(normalized)) normalized = "student";

            int n = 1;
            string email;
            do
            {
                email = $"{normalized}.student{n}@{Domain}";
                n++;
            }
            while (await context.SystemUsers.IgnoreQueryFilters()
                       .AnyAsync(u => u.Email == email || u.UniversityEmail == email));

            return email;
        }

        // ── Name normalization ────────────────────────────────────────────────

        private static string NormalizeName(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return "";

            // 1. Transliterate Arabic to English
            var transliterated = TransliterateArabic(fullName);

            // 2. Lowercase
            var lower = transliterated.ToLowerInvariant();

            // 3. Remove spaces + keep only a-z 0-9
            var clean = Regex.Replace(lower, @"[^a-z0-9]", "");

            return clean;
        }

        private static string TransliterateArabic(string input)
        {
            // Arabic → Latin transliteration table
            var map = new System.Collections.Generic.Dictionary<string, string>
            {
                // Letters
                {"ا","a"},{"أ","a"},{"إ","a"},{"آ","a"},{"ء","a"},
                {"ب","b"},{"ت","t"},{"ث","th"},{"ج","j"},{"ح","h"},
                {"خ","kh"},{"د","d"},{"ذ","th"},{"ر","r"},{"ز","z"},
                {"س","s"},{"ش","sh"},{"ص","s"},{"ض","d"},{"ط","t"},
                {"ظ","z"},{"ع","a"},{"غ","gh"},{"ف","f"},{"ق","k"},
                {"ك","k"},{"ل","l"},{"م","m"},{"ن","n"},{"ه","h"},
                {"و","w"},{"ي","y"},{"ى","a"},{"ة","a"},{"ئ","y"},
                {"ؤ","w"},{"لا","la"},
                // Diacritics (remove)
                {"َ",""},{"ُ",""},{"ِ",""},{"ً",""},{"ٌ",""},{"ٍ",""},
                {"ّ",""},{"ْ",""},{"ٰ",""},{"ٱ","a"},
            };

            var sb = new StringBuilder();
            int i = 0;
            while (i < input.Length)
            {
                // Try 2-char match first (لا)
                if (i + 1 < input.Length)
                {
                    var two = input.Substring(i, 2);
                    if (map.TryGetValue(two, out var twoVal))
                    {
                        sb.Append(twoVal);
                        i += 2;
                        continue;
                    }
                }
                var one = input[i].ToString();
                if (map.TryGetValue(one, out var oneVal))
                    sb.Append(oneVal);
                else
                    sb.Append(one); // keep as-is (e.g. Latin chars)
                i++;
            }

            return sb.ToString();
        }
    }
}
