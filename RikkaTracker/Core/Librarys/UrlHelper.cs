using System;
using System.Text.RegularExpressions;

namespace RikkaTracker.Core.Librarys
{
    public static class UrlHelper
    {
        private static readonly string[] DefaultIgnorePatterns =
        {
            "^chrome://.*",
            "^edge://.*",
            "^view-source:.*",
            "^about:.*",
            "^chrome-extension://.*",
            "^extension://.*",
            "^devtools://.*"
        };

        public static string GetDomain(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return string.Empty;

            var ph = Regex.Match(url, @"((https|http|ftp|rtsp|mms)?://)");
            var result = url;
            if (ph.Success)
                result = result.Replace(ph.Value, string.Empty);

            int index = result.IndexOf('/');
            if (index != -1)
                result = result[..index];

            return result;
        }

        public static string GetName(string domain)
        {
            if (string.IsNullOrWhiteSpace(domain))
                return string.Empty;

            var parts = domain.Split('.');
            string[] skipPrefixes = { "www" };

            if (parts.Length == 2)
                return FirstLetterToUpper(parts[0]);

            if (parts.Length >= 3)
                return skipPrefixes.Contains(parts[0])
                    ? FirstLetterToUpper(parts[1])
                    : $"{FirstLetterToUpper(parts[1])} {FirstLetterToUpper(parts[0])}";

            return FirstLetterToUpper(domain);
        }

        public static bool IsDefaultIgnore(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return true;

            foreach (var pattern in DefaultIgnorePatterns)
            {
                if (Regex.IsMatch(url, pattern, RegexOptions.IgnoreCase))
                    return true;
            }
            return false;
        }

        private static string FirstLetterToUpper(string str)
        {
            if (string.IsNullOrWhiteSpace(str))
                return str;
            return char.ToUpper(str[0]) + str[1..];
        }
    }
}
