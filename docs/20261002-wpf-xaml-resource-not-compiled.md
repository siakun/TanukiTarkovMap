# Resource 항목에 포함된 WPF XAML이 BAML로 컴파일되지 않는 문제

<!--
INTENT
빌드는 성공하는데 실행할 때 XAML이 형식을 찾지 못하는 증상의 원인과 진단 방법을 남긴다. 이 프로젝트의 csproj는
Resources 폴더 전체를 Resource 항목으로 임베드해 스타일 사전이 컴파일되지 않는다. 같은 구조에서 로컬 형식을
참조하는 XAML을 추가할 때와, 다른 WPF 프로젝트에서 같은 증상을 만났을 때 다시 활용하도록 원리와 선택 기준을 적는다.
-->

빌드는 성공하는데 앱이 XAML을 불러오는 순간 `형식 참조에서 이름이 '{clr-namespace:...}...'인 형식을 찾을 수
없습니다`(영문 `Type reference cannot find type named ...`)로 멈출 때 보는 문서입니다. SDK 형식 WPF 프로젝트에서
`.xaml` 파일이 Resource나 Content 항목에도 포함되면, 그 파일은 BAML로 컴파일되지 않고 원문 그대로 임베드됩니다.
원문 XAML은 실행 중에 해석되고, 이때는 어셈블리 이름을 생략한 `clr-namespace`가 가리킬 어셈블리가 없습니다.
`<Resource Include="Resources\**\*.*" />`처럼 폴더 전체를 잡는 항목이 리소스 사전 XAML까지 포함하는 경우에
적용합니다.

## 증상과 겉보기만 비슷한 문제

- 빌드 오류와 경고가 없는데, 그 XAML을 불러오는 시점(앱 시작 때 `App.xaml`이 리소스 사전을 병합하는 시점 등)에
  `XamlParseException`이 납니다. 안쪽 예외가 위 문구이고 대상은 같은 프로젝트에 있는 형식입니다.
- 그 XAML의 다른 오타도 빌드에서 드러나지 않고 실행할 때 처음 드러납니다.
- 형식 이름을 잘못 적었거나 참조가 빠진 경우와 메시지가 같습니다. 구별하는 단서는 빌드 결과입니다. 같은 실수가
  Page로 컴파일되는 XAML에 있으면 빌드가 실패합니다.

## 원인

SDK와 런타임의 동작 세 가지가 이어져서 생깁니다.

1. WindowsDesktop SDK는 프로젝트의 모든 `**/*.xaml`을 Page 항목(BAML 컴파일 대상)으로 모은 뒤,
   `Microsoft.NET.Sdk.WindowsDesktop.targets`의 `<Page Remove="@(Resource);@(Content)" />`로 Resource나 Content에도
   포함된 파일을 Page에서 다시 뺍니다. 그래서 폴더 전체를 Resource로 잡으면 그 안의 XAML은 컴파일되지 않고 원문
   그대로 `<어셈블리>.g.resources`에 들어갑니다.
2. 실행 중에 `Resources/Styles/X.xaml` 같은 pack URI를 열면, WPF의 `ResourcePart`는 먼저 같은 이름의 `.baml`을
   찾고 없으면 원문 `.xaml`을 엽니다. 원문을 받은 쪽은 이를 XAML로 해석합니다(loose XAML).
3. WPF `XamlReader`가 어셈블리를 생략한 `clr-namespace`의 기준 어셈블리(`LocalAssembly`)를 정하는 코드는 BAML을
   읽을 때만 실행됩니다. 원문 XAML에는 기준 어셈블리가 없으므로, 이미 메모리에 올라온 어셈블리의 형식이어도
   찾지 못합니다.

기존 리소스 사전이 문제없이 동작했다면, 그 사전들은 로컬 형식을 참조하지 않았을 가능성이 큽니다. 처음으로
`{x:Static local:...}`이나 로컬 Behavior, 컨버터 형식을 참조하는 순간 드러납니다.

## 진단

`<어셈블리>.g.resources`의 항목 이름을 보면 컴파일 여부를 바로 알 수 있습니다. `.baml`이면 컴파일된 것이고,
`.xaml`이면 원문 그대로 들어간 것입니다.

