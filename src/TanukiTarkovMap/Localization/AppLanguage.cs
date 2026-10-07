using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using CommunityToolkit.Mvvm.Messaging;
using TanukiTarkovMap.Messages;
using TanukiTarkovMap.Models.Utils;

/**
AppLanguage - 앱 화면 언어를 정하고, 실행 중에 바꾸면 화면 문구를 그 자리에서 모두 바꾼다

Purpose: settings.json의 Language로 화면 언어를 정한다. 값이 비어 있으면 Windows 표시 언어를 따르고,
지원하지 않는 Windows 언어는 영어로 표시한다. 설정 화면에서 언어를 고르면 다시 시작하지 않고 바뀌어야
하므로, 지금 언어와 바꾸는 방법을 이 클래스 한곳에 둔다.

Architecture: 문구의 원본은 Localization/Strings*.resx이고, 빌드가 Strings.resx에서 Strings 클래스를 만든다.
문구가 화면에 닿는 길은 둘이다.
- XAML: {loc:Text 키}(TextExtension)가 DynamicResource로 Application 리소스의 문구 사전을 가리킨다.
  Apply가 사전을 새 언어의 사전으로 바꾸면 WPF가 그 키를 쓰는 곳을 모두 다시 그린다.
- C#: Strings.키를 읽는다. Apply가 Strings.Culture를 정하므로 어느 스레드에서 읽어도 앱 언어다.
  ViewModel이 계산해 내보내는 문구는 LanguageChangedMessage를 받아 다시 알린다.

Core Functionality:
- Apply(setting): 언어를 정해 문구 사전과 Strings.Culture를 바꾼다. 시작할 때 창을 만들기 전에 한 번,
  설정 화면에서 고를 때마다 부른다. 실행 중에 바뀐 경우에만 LanguageChangedMessage를 보낸다
- Resolve(setting): 설정 값을 실제 언어 코드로 바꾼다. 빈 값과 지원하지 않는 값은 Windows 언어다
- NativeName(language): 그 언어로 적은 언어 이름 (한국어, English, 日本語)

State Management:
- WindowsCulture: 시작할 때의 Windows 표시 언어. 기본 UI 언어를 앱 언어로 바꾸기 전에 읽어 둔다
- Current: 지금 화면 언어 코드. Apply 전에는 Windows 언어다
- _texts: Application 리소스에 넣은 지금 언어의 문구 사전. 언어를 바꾸면 통째로 교체한다

Method Flow:
  App 시작 -> Settings.Load -> Apply(settings.Language) -> 창 생성 ({loc:Text}가 사전에서 문구를 찾는다)
  설정 화면에서 선택 -> SettingsViewModel 저장 -> Apply(code) -> 사전 교체 (XAML 문구 갱신)
    -> LanguageChangedMessage -> SettingsViewModel (계산 문구 다시 알림), App (트레이 메뉴 다시 만들기),
       WebBrowserViewModel (Local 미니맵 안내)

Dependencies:
- Strings: 빌드가 만든 문구 클래스. ResourceManager로 언어별 문구를 읽는다
- Application.Current.Resources: 문구 사전을 넣는 자리. UI 스레드에서만 부른다
- WeakReferenceMessenger: LanguageChangedMessage

Design Rationale: 언어를 고르는 즉시 화면이 바뀌어야 한다. XAML 문구는 바인딩 대신 WPF가 제공하는
DynamicResource로 사전을 가리켜, 사전 하나만 바꾸면 창 안의 문구와 그 요소의 툴팁 문자열이 함께 바뀐다.
스레드의 CurrentUICulture에는 기대지 않는다. .NET은 이 값을 비동기 흐름마다 따로 들고 있어서, 언어를
바꾸기 전에 시작한 작업(버전 목록 조회 등)은 끝난 뒤에도 옛 언어로 문구를 만든다. 그래서 문구 클래스의
Strings.Culture와 새 스레드의 기본값(DefaultThreadCurrentUICulture)을 바꾼다.
창의 Language(xml:lang)도 같은 사전의 XmlLanguageKey로 바꾼다. WPF는 이 값으로 복합 글꼴의 언어별 대체
글꼴을 고르므로, 기본값 en-US로 두면 한자를 화면 언어와 다른 언어의 글꼴로 그릴 수 있다.

Known Limitations:
- 사전 교체는 창(Application.Windows) 안의 요소에만 전달된다. 어느 창에도 속하지 않은 요소(App이 코드로 만드는
  트레이 메뉴)와 툴팁 속성에 넣은 ToolTip 요소는 이전 문구로 남는다. 트레이 메뉴는 LanguageChangedMessage를 받아
  새로 만들고, 꾸민 툴팁은 문구를 소유 요소의 ToolTip 속성에 두고 모양은 암시적 스타일로 입힌다
- XAML의 키는 문자열이라 오타를 빌드가 잡지 못한다. tools/verify-localization.mjs가 XAML의 키를 resx와 대조한다
- WPF 바인딩의 StringFormat은 요소의 Language로 숫자를 쓴다. 지금 언어는 모두 소수점이 점이지만, 쉼표를 쓰는
  언어를 더하면 바인딩으로 소수를 표시하는 자리를 확인해야 한다
- CEF 로캘(App.InitializeCef)과 Online 사이트의 언어는 이 값을 따르지 않는다. 이유는 AGENTS.md의 "화면 언어"에 있다

Last Updated: 2026-10-08 | .NET 8 | 화면 언어 설정(한국어, 영어, 일본어) 추가
*/
namespace TanukiTarkovMap.Localization
{
    public static class AppLanguage
    {
        /// <summary>
        /// 고를 수 있는 언어 코드. 설정 화면에 이 순서로 나온다.
        /// 영어는 기본 Strings.resx가, 나머지는 Strings.(코드).resx가 문구를 담는다
        /// </summary>
        public static IReadOnlyList<string> Supported { get; } = ["ko", "en", "ja"];

