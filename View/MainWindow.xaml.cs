using System;
using System.Diagnostics; // Process.Start
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Runtime.InteropServices; // DllImport
using System.Security.Principal; // WindowsPrincipal
using SayoOSD.ViewModels;
using SayoOSD.Models;
using SayoOSD.Managers;
using SayoOSD.Services; // For InputExecutor constants
using SayoOSD.Views;

namespace SayoOSD.Views
{
    public partial class MainWindow : Window
    {
        private OsdWindow _osd;
        private AppSettings _settings;
        private System.Windows.Forms.NotifyIcon _notifyIcon; // 트레이 아이콘
        private System.Windows.Forms.ContextMenuStrip _trayMenu; // [추가] 트레이 메뉴
        private int _selectedSlotIndex = 1; // 현재 선택된 슬롯 (1~12)

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        extern static bool DestroyIcon(IntPtr handle);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern uint RegisterWindowMessage(string lpString);
        private uint _wmShowMessage;

        // [추가] 활성 창 변경 감지 훅 (SetWinEventHook)
        private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);
        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);
        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
        private IntPtr _winEventHook;
        private WinEventDelegate _winEventDelegate; // GC 수집 방지용 델리게이트 참조
        
        private const int ACTION_OSD_CYCLE = 300; // [추가] OSD 모드 순환 상수
        private System.Windows.Controls.ScrollViewer _listScrollViewer; // [추가] 로그 리스트 스크롤 뷰어 캐시
        private MainViewModel _viewModel; // [추가] ViewModel

        public MainWindow()
        {
            // [핵심] App 클래스의 정적 생성자를 강제로 실행시켜 중복 검사를 가장 먼저 수행
            // MainWindow가 StartupObject일 경우, InitializeComponent보다 먼저 실행되어야 창이 뜨지 않고 종료됨
            var _ = App.StartupLogs; 

            InitializeComponent();
            _osd = new OsdWindow();
            _osd.DebugLog += (msg) => Log(msg); // [수정] OSD 로그를 화면 리스트 및 파일로 전달
            _osd.OnFileDrop += HandleOsdFileDrop; // [추가] 파일 드롭 핸들러 연결
            
            // 설정 로드
            _settings = AppSettings.Load();
            // [추가] ViewModel 초기화 및 DataContext 설정
            _viewModel = new MainViewModel(_settings);
            this.DataContext = _viewModel;
            
            // [추가] ViewModel의 OSD 업데이트 요청 처리
            _viewModel.RequestOsdUpdate += () => Dispatcher.Invoke(() => {
                _osd.UpdateNames(_settings.Buttons, _viewModel.CurrentLayer);
                _viewModel.LoadKeySlotsForLayer(_viewModel.CurrentLayer);
            });
            // [추가] ViewModel의 UI 요청 이벤트 구독
            _viewModel.RequestOsdHighlight += (keyIndex, micState) => _osd.HighlightKey(keyIndex, micState);
            _viewModel.RequestSpeakerUpdate += (muted) => _osd.SetSpeakerState(muted);
            _viewModel.RequestAutoMappingConfirmation += (signature) =>
            {
                string msg = string.Format(LanguageManager.GetString(_settings.Language, "MsgPatternMappedDetail"), _viewModel.SelectedSlot.Index, signature);
                System.Windows.MessageBox.Show(msg);
            };
            _viewModel.RequestOsdFeedback += (modeName, icon, idx) => _osd.ShowModeFeedback(modeName, icon, idx);
            _viewModel.RequestLayerChange += (layer) => UpdateLayerRadioButton(layer);

            // [추가] 설정 저장 이벤트 구독 (저장됨 메시지 표시)
            AppSettings.OnSettingsSaved += () => Dispatcher.Invoke(() => {
                ShowSavedMessage();
                InputExecutor.GlobalVolumeStep = _settings.VolumeStep; // [추가] 볼륨 설정 동기화
                UpdateLanguage(); // [수정] 설정(언어) 변경 시 메인 UI 언어도 즉시 갱신
            });

            // [추가] InputExecutor 전역 로그 구독 (활성 창 볼륨 조절 등 로그 표시)
            InputExecutor.GlobalLogMessage += (msg) => Dispatcher.Invoke(() => Log(msg));


            // 로그 파일 저장 설정 적용 및 이벤트 연결
            LogManager.Enabled = _settings.EnableFileLog; // [추가] 초기 로그 설정 적용

            // 언어 데이터 로드
            LanguageManager.Load();
            
            UpdateLanguage(); // 초기 언어 적용
            
            // OSD 설정 적용
            _osd.UpdateSettings(_settings);
            InputExecutor.GlobalVolumeStep = _settings.VolumeStep; // [추가] 초기 볼륨 설정 적용

            // 트레이 아이콘 초기화
            _notifyIcon = new System.Windows.Forms.NotifyIcon();
            
            // 트레이 아이콘 설정
            // 0. icon.png 파일이 있으면 최우선으로 사용 (투명 배경 유지)
            string pngPath = System.IO.Path.Combine(AppContext.BaseDirectory, "icon.png");
            if (System.IO.File.Exists(pngPath))
            {
                try
                {
                    // 윈도우(프로그램) 아이콘 설정 (WPF)
                    this.Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri(pngPath));

                    // 트레이 아이콘 설정 (PNG -> Icon 변환)
                    using (var bitmap = new System.Drawing.Bitmap(pngPath))
                    {
                        IntPtr hIcon = bitmap.GetHicon();
                        // 핸들 복사본을 만들어 아이콘으로 설정하고, 원본 핸들은 즉시 해제 (메모리 누수 방지 및 투명도 보존)
                        using (var tempIcon = System.Drawing.Icon.FromHandle(hIcon))
                        {
                            _notifyIcon.Icon = (System.Drawing.Icon)tempIcon.Clone();
                        }
                        DestroyIcon(hIcon);
                    }
                }
                catch { /* PNG 로드 실패 시 무시 */ }
            }

            // 1. PNG가 없을 경우: 실행 폴더의 icon.ico 파일 시도
            if (_notifyIcon.Icon == null)
            {
                string iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "icon.ico");
                if (System.IO.File.Exists(iconPath))
                {
                    try { _notifyIcon.Icon = new System.Drawing.Icon(iconPath); } catch { }
                }
            }

            // 2. 파일이 없을 경우: 프로젝트 속성 아이콘(.exe) 시도 (ExtractAssociatedIcon)
            if (_notifyIcon.Icon == null)
            {
                try
                {
                    if (!string.IsNullOrEmpty(Environment.ProcessPath))
                        _notifyIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath);
                }
                catch { }
            }

            // 3. 그래도 없으면 기본 시스템 아이콘 사용
            if (_notifyIcon.Icon == null)
                _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
            
            _notifyIcon.Visible = true;
            _notifyIcon.Text = "Sayo OSD";
            _notifyIcon.DoubleClick += (s, args) => {
                this.Show();
                this.WindowState = WindowState.Normal;
            };

            // [수정] 트레이 메뉴 초기화 (다국어 지원)
            UpdateTrayMenu();

            // UI 콤보박스도 저장된 레이어로 동기화
            UpdateLayerRadioButton(_viewModel.CurrentLayer);

            this.Loaded += MainWindow_Loaded;
            this.Closing += (s, e) => 
            { 
                _viewModel.Dispose();
                // [추가] 훅 해제
                if (_winEventHook != IntPtr.Zero)
                    UnhookWinEvent(_winEventHook);

                AppSettings.Save(_settings); // 종료 시 설정(위치 포함) 저장
                _osd.Close(); 
                _notifyIcon.Dispose(); 
            };
            this.StateChanged += MainWindow_StateChanged;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 중복 실행 시 창 복구를 위한 메시지 등록
            _wmShowMessage = RegisterWindowMessage("SayoOSD_Show_Window");

            // App.xaml.cs에서 기록된 시작 로그를 메인 화면 로그창에 출력
            if (App.StartupLogs != null)
            {
                int count = App.StartupLogs.Count;
                Log($"[System] App 로그 동기화 ({count}개)");

                if (count > 0)
                {
                    // 리스트를 복사하여 순회 (열거 중 수정 오류 방지)
                    var logs = App.StartupLogs.ToList();
                    foreach (var logMsg in logs)
                    {
                        Log(logMsg);
                    }
                }
                else
                {
                    Log("[System] 경고: App 로그가 비어있습니다. (App 초기화 문제 가능성)");
                }
                App.StartupLogs.Clear(); // 로그 출력 후 비우기 (중복 방지)
            }

            // UI 렌더링 및 초기 로그 출력이 완료된 후 장치 감지 시작 (DispatcherPriority.Background 사용)
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(async () =>
            {
                // [수정] 프로그램 시작 직후 키 로그가 쏟아져 App 로그가 묻히는 것을 방지하기 위해 0.1초 대기
                await System.Threading.Tasks.Task.Delay(100);
                IntPtr hwnd = new WindowInteropHelper(this).Handle;
                _viewModel.Initialize(hwnd);

                // 시작 시 OSD가 잘 뜨는지 테스트
                _osd.ShowBriefly();
                Log("프로그램 시작됨. OSD 테스트 표시.");

                // 메시지 루프 훅 추가
                HwndSource source = HwndSource.FromHwnd(hwnd);
                source.AddHook(WndProc);

                // [추가] 활성 창 변경 이벤트 훅 등록
                _winEventDelegate = new WinEventDelegate(WinEventProc);
                _winEventHook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, _winEventDelegate, 0, 0, WINEVENT_OUTOFCONTEXT);
            }));

            // 자동 실행(--tray)으로 시작된 경우 트레이로 숨김
            string[] args = Environment.GetCommandLineArgs();
            if (args.Any(a => a.Equals("--tray", StringComparison.OrdinalIgnoreCase)))
            {
                this.WindowState = WindowState.Minimized;
                this.Hide();
            }
        }

        // [추가] 활성 창 변경 시 호출되는 콜백
        private void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (eventType == EVENT_SYSTEM_FOREGROUND && hwnd != IntPtr.Zero)
            {
                string path = null;
                try
                {
                    GetWindowThreadProcessId(hwnd, out uint pid);
                    if (pid > 0)
                    {
                        // 프로세스 정보 가져오기
                        using (var proc = Process.GetProcessById((int)pid))
                        {
                            try { path = proc.MainModule.FileName; } catch { } // 권한 문제 등으로 실패 시 무시
                        }
                    }
                }
                catch { /* 프로세스 접근 실패 등 예외 무시 */ }

                // [수정] 경로 획득 성공 여부와 관계없이 업데이트 호출 (실패 시 null로 초기화하여 이전 아이콘 잔상 제거)
                _viewModel.UpdateActiveWindowIcons(path);
            }
        }

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            if (this.WindowState == WindowState.Minimized)
                this.Hide(); // 최소화 시 작업표시줄에서 숨김 (트레이로 이동)
        }

        // [추가] 저장됨 메시지 애니메이션 표시
        private void ShowSavedMessage()
        {
            if (LblStatusSaved == null) return;

            // 기존 애니메이션 중지 및 초기화
            LblStatusSaved.BeginAnimation(UIElement.OpacityProperty, null);
            LblStatusSaved.Opacity = 1.0;

            // 1.5초 대기 후 페이드 아웃 애니메이션
            var anim = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = new Duration(TimeSpan.FromSeconds(1.0)),
                BeginTime = TimeSpan.FromSeconds(1.0) // 1초 동안 보여주고 사라짐
            };
            LblStatusSaved.BeginAnimation(UIElement.OpacityProperty, anim);
        }

        private void RbLayer_Checked(object sender, RoutedEventArgs e)
        {
            if (_settings == null) return;
            if (sender is System.Windows.Controls.RadioButton rb && rb.IsChecked == true && rb.Tag != null)
            {
                if (int.TryParse(rb.Tag.ToString(), out int layer))
                {
                    _viewModel.ChangeLayerCommand.Execute(layer);
                }
            }
        }

        private void UpdateLayerRadioButton(int layer)
        {
            if (layer == 0 && RbLayer0 != null) RbLayer0.IsChecked = true;
            else if (layer == 1 && RbLayer1 != null) RbLayer1.IsChecked = true;
            else if (layer == 2 && RbLayer2 != null) RbLayer2.IsChecked = true;
            else if (layer == 3 && RbLayer3 != null) RbLayer3.IsChecked = true;
            else if (layer == 4 && RbLayer4 != null) RbLayer4.IsChecked = true;
        }

        private void TxtSlot_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox txt && txt.DataContext is KeySlotViewModel vm)
            {
                _selectedSlotIndex = vm.Index;
                _viewModel.SelectedSlot = vm; // VM 선택 상태 동기화
                // [수정] 상세 설정 패널의 내부 UI 상태 업데이트 (코드 비하인드 로직 제거됨, XAML 바인딩으로 대체)
                // UpdateDetailPanelVisibility(vm.TargetLayer); // 더 이상 필요하지 않음
            }
        }

        private void TxtSlot_LostFocus(object sender, RoutedEventArgs e)
        {
            SaveSlotName(sender);
        }

        private void TxtSlot_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SaveSlotName(sender);
                // 엔터키 입력 시 포커스를 해제하거나 유지 (여기서는 유지하되 소리만 제거)
                e.Handled = true; 
                Keyboard.ClearFocus(); // 포커스 해제하여 입력 완료 느낌 주기
            }
        }

        private void SaveSlotName(object sender)
        {
            if (sender is System.Windows.Controls.TextBox txt && txt.DataContext is KeySlotViewModel vm)
            {
                // 바인딩에 의해 이미 VM과 Model의 Name은 업데이트된 상태
                // 설정 저장 및 OSD 업데이트만 수행
                AppSettings.Save(_settings);
                _osd.UpdateNames(_settings.Buttons, _viewModel.CurrentLayer);
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // 중복 실행 시 기존 창 복구 메시지 처리
            if (msg == _wmShowMessage && _wmShowMessage != 0)
            {
                Log("[System] 중복 실행 시도가 감지되어 창을 활성화했습니다.");
                this.Show();
                this.WindowState = WindowState.Normal;
                this.Activate();
                handled = true;
                return IntPtr.Zero;
            }

            // Raw Input 메시지 처리
            if (_viewModel != null)
                _viewModel.ProcessRawInputMessage(msg, lParam);
            
            return IntPtr.Zero;
        }

        private string GetSignature(byte[] data)
        {
            if (data == null || data.Length == 0) return "";
            return BitConverter.ToString(data).Replace("-", " ");
        }

        // [추가] OSD 파일 드롭 처리 핸들러
        private void HandleOsdFileDrop(int slotIndex, string filePath)
        {
            string fileName = System.IO.Path.GetFileNameWithoutExtension(filePath);
            
            // 1. 등록 확인 (Yes/No)
            string msg = string.Format(LanguageManager.GetString(_settings.Language, "MsgConfirmRunProgram"), slotIndex, fileName);
            string title = LanguageManager.GetString(_settings.Language, "TitleRunProgram");

            if (System.Windows.MessageBox.Show(msg, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                int layer = _viewModel.CurrentLayer;
                var btn = _settings.Buttons.FirstOrDefault(b => b.Layer == layer && b.Index == slotIndex);
                if (btn != null)
                {
                    // 설정 저장 (이름, 경로, 기능)
                    btn.Name = fileName;
                    btn.ProgramPath = filePath;
                    btn.IconPath = null; // [추가] 드롭 시 기존 아이콘 설정 초기화
                    btn.TargetLayer = InputExecutor.ACTION_RUN_PROGRAM;
                    AppSettings.Save(_settings);

                    // UI 및 OSD 갱신
                    Dispatcher.Invoke(() => {
                        _osd.UpdateNames(_settings.Buttons, layer);
                        _viewModel.LoadKeySlotsForLayer(layer); // VM 갱신
                    });
                    
                    Log($"[Setting] Key {slotIndex} (Drop) -> Run: {filePath}");

                    // 2. 매핑 상태 확인 및 연결 유도
                    if (string.IsNullOrEmpty(btn.TriggerPattern))
                    {
                        string msg2 = LanguageManager.GetString(_settings.Language, "MsgNeedMapping");
                        string title2 = LanguageManager.GetString(_settings.Language, "TitleNeedMapping");

                        if (System.Windows.MessageBox.Show(msg2, title2, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                        {
                            // 메인 창 최상위로 활성화
                            this.Show();
                            this.WindowState = WindowState.Normal;
                            this.Activate();
                            this.Topmost = true; // 잠시 최상위 고정
                            
                            // 해당 슬롯 선택
                            _selectedSlotIndex = slotIndex;
                            var slotVm = _viewModel.KeySlots.FirstOrDefault(k => k.Index == slotIndex);
                            if (slotVm != null) _viewModel.SelectedSlot = slotVm;

                            // 자동 감지 시작 (이미 감지 중이 아닐 때만)
                            if (!_viewModel.IsAutoDetecting) _viewModel.ToggleAutoDetect();
                            
                            this.Topmost = false; // 최상위 해제
                        }
                    }
                }
            }
        }

        private void AddLog(byte[] data, string dataHex, string msg, bool isCandidate = false)
        {
            byte rawKey = 0;
            if (data != null && data.Length > 10) 
            {
                rawKey = data[10];
            }
            else if (data != null)
            {
                if (data.Length >= 3) rawKey = data[2];
                // 키 값이 0이거나 데이터가 짧은 경우 다른 바이트(인덱스 1)를 시도하여 Hex 표시
                if (rawKey == 0 && data.Length >= 2) rawKey = data[1];
            }

            // [추가] 스마트 오토 스크롤: 현재 스크롤이 맨 아래에 있을 때만 자동 스크롤
            bool isAtBottom = false;
            if (this.WindowState != WindowState.Minimized)
            {
                if (_listScrollViewer == null)
                    _listScrollViewer = GetScrollViewer(LstLog);

                if (_listScrollViewer != null)
                {
                    // 스크롤이 맨 아래(또는 근처)에 있는지 확인 (오차 범위 10px)
                    if (_listScrollViewer.VerticalOffset >= _listScrollViewer.ScrollableHeight - 10.0)
                    {
                        isAtBottom = true;
                    }
                }
                else isAtBottom = true; // 초기 상태
            }

            var entry = new LogEntry
            {
                Time = DateTime.Now.ToString("HH:mm:ss"),
                RawKeyHex = rawKey == 0 ? "-" : rawKey.ToString("X2"),
                RawKeyByte = rawKey,
                RawBytes = data,
                Data = string.IsNullOrEmpty(dataHex) ? msg : $"{dataHex} ({msg})",
                Foreground = isCandidate ? System.Windows.Media.Brushes.Blue : System.Windows.Media.Brushes.Black
            };

            // [수정] 최소화 상태여도 로그는 리스트에 추가해야 함 (데이터 유실 방지)
            // ViewModel을 통해 데이터 추가 (UI 바인딩 자동 갱신)
            _viewModel.LogEntries.Add(entry);
            if (_viewModel.LogEntries.Count > 1000) _viewModel.LogEntries.RemoveAt(0);

            // 스크롤만 최소화 상태가 아닐 때 수행 (UI 부하 방지)
            if (this.WindowState != WindowState.Minimized && isAtBottom)
            {
                LstLog.ScrollIntoView(entry);
            }

            // 파일에도 기록
            string logMsg = msg;
            // [수정] 로그 파일에도 Hex 데이터 포함 (매핑된 로그 확인용)
            if (!string.IsNullOrEmpty(dataHex)) logMsg = $"{dataHex} | {msg}";
            
            if (rawKey != 0) logMsg = $"[Key: {rawKey:X2}] {msg}";
            LogManager.Write(logMsg);
        }

        private void Log(string msg)
        {
            // 일반 텍스트 로그도 리스트에 추가
            AddLog(null, "", msg);
        }

        // [추가] VisualTree에서 ScrollViewer 찾기
        private System.Windows.Controls.ScrollViewer GetScrollViewer(DependencyObject o)
        {
            if (o is System.Windows.Controls.ScrollViewer sv) return sv;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(o); i++)
            {
                var child = VisualTreeHelper.GetChild(o, i);
                var result = GetScrollViewer(child);
                if (result != null) return result;
            }
            return null;
        }

        private void BtnAutoDetect_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.StartAutoDetectCommand.Execute(null);
        }

        // [추가] 수동 감지 버튼 핸들러
        private void BtnManualDetect_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.StartManualDetectCommand.Execute(null);
        }

        private void LstLog_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // 로그 더블 클릭 시 매핑
            if (LstLog.SelectedItem is LogEntry entry && entry.RawBytes != null)
            {
                string signature = GetSignature(entry.RawBytes);
                if (!string.IsNullOrEmpty(signature))
                {
                    _viewModel.PerformAutoMapping(signature);
                }
            }
        }

        private void BtnUnmap_Click(object sender, RoutedEventArgs e)
        {
            int layer = _viewModel.CurrentLayer;
            int slotIndex = _selectedSlotIndex;

            var btn = _settings.Buttons.FirstOrDefault(b => b.Layer == layer && b.Index == slotIndex);
            if (btn != null)
            {
                string msg = string.Format(LanguageManager.GetString(_settings.Language, "MsgUnmapConfirmDetail"), slotIndex, layer);
                string title = LanguageManager.GetString(_settings.Language, "TitleUnmap");

                if (System.Windows.MessageBox.Show(msg, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    btn.TriggerPattern = null;
                    btn.TargetLayer = -1;
                    btn.Name = $"Key {slotIndex}"; // 기본 이름으로 복구
                    btn.ProgramPath = null;
                    btn.IconPath = null; // [추가] 아이콘 경로 초기화

                    AppSettings.Save(_settings);

                    _osd.UpdateNames(_settings.Buttons, _viewModel.CurrentLayer);
                    _viewModel.LoadKeySlotsForLayer(_viewModel.CurrentLayer); // VM 갱신
                }
            }
        }

        // [추가] 사이드 메뉴 드래그 시작
        private void SideItem_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && sender is FrameworkElement fe && fe.Tag != null)
            {
                // [수정] 드래그 데이터에 메뉴 이름(Text)도 포함하여 전달
                string text = (fe as System.Windows.Controls.TextBlock)?.Text ?? "";
                string data = $"SayoFunc:{fe.Tag}:{text}";
                DragDrop.DoDragDrop(fe, data, System.Windows.DragDropEffects.Copy);
            }
        }

        // [추가] 슬롯 드래그 오버 (텍스트박스 기본 동작 방지)
        private void TxtSlot_PreviewDragOver(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.StringFormat) || e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                e.Effects = System.Windows.DragDropEffects.Copy;
                e.Handled = true;
            }
        }

        // [추가] 슬롯 드롭 처리 (사이드 메뉴 + 파일)
        private void TxtSlot_PreviewDrop(object sender, System.Windows.DragEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox txt && txt.DataContext is KeySlotViewModel vm)
            {
                int index = vm.Index;
                // 1. 사이드 메뉴 드롭
                if (e.Data.GetDataPresent(System.Windows.DataFormats.StringFormat))
                {
                    string data = (string)e.Data.GetData(System.Windows.DataFormats.StringFormat);
                    if (data.StartsWith("SayoFunc:"))
                    {
                        // [수정] 데이터 파싱 (Tag:Text)
                        string[] parts = data.Split(new[] { ':' }, 3);
                        string tagStr = parts.Length > 1 ? parts[1] : "";
                        string nameStr = parts.Length > 2 ? parts[2] : "";

                        if (int.TryParse(tagStr, out int funcId))
                        {
                            int layer = _viewModel.CurrentLayer;
                            var btn = _settings.Buttons.FirstOrDefault(b => b.Layer == layer && b.Index == index);
                            if (btn != null)
                            {
                                btn.TargetLayer = funcId;
                                btn.ProgramPath = null; // 초기화
                                btn.IconPath = null;
                                
                                // [수정] 드래그한 메뉴의 한글 이름으로 설정 (없으면 기존 로직 폴백)
                                if (!string.IsNullOrEmpty(nameStr))
                                {
                                    btn.Name = nameStr;
                                }
                                else
                                {
                                    // 기존 폴백 로직 (혹시 모를 구버전 데이터 호환)
                                    if (funcId >= 0 && funcId <= 4) btn.Name = $"Fn {funcId}";
                                    else if (funcId == 99) btn.Name = "Mic Mute";
                                    else if (funcId == 202) btn.Name = "Audio Cycle";
                                    else if (funcId == 300) btn.Name = "OSD Mode";
                                    else if (funcId >= 101 && funcId <= 106) btn.Name = "Media";
                                    else if (funcId == 200) btn.Name = "Run";
                                    else if (funcId == 201) btn.Name = "Macro";
                                    else if (funcId == 110 || funcId == 111) btn.Name = "Active Vol";
                                }

                                AppSettings.Save(_settings);
                                _osd.UpdateNames(_settings.Buttons, _viewModel.CurrentLayer);
                                _viewModel.LoadKeySlotsForLayer(_viewModel.CurrentLayer); // VM 갱신
                                
                                // [수정] 드롭 시점에도 선택 슬롯 갱신 및 패널 표시 여부 결정
                                _selectedSlotIndex = index;
                                
                                // [중요] VM 갱신 후 SelectedSlot이 초기화되므로, 드롭된 슬롯을 다시 선택해줘야 함
                                var newVm = _viewModel.KeySlots.FirstOrDefault(k => k.Index == index);
                                if (newVm != null) _viewModel.SelectedSlot = newVm;
                            }
                        }
                    }
                }
                // 2. 파일 드롭 (기존 로직 재활용)
                else if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
                {
                    string[] files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
                    if (files != null && files.Length > 0)
                    {
                        HandleOsdFileDrop(index, files[0]);
                    }
                }
            }
            e.Handled = true;
        }

        private void BtnOpenSettings_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow(_settings, _viewModel.RawInput, _osd);
            settingsWindow.Owner = this;
            settingsWindow.Show();
        }

        private void BtnCopyLog_Click(object sender, RoutedEventArgs e)
        {
            CopySelectedLogs();
        }

        private void LstLog_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.C && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                CopySelectedLogs();
            }
        }

        private void CopySelectedLogs()
        {
            if (LstLog.SelectedItems.Count == 0) return;

            var sb = new StringBuilder();
            foreach (LogEntry item in LstLog.SelectedItems)
            {
                sb.AppendLine($"{item.Time}\t{item.RawKeyHex}\t{item.Data}");
            }
            try { System.Windows.Clipboard.SetText(sb.ToString()); } catch { }
        }

        private void UpdateLanguage()
        {
            string lang = _settings.Language;

            this.Title = LanguageManager.GetString(lang, "Title");

            if (ColTime != null) ColTime.Header = LanguageManager.GetString(lang, "ColTime");
            if (ColKey != null) ColKey.Header = LanguageManager.GetString(lang, "ColKey");
            if (ColData != null) ColData.Header = LanguageManager.GetString(lang, "ColData");
            if (MnuCopy != null) MnuCopy.Header = LanguageManager.GetString(lang, "MnuCopy");
            if (CboTargetLayer != null) RefreshTargetLayerList();
            if (BtnUnmap != null) BtnUnmap.Content = LanguageManager.GetString(lang, "BtnUnmap");

            if (BtnOpenSettings != null) BtnOpenSettings.Content = LanguageManager.GetString(lang, "BtnOpenSettings");

            // [추가] 신규 UI 번역
            if (LblPaletteTitle != null)
            {
                // LblPaletteTitle.Text = LanguageManager.GetString(lang, "GrpFunctions");
                LblPaletteTitle.Visibility = Visibility.Collapsed; // 기능 팔레트 텍스트 숨김
            }
            if (LblArgs != null) LblArgs.Text = LanguageManager.GetString(lang, "LblArgs");
            if (LblIcon != null) LblIcon.Text = LanguageManager.GetString(lang, "LblIcon");
            if (BtnBrowse != null) BtnBrowse.Content = LanguageManager.GetString(lang, "BtnBrowse");
            if (BtnChangeIcon != null) BtnChangeIcon.Content = LanguageManager.GetString(lang, "BtnChange");
            if (BtnSavePanel != null) BtnSavePanel.Content = LanguageManager.GetString(lang, "BtnSavePanel");
            if (BtnCancelPanel != null) BtnCancelPanel.Content = LanguageManager.GetString(lang, "BtnCancelPanel");
            if (ExpSystem != null) ExpSystem.Header = LanguageManager.GetString(lang, "HeaderSystem");
            if (ExpAction != null) ExpAction.Header = LanguageManager.GetString(lang, "HeaderAction");
            if (GrpDetailSettings != null) GrpDetailSettings.Header = LanguageManager.GetString(lang, "BtnOpenSettings"); // "설정" 재사용
            if (LblStatusSaved != null) LblStatusSaved.Text = LanguageManager.GetString(lang, "MsgSaved");
            
            if (_viewModel != null) _viewModel.RefreshLocalization();
            UpdateTrayMenu(); // [추가] 트레이 메뉴 언어 업데이트
        }

        private void RefreshTargetLayerList()
        {
            if (CboTargetLayer == null) return;
            string lang = _settings.Language;
            CboTargetLayer.Items.Clear();

            try
            {
                // 1. 기능 없음
                CboTargetLayer.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = LanguageManager.GetString(lang, "TargetNone"), Tag = -1 });
                CboTargetLayer.Items.Add(new System.Windows.Controls.Separator());

                // 미디어 제어
                CboTargetLayer.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = LanguageManager.GetString(lang, "HeaderMedia"), IsEnabled = false, FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.Gray });
                CboTargetLayer.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = LanguageManager.GetString(lang, "ActionMediaPlayPause"), Tag = InputExecutor.ACTION_MEDIA_PLAYPAUSE });
                CboTargetLayer.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = LanguageManager.GetString(lang, "ActionMediaNext"), Tag = InputExecutor.ACTION_MEDIA_NEXT });
                CboTargetLayer.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = LanguageManager.GetString(lang, "ActionMediaPrev"), Tag = InputExecutor.ACTION_MEDIA_PREV });
                CboTargetLayer.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = LanguageManager.GetString(lang, "ActionVolUp"), Tag = InputExecutor.ACTION_VOL_UP });
                CboTargetLayer.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = LanguageManager.GetString(lang, "ActionVolDown"), Tag = InputExecutor.ACTION_VOL_DOWN });
                CboTargetLayer.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = LanguageManager.GetString(lang, "ActionVolMute"), Tag = InputExecutor.ACTION_VOL_MUTE });
                CboTargetLayer.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = LanguageManager.GetString(lang, "ActionActiveVolUp"), Tag = InputExecutor.ACTION_ACTIVE_VOL_UP });
                CboTargetLayer.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = LanguageManager.GetString(lang, "ActionActiveVolDown"), Tag = InputExecutor.ACTION_ACTIVE_VOL_DOWN });
                CboTargetLayer.Items.Add(new System.Windows.Controls.Separator());

                // 레이어 이동
                CboTargetLayer.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = LanguageManager.GetString(lang, "HeaderLayerMove"), IsEnabled = false, FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.Gray });
                for (int i = 0; i < 5; i++) CboTargetLayer.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = $"Fn {i}", Tag = i });
                CboTargetLayer.Items.Add(new System.Windows.Controls.Separator());

                // 마이크 & 프로그램
                CboTargetLayer.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = "Mic Mute (Toggle)", Tag = InputExecutor.LAYER_MIC_MUTE });
                CboTargetLayer.Items.Add(new System.Windows.Controls.Separator());
                CboTargetLayer.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = LanguageManager.GetString(lang, "ActionRun"), Tag = InputExecutor.ACTION_RUN_PROGRAM });
                
                // [추가] 매크로 (신규 입력)
                CboTargetLayer.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = LanguageManager.GetString(lang, "ActionTextMacro"), Tag = InputExecutor.ACTION_TEXT_MACRO });
                
                // [추가] 기존 상용구 목록 (중복 제거)
                var existingMacros = _settings.Buttons
                    .Where(b => b.TargetLayer == InputExecutor.ACTION_TEXT_MACRO && !string.IsNullOrEmpty(b.ProgramPath))
                    .Select(b => b.ProgramPath)
                    .Distinct()
                    .OrderBy(s => s)
                    .ToList();

                if (existingMacros.Count > 0)
                {
                    CboTargetLayer.Items.Add(new System.Windows.Controls.Separator());
                    foreach (var macro in existingMacros)
                    {
                        // Uid에 매크로 텍스트 저장
                        var cbi = new System.Windows.Controls.ComboBoxItem 
                        { 
                            Content = $"\"{macro}\"", 
                            Tag = InputExecutor.ACTION_TEXT_MACRO, 
                            Uid = macro, 
                            ToolTip = macro 
                        };

                        // [추가] 우클릭 삭제 메뉴
                        var contextMenu = new System.Windows.Controls.ContextMenu();
                        var deleteItem = new System.Windows.Controls.MenuItem { Header = LanguageManager.GetString(lang, "CtxDeleteMacro"), Tag = macro };
                        deleteItem.Click += DeleteMacro_Click;
                        contextMenu.Items.Add(deleteItem);
                        cbi.ContextMenu = contextMenu;

                        CboTargetLayer.Items.Add(cbi);
                    }
                }

                CboTargetLayer.Items.Add(new System.Windows.Controls.Separator());

                // 오디오 사이클
                CboTargetLayer.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = LanguageManager.GetString(lang, "ActionAudioCycle"), Tag = InputExecutor.ACTION_AUDIO_CYCLE });
                
                // [추가] OSD 모드 변경
                CboTargetLayer.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = LanguageManager.GetString(lang, "ActionOsdCycle"), Tag = ACTION_OSD_CYCLE });
            }
            catch (Exception ex)
            {
                Log($"[Error] Failed to refresh target layer list: {ex.Message}");
            }
        }

        // [추가] 상용구 삭제 처리
        private void DeleteMacro_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.MenuItem item && item.Tag is string macro)
            {
                string msg = LanguageManager.GetString(_settings.Language, "MsgDeleteMacroConfirm").Replace("{0}", macro);
                string title = LanguageManager.GetString(_settings.Language, "TitleDeleteMacro");

                if (System.Windows.MessageBox.Show(msg, title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    bool changed = false;
                    // 해당 매크로를 사용하는 모든 버튼 찾아서 초기화
                    foreach (var btn in _settings.Buttons)
                    {
                        if (btn.TargetLayer == InputExecutor.ACTION_TEXT_MACRO && btn.ProgramPath == macro)
                        {
                            btn.TargetLayer = -1;
                            btn.ProgramPath = null;
                            btn.IconPath = null; // [추가] 초기화
                            btn.Name = $"Key {btn.Index}"; // 이름도 기본값으로 복구
                            changed = true;
                        }
                    }

                    if (changed)
                    {
                        AppSettings.Save(_settings);
                        
                        // UI 및 OSD 갱신
                        _osd.UpdateNames(_settings.Buttons, _viewModel.CurrentLayer);
                        _viewModel.LoadKeySlotsForLayer(_viewModel.CurrentLayer); // VM 갱신
                        
                        // 목록 갱신 (삭제된 항목 제거됨)
                        RefreshTargetLayerList();
                        Log($"[Macro] Deleted preset: {macro}");
                    }
                }
            }
        }

        // [추가] 간단한 텍스트 입력 다이얼로그
        private string ShowInputDialog(string title, string prompt, string defaultText)
        {
            Window inputWindow = new Window
            {
                Title = title, Width = 400, Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this, ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.ToolWindow
            };

            var stack = new System.Windows.Controls.StackPanel { Margin = new Thickness(20) };
            stack.Children.Add(new System.Windows.Controls.TextBlock { Text = prompt, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) });
            
            var txtInput = new System.Windows.Controls.TextBox { Text = defaultText ?? "", Height = 30, VerticalContentAlignment = VerticalAlignment.Center };
            stack.Children.Add(txtInput);

            var btnPanel = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
            var btnOk = new System.Windows.Controls.Button { Content = "OK", Width = 80, Height = 30, IsDefault = true, Margin = new Thickness(0, 0, 10, 0) };
            var btnCancel = new System.Windows.Controls.Button { Content = "Cancel", Width = 80, Height = 30, IsCancel = true };
            
            string result = null;
            btnOk.Click += (s, e) => { result = txtInput.Text; inputWindow.Close(); };
            btnCancel.Click += (s, e) => { inputWindow.Close(); };

            btnPanel.Children.Add(btnOk);
            btnPanel.Children.Add(btnCancel);
            stack.Children.Add(btnPanel);
            inputWindow.Content = stack;

            inputWindow.ShowDialog();
            return result;
        }

        // [추가] 트레이 메뉴 업데이트 (다국어 지원)
        private void UpdateTrayMenu()
        {
            if (_notifyIcon == null) return;

            string lang = _settings.Language;
            _trayMenu = new System.Windows.Forms.ContextMenuStrip();

            // 1. OSD 표시 모드
            var modeItem = new System.Windows.Forms.ToolStripMenuItem(LanguageManager.GetString(lang, "CtxOsdMode"));
            modeItem.DropDownItems.Add(LanguageManager.GetString(lang, "ModeAuto"), null, (s, e) => { _settings.OsdMode = 0; _osd.UpdateSettings(_settings); AppSettings.Save(_settings); });
            modeItem.DropDownItems.Add(LanguageManager.GetString(lang, "ModeOn"), null, (s, e) => { _settings.OsdMode = 1; _osd.UpdateSettings(_settings); AppSettings.Save(_settings); });
            modeItem.DropDownItems.Add(LanguageManager.GetString(lang, "ModeOff"), null, (s, e) => { _settings.OsdMode = 2; _osd.UpdateSettings(_settings); AppSettings.Save(_settings); });
            _trayMenu.Items.Add(modeItem);

            // 2. OSD 이동
            var moveItem = new System.Windows.Forms.ToolStripMenuItem(LanguageManager.GetString(lang, "CtxMoveOsd"));
            moveItem.Click += (s, e) => {
                bool isMoving = _osd.ResizeMode == ResizeMode.CanResize;
                _osd.SetMoveMode(!isMoving);
                moveItem.Checked = !isMoving;
            };
            _trayMenu.Items.Add(moveItem);

            _trayMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

            // 3. 종료
            _trayMenu.Items.Add(LanguageManager.GetString(lang, "CtxExit"), null, (s, e) => Dispatcher.Invoke(() => this.Close()));

            // 메뉴 열릴 때 체크 상태 동기화
            _trayMenu.Opening += (s, e) => {
                moveItem.Checked = _osd.ResizeMode == ResizeMode.CanResize;
            };

            _notifyIcon.ContextMenuStrip = _trayMenu;
        }
    }
}
