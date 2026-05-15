using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;

namespace RikkaTracker.Services
{
    public interface ILocalizationService
    {
        string CurrentLanguage { get; }
        void SetLanguage(string languageCode);
        void Initialize(string preferredLanguage);
    }

    public class LocalizationService : ILocalizationService
    {
        private const string LanguageResourceTag = "LanguageResource";
        public string CurrentLanguage { get; private set; } = "zh-CN";

        public void Initialize(string preferredLanguage)
        {
            string target = preferredLanguage;
            if (string.IsNullOrEmpty(target) || target == "Auto")
            {
                // 自动识别系统语言
                var systemCulture = CultureInfo.CurrentUICulture.Name;
                if (systemCulture.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
                    target = "zh-CN";
                else
                    target = "en-US";
            }

            SetLanguage(target);
        }

        public void SetLanguage(string languageCode)
        {
            // 目前仅支持 zh-CN 和 en-US，其余降级为 zh-CN
            string validatedCode = languageCode;
            if (validatedCode != "zh-CN" && validatedCode != "en-US")
            {
                validatedCode = "zh-CN";
            }

            try
            {
                var uri = new Uri($"/Resources/Languages/{validatedCode}.xaml", UriKind.Relative);
                var dict = new ResourceDictionary { Source = uri };

                // 移除旧的语言资源
                var mergedDicts = Application.Current.Resources.MergedDictionaries;
                var oldDict = mergedDicts.FirstOrDefault(d => d.Contains("LanguageResourceTag"));
                if (oldDict != null)
                {
                    mergedDicts.Remove(oldDict);
                }

                // 添加特征标记以便后续查找
                dict.Add("LanguageResourceTag", true);
                mergedDicts.Add(dict);

                CurrentLanguage = validatedCode;
                
                // 同时更新当前线程的 Culture
                var culture = new CultureInfo(validatedCode);
                CultureInfo.CurrentCulture = culture;
                CultureInfo.CurrentUICulture = culture;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load language resource: {ex.Message}");
                // 递归防止无限循环，若 en-US 失败则尝试 zh-CN
                if (validatedCode != "zh-CN") SetLanguage("zh-CN");
            }
        }
    }
}
