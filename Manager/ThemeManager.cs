using System;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using SayoOSD; // App.Log 사용

namespace SayoOSD.Managers
{
    public static class ThemeManager
    {
        public enum Theme { Auto = 0, Light = 1, Dark = 2 }
        
        private static Theme _currentTheme = Theme.Auto;
        public static Theme CurrentTheme 
        {
            get => _currentTheme;
            set 
            { 
                _currentTheme = value; 
                App.Log($"[ThemeManager] 테마 변경 요청됨: {value}");
                ApplyTheme(); 
            }
        }

        public static void Initialize()
        {
            try
            {
                SystemEvents.UserPreferenceChanged += (s, e) => 
                {
                    if (_currentTheme == Theme.Auto && e.Category == UserPreferenceCategory.General)
                    {
                        ApplyTheme();
                    }
                };
            }
            catch { /* 레지스트리 접근 권한 등 예외 무시 */ }
            ApplyTheme();
        }

        public static void ApplyTheme()
        {
            App.Log("[ThemeManager] ApplyTheme 시작");

            bool isDark = false;
            if (_currentTheme == Theme.Auto)
                isDark = IsSystemDark();
            else
                isDark = (_currentTheme == Theme.Dark);

            // 다크 모드 색상 정의
            App.Log($"[ThemeManager] 적용할 모드: {(isDark ? "Dark" : "Light")}");

            var darkBg = System.Windows.Media.Color.FromRgb(20, 20, 20); // 전체 배경 (더 어두운 회색)
            var darkFg = System.Windows.Media.Color.FromRgb(240, 240, 240); // 텍스트 (밝은 흰색)
            var darkPanel = System.Windows.Media.Color.FromRgb(35, 35, 38); // 패널/리스트 배경
            var darkBorder = System.Windows.Media.Color.FromRgb(60, 60, 60); // 테두리
            var darkInput = System.Windows.Media.Color.FromRgb(40, 40, 40); // 입력창 배경
            var darkButton = System.Windows.Media.Color.FromRgb(50, 50, 50); // 버튼 배경

            // 라이트 모드 색상 정의
            var lightBg = Colors.White;
            var lightFg = Colors.Black;
            var lightPanel = System.Windows.Media.Color.FromRgb(240, 240, 240);
            var lightBorder = Colors.LightGray;
            var lightInput = Colors.White;
            var lightButton = System.Windows.Media.Color.FromRgb(221, 221, 221);

            var bg = isDark ? darkBg : lightBg;
            var fg = isDark ? darkFg : lightFg;
            var panel = isDark ? darkPanel : lightPanel;
            var border = isDark ? darkBorder : lightBorder;

            SetResource("WindowBackgroundBrush", new SolidColorBrush(bg));
            SetResource("PrimaryTextBrush", new SolidColorBrush(fg));
            SetResource("PanelBackgroundBrush", new SolidColorBrush(panel));
            SetResource("InputBackgroundBrush", new SolidColorBrush(isDark ? darkInput : lightInput));
            SetResource("ButtonBackgroundBrush", new SolidColorBrush(isDark ? darkButton : lightButton));
            SetResource("BorderBrush", new SolidColorBrush(border));
            
            // OSD 기본 베이스 컬러
            SetResource("OsdBaseColor", isDark ? Colors.Black : Colors.White);
            SetResource("OsdTextBrush", new SolidColorBrush(isDark ? Colors.White : Colors.Black));

            App.Log("[ThemeManager] 리소스 갱신 완료. 암시적 스타일 적용 시작.");

            // [추가] 컨트롤별 암시적 스타일(Implicit Styles) 적용
            SetImplicitStyles();
        }

        private static void SetResource(string key, object value)
        {
            if (System.Windows.Application.Current == null) return;
            var dict = System.Windows.Application.Current.Resources;
            if (dict.Contains(key)) dict.Remove(key);
            dict[key] = value;
            // App.Log($"[ThemeManager] Resource Set: {key}"); // 너무 많아서 주석 처리
        }

