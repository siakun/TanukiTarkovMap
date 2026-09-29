using System.Windows;
using CommunityToolkit.Mvvm.Messaging;
using Siakun.AutoUpdate;
using TanukiTarkovMap.Messages;
using TanukiTarkovMap.Models.Utils;

/**
UpdateServiceFactory - Siakun.AutoUpdate의 UpdateService를 이 앱에 맞게 구성

Purpose: 업데이트 확인, 버전 전환, 종료 시 적용은 Siakun.AutoUpdate 패키지가 맡는다.
이 앱에만 있는 것(릴리스 저장소 주소, settings.json, 로그, ViewModel 메시지)을 한곳에서 연결한다.

Architecture: ServiceLocator가 싱글톤을 만들 때 Create()를 호출한다.
다운로드 완료는 이 클래스가 UpdateReadyMessage로 바꿔 보내고, 재시작 요청은 App이 직접 구독한다.

Core Functionality:
- Create(): 저장소 카탈로그, 설정 어댑터, 로그 함수를 넣어 UpdateService를 만들고 다운로드 완료 이벤트를 메시지로 잇는다
- RepositoryUrl: 업데이트 조회, 설정 화면의 저장소 링크, App의 버전 조회가 함께 쓰는 주소

State Management:
- 상태를 두지 않는다. 업데이트 상태는 UpdateService가, 설정 값은 App.GetSettings()가 가진다

Method Flow:
  ServiceLocator.Initialize -> Create() -> UpdateService
  UpdateService.UpdateReady (백그라운드 스레드) -> UpdateReadyMessage -> MainWindowViewModel (UI 스레드로 전환)
  UpdateService가 구버전 선택 시 AutoUpdateEnabled = false -> UpdateSettingsAdapter -> UI 스레드에서 Settings.Save()

Dependencies:
- Siakun.AutoUpdate.UpdateService: 자동 갱신, 버전 목록, 버전 전환, 종료 시 적용
- App.GetSettings(), Settings.Save(): 자동 업데이트와 베타 수신 설정의 원본과 저장
- WeakReferenceMessenger: MainWindowViewModel에 UpdateReadyMessage 전달

Design Rationale: 설정 어댑터를 AppSettings에 직접 구현하지 않는다. 라이브러리는 AutoUpdateEnabled의
setter가 저장까지 마치기를 요구하는데, AppSettings는 JSON 직렬화 데이터라 setter가 저장하면
설정 화면이 값을 모아 한 번에 저장할 때마다 저장이 겹친다.
다운로드 완료를 이벤트로 ViewModel에 직접 잇지 않고 메시지로 바꾼다. MainWindowViewModel은
Transient라 싱글톤 이벤트를 구독하면 창이 닫혀도 구독이 남는다.

Critical Warnings: 라이브러리는 버전 전환 중 백그라운드 스레드에서 AutoUpdateEnabled를 끈다.
다른 설정 저장은 모두 UI 스레드에서 AppSettings를 바꾸므로, 직렬화 도중 다른 값이 바뀌지 않도록
저장을 UI 스레드로 넘긴다. 저장을 마친 뒤 반환해야 하므로 BeginInvoke가 아니라 Invoke를 쓴다.
앱에서 Velopack 버전을 따로 지정하지 않는다. 라이브러리가 동작을 확인한 버전과 어긋날 수 있다.

Last Updated: 2026-09-30 | .NET 8 / Siakun.AutoUpdate 0.1.0 | 앱의 업데이트 코드를 패키지로 교체
*/
namespace TanukiTarkovMap.Models.Services
{
    internal static class UpdateServiceFactory
    {
        /// <summary> 업데이트를 조회하는 GitHub 저장소 주소 </summary>
        internal const string RepositoryUrl = "https://github.com/siakun/TanukiTarkovMap";

        internal static UpdateService Create()
        {
            var catalog = new GitHubReleaseCatalog(RepositoryUrl, log: Logger.SimpleLog);
            var updateService = new UpdateService(catalog, new UpdateSettingsAdapter(), Logger.SimpleLog);

            // 다운로드를 마친 스레드에서 발생하므로 UI 스레드 전환은 받는 ViewModel이 맡는다
            updateService.UpdateReady += (_, e) =>
                WeakReferenceMessenger.Default.Send(new UpdateReadyMessage(e.Version.ToString()));

            return updateService;
        }

        private sealed class UpdateSettingsAdapter : IUpdateSettings
        {
            public bool AutoUpdateEnabled
            {
                get => App.GetSettings().AutoUpdateEnabled;
                set => RunOnUiThread(() =>
                {
                    App.GetSettings().AutoUpdateEnabled = value;
                    Settings.Save();
                });
            }

            public bool PrereleaseEnabled => App.GetSettings().PrereleaseEnabled;

            private static void RunOnUiThread(Action action)
            {
                // 종료 도중이라 Dispatcher가 없으면 호출한 스레드에서 저장한다
                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher == null || dispatcher.CheckAccess())
                {
                    action();
                    return;
                }

                dispatcher.Invoke(action);
            }
        }
    }
}
