# WPF 다국어: 빌드 시점 resx 문구 클래스와 실행 중 언어 전환

<!--
INTENT
WPF 앱에 화면 언어를 더하면서 실험으로 확인한 두 가지 함정과 해결 원리를 남긴다. 하나는 resx에서 빌드할 때 만든
문구 클래스를 WPF 임시 어셈블리가 찾지 못하는 빌드 실패이고, 다른 하나는 실행 중에 언어를 바꿔도 일부 문구만
이전 언어로 남는 문제다. 이 프로젝트에 언어나 문구를 더할 때와 다른 WPF 프로젝트에서 같은 증상을 만났을 때
원인을 구별하고 해결법을 고르는 데 쓴다.
-->

SDK 형식 WPF 프로젝트에서 resx 문구를 강력한 형식 클래스로 쓰거나, 앱을 다시 시작하지 않고 화면 언어를 바꿀 때
보는 문서입니다. 두 문제를 다룹니다.

1. 빌드할 때 resx에서 문구 클래스를 만들게 했더니 `<프로젝트>_xxxxxxxx_wpftmp.csproj`에서 그 클래스를 찾지 못해
   빌드가 실패합니다. WPF가 XAML을 컴파일하려고 따로 만드는 임시 어셈블리가 리소스 생성 단계보다 먼저 컴파일되기
   때문입니다. 문구 클래스를 XAML 컴파일 전에 직접 만들어 해결합니다.
2. Application 리소스의 문구 사전을 바꿔 언어를 전환하면 창 안의 문구는 바뀌는데, 트레이 메뉴와 ToolTip 요소,
   ViewModel이 만들어 둔 문구는 이전 언어로 남습니다. WPF가 사전 교체를 창 트리에만 전달하기 때문입니다. 사전 교체가
   닿지 않는 곳을 따로 다시 만들어 해결합니다.

확인한 환경은 .NET SDK 10.0.401로 `net8.0-windows` 프로젝트를 빌드한 경우입니다. 다른 SDK의 빌드 순서는 확인하지
않았습니다.

## 1. 빌드할 때 만든 resx 문구 클래스를 WPF 임시 어셈블리가 찾지 못하는 문제

### 증상과 겉보기만 비슷한 문제

- `dotnet build`가 `error CS0234: 'MyApp' 네임스페이스에 'Localization' 형식 또는 네임스페이스 이름이 없습니다`처럼
  resx에서 만든 네임스페이스나 클래스를 찾지 못해 실패합니다.
- 오류 줄 끝에 적힌 프로젝트가 원래 csproj가 아니라 `MyApp_xxxxxxxx_wpftmp.csproj`처럼 `_wpftmp`가 붙은 이름입니다.
  그 클래스를 쓰는 일반 C# 파일과 XAML이 만든 `.g.cs`가 함께 실패합니다.
- 네임스페이스를 잘못 적은 경우와 메시지가 같습니다. 구별하는 단서는 `_wpftmp` 프로젝트 이름입니다. XAML이 같은
  프로젝트의 형식(로컬 형식)을 하나도 참조하지 않으면 WPF가 임시 어셈블리를 만들지 않으므로 이 문제도 없습니다.

### 원인

resx에서 강력한 형식 클래스를 만드는 일반적인 길은 둘입니다.

- **Visual Studio의 ResXFileCodeGenerator**: resx를 디자이너에서 저장할 때 원본 폴더에 `Strings.Designer.cs`를
  만듭니다. `dotnet build`는 이 도구를 실행하지 않으므로, 텍스트 편집기로 resx만 고치면 클래스가 resx를 따라가지
  못합니다.
- **MSBuild의 GenerateResource**: `EmbeddedResource`에 `StronglyTypedLanguage`, `StronglyTypedClassName` 같은
  메타데이터를 달면 리소스 단계(`CoreResGen` 타깃)가 빌드할 때마다 클래스를 만들어 Compile 항목에 더합니다. 원본
  폴더에 생성 파일이 남지 않고 resx와 어긋날 수 없어 CLI 빌드에 맞습니다.

두 번째 방식이 WPF에서 실패하는 원인은 빌드 순서입니다. XAML이 로컬 형식을 참조하면 WPF 빌드는 마크업 컴파일
2단계에 앞서 그 형식을 담은 임시 어셈블리를 컴파일합니다(`GenerateTemporaryTargetAssembly`). 자세한 빌드
로그(`-v:d`)에서 본 실제 순서는 다음과 같습니다.