        /// <summary> 문구 사전에서 창의 Language(xml:lang)를 담는 키 </summary>
        public const string XmlLanguageKey = "AppXmlLanguage";

        /// <summary> 지원하지 않는 Windows 언어일 때 쓰는 언어. 기본 Strings.resx의 언어(NeutralLanguage)와 같다 </summary>
        private const string FallbackLanguage = "en";

        // 기본 UI 언어를 바꾸기 전에 읽어야 Windows 표시 언어다. 이 값을 쓰는 아래 속성보다 먼저 초기화되도록 앞에 둔다
        private static readonly CultureInfo WindowsCulture = CultureInfo.CurrentUICulture;

        private static ResourceDictionary? _texts;

        /// <summary> Windows 표시 언어에 해당하는 지원 언어. 없으면 영어 </summary>
        public static string WindowsLanguage { get; } = SupportedLanguageOf(WindowsCulture) ?? FallbackLanguage;

        /// <summary> 지금 화면 언어 코드 </summary>
        public static string Current { get; private set; } = WindowsLanguage;

        /// <summary>
        /// 지금 화면 언어의 xml:lang (ja-JP처럼 지역까지 정한 태그).
        /// 창은 문구 사전의 XmlLanguageKey로, 창 밖에서 만드는 요소(트레이 메뉴)는 이 값으로 받는다
        /// </summary>
        public static XmlLanguage CurrentXmlLanguage =>
            XmlLanguage.GetLanguage(CultureInfo.CreateSpecificCulture(Current).IetfLanguageTag);

        /// <summary> 설정 값을 언어 코드로 바꾼다. 빈 값과 지원하지 않는 값은 Windows 언어를 따른다 </summary>
        public static string Resolve(string? setting) =>
            setting is { } language && Supported.Contains(language) ? language : WindowsLanguage;

        /// <summary> 그 언어로 적은 언어 이름. 지금 화면 언어를 읽지 못하는 사람도 자기 언어를 찾게 한다 </summary>
        public static string NativeName(string language) => CultureInfo.GetCultureInfo(language).NativeName;

        /// <summary>
        /// 화면 언어를 정한다. 처음 부르면 문구 사전을 넣고, 그 뒤에는 언어가 달라졌을 때만 사전을 바꾸고 알린다
        /// </summary>
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

        /// <summary> ko-KR이면 ko처럼 상위 문화권까지 올라가며 지원 언어를 찾는다 </summary>
        private static string? SupportedLanguageOf(CultureInfo culture)
        {
            for (var current = culture; !string.IsNullOrEmpty(current.Name); current = current.Parent)
            {
                if (Supported.Contains(current.Name)) return current.Name;
            }
            return null;
        }

        /// <summary>
        /// 그 언어의 문구 사전을 만들어 Application 리소스의 이전 사전과 바꾼다.
        /// 키 목록은 기본 Strings.resx에서 읽고, 번역이 빠진 키는 ResourceManager가 영어 문구로 채운다
        /// </summary>
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
