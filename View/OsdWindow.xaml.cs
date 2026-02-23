using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Runtime.InteropServices; // DllImport, StructLayout 등
using System.Windows.Interop; // WindowInteropHelper, HwndSource
using SayoOSD.Models;
using SayoOSD.Helpers;
using SayoOSD.Managers;
using SayoOSD.Views;

namespace SayoOSD.Views
{
    public partial class OsdWindow : Window
    {
        private DispatcherTimer _timer;
        private DispatcherTimer _holdTimer; // 0.5초 대기용 타이머
        private DispatcherTimer _inputCheckTimer; // [추가] 입력 상태 감지 타이머
        private DispatcherTimer _feedbackSequenceTimer; // [추가] 피드백 순차 표시 타이머
        private Border[] _slots;
        private TextBlock[] _texts;
        private System.Windows.Controls.Image[] _images; // [추가] 아이콘 표시용 이미지 컨트롤 배열
        private Dictionary<string, ImageSource> _iconCache = new Dictionary<string, ImageSource>(); // [추가] 아이콘 캐시
        private int _osdMode = 0; // 0: Auto, 1: AlwaysOn, 2: AlwaysOff, 3: Bottommost
        private double _aspectRatio = 0; // 가로세로 비율 저장
        private AppSettings _currentSettings; // 현재 설정 참조
        private int _currentLayer = 0; // 현재 레이어
        private bool? _isMicMuted = null; // 마이크 상태
        private bool? _isSpeakerMuted = null; // 스피커 상태
        private List<ButtonConfig> _currentConfigs; // [추가] 현재 표시 중인 버튼 설정 목록
        private AppProfile _currentProfile; // [추가] 현재 활성화된 프로필 (없으면 null)
        private int _highlightedKeyIndex = -1; // [추가] 현재 하이라이트 중인 키 인덱스 추적
        public event Action<string> DebugLog; // 로그 전달 이벤트
        public event Action<int, string> OnFileDrop; // [추가] 파일 드롭 이벤트
        private bool _isUpdatingLocation = false; // [추가] 코드로 위치 변경 중인지 확인하는 플래그

        // [추가] 관리자 권한 실행 시 드래그 앤 드롭 허용을 위한 API
        [DllImport("user32.dll")]
        public static extern bool ChangeWindowMessageFilter(uint msg, uint flags);
        private const uint WM_DROPFILES = 0x0233;
        private const uint WM_COPYGLOBALDATA = 0x0049;
        private const uint MSGFLT_ADD = 1;

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);
        private const int VK_CONTROL = 0x11;