```
MarkupCompilePass1                    (PrepareResources가 의존하는 타깃)
GenerateTemporaryTargetAssembly       (MarkupCompilePass2ForMainAssembly가 의존하는 타깃)
  -> MyApp_xxxxxxxx_wpftmp.csproj: _CompileTemporaryAssembly -> CoreCompile   CS0234로 실패
```

`CoreResGen`은 빌드가 실패할 때까지 로그에 한 번도 나오지 않았습니다. 리소스 생성은 마크업 컴파일보다 뒤에 오고,
임시 프로젝트는 컴파일에 필요한 타깃만 실행합니다. 그래서 만들어질 클래스 파일이 임시 컴파일의 Compile 항목에
들어가지 못합니다.

### 진단

1. 오류가 난 프로젝트 이름에 `_wpftmp`가 붙었는지 봅니다. 붙었다면 원래 프로젝트에서는 보이는 형식이 임시 컴파일에
   없는 것입니다.
2. 그 형식이 빌드 중에 만들어지는지(리소스 단계, 코드 생성 타깃) 확인합니다. 원본 폴더에 소스가 있는 형식이라면 이
   문제가 아닙니다.
3. 순서를 직접 보려면 `dotnet build -v:d > build.log`로 로그를 받아 `MarkupCompilePass1`,
   `GenerateTemporaryTargetAssembly`, `CoreResGen`이 나오는 순서를 찾습니다. 한국어 로그에서는
   `"CoreResGen" 대상`처럼 나옵니다.

### 해결 방법

문구 클래스를 XAML 컴파일 전에 직접 만들고, 그 파일을 고정 경로의 Compile 항목으로 넣습니다. 경로가 고정돼 있어
원래 프로젝트와 임시 프로젝트가 같은 항목을 평가하고, 임시 컴파일이 시작될 때 파일은 이미 만들어져 있습니다.

```xml
<PropertyGroup>
  <!-- 기본 Strings.resx의 언어. 이 언어로 실행할 때는 위성 어셈블리를 찾지 않는다 -->
  <NeutralLanguage>en</NeutralLanguage>
</PropertyGroup>

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
                    StronglyTypedNamespace="MyApp.Localization"
                    StronglyTypedClassName="Strings"
                    StronglyTypedManifestPrefix="MyApp.Localization"
                    StronglyTypedFileName="$(IntermediateOutputPath)Strings.g.cs"
                    PublicClass="true"
                    ExecuteAsTool="false" />
</Target>
```

- `EmbeddedResource`에는 StronglyTyped 메타데이터를 달지 않고, 클래스는 위 타깃 하나만 만들게 합니다. `Strings.resx`와
  `Strings.ko.resx` 같은 번역 파일은 기본 리소스 처리로 본 어셈블리 리소스와 언어별 위성
  어셈블리(`ko\MyApp.resources.dll`)가 됩니다.
- `ExecuteAsTool="false"`를 빼면 `ResGen.exe not supported on .NET Core MSBuild` 오류로 실패합니다. .NET의 MSBuild는
  외부 ResGen.exe를 실행할 수 없어 태스크를 프로세스 안에서 돌려야 합니다.
- `StronglyTypedManifestPrefix`는 생성된 클래스가 `ResourceManager`에 넘길 이름의 앞부분입니다. 기본 이름 규칙에서
  `Localization\Strings.resx`는 `<RootNamespace>.Localization.Strings`라는 이름으로 임베드되므로 그 이름과 맞춥니다.
  어긋나면 빌드는 통과하지만 실행할 때 `ResourceManager`가 리소스를 찾지 못합니다.
- `OutputResources`는 태스크가 요구해서 두는 부산물이며 쓰지 않습니다.
- `Inputs`와 `Outputs`는 resx가 바뀔 때만 다시 만들게 합니다. 이는 resx가 문자열만 담는다는 전제입니다. MSBuild
  문서는 resx가 다른 파일을 링크(ResXFileRef)할 수 있어 이 조건을 달면 링크한 파일의 변경을 놓칠 수 있다고
  경고합니다. 링크를 쓰면 조건을 지웁니다.

### 대안과 선택 기준

