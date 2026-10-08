<!--
INTENT
검토자가 이번 브랜치에서 무엇이 문제였고 코드에서 무엇을 바꿨는지를 한 번에 판단하도록 쓴 작업 보고서다.
PR 설명으로도 쓴다. 설계 근거의 원천은 각 파일의 머리 주석, AGENTS.md의 "화면 언어", WPF 다국어 레퍼런스이고,
이 문서는 문제와 수정의 대응과 검증 결과만 정리한다.
-->

# 화면 언어 설정 작업 보고

## 요약

- 설정 맨 위에서 화면 언어를 고르면 다시 시작하지 않고 앱 전체 문구가 그 자리에서 바뀝니다. 선택지는
  `Windows 설정 따르기 (한국어)`, `한국어`, `English`, `日本語`입니다. 기본값은 Windows 표시 언어이고, 지원하지
  않는 Windows 언어는 영어로 표시합니다. 기존 사용자도 같은 기본값을 따릅니다.
- 화면 문구 98개를 resx로 옮겼습니다. 빌드할 때 문구 클래스를 만드는 표준 방식이 WPF 임시 어셈블리 단계에서
  실패해, 클래스 생성을 XAML 컴파일 앞으로 당겼습니다.
- 즉시 전환이 닿지 않는 곳(ViewModel이 만든 문구, 트레이 메뉴, 툴팁 요소, Local 미니맵)을 화면을 띄우지 않는
  검사로 찾아 각각 다시 만들게 했습니다.
- Online 사이트 언어와 CEF 로캘은 PMC/SCAV 전환을 지키려고 앱 언어와 묶지 않았습니다.
- 빌드, 번역 검사 7개, Local 미니맵 73개 항목, Online 위치 복구 12개 항목이 통과했습니다. 실제 앱 실행
  확인은 남아 있습니다.

## 문제와 수정

### 1. 설정: 화면 언어를 고를 수 없음

**문제**: 화면 문구가 XAML과 C#에 한국어로 직접 적혀 있어 다른 언어로 바꿀 방법이 없었습니다. 일부 라벨은
두 언어를 함께 적어 대신했고, 폴더 선택 창 제목은 영어로만 적혀 있었습니다.

```xml
<TextBlock Text="게임 폴더 경로 / Game Folder Path" .../>
<Button Content="찾아보기" Width="65" .../>
```

```csharp
Title = "Select Escape From Tarkov game folder",
BrowserCacheSizeText = "확인 중...";
var exitItem = new MenuItem { Header = "종료" };
```

**수정**

- 문구를 `src/TanukiTarkovMap/Localization/`의 resx 세 개로 옮겼습니다. `Strings.resx`가 영어 기본 문구이고,
  같은 키로 `Strings.ko.resx`와 `Strings.ja.resx`에 번역합니다. 키는 `화면_항목` 꼴입니다(`Settings_GameFolder`,
  `Tray_Exit`).
- XAML은 `{loc:Text 키}`, C#은 빌드가 만드는 `Strings` 클래스의 속성으로 읽습니다.

  ```xml
  <TextBlock Text="{loc:Text Settings_GameFolder}" .../>
  <Button Content="{loc:Text Common_Browse}" MinWidth="65" .../>
  ```

  ```csharp
  Title = Strings.Dialog_SelectGameFolder,
  var exitItem = new MenuItem { Header = Strings.Tray_Exit };
  ```

- 설정 화면 맨 위에 언어 섹션을 추가했습니다(`SettingsPage.xaml`). 언어 이름은 그 언어로 적어, 지금 화면 언어를
  읽지 못하는 사람도 자기 언어를 찾게 했습니다. 고른 값은 `settings.json`의 `Language`에 저장합니다.
- 한국어 문구는 지금 화면 문구를 그대로 옮겼습니다. 두 언어를 함께 적은 라벨만 언어별 한 가지로 바꿨습니다.
- 로그와 예외 메시지는 화면 문구가 아니므로 옮기지 않았습니다. `PMC`, `SCAV`, `Online`, `Local`, `Map:`과 맵
  이름도 그대로 둡니다.

**확인**: `verify-localization.mjs`가 XAML의 주석 밖에 한글 문구가 남지 않았음을 확인합니다(통과).

### 2. 빌드: resx 문구 클래스를 빌드할 때 만들면 WPF 임시 어셈블리가 찾지 못함

