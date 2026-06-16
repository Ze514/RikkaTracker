using System;
using System.Collections.Generic;
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

        private static readonly Dictionary<string, string> _domainNamesDict = new()
        {
            {"www.google.com", "Google"},
            {"github.com", "Github"},
            {"www.bilibili.com", "哔哩哔哩"},
            {"v2ex.com", "V2EX"},
            {"translate.google.com", "Google 翻译"},
            {"www.youtube.com", "YouTube"},
            {"www.reddit.com", "Reddit"},
            {"stackoverflow.com", "Stack Overflow"},
            {"www.wikipedia.org", "Wikipedia"},
            {"twitter.com", "X"},
            {"x.com", "X"},
            {"www.baidu.com", "百度"},
            {"www.zhihu.com", "知乎"},
            {"www.bing.com", "Bing"},
            {"chat.openai.com", "ChatGPT"},
            {"chatgpt.com", "ChatGPT"},
            {"claude.ai", "Claude"},
            {"mail.google.com", "Gmail"},
            {"outlook.live.com", "Outlook"},
            {"www.taobao.com", "淘宝"},
            {"www.jd.com", "京东"},
            {"www.weibo.com", "微博"},
            {"www.douyin.com", "抖音"},
            {"www.xiaohongshu.com", "小红书"},
            {"www.doubao.com", "豆包"},
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

            if (_domainNamesDict.TryGetValue(domain, out var known))
                return known;

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