```powershell
$dll = 'bin\Debug\net8.0-windows\MyApp.dll'
$context = New-Object System.Runtime.Loader.AssemblyLoadContext('probe', $true)
$assembly = $context.LoadFromStream([IO.File]::OpenRead($dll))
$stream = $assembly.GetManifestResourceStream('MyApp.g.resources')
$reader = New-Object System.Resources.ResourceReader($stream)
$reader | ForEach-Object { $_.Key } | Sort-Object   # app.baml, resources/styles/buttonstyles.xaml ...
$reader.Close(); $context.Unload()
```

원문으로 들어간 XAML이 보이면 csproj에서 그 파일을 잡는 Resource나 Content 항목(와일드카드 포함)을 찾습니다.

## 해결 방법과 선택 기준

**어셈블리 이름 명시**: `xmlns:local="clr-namespace:MyApp.Behaviors;assembly=MyApp"`처럼 적으면 원문 XAML도
형식을 찾습니다. 변경 범위가 그 파일 하나뿐이고 다른 사전의 동작은 그대로입니다. 원문 해석 자체는 남으므로
빌드가 이 파일의 오류를 잡아 주지 않는다는 한계도 그대로입니다. 이 프로젝트의 `ToolTipStyles.xaml`이 이 방법을
사용하고, 같은 이유를 파일 머리 주석에 적어 두었습니다.

**XAML을 Resource 항목에서 제외**: `<Resource Include="Resources\**\*.*" Exclude="Resources\**\*.xaml" />`로
바꾸면 리소스 사전이 Page로 돌아가 BAML로 컴파일됩니다. 빌드가 형식과 오타를 검사하고, 실행 중에는 원문 해석이
사라집니다. 위 2번 동작 덕분에 `Source="Resources/Styles/X.xaml"` 같은 기존 경로는 고치지 않아도 `.baml`을
찾습니다. 대신 그 폴더의 모든 사전이 한꺼번에 다른 경로로 불러와지므로, 각 사전이 컴파일되고 실제로 불러와지는지
확인해야 합니다. 이 프로젝트에는 아직 적용하지 않았습니다.

로컬 형식을 참조하는 사전이 한두 개뿐이면 첫 방법이 변경이 작습니다. 리소스 사전이 자주 바뀌거나 앱 시작 경로에
있어 실행 전에 오류를 잡아야 하면 두 번째 방법이 근본 해결입니다.

## 검증

앱을 띄우지 않고도 같은 해석 경로를 재현할 수 있습니다. 별도 WPF 프로세스에서 `Application`을 만들고
`Application.LoadComponent(new Uri("/MyApp;component/Resources/Styles/X.xaml", UriKind.Relative))`로 그 사전을
불러오면, 위 2번과 같은 순서로 `.baml`을 찾다가 원문을 해석합니다. 이 프로젝트에서는 수정 전에 이 방법으로 같은
예외가 났고, `assembly=`를 적은 뒤에는 사전이 불러와져 그 스타일로 그린 화면까지 확인했습니다. 최종 확인은
실제 앱을 시작해 그 사전을 쓰는 화면을 여는 것입니다.

두 번째 해결 방법을 적용했다면 진단 절차로 `.g.resources`에 `.baml`만 남았는지 확인합니다.

## 근거

- WindowsDesktop SDK: `Sdks/Microsoft.NET.Sdk.WindowsDesktop/targets/Microsoft.NET.Sdk.WindowsDesktop.props`의
  `<Page Include="**/*.xaml" ...>`과 같은 폴더 `Microsoft.NET.Sdk.WindowsDesktop.targets`의
  `<Page Remove="@(Resource);@(Content)" />`
- [dotnet/wpf `ResourcePart.cs`](https://github.com/dotnet/wpf/blob/release/8.0/src/Microsoft.DotNet.Wpf/src/PresentationFramework/MS/Internal/AppModel/ResourcePart.cs):
  `.xaml` 요청에 `.baml`을 먼저 찾고 없으면 원래 이름으로 엽니다(`try baml extension first`).
- [dotnet/wpf `XamlReader.cs`](https://github.com/dotnet/wpf/blob/release/8.0/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Markup/XamlReader.cs):
  `readerSettings.LocalAssembly`를 BAML 스트림의 어셈블리로 정하는 코드만 있습니다.