**문제**: resx에서 문구 클래스를 만드는 표준 방식은 `EmbeddedResource`에 `StronglyTyped*` 메타데이터를 다는
것입니다. 재현 프로젝트에서 이 방식은 다음 오류로 빌드에 실패했습니다.

```
error CS0234: 'Probe' 네임스페이스에 'Localization' 형식 또는 네임스페이스 이름이 없습니다
  [...\Probe_xxxxxxxx_wpftmp.csproj]
```

Visual Studio의 ResXFileCodeGenerator는 디자이너에서 저장할 때만 `Strings.Designer.cs`를 갱신하므로,
`dotnet build`만 쓰면 resx와 클래스가 어긋납니다. 둘 다 이 저장소에는 맞지 않았습니다.

**원인**: XAML이 같은 프로젝트의 형식을 참조하면 WPF 빌드는 마크업 컴파일 2단계 전에 임시 어셈블리(`_wpftmp`)를
따로 컴파일합니다. 자세한 빌드 로그(`-v:d`)의 순서는 `MarkupCompilePass1` -> `GenerateTemporaryTargetAssembly`
(임시 프로젝트의 `CoreCompile` 실패)였고, 클래스를 만드는 `CoreResGen`은 실패할 때까지 한 번도 실행되지
않았습니다.

**수정** (`src/TanukiTarkovMap/TanukiTarkovMap.csproj`): XAML 컴파일 전에 `GenerateResource` 태스크를 직접
실행하고, 결과 파일을 고정 경로의 Compile 항목으로 넣었습니다. 경로가 고정돼 있어 원래 프로젝트와 임시
프로젝트가 같은 항목을 보고, 임시 컴파일이 시작될 때 파일은 이미 있습니다.

```xml
<NeutralLanguage>en</NeutralLanguage>

<ItemGroup>
  <Compile Include="$(IntermediateOutputPath)Strings.g.cs" />
</ItemGroup>

<Target Name="GenerateLocalizedStrings"
        BeforeTargets="MarkupCompilePass1;CoreCompile"
        Inputs="Localization\Strings.resx"
        Outputs="$(IntermediateOutputPath)Strings.g.cs">
  <GenerateResource Sources="Localization\Strings.resx"
                    OutputResources="$(IntermediateOutputPath)Strings.class-only.resources"
                    StronglyTypedLanguage="CSharp"
                    StronglyTypedNamespace="TanukiTarkovMap.Localization"
                    StronglyTypedClassName="Strings"
                    StronglyTypedManifestPrefix="TanukiTarkovMap.Localization"
                    StronglyTypedFileName="$(IntermediateOutputPath)Strings.g.cs"
                    PublicClass="true"
                    ExecuteAsTool="false" />
</Target>
```

- `ExecuteAsTool="false"`를 빼면 `ResGen.exe not supported on .NET Core MSBuild`로 실패합니다.
- `NeutralLanguage`를 `en`으로 정해, 영어로 실행할 때는 위성 어셈블리를 찾지 않고 본 어셈블리의 문구를 읽습니다.
- `Inputs`와 `Outputs`는 resx가 문자열만 담는다는 전제입니다. resx가 다른 파일을 링크하면 그 파일의 변경을
  놓치므로, 그때는 이 조건을 지우라고 주석에 적었습니다.

