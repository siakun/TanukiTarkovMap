using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using CommunityToolkit.Mvvm.Messaging;
using TanukiTarkovMap.Messages;
using TanukiTarkovMap.Models.Utils;
using TanukiTarkovMap.Core.Utils;

namespace TanukiTarkovMap.Localization
{
    public static class AppLanguage
    {
        public static IReadOnlyList<string> Supported { get; } = ["ko", "en", "ja"];

        public const string XmlLanguageKey = "AppXmlLanguage";

        private const string FallbackLanguage = "en";

        private static readonly CultureInfo WindowsCulture = CultureInfo.CurrentUICulture;

        private static ResourceDictionary? _texts;

        public static string WindowsLanguage { get; } = SupportedLanguageOf(WindowsCulture) ?? FallbackLanguage;

        public static string Current { get; private set; } = WindowsLanguage;

        public static XmlLanguage CurrentXmlLanguage =>
            XmlLanguage.GetLanguage(CultureInfo.CreateSpecificCulture(Current).IetfLanguageTag);

        public static string Resolve(string? setting) =>
            setting is { } language && Supported.Contains(language) ? language : WindowsLanguage;

        public static string NativeName(string language) => CultureInfo.GetCultureInfo(language).NativeName;

        public static void Apply(string? setting)
        {
            var language = Resolve(setting);
            var changing = _texts != null;
            if (changing && language == Current) return;

            var culture = CultureInfo.GetCultureInfo(language);
            Strings.Culture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            Current = language;
            ReplaceTexts(culture);

            Logger.SimpleLog($"[AppLanguage] Applied {language} (setting: '{setting}', windows: {WindowsCulture.Name})");
            if (changing) WeakReferenceMessenger.Default.Send(new LanguageChangedMessage(language));
        }

        private static string? SupportedLanguageOf(CultureInfo culture)
        {
            for (var current = culture; !string.IsNullOrEmpty(current.Name); current = current.Parent)
            {
                if (Supported.Contains(current.Name)) return current.Name;
            }
            return null;
        }

        private static void ReplaceTexts(CultureInfo culture)
        {
            var neutralTexts = Strings.ResourceManager.GetResourceSet(CultureInfo.InvariantCulture, createIfNotExists: true, tryParents: true)
                               ?? throw new InvalidOperationException("기본 문구 리소스(Strings.resx)를 찾지 못했습니다.");

            var texts = new ResourceDictionary();
            foreach (DictionaryEntry entry in neutralTexts)
            {
                var key = (string)entry.Key;
                texts[key] = Strings.ResourceManager.GetString(key, culture);
            }

            texts[XmlLanguageKey] = CurrentXmlLanguage;

            var dictionaries = Application.Current.Resources.MergedDictionaries;
            if (_texts != null) dictionaries.Remove(_texts);
            dictionaries.Add(texts);
            _texts = texts;
        }
    }
}