        public OsdWindow()
        {
            InitializeComponent();
            
            this.ResizeMode = ResizeMode.NoResize; // 기본은 크기 조절 불가 (테두리 없음)
            this.Topmost = true; // OSD가 항상 최상위에 표시되도록 설정
            this.IsHitTestVisible = false; // [수정] 기본은 클릭 투과 (게임 방해 금지)

            // 화면 오른쪽 하단 배치
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;
            this.Left = screenWidth - this.Width - 20; // 오른쪽 여백 20
            this.Top = screenHeight - this.Height - 50; // 하단 여백 50 (작업표시줄 고려)

            // 슬롯 배열 초기화
            _slots = new Border[] { Slot1, Slot2, Slot3, Slot4, Slot5, Slot6, Slot7, Slot8, Slot9, Slot10, Slot11, Slot12 };
            _texts = new TextBlock[] { Txt1, Txt2, Txt3, Txt4, Txt5, Txt6, Txt7, Txt8, Txt9, Txt10, Txt11, Txt12 };

            // [추가] UI 구조 변경: Border > Viewbox > TextBlock 구조를 Border > Grid > (Image, Viewbox > TextBlock) 구조로 변경
            _images = new System.Windows.Controls.Image[12];
            for (int i = 0; i < 12; i++)
            {
                var border = _slots[i];
                var viewbox = border.Child; // 기존 Viewbox (TextBlock 포함)
                border.Child = null; // 연결 해제

                var grid = new Grid();
                
                // 이미지 컨트롤 생성
                var img = new System.Windows.Controls.Image();
                img.Stretch = Stretch.Uniform;
                img.Margin = new Thickness(10);
                img.Visibility = Visibility.Collapsed; // 기본은 숨김
                img.SnapsToDevicePixels = true; // [추가] 픽셀 정렬로 선명도 향상
                img.UseLayoutRounding = true;   // [추가] 레이아웃 반올림
                RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                _images[i] = img;

                grid.Children.Add(img);
                if (viewbox != null) grid.Children.Add(viewbox); // 기존 텍스트 추가

                border.Child = grid;
            }

            // [추가] 드래그 앤 드롭 이벤트 연결
            for (int i = 0; i < _slots.Length; i++)
            {
                int idx = i + 1; // 1-based index
                _slots[i].AllowDrop = true;
                _slots[i].Drop += (s, e) =>
                {
                    if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
                    {
                        string[] files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
                        if (files != null && files.Length > 0)
                        {
                            OnFileDrop?.Invoke(idx, files[0]);
                        }
                    }
                };
            }

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(3); // 3초 뒤 사라짐
            _timer.Tick += (s, e) => { this.Hide(); _timer.Stop(); };

            _holdTimer = new DispatcherTimer();
            _holdTimer.Tick += HoldTimer_Tick;

            // 마우스 드래그 이동 기능
            this.MouseLeftButtonDown += (s, e) => 
            { 
                try
                {
                    this.DragMove();
                    
                    // [수정] 드래그 종료 후 명시적으로 위치 업데이트 (상하 위치 누락 방지)
                    if (_currentProfile != null)
                    {
                        _currentProfile.CustomOsdTop = this.Top;
                        _currentProfile.CustomOsdLeft = this.Left;
                    }
                    else if (_currentSettings != null)
                    {
                        _currentSettings.OsdTop = this.Top;
                        _currentSettings.OsdLeft = this.Left;
                    }

                    if (_currentSettings != null)
                        AppSettings.Save(_currentSettings);
                }
                catch { /* 드래그 시작 실패 등 예외 무시 */ }
            };
            
            // 위치 변경 시 설정 업데이트
            this.LocationChanged += (s, e) => 
            {
                // [수정] 코드로 위치를 변경하는 중(프로필 전환 등)에는 설정을 덮어쓰지 않음
                if (_isUpdatingLocation) return;

                // [수정] 프로필 모드일 경우 프로필에 위치 저장, 아니면 전역 설정에 저장
                if (_currentProfile != null)
                {
                    _currentProfile.CustomOsdTop = this.Top;
                    _currentProfile.CustomOsdLeft = this.Left;
                }
                else if (_currentSettings != null)
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

                // [추가] 관리자 권한으로 실행 시에도 탐색기(일반 권한)의 드롭 메시지를 허용
                ChangeWindowMessageFilter(WM_DROPFILES, MSGFLT_ADD);
                ChangeWindowMessageFilter(0x004A, MSGFLT_ADD); // WM_COPYDATA
                ChangeWindowMessageFilter(WM_COPYGLOBALDATA, MSGFLT_ADD); // WM_COPYGLOBALDATA
            };

            // [추가] Ctrl 키 감지 및 클릭 투과 제어 타이머
            _inputCheckTimer = new DispatcherTimer();
            _inputCheckTimer.Interval = TimeSpan.FromMilliseconds(100);
            _inputCheckTimer.Tick += (s, e) => 
            {
                // Ctrl 키가 눌려있거나, 이동 모드(테두리 있음)일 때만 마우스 입력 허용
                bool isCtrlDown = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
                bool isMoveMode = this.ResizeMode == ResizeMode.CanResize;

                bool shouldBeInteractive = isCtrlDown || isMoveMode;

                if (this.IsHitTestVisible != shouldBeInteractive)
                {
                    this.IsHitTestVisible = shouldBeInteractive;
                }
            };
            _inputCheckTimer.Start();
        }

        private void HoldTimer_Tick(object sender, EventArgs e)
        {
            _holdTimer.Stop();
            _highlightedKeyIndex = -1; // [추가] 하이라이트 종료 상태 기록
            
            // 하이라이트(노란 테두리) 제거 및 상태 색상 복구 (공통)
            RefreshAllSlotsBackground();

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
            else if (_osdMode == 1 || _osdMode == 3) // 항상 켜기/제일 아래: 하이라이트만 끄기
            {
                DebugLog?.Invoke("[OSD] Always On: 하이라이트 초기화");
            }
        }

        public void SetMoveMode(bool enable)
        {
            // IsHitTestVisible 제어는 _inputCheckTimer에서 담당함
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
                
                if (_osdMode == 1 || _osdMode == 3) // 항상 켜기 or 제일 아래
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
            if (_currentSettings != null && _currentSettings.OsdVertical)
            {
                this.Width = 180;
                this.Height = 500;
            }
            // 비율 재설정
            if (this.Height > 0) _aspectRatio = this.Width / this.Height;
        }

