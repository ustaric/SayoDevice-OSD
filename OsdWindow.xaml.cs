using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Runtime.InteropServices; // DllImport, StructLayout 등
using System.Windows.Interop; // WindowInteropHelper, HwndSource

namespace SayoOSD
{
    public partial class OsdWindow : Window
    {
        private DispatcherTimer _timer;
        private DispatcherTimer _holdTimer; // 0.5초 대기용 타이머
        private Border[] _slots;
        private TextBlock[] _texts;
        private int _osdMode = 0; // 0: Auto, 1: AlwaysOn, 2: AlwaysOff
        private double _aspectRatio = 0; // 가로세로 비율 저장
        private AppSettings _currentSettings; // 현재 설정 참조
        public event Action<string> DebugLog; // 로그 전달 이벤트

        public OsdWindow()
        {
            InitializeComponent();
            
            this.ResizeMode = ResizeMode.NoResize; // 기본은 크기 조절 불가 (테두리 없음)
            this.Topmost = true; // OSD가 항상 최상위에 표시되도록 설정

            // 화면 오른쪽 하단 배치
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;
            this.Left = screenWidth - this.Width - 20; // 오른쪽 여백 20
            this.Top = screenHeight - this.Height - 50; // 하단 여백 50 (작업표시줄 고려)

            // 슬롯 배열 초기화
            _slots = new Border[] { Slot1, Slot2, Slot3, Slot4, Slot5, Slot6, Slot7, Slot8, Slot9, Slot10, Slot11, Slot12 };
            _texts = new TextBlock[] { Txt1, Txt2, Txt3, Txt4, Txt5, Txt6, Txt7, Txt8, Txt9, Txt10, Txt11, Txt12 };

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(3); // 3초 뒤 사라짐
            _timer.Tick += (s, e) => { this.Hide(); _timer.Stop(); };

            _holdTimer = new DispatcherTimer();
            _holdTimer.Tick += HoldTimer_Tick;

            // 마우스 드래그 이동 기능
            this.MouseLeftButtonDown += (s, e) => { this.DragMove(); };
            
            // 위치 변경 시 설정 업데이트
            this.LocationChanged += (s, e) => 
            {
                if (_currentSettings != null)
                {
                    _currentSettings.OsdTop = this.Top;
                    _currentSettings.OsdLeft = this.Left;
                }
            };

            // 크기 변경 시 설정 업데이트
            this.SizeChanged += (s, e) =>
            {
                if (_currentSettings != null)
                {
                    _currentSettings.OsdWidth = this.Width;
                    _currentSettings.OsdHeight = this.Height;
                }
                // 비율 갱신 (가로/세로 개별 조절 후 모서리 드래그 시 자연스럽게 이어지도록)
                if (this.ActualHeight > 0) _aspectRatio = this.ActualWidth / this.ActualHeight;
            };

            // 로드 완료 시 비율 계산 및 훅 등록
            this.Loaded += (s, e) =>
            {
                if (_aspectRatio == 0 && this.ActualHeight > 0) _aspectRatio = this.ActualWidth / this.ActualHeight;
                var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
                source.AddHook(WndProc);
            };
        }