- **ResXFileCodeGenerator와 커밋한 `Designer.cs`**: 팀이 resx를 늘 Visual Studio 디자이너로 고친다면 가장 흔한
  방식이고, 생성 파일이 원본 폴더에 있어 임시 어셈블리 문제도 없습니다. 텍스트 편집기나 자동화 도구로 resx를 고치고
  CLI로 빌드하는 저장소에서는 클래스가 뒤처집니다. 키를 더한 경우에는 그 키를 쓰는 쪽이 컴파일에 실패해 드러나지만,
  키를 지우거나 이름을 바꾼 경우에는 남은 속성이 `ResourceManager.GetString`의 결과인 null을 돌려줘 조용히 빈 문구가
  됩니다.
- **문구 클래스를 별도 클래스 라이브러리로 분리**: 이번에는 시도하지 않았습니다. 참조한 어셈블리의 형식은 임시
  컴파일도 참조로 받으므로 표준 메타데이터 방식으로 동작할 것으로 보이지만 확인하지 않았습니다. 이 방식을 쓰면
  프로젝트가 하나 늘어납니다.

### 검증

- 빌드가 통과하고 `obj/<구성>/<TFM>/Strings.g.cs`가 생깁니다.
- XAML에서 `{x:Static loc:Strings.없는키}`처럼 키를 잘못 적으면 `MC3011`(정적 멤버를 찾을 수 없음) 빌드 오류가
  납니다. C#에서 잘못 적으면 일반 컴파일 오류가 납니다.