        // [추가] 현재 프로필 설정 및 위치 복원
        public void SetCurrentProfile(AppProfile profile)
        {
            _isUpdatingLocation = true; // [추가] 위치 변경 시작 (이벤트 무시)
            try
            {
                _currentProfile = profile;

                if (_currentProfile != null && _currentProfile.CustomOsdLeft.HasValue && _currentProfile.CustomOsdTop.HasValue)
                {
                    this.Left = _currentProfile.CustomOsdLeft.Value;
                    this.Top = _currentProfile.CustomOsdTop.Value;
                }
                else if (_currentSettings != null && _currentSettings.OsdLeft != -1 && _currentSettings.OsdTop != -1)
                {
                    this.Left = _currentSettings.OsdLeft;
                    this.Top = _currentSettings.OsdTop;
                }
            }
            finally
            {
                _isUpdatingLocation = false; // [추가] 위치 변경 종료
            }
        }

        public void UpdateSettings(AppSettings settings)
        {
            _currentSettings = settings;
            this.Opacity = settings.OsdOpacity;
            _osdMode = settings.OsdMode;
            DebugLog?.Invoke($"[OSD] UpdateSettings: Mode={_osdMode}, Opacity={this.Opacity}");

            // 저장된 위치가 유효하면 복원
            _isUpdatingLocation = true; // [추가] 위치 복원 중 이벤트 무시
            try
            {
                if (settings.OsdTop != -1 && settings.OsdLeft != -1)
                {
                    // [수정] 프로필 모드이고 프로필 위치가 설정되어 있다면 전역 위치로 덮어쓰지 않음
                    bool useProfilePos = _currentProfile != null && _currentProfile.CustomOsdTop.HasValue && _currentProfile.CustomOsdLeft.HasValue;
                    if (!useProfilePos)
                    {
                        this.Top = settings.OsdTop;
                        this.Left = settings.OsdLeft;
                    }
                }
            }
            finally
            {
                _isUpdatingLocation = false;
            }

            // 저장된 크기가 유효하면 복원
            if (settings.OsdWidth > 0 && settings.OsdHeight > 0)
            {
                this.Width = settings.OsdWidth;
                this.Height = settings.OsdHeight;
            }

            if (settings.OsdTimeout > 0)
                _timer.Interval = TimeSpan.FromSeconds(settings.OsdTimeout);

            // [추가] 레이아웃 방향 및 순서 업데이트
            UpdateLayoutOrientation();

            // [추가] 모드에 따른 Topmost 설정
            // 1(AlwaysOn), 0(Auto)는 Topmost=true / 3(Bottommost)는 Topmost=false
            this.Topmost = (_osdMode != 3);

            // 배경색 즉시 적용 (설정 변경 시 미리보기)
            RefreshAllSlotsBackground();

            // [추가] 폰트 설정 적용
            RefreshAllSlotsFont();

            UpdateNames(settings.Buttons, settings.LastLayerIndex); // 저장된 마지막 레이어 표시

            // 모드에 따른 즉시 처리
            if (_osdMode == 1) // 항상 켜기
            {
                this.Show();
                _timer.Stop();
                _holdTimer.Stop();
                this.BeginAnimation(Window.OpacityProperty, null);
            }
            else if (_osdMode == 3) // 제일 아래 (바탕화면 모드)
            {
                this.Show();
                _timer.Stop();
                _holdTimer.Stop();
                this.BeginAnimation(Window.OpacityProperty, null);
            }
            else if (_osdMode == 2) // 항상 끄기
                this.Hide();
        }

