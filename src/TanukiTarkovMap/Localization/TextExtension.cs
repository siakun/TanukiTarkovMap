using System.Windows;

/**
TextExtension - XAML의 화면 문구를 지금 화면 언어로 표시하는 마크업 확장 ({loc:Text 키})

Purpose: XAML에 적은 문구가 AppLanguage의 문구 사전을 가리키게 해, 언어를 바꾸면 그 자리에서 다시 그려지게 한다.
Architecture: 동작은 WPF의 DynamicResource와 같다. 키는 Strings.resx의 이름이고, AppLanguage가 Application
리소스에 넣은 사전에서 찾는다. 이름을 따로 둔 이유는 화면 문구 참조를 다른 동적 리소스와 구별하기 위해서다.
tools/verify-localization.mjs가 이 표기를 찾아 키가 Strings.resx에 있는지 확인한다.

Usage:
  xmlns:loc="clr-namespace:TanukiTarkovMap.Localization"
  <TextBlock Text="{loc:Text Settings_Storage_Title}"/>

Design Rationale: 바인딩(인덱서 + PropertyChanged)으로도 같은 일을 할 수 있지만, DynamicResource는 사전을
바꾸는 것만으로 WPF가 창 안의 참조를 모두 다시 읽어, 문구마다 갱신 코드를 둘 필요가 없다.
Known Limitations:
- 없는 키는 빈 문구가 되고 빌드도 통과한다. 그래서 위 검사 도구가 키를 대조한다
- 툴팁 속성에 넣은 ToolTip 요소 안에서 쓰면 언어를 바꿔도 이전 문구로 남는다. 그 요소는 창의 트리에 속하지
  않아 사전 교체가 전달되지 않는다. 문구는 소유 요소의 ToolTip 속성에 두고 모양은 암시적 ToolTip 스타일로 입힌다

Last Updated: 2026-10-08 | .NET 8 | 화면 언어 설정 추가
*/
namespace TanukiTarkovMap.Localization
{
    public sealed class TextExtension : DynamicResourceExtension
    {
        public TextExtension() { }

        public TextExtension(string key) : base(key) { }
    }
}