- resx에 키를 추가하고 정리 없이 다시 빌드하면 새 속성을 바로 쓸 수 있습니다.
- 출력 폴더에 `ko\`, `ja\` 같은 위성 어셈블리 폴더가 생깁니다. 실행해 보면 문화권 `ko-KR`은 `ko` 문구로, 번역이 없는
  `fr`은 기본 언어 문구로 대체되고, `Task.Run` 안에서 읽어도 같은 언어가 나옵니다.

## 2. 실행 중에 언어를 바꾸면 일부 문구만 이전 언어로 남는 문제

### 증상과 적용 범위

XAML 문구를 DynamicResource로 Application 리소스의 문구 사전에 연결하고, 언어를 바꿀 때
`Application.Current.Resources.MergedDictionaries`의 사전을 새 언어의 사전으로 교체하는 구조에서 생깁니다.

- 창 안의 TextBlock, 버튼, 체크박스 문구와 그 요소의 `ToolTip` 속성(문자열)은 그 자리에서 바뀝니다.
- 다음 문구는 이전 언어로 남습니다.
  - 코드로 만들었고 어느 창에도 속하지 않은 요소의 문구(트레이 아이콘의 ContextMenu 등)
  - 요소의 `ToolTip` 속성 값으로 넣은 ToolTip 요소 안의 문구
    (`<Button.ToolTip><ToolTip Content="{DynamicResource ...}"/></Button.ToolTip>`)
  - ViewModel이 일이 생긴 순간 만들어 필드에 저장해 둔 문구(상태 안내, 목록 항목의 라벨)
  - 언어를 바꾸기 전에 시작한 비동기 작업이 끝난 뒤, 스레드 문화권으로 찾은 문구

### 원인

- **사전 교체는 창 트리로만 전달됩니다.** Application 리소스가 바뀌면 WPF는 `Application.Windows`의 창마다 트리를
  걸으며 DynamicResource 참조를 다시 찾게 합니다. 화면에 띄우지 않은 검사 프로그램에서 확인한 결과는 다음과 같습니다.
  - 창(Show하지 않은 Window)에 넣은 설정 화면 UserControl의 모든 문구, 창 안 버튼의 `ToolTip` 속성 문자열은 사전
    교체 직후 새 언어였습니다.
  - 어느 창에도 속하지 않은 MenuItem과, 버튼의 `ToolTip` 속성 값으로 넣은 ToolTip 요소는 이전 언어 그대로였습니다.
  - 그 MenuItem을 다른 패널에 붙이자(부모가 생기자) 새 언어를 다시 찾았습니다. 트리에 붙을 때는 리소스를 다시 찾는다는
    뜻입니다.
- **ContextMenu와 ToolTip은 팝업을 재사용합니다.** 둘 다 처음 열릴 때 내부 팝업(`_parentPopup`)을 만들고, 그 뒤로는
  필드를 비우지 않고 재사용합니다(dotnet/wpf의 `ContextMenu.cs`, `ToolTip.cs`). 그래서 열릴 때마다 트리에 새로 붙으며
  다시 찾을 것이라 기대할 수 없습니다. 첫 열림 뒤에 언어를 바꾸면 부모가 그대로라 다시 찾을 계기가 없습니다.
- **저장한 문자열은 리소스 참조가 아닙니다.** 사전을 바꿔도 ViewModel 필드의 문자열은 그대로입니다.
- **문화권은 비동기 흐름을 따라갑니다.** .NET에서 비동기 작업은 시작한 스레드의 `CurrentCulture`와
  `CurrentUICulture`를 물려받습니다. 언어를 바꾸기 전에 시작한 `await` 뒤의 코드가 `CurrentUICulture`로 문구를 찾으면
  이전 언어가 나옵니다. 이 동작은 공식 문서로 확인했고 이번 작업에서 따로 재현하지는 않았습니다.

### 진단

이전 언어로 남은 문구가 어디서 왔는지로 나눕니다.

1. XAML의 DynamicResource 문구라면 그 요소가 창 트리에 있는지 봅니다. 코드로 만들어 창에 붙이지 않은 요소이거나
   `ToolTip`, `ContextMenu` 속성 값으로 넣은 요소라면 사전 교체가 닿지 않은 것입니다.
2. ViewModel 속성이라면 그 속성이 저장한 문자열을 돌려주는지, 계산 속성인데 언어가 바뀐 뒤 PropertyChanged를 다시
   내지 않는지 봅니다.
3. 특정 작업 뒤에만 남는다면 문구를 문화권 없이 찾는지(`ResourceManager.GetString(key)`, `CurrentUICulture`에 기대는
   생성 클래스) 봅니다.

앱을 실행하지 않고 1과 2를 확인하려면 앱 어셈블리를 참조하는 작은 WPF 검사 프로그램을 씁니다. `new App()`과
`InitializeComponent()`로 앱 리소스를 불러오고, 화면을 Show하지 않은 Window에 넣은 뒤 언어를 바꿔 `TextBlock.Text` 같은
속성 값을 읽습니다. 두 가지를 주의합니다.

- 루트 기준 pack URI(`/Resources/icon.png`)는 `Application.ResourceAssembly`, 곧 실행 파일인 검사 프로그램을 기준으로
  찾습니다. 공개 setter는 이미 값을 읽은 뒤라 거부하므로 검사 프로그램에서만 이 값을 앱 어셈블리로 바꿔야 합니다.
- 검사 중에 설정 저장 경로를 타면 실제 사용자 설정 파일을 덮어쓸 수 있습니다. 저장을 부르는 조작(값 변경)은 하지
  않고 읽기만 합니다.

### 해결 방법

| 문구가 있는 곳 | 다시 그리는 방법 |
|---|---|
| 창 안의 XAML | DynamicResource로 사전을 가리킴. 사전 교체만으로 바뀜 |
| 툴팁 | 문구를 소유 요소의 `ToolTip` 속성(문자열)에 두고, 꾸민 모양은 소유 요소 범위의 암시적 ToolTip 스타일로 입힘 |
| 창 밖에서 코드로 만든 요소 | 언어 변경 메시지를 받아 새로 만듦 |
| ViewModel 문구 | 문자열 대신 상태를 저장하고 읽을 때 만드는 계산 속성으로 둠. 언어가 바뀌면 `PropertyChanged(string.Empty)`로 모든 속성을 다시 읽게 하고, 목록 항목은 항목마다 알림 |
| 코드의 문구 조회 | 스레드 문화권 대신 생성 클래스의 `Culture` 속성(`Strings.Culture`)에 앱 언어를 넣음 |

`PropertyChanged`의 속성 이름을 빈 문자열로 보내면 WPF는 그 객체의 모든 속성이 바뀐 것으로 보고 바인딩을 다시
읽습니다. 계산 속성을 하나씩 나열하지 않으므로 나중에 문구 속성을 더해도 빠뜨리지 않습니다.

요소의 xml:lang(`FrameworkElement.Language`, 기본값 en-US)도 같은 사전에 넣어 창이 DynamicResource로 받게 하면 함께
바뀝니다. WPF 문서는 중국어, 일본어, 한국어가 유니코드 코드 포인트를 공유하지만 글자 모양과 글꼴이 다르다고 설명하고,
요소의 언어를 xml:lang으로 지정하게 합니다. 화면 언어를 바꿨는데 이 값이 en-US로 남으면 한자를 화면 언어와 다른
글꼴로 그릴 수 있습니다. 트레이 메뉴처럼 창 밖의 요소는 창의 값을 물려받지 못하므로 만들 때 직접 넣습니다.

암시적 ToolTip 스타일이 소유 요소 범위에서 적용되는지는 툴팁을 실제로 띄워야 확인할 수 있어, 화면을 띄우지 않는
검사로는 확인하지 못했습니다. 이 프로젝트에서는 앱 직접 확인 항목으로 남겼습니다.

### 대안과 선택 기준

- **다시 시작해야 적용하는 방식**: 문구를 `{x:Static}`으로 읽고 시작할 때 한 번 문화권을 정하면 위의 문제가 모두
  사라지고 XAML 키 오타도 빌드가 잡습니다(1장의 `MC3011`). 언어를 거의 바꾸지 않는 앱이라면 이쪽이 단순합니다. 대신
  언어를 바꾼 뒤 앱을 다시 시작해야 합니다.
- **바인딩 인덱서 방식**(`{Binding [Key], Source={x:Static ...}}`과 `PropertyChanged("Item[]")`): 바인딩은 소스 객체의
  알림을 직접 받으므로 창 밖 요소도 갱신될 것으로 보이지만 이번에는 시도하지 않았습니다. XAML의 키가 문자열이라 빌드가
  오타를 잡지 못하는 점은 DynamicResource와 같습니다.
- 어느 실행 중 전환 방식이든 XAML 키는 문자열이므로, resx 키와 XAML의 키를 대조하는 검사가 따로 필요합니다.

### 검증

- 화면을 띄우지 않은 검사에서 사전 교체 전후로 설정 화면의 모든 문구 키가 새 언어 문구인지, ViewModel이
  `PropertyChanged("")`를 내고 계산 문구가 새 언어인지 확인했습니다.
- 트레이 메뉴와 툴팁의 실제 표시, 글꼴은 화면을 띄워야 하므로 앱에서 직접 확인합니다.

## 이 저장소에서의 적용

- 빌드 설정: [TanukiTarkovMap.csproj](../src/TanukiTarkovMap/TanukiTarkovMap.csproj)의 `GenerateLocalizedStrings` 타깃
- 언어 적용과 사전 교체: [AppLanguage](../src/TanukiTarkovMap/Localization/AppLanguage.cs),
  XAML 표기 [TextExtension](../src/TanukiTarkovMap/Localization/TextExtension.cs)(`{loc:Text 키}`)
- ViewModel 문구: [SettingsViewModel](../src/TanukiTarkovMap/ViewModels/SettingsViewModel.cs)
- 트레이 메뉴: [App.xaml.cs](../src/TanukiTarkovMap/App.xaml.cs)의 `CreateTrayMenu`
- 키와 번역 대조: [verify-localization.mjs](../tools/verify-localization.mjs). 검사 목록과 앱 직접 확인 항목은
  [TESTING.md](../TESTING.md)에 있습니다
- 작업 규칙: [AGENTS.md](../AGENTS.md)의 "화면 언어"

## 근거

- MSBuild GenerateResource 태스크의 매개변수와 Inputs/Outputs 주의:
  [GenerateResource task](https://learn.microsoft.com/visualstudio/msbuild/generateresource-task)
- 비동기 작업과 문화권:
  [CultureInfo의 Culture and task-based asynchronous operations](https://learn.microsoft.com/dotnet/fundamentals/runtime-libraries/system-globalization-cultureinfo#culture-and-task-based-asynchronous-operations)
- 한자 글꼴과 xml:lang: [Globalization - WPF의 Language Attribute](https://learn.microsoft.com/dotnet/desktop/wpf/advanced/globalization-for-wpf#language-attribute)
- ContextMenu와 ToolTip의 팝업 재사용: dotnet/wpf 저장소의
  [ContextMenu.cs](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Controls/ContextMenu.cs)와
  [ToolTip.cs](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Controls/ToolTip.cs)의
  `HookupParentPopup`
- 빌드 순서와 사전 교체의 전달 범위: 위에 적은 재현 프로젝트와 검사 프로그램의 실행 결과