        // [추가] 가로/세로 모드 및 줄 교체 처리
        private void UpdateLayoutOrientation()
        {
            if (_slots == null || _slots.Length == 0 || _currentSettings == null) return;

            // 슬롯들의 부모 컨테이너 찾기 (UniformGrid 가정)
            var parent = _slots[0].Parent as System.Windows.Controls.Primitives.UniformGrid;
            if (parent == null) return;

            bool isVertical = _currentSettings.OsdVertical;
            bool swapRows = _currentSettings.OsdSwapRows;

            // 기존 자식 요소 제거 후 재배치
            parent.Children.Clear();

            if (!isVertical)
            {
                // --- 가로 모드 (2행 6열) ---
                parent.Rows = 2;
                parent.Columns = 6;

                // 가로/세로 크기 자동 스왑 (가로 모드인데 세로가 더 길면 스왑)
                if (this.Height > this.Width)
                {
                    double temp = this.Width;
                    this.Width = this.Height;
                    this.Height = temp;
                }

                if (!swapRows)
                {
                    // 기본: 1~6 (윗줄), 7~12 (아랫줄)
                    for (int i = 0; i < 12; i++) parent.Children.Add(_slots[i]);
                }
                else
                {
                    // 교체: 7~12 (윗줄), 1~6 (아랫줄)
                    for (int i = 6; i < 12; i++) parent.Children.Add(_slots[i]);
                    for (int i = 0; i < 6; i++) parent.Children.Add(_slots[i]);
                }
            }
            else
            {
                // --- 세로 모드 (6행 2열) ---
                parent.Rows = 6;
                parent.Columns = 2;

                // 가로/세로 크기 자동 스왑 (세로 모드인데 가로가 더 길면 스왑)
                if (this.Width > this.Height)
                {
                    double temp = this.Width;
                    this.Width = this.Height;
                    this.Height = temp;
                }

                // 세로 배치: (1,7), (2,8)... 순서로 채움 (UniformGrid는 행 우선 채움)
                for (int r = 0; r < 6; r++)
                {
                    int idx1 = r;      // 0..5 (1~6번 키)
                    int idx2 = r + 6;  // 6..11 (7~12번 키)

                    if (!swapRows)
                    {
                        // 기본: 왼쪽 열(1~6), 오른쪽 열(7~12)
                        parent.Children.Add(_slots[idx1]);
                        parent.Children.Add(_slots[idx2]);
                    }
                    else
                    {
                        // 교체: 왼쪽 열(7~12), 오른쪽 열(1~6)
                        parent.Children.Add(_slots[idx2]);
                        parent.Children.Add(_slots[idx1]);
                    }
                }
            }
        }

        public void UpdateNames(List<ButtonConfig> configs, int layer)
        {
            _currentConfigs = configs; // [추가] 현재 버튼 목록 저장 (가상/하드웨어 구분 없이 사용)
            _currentLayer = layer;
            foreach (var cfg in configs)
            {
                if (cfg.Layer == layer && cfg.Index >= 1 && cfg.Index <= 12)
                {
                    _texts[cfg.Index - 1].Text = cfg.Name;
                    UpdateSlotIcon(cfg.Index - 1, cfg.IconPath, cfg.ProgramPath); // [수정] 아이콘 경로 우선 사용
                }
            }
            RefreshAllSlotsBackground();
            RefreshAllSlotsFont(); // [추가] 레이어 변경 시 폰트도 갱신 (레이어별 폰트 지원)
        }

        public void SetMicState(bool isMuted)
        {
            _isMicMuted = isMuted;
            RefreshAllSlotsBackground();
        }

        public void SetSpeakerState(bool isMuted)
        {
            _isSpeakerMuted = isMuted;
            RefreshAllSlotsBackground();
        }