**확인**: 빌드가 통과하고, 출력 폴더에 `ko\`, `ja\` 위성 어셈블리가 생깁니다. 재현 프로젝트에서 다음을
확인했습니다.

- XAML 키 오타(`{x:Static loc:Strings.Helo}`)가 `MC3011` 빌드 오류로 잡힘
- 키를 추가하고 정리 없이 다시 빌드하면 클래스가 다시 만들어짐
- `ko-KR`은 한국어로, 번역이 없는 `fr`은 영어로 대체됨

원인과 진단 절차는 [WPF 다국어 레퍼런스](20261008-wpf-localization-resx-and-live-switch.md) 1장에 정리했습니다.

### 3. 시작: Windows 표시 언어를 기본으로 정할 기준이 없음

**문제**: 기본값을 Windows 표시 언어로 하려면 그 언어를 읽어 지원 언어로 바꾸는 규칙이 필요했습니다. 표시
언어는 `CurrentUICulture`로 읽는데, 앱 언어를 적용하면 이 값도 바뀌므로 그 전에 읽어 두어야 합니다. 기존
사용자의 `settings.json`에는 언어 값이 없습니다.

**수정** (`Localization/AppLanguage.cs`, `Models/Data/DataTypes.cs`)

- 앱 언어를 적용하기 전에 Windows 표시 언어를 읽어 둡니다. 정적 필드는 적힌 순서대로 초기화되므로, 이 값을
  쓰는 속성보다 앞에 둡니다.
- `ko-KR`처럼 지역까지 정한 문화권은 상위 문화권으로 올라가며 지원 언어를 찾고, 없으면 영어입니다.

  ```csharp
  private static readonly CultureInfo WindowsCulture = CultureInfo.CurrentUICulture;

  public static string WindowsLanguage { get; } = SupportedLanguageOf(WindowsCulture) ?? FallbackLanguage;

  private static string? SupportedLanguageOf(CultureInfo culture)
  {
      for (var current = culture; !string.IsNullOrEmpty(current.Name); current = current.Parent)
      {
          if (Supported.Contains(current.Name)) return current.Name;
      }
      return null;
  }
  ```

- `AppSettings.Language`의 기본값을 빈 문자열(Windows 언어를 따름)로 두었습니다. 언어 값이 없는 예전 설정
  파일도 별도의 마이그레이션 규칙 없이 Windows 언어를 따릅니다.

**확인**: 화면을 띄우지 않는 검사에서 Windows 언어를 프랑스어로 두고 시작하면 `WindowsLanguage`가 `en`이 되고,
빈 설정이 영어를 따랐습니다.

### 4. 시작: 설정을 창보다 늦게 읽어 첫 화면을 고른 언어로 띄울 수 없음

**문제**: `Application_Startup`의 순서가 CEF 초기화 -> 스플래시 -> 트레이 아이콘 생성 -> `Settings.Load()`였습니다.
언어 설정을 읽기 전에 스플래시와 트레이 메뉴가 만들어지므로, Windows 언어와 다른 언어를 고른 사용자는 첫
화면을 다른 언어로 보게 됩니다.

**수정** (`App.xaml.cs`): 설정을 읽고 언어를 적용하는 단계를 CEF 초기화 앞으로 옮겼습니다. 언어는 부가
기능이므로, 적용이 실패해도 로그만 남기고 시작은 계속합니다.

```csharp
AppPaths.PrepareOnStartup();

// 1. 설정을 읽고 화면 언어를 정한다
Settings.Load();
ApplyLanguage();

