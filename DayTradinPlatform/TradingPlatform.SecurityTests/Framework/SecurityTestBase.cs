using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.Security.Application;
using Xunit.Abstractions;
using TradingPlatform.UnitTests.Framework;

namespace TradingPlatform.SecurityTests.Framework
{
    /// <summary>
    /// Base class for security tests with common security validation utilities
    /// </summary>
    public abstract class SecurityTestBase : CanonicalTestBase
    {
        protected SecurityTestBase(ITestOutputHelper output) : base(output)
        {
        }

        /// <summary>
        /// Common SQL injection test patterns
        /// </summary>
        protected static readonly string[] SqlInjectionPatterns = new[]
        {
            "' OR '1'='1",
            "'; DROP TABLE users; --",
            "1' OR '1' = '1",
            "' OR 1=1--",
            "admin'--",
            "' UNION SELECT * FROM users--",
            "1' AND '1' = '2",
            "' OR 'a'='a",
            "'; EXEC xp_cmdshell('dir'); --",
            "' OR EXISTS(SELECT * FROM users WHERE username='admin' AND password LIKE '%')"
        };

        /// <summary>
        /// Common XSS test patterns
        /// </summary>
        protected static readonly string[] XssPatterns = new[]
        {
            "<script>alert('XSS')</script>",
            "<img src=x onerror=alert('XSS')>",
            "<iframe src='javascript:alert(\"XSS\")'></iframe>",
            "<svg onload=alert('XSS')>",
            "javascript:alert('XSS')",
            "<body onload=alert('XSS')>",
            "<input type=\"text\" value=\"\" onfocus=\"alert('XSS')\">",
            "';alert(String.fromCharCode(88,83,83))//",
            "<IMG SRC=\"javascript:alert('XSS');\">",
            "<SCRIPT SRC=http://xss.rocks/xss.js></SCRIPT>"
        };

        /// <summary>
        /// Path traversal test patterns
        /// </summary>
        protected static readonly string[] PathTraversalPatterns = new[]
        {
            "../../../etc/passwd",
            "..\\..\\..\\windows\\system32\\config\\sam",
            "../../../../../../../../etc/hosts",
            "..%2F..%2F..%2Fetc%2Fpasswd",
            "..%5C..%5C..%5Cwindows%5Csystem32%5Cconfig%5Csam",
            "/var/www/../../etc/passwd",
            "C:\\..\\..\\..\\windows\\system32\\drivers\\etc\\hosts",
            "....//....//....//etc/passwd",
            "..%252f..%252f..%252fetc%252fpasswd",
            "..%c0%af..%c0%af..%c0%afetc%c0%afpasswd"
        };

        /// <summary>
        /// Validate input against SQL injection
        /// </summary>
        protected bool IsSqlInjectionSafe(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return true;

            var lowerInput = input.ToLowerInvariant();
            
            // Check for common SQL keywords in suspicious contexts
            var suspiciousPatterns = new[]
            {
                @"(\b(union|select|insert|update|delete|drop|create|alter|exec|execute)\b.*\b(from|into|where|table)\b)",
                @"(--|#|\/\*|\*\/)",
                @"(\bor\b.*=.*\bor\b)",
                @"('.*\bor\b.*'.*=.*')",
                @"(;.*\b(drop|delete|update|insert|create|alter)\b)",
                @"(\bexec\b.*\()",
                @"(\bxp_\w+)",
                @"(\bsp_\w+)"
            };

            foreach (var pattern in suspiciousPatterns)
            {
                if (Regex.IsMatch(lowerInput, pattern, RegexOptions.IgnoreCase))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Validate input against XSS attacks
        /// </summary>
        protected bool IsXssSafe(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return true;

            // Check if the input contains any HTML/JavaScript
            var htmlPattern = @"<[^>]+>|javascript:|on\w+\s*=";
            return !Regex.IsMatch(input, htmlPattern, RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// Validate file path against path traversal
        /// </summary>
        protected bool IsPathTraversalSafe(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return true;

            // Check for path traversal patterns
            var traversalPatterns = new[]
            {
                @"\.\.",
                @"%2e%2e",
                @"%252e%252e",
                @"\.\%2e",
                @"%c0%af",
                @"%c1%9c"
            };

            var lowerPath = path.ToLowerInvariant();
            foreach (var pattern in traversalPatterns)
            {
                if (lowerPath.Contains(pattern))
                    return false;
            }

            // Check for absolute paths
            if (System.IO.Path.IsPathRooted(path))
                return false;

            return true;
        }

        /// <summary>
        /// Sanitize HTML input
        /// </summary>
        protected string SanitizeHtml(string input)
        {
            return Sanitizer.GetSafeHtmlFragment(input);
        }

        /// <summary>
        /// Hash sensitive data
        /// </summary>
        protected string HashSensitiveData(string data)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(data));
                return Convert.ToBase64String(bytes);
            }
        }

        /// <summary>
        /// Validate password strength
        /// </summary>
        protected bool IsPasswordStrong(string password)
        {
            if (string.IsNullOrEmpty(password) || password.Length < 8)
                return false;

            // Must contain at least one uppercase, lowercase, number, and special character
            var hasUpper = Regex.IsMatch(password, @"[A-Z]");
            var hasLower = Regex.IsMatch(password, @"[a-z]");
            var hasNumber = Regex.IsMatch(password, @"\d");
            var hasSpecial = Regex.IsMatch(password, @"[!@#$%^&*(),.?""':{}|<>]");

            return hasUpper && hasLower && hasNumber && hasSpecial;
        }

        /// <summary>
        /// Validate email format
        /// </summary>
        protected bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            var emailPattern = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$";
            return Regex.IsMatch(email, emailPattern);
        }

        /// <summary>
        /// Generate secure random token
        /// </summary>
        protected string GenerateSecureToken(int length = 32)
        {
            var bytes = new byte[length];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            return Convert.ToBase64String(bytes);
        }

        /// <summary>
        /// Test for timing attacks
        /// </summary>
        protected void AssertConstantTimeComparison(Func<string, string, bool> comparisonFunc)
        {
            var secret = "SecretValue123!";
            var attempts = new List<long>();

            // Test with correct value
            for (int i = 0; i < 100; i++)
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                comparisonFunc(secret, secret);
                sw.Stop();
                attempts.Add(sw.ElapsedTicks);
            }

            var correctAvg = attempts.Average();
            attempts.Clear();

            // Test with incorrect value
            for (int i = 0; i < 100; i++)
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                comparisonFunc(secret, "WrongValue123!");
                sw.Stop();
                attempts.Add(sw.ElapsedTicks);
            }

            var incorrectAvg = attempts.Average();

            // The difference should be minimal (within 20% variance)
            var variance = Math.Abs(correctAvg - incorrectAvg) / correctAvg;
            variance.Should().BeLessThan(0.2, "Comparison should be constant-time to prevent timing attacks");
        }

        /// <summary>
        /// Validate API key format
        /// </summary>
        protected bool IsValidApiKey(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                return false;

            // API key should be at least 32 characters and contain only alphanumeric
            return apiKey.Length >= 32 && Regex.IsMatch(apiKey, @"^[a-zA-Z0-9]+$");
        }

        /// <summary>
        /// Check for sensitive data in logs
        /// </summary>
        protected void AssertNoSensitiveDataInLogs(string logContent, string[] sensitivePatterns)
        {
            foreach (var pattern in sensitivePatterns)
            {
                logContent.Should().NotContain(pattern, 
                    $"Logs should not contain sensitive data: {pattern}");
            }
        }
    }
}