        // [추가] 슬롯 아이콘 업데이트 메서드
        private void UpdateSlotIcon(int index, string iconPath, string programPath)
        {
            if (_images == null || index < 0 || index >= _images.Length) return;

            // 1. IconPath가 있으면 최우선 사용
            string targetPath = iconPath;

            // 2. 없으면 ProgramPath 사용 (인수 제거 로직 필요)
            if (string.IsNullOrEmpty(targetPath) && !string.IsNullOrEmpty(programPath))
            {
                targetPath = programPath;
                // 인수가 포함된 경로일 경우 실행 파일만 추출
                if (!System.IO.File.Exists(targetPath))
                {
                    if (targetPath.StartsWith("\""))
                    {
                        int endQuote = targetPath.IndexOf('\"', 1);
                        if (endQuote > 0) targetPath = targetPath.Substring(1, endQuote - 1);
                    }
                    else
                    {
                        int exeIndex = targetPath.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
                        if (exeIndex > 0)
                        {
                            // .exe 뒤에 공백이 있거나 끝이면 자름
                            int splitIndex = exeIndex + 4;
                            if (splitIndex < targetPath.Length && targetPath[splitIndex] == ' ')
                                targetPath = targetPath.Substring(0, splitIndex);
                        }
                    }
                }
            }

            // [수정] 파일이 존재하지 않을 경우 경로 보정 시도 및 설정 자동 업데이트
            string finalPath = targetPath;
            bool pathChanged = false;

            if (!string.IsNullOrEmpty(finalPath) && !System.IO.File.Exists(finalPath))
            {
                string resolved = IconHelper.ResolvePath(finalPath);
                if (resolved != null)
                {
                    finalPath = resolved;
                    pathChanged = true;
                }
            }

            // 경로가 변경되었다면 설정 파일(settings.json) 자동 업데이트
            if (pathChanged && _currentSettings != null)
            {
                var btn = _currentSettings.Buttons.Find(b => b.Layer == _currentLayer && b.Index == index + 1);
                if (btn != null)
                {
                    bool saved = false;
                    // 1. 아이콘 경로가 변경된 경우
                    if (!string.IsNullOrEmpty(btn.IconPath) && !System.IO.File.Exists(btn.IconPath))
                    {
                        btn.IconPath = finalPath;
                        saved = true;
                    }
                    // 2. 프로그램 경로가 변경된 경우 (인수 유지 로직 필요)
                    else if (!string.IsNullOrEmpty(btn.ProgramPath))
                    {
                        // 기존 경로(targetPath)를 새 경로(finalPath)로 교체
                        // 단순 Replace는 위험하므로, 앞부분이 일치하는지 확인 후 교체
                        if (btn.ProgramPath.Contains(targetPath))
                        {
                            btn.ProgramPath = btn.ProgramPath.Replace(targetPath, finalPath);
                            saved = true;
                        }
                    }

                    if (saved) AppSettings.Save(_currentSettings);
                }
            }

            ImageSource icon = null;
            // 파일이 존재할 때만 아이콘 추출 시도 (.exe, .ico, .dll 등)
            if (!string.IsNullOrEmpty(finalPath) && System.IO.File.Exists(finalPath))
            {
                if (!_iconCache.TryGetValue(finalPath, out icon))
                {
                    icon = IconHelper.GetIconFromPath(finalPath, (msg) => DebugLog?.Invoke($"[Icon] {msg}"));
                    if (icon != null) _iconCache[finalPath] = icon;
                }
            }

            var img = _images[index];
            var txt = _texts[index];

            if (icon != null)
            {
                img.Source = icon;
                img.Visibility = Visibility.Visible;
                txt.Visibility = Visibility.Collapsed; // 아이콘이 있으면 텍스트 숨김
            }
            else
            {
                img.Source = null;
                img.Visibility = Visibility.Collapsed;
                txt.Visibility = Visibility.Visible; // 아이콘이 없으면 텍스트 표시
            }
        }

        // [추가] Hex 색상 문자열을 Brush로 변환하는 헬퍼
        private SolidColorBrush GetBrushFromHex(string hex, string fallbackHex = "#00000000")
        {
            try
            {
                if (string.IsNullOrEmpty(hex)) hex = fallbackHex;
                var brush = (SolidColorBrush)new BrushConverter().ConvertFrom(hex);
                return brush;
            }
            catch
            {
                try { return (SolidColorBrush)new BrushConverter().ConvertFrom(fallbackHex); }
                catch { return System.Windows.Media.Brushes.Transparent; }
            }
        }

        private void RefreshAllSlotsBackground()
        {
            for (int i = 1; i <= 12; i++) UpdateSlotBackground(i);
        }

        // [추가] 모든 슬롯의 폰트 스타일 갱신
        private void RefreshAllSlotsFont()
        {
            if (_texts == null) return;

            // 1. 기본값 (전역 설정)
            string familyName = _currentSettings?.OsdFontFamily ?? "Segoe UI";
            double size = _currentSettings?.OsdFontSize ?? 20.0;
            string weightStr = _currentSettings?.OsdFontWeight ?? "Normal";

            // 2. 레이어별 설정 (오버라이드)
            if (_currentSettings != null && _currentLayer >= 0 && _currentSettings.LayerStyles.Count > _currentLayer)
            {
                var style = _currentSettings.LayerStyles[_currentLayer];
                if (!string.IsNullOrEmpty(style.FontFamily)) familyName = style.FontFamily;
                if (style.FontSize.HasValue) size = style.FontSize.Value;
                if (!string.IsNullOrEmpty(style.FontWeight)) weightStr = style.FontWeight;
            }

            // 3. 프로필별 설정 (최우선 오버라이드)
            if (_currentProfile != null)
            {
                if (!string.IsNullOrEmpty(_currentProfile.CustomOsdFontFamily)) familyName = _currentProfile.CustomOsdFontFamily;
                if (_currentProfile.CustomOsdFontSize.HasValue) size = _currentProfile.CustomOsdFontSize.Value;
                if (!string.IsNullOrEmpty(_currentProfile.CustomOsdFontWeight)) weightStr = _currentProfile.CustomOsdFontWeight;
            }

            try
            {
                var family = new System.Windows.Media.FontFamily(familyName);
                var weight = (FontWeight)new FontWeightConverter().ConvertFromString(weightStr);

                foreach (var txt in _texts)
                {
                    if (txt != null)
                    {
                        txt.FontFamily = family;
                        txt.FontSize = size;
                        txt.FontWeight = weight;
                    }
                }
            }
            catch { /* 폰트 변환 실패 시 무시 */ }
        }