        private void HoldTimer_Tick(object sender, EventArgs e)
        {
            _holdTimer.Stop();
            
            if (_osdMode == 0) // 자동 모드: 창 페이드 아웃
            {
                DebugLog?.Invoke($"[OSD] HoldTimer_Tick: 0.5초 대기 종료. 페이드 아웃 시작. (Current Opacity: {this.Opacity})");

                // 0.5초 페이드 아웃 애니메이션 시작
                var anim = new DoubleAnimation
                {
                    From = this.Opacity,
                    To = 0.0,
                    Duration = new Duration(TimeSpan.FromSeconds(0.5)),
                    FillBehavior = FillBehavior.HoldEnd
                };

                anim.Completed += (s, _) => {
                    DebugLog?.Invoke("[OSD] 페이드 아웃 애니메이션 완료. 창 숨김.");
                    this.Hide();
                    this.BeginAnimation(Window.OpacityProperty, null); // 애니메이션 해제
                    if (_currentSettings != null)
                        this.Opacity = _currentSettings.OsdOpacity; // 투명도 복구
                };
                this.BeginAnimation(Window.OpacityProperty, anim);
            }
            else if (_osdMode == 1) // 항상 켜기: 하이라이트만 끄기
            {
                DebugLog?.Invoke("[OSD] Always On: 하이라이트 초기화");
                foreach (var slot in _slots)
                {
                    slot.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)); // 반투명 복구
                    byte bgAlpha = _currentSettings != null ? (byte)_currentSettings.OsdBackgroundAlpha : (byte)50;
                    slot.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(bgAlpha, 0xFF, 0xFF, 0xFF)); // 설정된 배경 농도 복구
                    slot.BorderBrush = System.Windows.Media.Brushes.Transparent;
                    slot.BorderThickness = new Thickness(0);
                }
            }
        }

        public void SetMoveMode(bool enable)
        {
            this.IsHitTestVisible = enable;
            DebugLog?.Invoke($"[OSD] SetMoveMode: {enable}");
            this.ResizeMode = enable ? ResizeMode.CanResize : ResizeMode.NoResize; // 이동 모드일 때만 테두리 표시
            if (enable)
            {
                _timer.Stop();
                _holdTimer.Stop();
                this.BeginAnimation(Window.OpacityProperty, null); // 애니메이션 중지
                this.Show();
                this.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x55, 0xFF, 0x00, 0x00)); // 이동 모드일 때 붉은 배경
            }
            else
            {
                this.Background = System.Windows.Media.Brushes.Transparent;
                
                if (_osdMode == 1) // 항상 켜기
                {
                    this.Show();
                }
                else if (_osdMode == 0) // 자동: 이동 후 잠시 보여주고 사라짐
                {
                    _timer.Stop();
                    _holdTimer.Stop();
                    this.BeginAnimation(Window.OpacityProperty, null);
                    _timer.Start();
                }
                else
                {
                    this.Hide();
                }
            }
        }

        public void ResetSize()
        {
            this.Width = 500;
            this.Height = 180;
            // 비율 재설정
            if (this.Height > 0) _aspectRatio = this.Width / this.Height;
        }

        public void UpdateSettings(AppSettings settings)
        {
            _currentSettings = settings;
            this.Opacity = settings.OsdOpacity;
            _osdMode = settings.OsdMode;
            DebugLog?.Invoke($"[OSD] UpdateSettings: Mode={_osdMode}, Opacity={this.Opacity}");

            // 저장된 위치가 유효하면 복원
            if (settings.OsdTop != -1 && settings.OsdLeft != -1)
            {
                this.Top = settings.OsdTop;
                this.Left = settings.OsdLeft;
            }
            // 저장된 크기가 유효하면 복원
            if (settings.OsdWidth > 0 && settings.OsdHeight > 0)
            {
                this.Width = settings.OsdWidth;
                this.Height = settings.OsdHeight;
            }

            if (settings.OsdTimeout > 0)
                _timer.Interval = TimeSpan.FromSeconds(settings.OsdTimeout);

            // 배경색 즉시 적용 (설정 변경 시 미리보기)
            byte bgAlpha = (byte)settings.OsdBackgroundAlpha;
            foreach (var slot in _slots)
            {
                // 모든 슬롯을 설정된 배경색으로 초기화
                slot.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(bgAlpha, 0xFF, 0xFF, 0xFF));
                slot.BorderThickness = new Thickness(0);
            }

            UpdateNames(settings.Buttons, settings.LastLayerIndex); // 저장된 마지막 레이어 표시

            // 모드에 따른 즉시 처리
            if (_osdMode == 1) // 항상 켜기
            {
                this.Show();
                _timer.Stop();
                _holdTimer.Stop();
                this.BeginAnimation(Window.OpacityProperty, null);
            }
            else if (_osdMode == 2) // 항상 끄기
                this.Hide();
        }

        public void UpdateNames(List<ButtonConfig> configs, int layer)
        {
            foreach (var cfg in configs)
            {
                if (cfg.Layer == layer && cfg.Index >= 1 && cfg.Index <= 12)
                {
                    _texts[cfg.Index - 1].Text = cfg.Name;
                }
            }
        }

        public void HighlightKey(int keyIndex)
        {
            // IsHitTestVisible을 잠시 True로 바꿔야 드래그가 가능하지만, 
            // 평소에는 클릭 통과를 위해 False여야 함. 
            // 드래그 기능을 위해 항상 True로 두면 클릭 통과가 안 됨.
            // 따라서 드래그 기능을 원하시면 IsHitTestVisible="True"로 XAML을 수정해야 합니다.
            // 여기서는 코드에서 강제로 True로 설정하지 않고 XAML 설정을 따릅니다.

            DebugLog?.Invoke($"[OSD] HighlightKey: {keyIndex} (Mode: {_osdMode})");
            if (_osdMode == 2) return; // 항상 끄기 모드면 무시

            // 모든 슬롯 초기화
            foreach (var slot in _slots)
            {
                slot.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)); // 반투명
                byte bgAlpha = _currentSettings != null ? (byte)_currentSettings.OsdBackgroundAlpha : (byte)50;
                slot.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(bgAlpha, 0xFF, 0xFF, 0xFF)); // 설정된 배경 농도
                slot.BorderBrush = System.Windows.Media.Brushes.Transparent;
                slot.BorderThickness = new Thickness(0);
            }

            // 해당 키 하이라이트
            if (keyIndex >= 1 && keyIndex <= 12)
            {
                var target = _slots[keyIndex - 1];
                target.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0xFF, 0x00, 0x7A, 0xCC)); // 파란색 강조
                target.BorderBrush = System.Windows.Media.Brushes.Yellow;
                target.BorderThickness = new Thickness(2);
            }
            
            // 애니메이션/타이머 초기화 및 창 표시
            _timer.Stop();
            _holdTimer.Stop();
            this.BeginAnimation(Window.OpacityProperty, null); // 기존 애니메이션 제거
            if (_currentSettings != null)
                this.Opacity = _currentSettings.OsdOpacity;
            this.Show();
            
            if (_osdMode == 0 || _osdMode == 1) // 자동 모드 또는 항상 켜기 모드일 때 타이머 작동
            {
                DebugLog?.Invoke($"[OSD] Hold Timer 시작 (0.5s) Mode: {_osdMode}");
                _holdTimer.Interval = TimeSpan.FromSeconds(0.5);
                _holdTimer.Start();
            }
        }

        // 프로그램 시작 시 테스트용으로 보여주기
        public void ShowBriefly()
        {
            this.Show();
            if (_osdMode != 1)
            {
                _timer.Stop();
                _holdTimer.Stop();
                this.BeginAnimation(Window.OpacityProperty, null);
                _timer.Start();
            }
        }

        // Win32 API 및 메시지 처리
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int left, top, right, bottom; }

        private const int WM_SIZING = 0x0214;
        private const int WMSZ_LEFT = 1;
        private const int WMSZ_RIGHT = 2;
        private const int WMSZ_TOP = 3;
        private const int WMSZ_BOTTOM = 6;

        private const int WM_NCHITTEST = 0x0084;
        private const int HTLEFT = 10;
        private const int HTRIGHT = 11;
        private const int HTTOP = 12;
        private const int HTTOPLEFT = 13;
        private const int HTTOPRIGHT = 14;
        private const int HTBOTTOM = 15;
        private const int HTBOTTOMLEFT = 16;
        private const int HTBOTTOMRIGHT = 17;

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_SIZING)
            {
                if (_aspectRatio > 0)
                {
                    RECT rc = Marshal.PtrToStructure<RECT>(lParam);
                    int w = rc.right - rc.left;
                    int h = rc.bottom - rc.top;
                    int side = (int)wParam;

                    // 가로(1,2)나 세로(3,6)만 조절할 때는 비율 유지 안 함
                    if (side == WMSZ_LEFT || side == WMSZ_RIGHT || side == WMSZ_TOP || side == WMSZ_BOTTOM)
                    {
                        return IntPtr.Zero;
                    }

                    // 모서리(대각선) 조절 시에만 비율 유지
                    int newH = (int)(w / _aspectRatio);
                    rc.bottom = rc.top + newH;
                    
                    Marshal.StructureToPtr(rc, lParam, true);
                }
            }
            else if (msg == WM_NCHITTEST && this.ResizeMode == ResizeMode.CanResize)
            {
                // 화면 좌표를 클라이언트 좌표로 변환하여 테두리 영역 판별
                int x = (int)(short)(lParam.ToInt64() & 0xFFFF);
                int y = (int)(short)((lParam.ToInt64() >> 16) & 0xFFFF);
                System.Windows.Point pt = this.PointFromScreen(new System.Windows.Point(x, y));

                int border = 10; // 테두리 감지 영역 (픽셀)

                if (pt.X < border && pt.Y < border) { handled = true; return (IntPtr)HTTOPLEFT; }
                if (pt.X > this.ActualWidth - border && pt.Y < border) { handled = true; return (IntPtr)HTTOPRIGHT; }
                if (pt.X < border && pt.Y > this.ActualHeight - border) { handled = true; return (IntPtr)HTBOTTOMLEFT; }
                if (pt.X > this.ActualWidth - border && pt.Y > this.ActualHeight - border) { handled = true; return (IntPtr)HTBOTTOMRIGHT; }
                if (pt.X < border) { handled = true; return (IntPtr)HTLEFT; }
                if (pt.X > this.ActualWidth - border) { handled = true; return (IntPtr)HTRIGHT; }
                if (pt.Y < border) { handled = true; return (IntPtr)HTTOP; }
                if (pt.Y > this.ActualHeight - border) { handled = true; return (IntPtr)HTBOTTOM; }
            }

            return IntPtr.Zero;
        }
    }
}