        // [추가] 모든 컨트롤에 자동으로 테마 리소스를 연결하는 스타일 정의
        private static void SetImplicitStyles()
        {
            if (System.Windows.Application.Current == null) return;
            var dict = System.Windows.Application.Current.Resources;
            int count = 0;

            void SetStyle<T>(string bgKey, string fgKey, string borderKey = null) where T : System.Windows.FrameworkElement
            {
                var style = new Style(typeof(T));
                
                // [추가] 기존 기본 스타일이 있다면 상속 (TextBox 등 기본 동작 유지)
                // 주의: BasedOn은 StaticResource로 가져와야 하는데, 여기서는 동적으로 생성하므로 
                // Application.Current.Resources에서 해당 타입의 기본 스타일을 찾아야 함.
                // 복잡성을 피하기 위해 여기서는 속성만 덮어쓰는 방식을 유지하되, 우선순위를 높임.

                if (bgKey != null) style.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, new DynamicResourceExtension(bgKey)));
                if (fgKey != null) style.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, new DynamicResourceExtension(fgKey)));
                if (borderKey != null)
                {
                    style.Setters.Add(new Setter(System.Windows.Controls.Control.BorderBrushProperty, new DynamicResourceExtension(borderKey)));
                    style.Setters.Add(new Setter(System.Windows.Controls.Control.BorderThicknessProperty, new Thickness(1)));
                }
                dict[typeof(T)] = style;
                count++;
            }

            // 1. 입력 컨트롤 (TextBox)
            SetStyle<System.Windows.Controls.TextBox>("InputBackgroundBrush", "PrimaryTextBrush", "BorderBrush");

            // 2. 버튼 (Button)
            SetStyle<System.Windows.Controls.Button>("ButtonBackgroundBrush", "PrimaryTextBrush", "BorderBrush");

            // 3. 콤보박스 (ComboBox)
            SetStyle<System.Windows.Controls.ComboBox>("InputBackgroundBrush", "PrimaryTextBrush", "BorderBrush");

            // 4. 그룹박스 (GroupBox)
            SetStyle<System.Windows.Controls.GroupBox>("WindowBackgroundBrush", "PrimaryTextBrush", "BorderBrush");

            // 5. Expander (기능 팔레트)
            SetStyle<System.Windows.Controls.Expander>("WindowBackgroundBrush", "PrimaryTextBrush", null);

            // 6. Label & TextBlock (텍스트)
            // TextBlock은 Control이 아니므로 별도 처리
            var styleText = new Style(typeof(System.Windows.Controls.TextBlock));
            styleText.Setters.Add(new Setter(System.Windows.Controls.TextBlock.ForegroundProperty, new DynamicResourceExtension("PrimaryTextBrush")));
            dict[typeof(System.Windows.Controls.TextBlock)] = styleText;

            var styleLabel = new Style(typeof(System.Windows.Controls.Label));
            styleLabel.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, new DynamicResourceExtension("PrimaryTextBrush")));
            dict[typeof(System.Windows.Controls.Label)] = styleLabel;

            // 7. ComboBoxItem (드롭다운 메뉴)
            SetStyle<System.Windows.Controls.ComboBoxItem>("InputBackgroundBrush", "PrimaryTextBrush", "BorderBrush");

            // 8. ToolTip
            SetStyle<System.Windows.Controls.ToolTip>("PanelBackgroundBrush", "PrimaryTextBrush", "BorderBrush");

            // 9. ScrollViewer (스크롤바 배경 등)
            // ScrollViewer는 복잡한 템플릿이라 배경색만 지정
            SetStyle<System.Windows.Controls.ScrollViewer>(null, null, null);

            App.Log($"[ThemeManager] 암시적 스타일 {count}개 등록 완료.");
        }

        private static bool IsSystemDark()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (key != null)
                    {
                        object val = key.GetValue("AppsUseLightTheme");
                        if (val is int i) return i == 0;
                    }
                }
            }
            catch { }
            return false; // 기본값: 라이트
        }
    }
}