        private void UpdateSlotBackground(int keyIndex)
        {
            if (keyIndex < 1 || keyIndex > 12) return;
            
            // [추가] 현재 하이라이트 중인 키라면 배경색 초기화를 건너뛰어 시각 효과 유지
            if (keyIndex == _highlightedKeyIndex) return;

            var slot = _slots[keyIndex - 1];

            bool isMicKey = false;
            bool isSpeakerKey = false;
            if (_currentConfigs != null) // [수정] _currentSettings.Buttons 대신 현재 표시 중인 _currentConfigs 사용
            {
                var btn = _currentConfigs.Find(b => b.Layer == _currentLayer && b.Index == keyIndex);
                if (btn != null)
                {
                    if (btn.TargetLayer == 99) isMicKey = true;
                    if (btn.TargetLayer == 106 || btn.TargetLayer == 202) isSpeakerKey = true; // 106: Mute, 202: Audio Cycle
                }
            }

            if (isMicKey && _isMicMuted.HasValue)
            {
                // 마이크 상태 표시 (Muted: 옅은 빨강, Unmuted: 옅은 초록)
                if (_isMicMuted.Value)
                    slot.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x66, 0xFF, 0x00, 0x00));
                else
                    slot.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x66, 0x00, 0xFF, 0x00));
            }
            else if (isSpeakerKey && _isSpeakerMuted.HasValue)
            {
                // 스피커 상태 표시 (Muted: 주황, Unmuted: 하늘색)
                if (_isSpeakerMuted.Value)
                    slot.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x66, 0xFF, 0x45, 0x00));
                else
                    slot.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x66, 0x00, 0xBF, 0xFF));
            }
            else
            {
                // [수정] 설정된 배경색 적용 (프로필 -> 레이어 -> 전역 -> 기본값)
                string colorHex = _currentProfile?.CustomOsdBackgroundColor;
                
                // 프로필 색상이 없고 레이어별 색상이 설정되어 있다면 적용
                if (string.IsNullOrEmpty(colorHex) && _currentSettings != null && _currentLayer >= 0 && _currentSettings.LayerStyles.Count > _currentLayer)
                    colorHex = _currentSettings.LayerStyles[_currentLayer].BackgroundColor;

                if (string.IsNullOrEmpty(colorHex) && _currentSettings != null)
                    colorHex = _currentSettings.OsdBackgroundColor;
                
                slot.Background = GetBrushFromHex(colorHex, "#32FFFFFF");
            }
            slot.BorderBrush = System.Windows.Media.Brushes.Transparent;
            slot.BorderThickness = new Thickness(0);
        }

        public void HighlightKey(int keyIndex, bool? isMicMuted = null)
        {
            // IsHitTestVisible을 잠시 True로 바꿔야 드래그가 가능하지만, 
            // 평소에는 클릭 통과를 위해 False여야 함. 
            // 드래그 기능을 위해 항상 True로 두면 클릭 통과가 안 됨.
            // 따라서 드래그 기능을 원하시면 IsHitTestVisible="True"로 XAML을 수정해야 합니다.
            // 여기서는 코드에서 강제로 True로 설정하지 않고 XAML 설정을 따릅니다.

            DebugLog?.Invoke($"[OSD] HighlightKey: {keyIndex} (Mode: {_osdMode})");
            if (_osdMode == 2) return; // 항상 끄기 모드면 무시

            if (isMicMuted.HasValue) _isMicMuted = isMicMuted;

            // 모든 슬롯 초기화
            _highlightedKeyIndex = -1; // [추가] 초기화 전 인덱스 리셋하여 모든 배경을 지움
            RefreshAllSlotsBackground();

            // 해당 키 하이라이트
            if (keyIndex >= 1 && keyIndex <= 12)
            {
                var target = _slots[keyIndex - 1];
                _highlightedKeyIndex = keyIndex; // [추가] 하이라이트 중인 키 인덱스 저장

                // 마이크 키인지 확인
                bool isMicKey = false;
                bool isSpeakerKey = false;
                if (_currentConfigs != null) // [수정] 현재 표시 중인 버튼 목록에서 검색
                {
                    var btn = _currentConfigs.Find(b => b.Layer == _currentLayer && b.Index == keyIndex);
                    if (btn != null)
                    {
                        if (btn.TargetLayer == 99) isMicKey = true;
                        if (btn.TargetLayer == 106 || btn.TargetLayer == 202) isSpeakerKey = true;
                    }
                }

                if (isMicKey && _isMicMuted.HasValue)
                {
                    // 마이크 상태 강조 (더 진한 색)
                    target.Background = _isMicMuted.Value 
                        ? new SolidColorBrush(System.Windows.Media.Color.FromArgb(0xCC, 0xFF, 0x00, 0x00)) // Mute: Red
                        : new SolidColorBrush(System.Windows.Media.Color.FromArgb(0xCC, 0x00, 0xFF, 0x00)); // Unmute: Green
                }
                else if (isSpeakerKey && _isSpeakerMuted.HasValue)
                {
                    // 스피커 상태 강조
                    target.Background = _isSpeakerMuted.Value
                        ? new SolidColorBrush(System.Windows.Media.Color.FromArgb(0xCC, 0xFF, 0x45, 0x00)) // Mute: OrangeRed
                        : new SolidColorBrush(System.Windows.Media.Color.FromArgb(0xCC, 0x00, 0xBF, 0xFF)); // Unmute: DeepSkyBlue
                }
                else
                {
                    // [수정] 설정된 하이라이트 색상 적용 (프로필 -> 레이어 -> 전역)
                    string highlightHex = _currentProfile?.CustomOsdHighlightColor;
                    
                    if (string.IsNullOrEmpty(highlightHex) && _currentSettings != null && _currentLayer >= 0 && _currentSettings.LayerStyles.Count > _currentLayer)
                        highlightHex = _currentSettings.LayerStyles[_currentLayer].HighlightColor;

                    if (string.IsNullOrEmpty(highlightHex) && _currentSettings != null)
                        highlightHex = _currentSettings.OsdHighlightColor;

                    target.Background = GetBrushFromHex(highlightHex, "#FF007ACC");
                }
                
                // [수정] 설정된 테두리 색상 적용
                string borderHex = _currentProfile?.CustomOsdBorderColor;

                if (string.IsNullOrEmpty(borderHex) && _currentSettings != null && _currentLayer >= 0 && _currentSettings.LayerStyles.Count > _currentLayer)
                    borderHex = _currentSettings.LayerStyles[_currentLayer].BorderColor;

                if (string.IsNullOrEmpty(borderHex) && _currentSettings != null)
                    borderHex = _currentSettings.OsdBorderColor;

                target.BorderBrush = GetBrushFromHex(borderHex, "#FFFFFF00");
                target.BorderThickness = new Thickness(2);
            }
            
            // 애니메이션/타이머 초기화 및 창 표시
            _timer.Stop();
            _holdTimer.Stop();
            this.BeginAnimation(Window.OpacityProperty, null); // 기존 애니메이션 제거
            if (_currentSettings != null)
                this.Opacity = _currentSettings.OsdOpacity;
            this.Show();
            
            if (_osdMode == 0 || _osdMode == 1 || _osdMode == 3) // 자동, 항상 켜기, 제일 아래 모두 하이라이트 복구 타이머 작동
            {
                double duration = 0.5; // 기본 하이라이트 지속 시간 (항상 켜기/제일 아래 모드용)

                // [수정] 자동 모드일 경우 설정된 표시 시간(OsdTimeout)을 사용
                if (_osdMode == 0 && _currentSettings != null && _currentSettings.OsdTimeout > 0)
                {
                    duration = _currentSettings.OsdTimeout;
                }

                DebugLog?.Invoke($"[OSD] Hold Timer 시작 ({duration}s) Mode: {_osdMode}");
                _holdTimer.Interval = TimeSpan.FromSeconds(duration);
                _holdTimer.Start();
            }
        }

        // [추가] 모드 변경 시 피드백 표시
        public void ShowModeFeedback(string modeName, string iconPath = null, int keyIndex = -1)
        {
            // 기존 피드백 타이머 중지 (연속 입력 시 리셋)
            if (_feedbackSequenceTimer != null)
            {
                _feedbackSequenceTimer.Stop();
                _feedbackSequenceTimer = null;
            }

            // 아이콘 로드
            ImageSource icon = null;
            if (!string.IsNullOrEmpty(iconPath) && System.IO.File.Exists(iconPath))
            {
                if (!_iconCache.TryGetValue(iconPath, out icon))
                {
                    icon = IconHelper.GetIconFromPath(iconPath);
                    if (icon != null) _iconCache[iconPath] = icon;
                }
            }

            // 상태 업데이트 로컬 함수
            void UpdateState(bool showIconPhase)
            {
                if (keyIndex >= 1 && keyIndex <= 12)
                {
                    UpdateSlotFeedback(keyIndex - 1, modeName, icon, showIconPhase);
                }
                else
                {
                    for (int i = 0; i < 12; i++) UpdateSlotFeedback(i, modeName, icon, showIconPhase);
                }
            }

            // 복구 로컬 함수
            void RestoreState()
            {
                // [수정] 현재 로드된 설정(_currentConfigs)을 사용하여 복구
                // 가상 레이어(-1)일 때 하드웨어 버튼(0~4)만 있는 _currentSettings를 쓰면 매칭 실패로 텍스트가 갱신되지 않아 피드백 텍스트가 남는 문제 해결
                if (_currentConfigs != null)
                {
                    UpdateNames(_currentConfigs, _currentLayer);
                }
                else if (_currentSettings != null)
                {
                    UpdateNames(_currentSettings.Buttons, _currentLayer);
                }

                if (_currentSettings != null)
                {
                    this.Opacity = _currentSettings.OsdOpacity; // 투명도 복구
                }
                // Auto 모드면 숨김
                if (_osdMode == 0) this.Hide();
            }

            // 시퀀스 시작
            if (icon != null)
            {
                // 1단계: 아이콘 2초
                UpdateState(true); // 아이콘 표시
                
                _feedbackSequenceTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.0) };
                _feedbackSequenceTimer.Tick += (s, e) =>
                {
                    _feedbackSequenceTimer.Stop();
                    
                    // 2단계: 글씨 2초
                    UpdateState(false); // 글씨 표시
                    
                    _feedbackSequenceTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.0) };
                    _feedbackSequenceTimer.Tick += (s2, e2) => { _feedbackSequenceTimer.Stop(); RestoreState(); };
                    _feedbackSequenceTimer.Start();
                };
                _feedbackSequenceTimer.Start();
            }
            else
            {
                // 아이콘 없으면 글씨만 2초
                UpdateState(false);
                _feedbackSequenceTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.0) };
                _feedbackSequenceTimer.Tick += (s, e) => { _feedbackSequenceTimer.Stop(); RestoreState(); };
                _feedbackSequenceTimer.Start();
            }

            // 창이 꺼져있다면 잠시 켜기 (Auto 모드 등에서 확인 가능하도록)
            if (_osdMode == 1 || _osdMode == 3 || !this.IsVisible || this.Opacity < 0.1)
            {
                this.Opacity = 1.0;
                this.Show();
            }
        }

        // [추가] 개별 슬롯 피드백 업데이트 헬퍼
        private void UpdateSlotFeedback(int i, string text, ImageSource icon, bool showIconPhase)
        {
            if (_images != null && _images[i] != null)
            {
                _images[i].Source = icon;
                // 아이콘이 있고, 아이콘 표시 단계일 때만 보임
                if (icon != null && showIconPhase)
                {
                    _images[i].Visibility = Visibility.Visible;
                    _images[i].Opacity = 1.0; // 선명하게
                }
                else
                {
                    _images[i].Visibility = Visibility.Collapsed;
                }
            }
            if (_texts != null && _texts[i] != null)
            {
                _texts[i].Text = text;
                // 아이콘 표시 단계가 아니거나(글씨 단계), 아이콘이 아예 없으면 글씨 표시
                if (!showIconPhase || icon == null)
                {
                    _texts[i].Visibility = Visibility.Visible;
                }
                else
                {
                    _texts[i].Visibility = Visibility.Collapsed;
                }
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