// 2. CEF 초기화. 시작에서 가장 오래 걸리는 단계다
InitializeCef();
ShowSplashIfStartupIsSlow(Strings.Startup_Initializing);
```

```csharp
private static void ApplyLanguage()
{
    try
    {
        AppLanguage.Apply(GetSettings().Language);
    }
    catch (Exception ex)
    {
        Logger.SimpleLog($"[AppLanguage] Failed to apply language: {ex}");
    }
}
```

순서만 바뀌므로 전체 시작 시간은 같습니다. 스플래시는 CEF 초기화 뒤에야 뜰 수 있는데 설정 읽기가 그보다
앞으로 가면서, 스플래시의 "설정을 불러오는 중..." 단계는 없어졌습니다.

**확인**: Debug와 Release 빌드가 통과했습니다. 실제 시작 순서는 앱 실행 확인이 남아 있습니다.

### 5. 즉시 전환: 이미 그린 XAML 문구를 다시 그릴 방법이 없음

**문제**: 언어를 고르는 즉시 화면이 바뀌어야 합니다. `{x:Static}`이나 한 번 넣은 문자열은 화면을 만들 때 한 번만
읽으므로, 나중에 언어를 바꿔도 화면이 바뀌지 않습니다.

**수정** (`Localization/AppLanguage.cs`, `Localization/TextExtension.cs`)

- `{loc:Text 키}`는 WPF의 `DynamicResource`와 같은 마크업 확장입니다. 화면 문구 참조를 다른 동적 리소스와
  구별하고 검사 도구가 찾을 수 있게 이름을 따로 두었습니다.

  ```csharp
  public sealed class TextExtension : DynamicResourceExtension
  {
      public TextExtension() { }

      public TextExtension(string key) : base(key) { }
  }
  ```

- `AppLanguage.Apply`는 그 언어의 문구 사전을 만들어 Application 리소스의 이전 사전과 바꿉니다. 키 목록은 기본
  resx에서 읽고, 번역이 빠진 키는 `ResourceManager`가 영어 문구로 채웁니다.

  ```csharp
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
  ```

- 설정 화면에서 언어를 고르면 저장한 뒤 바로 적용합니다(`SettingsViewModel.OnSelectedLanguageChanged`).

**확인**: 화면을 띄우지 않는 검사에서 설정 화면을 일본어로 만든 뒤 한국어로 바꾸자, XAML의 모든 키 문구가 그
자리에서 한국어로 바뀌었습니다.

### 6. 즉시 전환: ViewModel이 만들어 저장해 둔 문구가 이전 언어로 남음

**문제**: `SettingsViewModel`은 일이 생긴 순간 문구를 만들어 속성에 저장했습니다. 사전 교체는 저장한 문자열에
닿지 않으므로, 언어를 바꿔도 이 문구들은 이전 언어로 남습니다.

```csharp
BrowserCacheSizeText = "확인 중...";
UpdateStatusMessage = "GitHub 요청 한도를 넘었습니다. 한 시간쯤 뒤에 다시 시도하세요";
InstallProgressText = $"{percent}% ({receivedMegabytes:F1} / {totalMegabytes:F1} MB)";
AvailableVersions.Add(new VersionItem(release, BuildDisplayName(release, isCurrent, isLatest), isCurrent, isLatest));
```

**수정** (`ViewModels/SettingsViewModel.cs`): 문자열 대신 상태를 저장하고, 화면이 읽을 때 지금 언어로 만드는
계산 속성으로 바꿨습니다.

- 캐시 크기: 바이트 수와 측정 여부를 저장합니다.

  ```csharp
  public string BrowserCacheSizeText => _measuringBrowserCache
      ? Strings.Cache_Measuring
      : _browserCacheBytes switch
      {
          null => string.Empty,
          { } bytes when bytes > 0 => $"{bytes / 1024d / 1024d:N1} MB",
          _ => Strings.Cache_Empty,
      };
  ```

- 업데이트 실패 안내: 경우가 넷이고 버전과 오류 같은 값이 붙으므로, 문구 대신 문구를 만드는 함수를 저장합니다.

  ```csharp
  private Func<string>? _updateStatus;
  public string UpdateStatusMessage => _updateStatus?.Invoke() ?? string.Empty;

  ShowUpdateStatus(() => Strings.Update_RateLimited);
  ShowUpdateStatus(() => string.Format(Strings.Update_InstallFailed, version, reason));
  ```

- 설치 진행률: `IsInstalling`과 `InstallProgress`로 계산합니다.
- 버전 목록 라벨(현재, 최신, 베타): `VersionItem`을 record에서 `ObservableObject` 클래스로 바꾸고 `DisplayName`을
  읽을 때 만듭니다. 언어 선택지의 `Windows 설정 따르기 (한국어)`도 같은 방식입니다(`LanguageOption`).
- 언어가 바뀌면 모든 속성을 다시 읽게 합니다. 속성 이름을 빈 문자열로 보내면 WPF는 모든 속성이 바뀐 것으로
  보므로, 계산 속성을 하나씩 나열하다 빠뜨리는 일이 없습니다.

  ```csharp
  public void Receive(LanguageChangedMessage message)
  {
      OnPropertyChanged(string.Empty);
      foreach (var option in LanguageOptions) option.RefreshText();
      foreach (var version in AvailableVersions) version.RefreshText();
  }
  ```

진행률 문구를 계산 속성으로 바꾸면서 생길 수 있던 회귀도 하나 막았습니다. 설치를 시작할 때 부르는
`ReportInstallProgress(0)`은 `InstallProgress`가 이미 0이면 변경 알림을 내지 않습니다. 이 경우 바뀐 받을
크기(`_installTargetBytes`)가 문구에 반영되지 않으므로, 진행률을 보고할 때마다 문구 알림을 직접 보냅니다.

```csharp
private void ReportInstallProgress(int percent)
{
    InstallProgress = percent;

    // 진행률이 같아도 받을 크기(_installTargetBytes)가 바뀌었을 수 있으므로 문구는 늘 다시 알린다
    OnPropertyChanged(nameof(InstallProgressText));
}
```

**확인**: 화면을 띄우지 않는 검사에서 언어를 바꾸자 `PropertyChanged("")`가 나갔고, 캐시 비우기 버튼에 그려진
문구가 새 언어로 바뀌었습니다.

### 7. 즉시 전환: 언어를 바꾸기 전에 시작한 비동기 작업이 이전 언어로 문구를 만듦

**문제**: .NET은 `CurrentUICulture`를 비동기 작업의 컨텍스트에 실어 나릅니다. 공식 문서는 비동기 작업이 시작한
스레드의 `CurrentCulture`와 `CurrentUICulture`를 물려받는다고 설명합니다. 버전 목록 조회처럼 언어를 바꾸기 전에
시작한 작업이 `await` 뒤에 스레드 문화권으로 문구를 찾으면 이전 언어가 나옵니다.

**수정** (`AppLanguage.Apply`): 스레드의 `CurrentUICulture`를 바꾸지 않고, 생성된 문구 클래스의 `Strings.Culture`와
새 스레드의 기본값을 바꿉니다. `Strings.키`는 `Strings.Culture`로 문구를 찾으므로 어느 흐름에서 읽어도 지금 앱
언어입니다.

```csharp
Strings.Culture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;
```

**확인**: 재현 프로젝트에서 `Task.Run` 안에서 읽은 문구가 앱 언어와 같았습니다. 언어를 바꾼 뒤에 끝나는 비동기
작업의 경우는 공식 문서의 동작을 근거로 한 설계이며, 따로 재현하지는 않았습니다.

### 8. 즉시 전환: 트레이 메뉴와 업데이트 툴팁이 이전 언어로 남음

**문제**: 처음에는 트레이 메뉴 항목과 업데이트 준비 툴팁도 문구 사전을 가리키게(`SetResourceReference`,
`{loc:Text}`) 만들었습니다. 화면을 띄우지 않는 검사로 확인하니 다음 두 요소는 사전을 바꿔도 이전 언어
그대로였습니다.

- 어느 창에도 속하지 않은 메뉴 항목(트레이 메뉴와 같은 구조)
- 버튼의 `ToolTip` 속성 값으로 넣은 ToolTip 요소(업데이트 준비 툴팁과 같은 구조)

**원인**: Application 리소스가 바뀌면 WPF는 `Application.Windows`의 창 트리만 걸으며 참조를 다시 찾습니다.
검사에서 그 메뉴 항목을 패널에 붙이자 새 언어를 다시 찾았으므로, 트리에 붙을 때 다시 찾는 것은 맞습니다.
그러나 WPF 소스(dotnet/wpf의 `ContextMenu.cs`, `ToolTip.cs`)를 보면 둘 다 처음 열릴 때 만든 팝업(`_parentPopup`)을
계속 재사용합니다. 그래서 열릴 때마다 다시 찾는다고 기대할 수 없습니다.

**수정**

- 트레이 메뉴(`App.xaml.cs`): 메뉴를 만드는 부분을 `CreateTrayMenu()`로 분리하고, 언어가 바뀌면 메뉴를 새로
  만듭니다.

  ```csharp
  WeakReferenceMessenger.Default.Register<App, LanguageChangedMessage>(this,
      static (app, _) => { if (app._trayIcon != null) app._trayIcon.ContextMenu = app.CreateTrayMenu(); });
  ```

  ```csharp
  private ContextMenu CreateTrayMenu()
  {
      var contextMenu = new ContextMenu { Language = AppLanguage.CurrentXmlLanguage };

      var toggleWindowItem = new MenuItem { Header = Strings.Tray_ToggleWindow };
      toggleWindowItem.Click += (s, args) => ToggleMainWindow();
      contextMenu.Items.Add(toggleWindowItem);
      // 설정, Tarkov Market 열기, 종료도 같은 방식
      return contextMenu;
  }
  ```

- 업데이트 준비 툴팁(`MainWindow.xaml`): 문구를 ToolTip 요소 안에서 버튼의 `ToolTip` 속성으로 옮겼습니다. 창 안
  버튼의 속성에는 사전 교체가 닿습니다. 말풍선 모양은 버튼 범위의 암시적 스타일로 입힙니다.

  ```xml
  <!-- 전 -->
  <Button.ToolTip>
      <ToolTip Style="{StaticResource CalloutToolTip}" Content="업데이트 준비 완료!"/>
  </Button.ToolTip>

  <!-- 후 -->
  <Button ... ToolTip="{loc:Text TopBar_UpdateReady_ToolTip}">
      <Button.Resources>
          <Style TargetType="ToolTip" BasedOn="{StaticResource CalloutToolTip}"/>
      </Button.Resources>
  ```

**확인**: 검사에서 창 안 버튼의 `ToolTip` 문자열은 언어 전환 직후 새 언어였습니다. 트레이 메뉴는 WPF의 전달
방식에 기대지 않고 새로 만드므로 이 문제가 생기지 않습니다. 자동으로 만들어지는 툴팁에 말풍선 스타일이
적용되는지는 화면을 띄워야 알 수 있어 앱 실행 확인으로 남겼습니다.

### 9. 글꼴: 창 언어가 en-US로 고정되어 한자를 화면 언어와 다른 글꼴로 그릴 수 있음

**문제**: WPF 요소의 언어(`Language`, xml:lang) 기본값은 en-US입니다. WPF 문서는 중국어, 일본어, 한국어가 유니코드
코드 포인트를 공유하지만 글자 모양과 글꼴이 다르다고 설명하고, 언어를 xml:lang으로 지정하게 합니다. 일본어
화면에서 이 값이 en-US로 남으면 한자를 일본어가 아닌 글꼴로 그릴 수 있습니다.

**수정**: 문구 사전에 `ja-JP` 같은 언어 태그를 함께 넣고, 창이 DynamicResource로 받습니다(`MainWindow.xaml`,
`SplashWindow.xaml`). 키는 문자열로 다시 적지 않고 `x:Static`으로 가리켜 오타를 빌드가 잡게 했습니다. 창 밖의
트레이 메뉴는 만들 때 직접 넣습니다(8번).

```xml
Language="{DynamicResource {x:Static loc:AppLanguage.XmlLanguageKey}}"
```

**확인**: 검사에서 스플래시 창의 언어가 `ko-kr`에서 `ja-jp`로 바뀌었습니다. 실제 글꼴 모양은 확인하지 못했습니다.

### 10. 레이아웃: 고정 폭 버튼에서 길어진 번역이 잘릴 수 있음

**문제**: 설정 화면 버튼 일부가 한국어 문구에 맞춘 고정 폭이었습니다(`찾아보기` 65px, `폴더 열기` 60px, `이동`
50px, 단축키 입력 버튼 100px). `フォルダーを開く`나 입력 대기 안내 `キーを押してください...`처럼 더 긴 번역은 잘릴 수
있습니다.

**수정** (`SettingsPage.xaml`): 다섯 버튼의 `Width`를 `MinWidth`로 바꿨습니다. 한국어에서는 지금 폭을 유지하고 긴
문구에서는 늘어납니다. 언어 선택 상자는 언어를 바꿀 때 폭이 들썩이지 않도록 240px로 고정했습니다.

**확인**: 앱 실행 확인으로 남았습니다(TESTING.md 12번 9항).

### 11. Local 미니맵: 안내 문구와 오류 사유가 한국어로 고정됨

**문제**: 미니맵(`viewer/`)이 직접 그리는 로딩, 오류 안내가 한국어로 적혀 있었습니다. 오류 사유도 한국어 문장이라
앱 로그(`status()`)에 그대로 남았습니다.

```js
if (!response.ok) throw new Error(`${path}를 읽지 못했습니다 (HTTP ${response.status})`);
elements.status.textContent = `지도를 열지 못했습니다. ${error.message}`;
```

**수정** (`viewer/i18n.js`, `viewer/main.js`, `viewer/index.html`, `LocalViewer.cs`, `WebBrowserViewModel.cs`)

- 번역을 `viewer/i18n.js`에 두고, 오류는 문구 대신 키와 값을 들고 다니게 했습니다. 화면에는 지금 언어로 다시
  만들고, 로그에는 영어 문장을 남깁니다.

  ```js
  class ViewerError extends Error {
    constructor(key, params = {}) {
      super(message('en', key, params));   // status()와 앱 로그에 남는 영어 문장
      this.key = key;
      this.params = params;
    }
  }

  if (!response.ok) throw new ViewerError('resourceUnreadable', { path, status: response.status });
  ```

- 언어는 앱이 넘깁니다. 첫 화면은 주소의 `lang`(`LocalViewer.PageUrl(map, AppLanguage.Current)`)으로, 실행 중
  변경은 `window.tanukiViewer.setLanguage(코드)`로 받습니다. 미니맵 페이지를 다시 열면 레이드 중에 지도가 잠깐
  사라지므로 새로 고치지 않습니다.

  ```csharp
  public void Receive(LanguageChangedMessage message)
  {
      if (LocalViewer.MapName(Address) == null) return;
      _ = ExecuteScriptAsync(LocalViewer.SetLanguage(message.Value));
  }
  ```

- 로딩 중에 언어를 바꾼 경우에 대비해, 페이지 로드가 끝날 때 진영과 UI 숨김 설정과 함께 현재 언어를 한 번 더
  보냅니다.
- `index.html`의 안내 문구를 비우고 모듈이 실행되는 즉시 그 언어로 채워, 다른 언어가 잠깐 보이지 않게 했습니다.
  문서 언어(`<html lang>`)도 바꿔 브라우저가 그 언어의 글꼴을 고르게 했습니다.
- Levels 패널은 사이트 화면을 그대로 옮긴 것이고 층 이름도 사이트의 영어 데이터라 영어로 둡니다.

**확인**: `verify-viewer.mjs`에 언어 검사를 추가했습니다. 번역 사전을 다시 적지 않고 화면 글자의 문자 종류로
판정합니다. `lang=ja`는 가나, `setLanguage('ko')`는 한글, 모르는 언어는 ASCII로 그려지고, `status()`는 언어와
관계없이 영어인지 봅니다. 73개 항목이 모두 통과했습니다.

### 12. Online: 사이트 언어와 CEF 로캘을 앱 언어에 묶으면 PMC/SCAV 전환이 깨질 위험

**문제**: 앱 언어에 맞춰 Online 사이트도 그 언어로 여는 것을 검토했습니다. 사이트는 주소 앞부분(`/hu/`, `/tr/`)으로
언어를 바꾸고, 언어 메뉴에 한국어와 일본어가 있습니다. 그런데 상단바의 PMC/SCAV 전환은 사이트의 필터를 영어
라벨로 찾습니다(`web-elements-control.js`).

```js
var pmcFilter = findExtractionFilter(items, 'PMC Extraction');
var scavFilter = findExtractionFilter(items, 'Scav Extraction');
```

사이트를 다른 언어로 열면 이 라벨이 바뀌어 코어 기능인 진영 전환이 깨집니다. CEF 로캘은 초기화할 때만 정할
수 있어 즉시 전환을 따라가지도 못합니다.

**수정**: 둘 다 앱 언어와 묶지 않았습니다. 이유를 `App.InitializeCef`의 주석과 AGENTS.md "화면 언어"에 적었고,
설정 화면에 "Online 지도의 문구는 tarkov-market.com 사이트를 따릅니다"라는 안내를 두었습니다.

**확인**: Online 주입 스크립트는 바꾸지 않았습니다. `verify-map-recovery.mjs` 12개 항목이 통과했습니다(Chrome).

### 13. 검사: XAML 키 오타와 번역 누락, 자리 표시자 불일치를 빌드가 잡지 못함

**문제**: 즉시 전환을 위해 XAML은 키를 문자열로 적습니다(`{loc:Text 키}`). 키를 잘못 적어도 빌드는 통과하고
화면에는 빈 문구가 나옵니다. 번역 파일에서 키가 빠지면 조용히 영어로 보이고, `{0}` 같은 자리 표시자가 어긋난
번역은 그 언어로 실행할 때만 `string.Format` 예외를 던집니다.

**수정**: `tools/verify-localization.mjs`를 추가하고 CI(`verify-map-recovery.yml`)에 넣었습니다. TESTING.md에는
6번 "번역 문구"로 등록하고 뒤 번호를 하나씩 미뤘습니다. 검사 항목은 다음과 같습니다.

- 세 resx의 키가 같은지, 키마다 자리 표시자 번호가 같은지, 중괄호가 .NET 서식 문자열로 올바른지, 빈 번역이
  없는지
- `AppLanguage.Supported` 목록이 번역 파일의 언어와 같은지
- XAML의 `{loc:Text 키}`가 있는 키인지, 주석 밖에 한글 문구가 남지 않았는지
- `viewer/i18n.js`의 언어와 키가 앱과 같은지

**확인**: 일본어 번역의 자리 표시자 하나(`{1}`)를 지우고 XAML 키 하나를 잘못 적자 두 검사가 실패했습니다. 되돌린
뒤 7개가 모두 통과합니다.

### 14. 문서: 새 구조와 작업 규칙이 문서에 없음

**수정**

- AGENTS.md: "화면 언어" 절을 추가했습니다. 문구를 resx에 두는 규칙, ViewModel에 번역 문구를 저장하지 않는
  규칙, 창 밖 요소와 툴팁의 처리, Online 사이트와 CEF 로캘을 묶지 않는 이유, 언어를 더하는 절차를 담았습니다.
- PROJECT.md: 구조도에 `AppLanguage`와 `Localization/` 폴더를, "화면 언어" 절에 언어 전환 흐름도를 추가했습니다.
- TESTING.md: 6번 번역 문구 검사를 추가하고, 앱 직접 확인에 언어 항목 세 개(12번의 7~9항)를 더했습니다.
- README: 주요 기능 표에 화면 언어를 추가했습니다.
- [WPF 다국어 레퍼런스](20261008-wpf-localization-resx-and-live-switch.md): 2번과 8번에서 확인한 WPF 동작의 원인,
  진단, 해결법을 다른 프로젝트에서도 쓸 수 있게 정리했습니다.

## 검증 결과

| 검사 | 결과 | 보는 것 |
|---|---|---|
| `verify-localization.mjs` | 7/7 통과 | 번역 키와 자리 표시자, XAML 키, 미니맵 번역 |
| `verify-viewer.mjs` | 73/73 통과 | Local 미니맵 12개 맵과 안내 언어 |
| `verify-map-recovery.mjs` | 12/12 통과 | Online 위치 입력 경로 (Chrome으로 실행) |
| `verify-map-keep-visible.mjs` | 9/9 통과 | Online 지도 맞춤과 이동 제한 |
| `resource-bundle.mjs check` | 통과 | 리소스 계약 |
| `test-resource-pipeline.mjs` | 7/7 통과 | 잘못된 지도 문서와 파일의 거절 |
| `dotnet build` (Debug, Release) | 오류 0 | 경고는 main부터 있던 nullable 경고뿐 |
| GitHub Actions (`verify-map-recovery.yml`) | 통과 | 위 Node 검사 전부와 Release 빌드 |
| 화면을 띄우지 않는 WPF 검사 | 19/19, 3/3 통과 | 설정 화면과 스플래시의 즉시 전환, ViewModel 문구, 창 밖 요소, Windows 언어 대체 |

마지막 검사는 빌드된 앱 어셈블리를 참조해 설정 화면과 스플래시를 창에 넣되 띄우지 않고 언어를 바꿔 보는 검사
프로그램입니다. 저장소에는 넣지 않았습니다. 실제 WPF 앱은 프로젝트 지침에 따라 실행하지 않았습니다.

GitHub Actions는 저장소에 `global.json`이 없어 러너에 설치된 가장 높은 .NET SDK로 빌드하므로, 통과했다고 SDK 8
빌드를 확인한 것은 아닙니다. 작업한 PC에도 SDK 10만 있어 SDK 8로는 빌드하지 못했습니다. 릴리스는 CI와 같은
러너 설정으로 빌드하므로, 배포본을 만드는 환경에서는 빌드가 확인됐습니다.

## 남은 일

- Debug 빌드 실행 확인(TESTING.md 12번 7~9항)
  - 언어를 바꾸면 설정 화면, 상단바와 툴팁, 트레이 메뉴가 다시 시작하지 않고 바뀌는지
  - 개발자 도구에서 업데이트 아이콘을 켰을 때 툴팁이 말풍선 모양으로 뜨는지
  - 영어와 일본어에서 버튼과 상단바 문구가 잘리지 않는지
- 일본어 번역 원어민 검수
- 결정 필요
  - 기존 문구 "설치본에서만 버전을 바꿀 수 있습니다. 지금은 포터블이나 개발 빌드로 실행 중입니다"는 코드상
    포터블에서도 버전 전환이 되므로 사실과 맞지 않습니다. 이번에는 문구를 그대로 번역했습니다.
  - Online 사이트 언어를 앱 언어에 맞출지. 맞추려면 먼저 PMC/SCAV 필터를 언어와 관계없이 찾도록 바꿔야 합니다.
- 이 PC의 Edge 헤드리스 실행에서 `verify-map-recovery.mjs`가 멈추거나 임시 프로필 정리에서 실패합니다. Chrome으로는
  통과하므로 이번 변경과 관계없는 실행 환경 문제입니다. 정리 단계의 예외가 원래 오류를 덮는 점은 도구에서 고칠
  여지가 있습니다.
