using System;
using System.Diagnostics; // Process.Start
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Runtime.InteropServices;
using Microsoft.Win32; // 레지스트리 사용

namespace SayoOSD
{
    public class LogEntry
    {
        public string Time { get; set; }
        public string RawKeyHex { get; set; }
        public byte RawKeyByte { get; set; }
        public byte[] RawBytes { get; set; } // 원본 데이터 저장
        public string Data { get; set; }
        public System.Windows.Media.Brush Foreground { get; set; } = System.Windows.Media.Brushes.Black;
    }

    public partial class MainWindow : Window
    {
        private RawInputReceiver _rawInput;
        private OsdWindow _osd;
        private AppSettings _settings;
        private System.Windows.Forms.NotifyIcon _notifyIcon; // 트레이 아이콘
        private int _currentLayer = 0; // 현재 활성화된 레이어
        private bool _isListening = false; // 입력 감지 모드 여부
        private bool _isAutoDetecting = false; // 자동 감지 모드
        private int _candidateCount = 0; // 감지된 신호 개수
        private int _selectedSlotIndex = 1; // 현재 선택된 슬롯 (1~12)
        private bool _isUpdatingUi = false; // UI 업데이트 중 플래그

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        extern static bool DestroyIcon(IntPtr handle);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern uint RegisterWindowMessage(string lpString);
        private uint _wmShowMessage;
        
        private AudioController _audioController; // 오디오 제어 분리
        private InputExecutor _inputExecutor; // [추가] 실행 로직 분리

        public MainWindow()
        {
            // [핵심] App 클래스의 정적 생성자를 강제로 실행시켜 중복 검사를 가장 먼저 수행
            // MainWindow가 StartupObject일 경우, InitializeComponent보다 먼저 실행되어야 창이 뜨지 않고 종료됨
            var _ = App.StartupLogs; 

            InitializeComponent();
            _osd = new OsdWindow();
            _osd.DebugLog += (msg) => LogManager.Write(msg); // [수정] OSD 로그를 LogManager로 전달
            
            // 설정 로드
            _settings = AppSettings.Load();
            _currentLayer = _settings.LastLayerIndex; // 저장된 마지막 레이어 불러오기

            // 로그 파일 저장 설정 적용 및 이벤트 연결
            ChkEnableFileLog.IsChecked = _settings.EnableFileLog;
            LogManager.Enabled = _settings.EnableFileLog; // [추가] 초기 로그 설정 적용
            ChkEnableFileLog.Checked += ChkEnableFileLog_CheckedChanged;
            ChkEnableFileLog.Unchecked += ChkEnableFileLog_CheckedChanged;

            // 언어 데이터 로드
            LanguageManager.Load();
            
            // 언어 콤보박스 구성 (완성도 % 표시)
            CboLanguage.Items.Clear();
            foreach (var lang in LanguageManager.GetLanguages())
            {
                int percent = lang.GetCompletionPercentage(LanguageManager.Keys.Count);
                var item = new System.Windows.Controls.ComboBoxItem();
                item.Content = $"{lang.Name} ({percent}%)";
                item.Tag = lang.Code;
                CboLanguage.Items.Add(item);
                
                // 현재 설정된 언어 선택
                if (lang.Code == _settings.Language) CboLanguage.SelectedItem = item;
            }
            if (CboLanguage.SelectedIndex < 0 && CboLanguage.Items.Count > 0) CboLanguage.SelectedIndex = 0;
            
            UpdateLanguage(); // 초기 언어 적용
            
            // OSD 설정 적용
            _osd.UpdateSettings(_settings);

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

            // 트레이 아이콘 우클릭 메뉴 구성
            var contextMenu = new System.Windows.Forms.ContextMenuStrip();
            
            // 1. OSD 표시 모드
            var modeItem = new System.Windows.Forms.ToolStripMenuItem("OSD 표시 모드");
            modeItem.DropDownItems.Add("자동", null, (s, e) => { _settings.OsdMode = 0; _osd.UpdateSettings(_settings); AppSettings.Save(_settings); });
            modeItem.DropDownItems.Add("항상 켜기", null, (s, e) => { _settings.OsdMode = 1; _osd.UpdateSettings(_settings); AppSettings.Save(_settings); });
            modeItem.DropDownItems.Add("항상 끄기", null, (s, e) => { _settings.OsdMode = 2; _osd.UpdateSettings(_settings); AppSettings.Save(_settings); });
            contextMenu.Items.Add(modeItem);

            // 2. OSD 이동
            var moveItem = new System.Windows.Forms.ToolStripMenuItem("OSD 이동");
            moveItem.Click += (s, e) => {
                bool isMoving = _osd.ResizeMode == ResizeMode.CanResize;
                _osd.SetMoveMode(!isMoving);
                moveItem.Checked = !isMoving;
            };
            contextMenu.Items.Add(moveItem);

            contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

            // 3. 종료
            contextMenu.Items.Add("종료", null, (s, e) => Dispatcher.Invoke(() => this.Close()));

            // 메뉴 열릴 때 체크 상태 동기화
            contextMenu.Opening += (s, e) => {
                moveItem.Checked = _osd.ResizeMode == ResizeMode.CanResize;
            };

            _notifyIcon.ContextMenuStrip = contextMenu;

            // UI 콤보박스도 저장된 레이어로 동기화
            UpdateLayerRadioButton(_currentLayer);

            // 배지 및 선택 상태 초기화
            RefreshBadges();
            UpdateBadgeSelection();

            // 윈도우 시작 시 자동 실행 여부 확인 (레지스트리)
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
                {
                    if (key.GetValue("SayoOSD") != null)
                        ChkStartWithWindows.IsChecked = true;
                }
            }
            catch { /* 권한 문제 등으로 읽기 실패 시 무시 */ }

            this.Loaded += MainWindow_Loaded;
            this.Closing += (s, e) => 
            { 
                // 오디오 컨트롤러 정리
                if (_audioController != null)
                {
                    _audioController.Dispose();
                    _audioController = null;
                }
                AppSettings.Save(_settings); // 종료 시 설정(위치 포함) 저장
                _osd.Close(); 
                _notifyIcon.Dispose(); 
            };
            this.StateChanged += MainWindow_StateChanged;
        }

        private void RefreshBadges()
        {
            int layer = _currentLayer;
            var buttons = _settings.Buttons.Where(b => b.Layer == layer).ToList();

            // GridBadges 내의 모든 버튼을 순회하며 텍스트 업데이트
            foreach (var child in GridBadges.Children)
            {
                if (child is System.Windows.Controls.TextBox txtControl && txtControl.Tag != null)
                {
                    if (int.TryParse(txtControl.Tag.ToString(), out int index))
                    {
                        var config = buttons.FirstOrDefault(b => b.Index == index);
                        if (config != null)
                        {
                            txtControl.Text = config.Name;
                        }
                    }
                }
            }
            
            // 현재 선택된 슬롯의 정보로 하단 입력창 업데이트
            UpdateEditControls();

            // [추가] 배지 색상 업데이트 (레이어 이동 표시 등)
            UpdateBadgeSelection();
        }

        private void UpdateBadgeSelection()
        {
            // [추가] 현재 레이어 버튼 정보 가져오기
            var layerButtons = _settings.Buttons.Where(b => b.Layer == _currentLayer).ToList();

            foreach (var child in GridBadges.Children)
            {
                if (child is System.Windows.Controls.TextBox txtControl && txtControl.Tag != null)
                {
                    if (int.TryParse(txtControl.Tag.ToString(), out int index))
                    {
                        // [추가] 레이어 이동 설정 여부 확인
                        var btn = layerButtons.FirstOrDefault(b => b.Index == index);
                        bool isLayerMove = btn != null && ((btn.TargetLayer >= 0 && btn.TargetLayer <= 4) || btn.TargetLayer == InputExecutor.LAYER_MIC_MUTE);

                        // 선택된 버튼은 강조색, 나머지는 기본색
                        if (index == _selectedSlotIndex)
                        {
                            txtControl.Background = System.Windows.Media.Brushes.White; // 입력 가능하도록 흰색 배경
                            txtControl.Foreground = System.Windows.Media.Brushes.Black;
                            txtControl.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0x7A, 0xCC)); // 파란색 테두리
                            txtControl.BorderThickness = new Thickness(2);
                        }
                        else
                        {
                            // [수정] 레이어 이동 키는 연한 파란색으로 표시, 일반 키는 회색
                            if (isLayerMove) txtControl.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xD0, 0xF0, 0xFF));
                            else txtControl.Background = System.Windows.Media.Brushes.LightGray;

                            txtControl.Foreground = System.Windows.Media.Brushes.Black;
                            txtControl.BorderBrush = System.Windows.Media.Brushes.Gray;
                            txtControl.BorderThickness = new Thickness(1);
                        }
                    }
                }
            }
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

                // 윈도우 핸들을 얻은 후 Raw Input 등록
                IntPtr hwnd = new WindowInteropHelper(this).Handle;
                _rawInput = new RawInputReceiver(hwnd, _settings.DeviceVid, _settings.DevicePid);
                _rawInput.HidDataReceived += OnHidDataReceived;
                _rawInput.DebugLog += (msg) => Dispatcher.Invoke(() => Log(msg)); // 디버그 로그 연결

                // 로그 연결 후 장치 검색 시작
                _rawInput.Initialize();

                // [수정] AudioController 초기화 및 이벤트 연결
                _audioController = new AudioController();
                _audioController.LogMessage += (msg) => Dispatcher.Invoke(() => Log(msg));
                _audioController.MicMuteChanged += (muted) => Dispatcher.Invoke(() => _osd.SetMicState(muted));
                _audioController.SpeakerMuteChanged += (muted) => Dispatcher.Invoke(() => _osd.SetSpeakerState(muted));
                
                // [추가] 시스템 오디오 장치 변경 시 OSD 이름 자동 업데이트
                _audioController.AudioDeviceChanged += (deviceName) => Dispatcher.Invoke(() => 
                {
                    var buttons = _settings.Buttons.Where(b => b.TargetLayer == InputExecutor.ACTION_AUDIO_CYCLE).ToList();
                    if (buttons.Count > 0)
                    {
                        foreach (var btn in buttons) btn.Name = deviceName;
                        AppSettings.Save(_settings);
                        _osd.UpdateNames(_settings.Buttons, _currentLayer);
                        RefreshBadges();
                        Log($"[Audio] OSD updated to: {deviceName}");
                    }
                });
                
                _audioController.Initialize();

                // [추가] InputExecutor 초기화 및 로그 연결
                _inputExecutor = new InputExecutor();
                _inputExecutor.LogMessage += (msg) => Dispatcher.Invoke(() => Log(msg));

                // 시작 시 OSD가 잘 뜨는지 테스트
                _osd.ShowBriefly();
                Log("프로그램 시작됨. OSD 테스트 표시.");

                // 메시지 루프 훅 추가
                HwndSource source = HwndSource.FromHwnd(hwnd);
                source.AddHook(WndProc);
            }));

            // 자동 실행(--tray)으로 시작된 경우 트레이로 숨김
            string[] args = Environment.GetCommandLineArgs();
            if (args.Any(a => a.Equals("--tray", StringComparison.OrdinalIgnoreCase)))
            {
                this.WindowState = WindowState.Minimized;
                this.Hide();
            }
        }

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            if (this.WindowState == WindowState.Minimized)
                this.Hide(); // 최소화 시 작업표시줄에서 숨김 (트레이로 이동)
        }

        private void RbLayer_Checked(object sender, RoutedEventArgs e)
        {
            if (_settings == null) return;
            if (sender is System.Windows.Controls.RadioButton rb && rb.IsChecked == true && rb.Tag != null)
            {
                if (int.TryParse(rb.Tag.ToString(), out int layer))
                {
                    _currentLayer = layer;
                    _settings.LastLayerIndex = layer;
                    AppSettings.Save(_settings); // 레이어 변경 시 즉시 저장

                    if (_osd != null) _osd.UpdateNames(_settings.Buttons, layer);
                    RefreshBadges();
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
            if (sender is System.Windows.Controls.TextBox txt && txt.Tag != null)
            {
                if (int.TryParse(txt.Tag.ToString(), out int index))
                {
                    _selectedSlotIndex = index;
                    UpdateBadgeSelection();
                    UpdateEditControls();
                }
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
            if (sender is System.Windows.Controls.TextBox txt && txt.Tag != null)
            {
                if (int.TryParse(txt.Tag.ToString(), out int index))
                {
                    int layer = _currentLayer;
                    var btn = _settings.Buttons.FirstOrDefault(b => b.Layer == layer && b.Index == index);
                    if (btn != null && btn.Name != txt.Text)
                    {
                        btn.Name = txt.Text;
                        AppSettings.Save(_settings);
                        _osd.UpdateNames(_settings.Buttons, layer);
                    }
                }
            }
        }

        private void UpdateEditControls()
        {
            _isUpdatingUi = true;
            try
            {
                int layer = _currentLayer;
                var btn = _settings.Buttons.FirstOrDefault(b => b.Layer == layer && b.Index == _selectedSlotIndex);

                if (btn != null)
                {
                    // 타겟 레이어 콤보박스 설정
                    CboTargetLayer.SelectedIndex = 0; // 기본값: 이동 없음
                    
                    // 프로그램 실행 설정이 있는지 확인
                    if (!string.IsNullOrEmpty(btn.ProgramPath) && btn.TargetLayer != InputExecutor.ACTION_TEXT_MACRO)
                    {
                        foreach (var item in CboTargetLayer.Items)
                        {
                            if (item is System.Windows.Controls.ComboBoxItem cbi && 
                                cbi.Tag != null && 
                                int.TryParse(cbi.Tag.ToString(), out int tagVal) && 
                                tagVal == InputExecutor.ACTION_RUN_PROGRAM)
                            {
                                CboTargetLayer.SelectedItem = cbi;
                                cbi.ToolTip = btn.ProgramPath; // 툴팁으로 경로 표시
                                break;
                            }
                        }
                    }
                    // [추가] 텍스트 매크로 선택 시 (프리셋 확인 및 선택)
                    else if (btn.TargetLayer == InputExecutor.ACTION_TEXT_MACRO)
                    {
                        bool found = false;
                        // 1. 프리셋(Uid 일치) 찾기
                        foreach (var item in CboTargetLayer.Items)
                        {
                            if (item is System.Windows.Controls.ComboBoxItem cbi && cbi.Uid == btn.ProgramPath)
                            {
                                CboTargetLayer.SelectedItem = cbi;
                                found = true;
                                break;
                            }
                        }
                        // 2. 없으면 기본 항목(새로 입력) 선택
                        if (!found)
                        {
                            foreach (var item in CboTargetLayer.Items)
                            {
                                if (item is System.Windows.Controls.ComboBoxItem cbi && 
                                    int.TryParse(cbi.Tag?.ToString(), out int t) && t == InputExecutor.ACTION_TEXT_MACRO && string.IsNullOrEmpty(cbi.Uid))
                                {
                                    CboTargetLayer.SelectedItem = cbi;
                                    cbi.ToolTip = btn.ProgramPath;
                                    break;
                                }
                            }
                        }
                    }
                    else
                    {
                        // 레이어 이동 또는 마이크 설정 확인
                        foreach (var item in CboTargetLayer.Items)
                        {
                            if (item is System.Windows.Controls.ComboBoxItem cbi && 
                                cbi.Tag != null && 
                                int.TryParse(cbi.Tag.ToString(), out int tagVal) && 
                                tagVal == btn.TargetLayer)
                            {
                                CboTargetLayer.SelectedItem = cbi;
                                break;
                            }
                        }
                    }
                }
            }
            finally
            {
                _isUpdatingUi = false;
            }
        }

        private void CboTargetLayer_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_isUpdatingUi || _settings == null) return;

            int layer = _currentLayer;
            var btn = _settings.Buttons.FirstOrDefault(b => b.Layer == layer && b.Index == _selectedSlotIndex);

            if (btn != null && CboTargetLayer.SelectedItem is System.Windows.Controls.ComboBoxItem item)
            {
                if (int.TryParse(item.Tag.ToString(), out int targetLayer))
                {
                    // 프로그램 연결 선택 시
                    if (targetLayer == InputExecutor.ACTION_RUN_PROGRAM)
                    {
                        // 파일 선택 대화상자 표시
                        var dlg = new Microsoft.Win32.OpenFileDialog();
                        dlg.Filter = "Executables (*.exe)|*.exe|All files (*.*)|*.*";
                        
                        // 이미 경로가 있다면 해당 경로에서 시작 (선택 사항)
                        if (!string.IsNullOrEmpty(btn.ProgramPath))
                        {
                            try { dlg.InitialDirectory = System.IO.Path.GetDirectoryName(btn.ProgramPath); } catch { }
                        }

                        if (dlg.ShowDialog() == true)
                        {
                            btn.ProgramPath = dlg.FileName;
                            btn.TargetLayer = -1; // 프로그램 실행 시 레이어 이동 해제
                            item.ToolTip = btn.ProgramPath; // 툴팁 업데이트
                            AppSettings.Save(_settings);
                            Log($"[Setting] Key {btn.Index} Action -> Run: {btn.ProgramPath}");
                        }
                        else
                        {
                            // 취소 시 이전 상태로 복구 (UI 리셋)
                            UpdateEditControls();
                            return;
                        }
                    }
                    // [추가] 텍스트 매크로 선택 시
                    else if (targetLayer == InputExecutor.ACTION_TEXT_MACRO)
                    {
                        // 프리셋 선택인 경우 (Uid에 텍스트가 있음)
                        if (!string.IsNullOrEmpty(item.Uid))
                        {
                            btn.ProgramPath = item.Uid;
                            btn.TargetLayer = targetLayer;
                            btn.Name = item.Uid; // 이름도 변경
                            
                            _osd.UpdateNames(_settings.Buttons, _currentLayer);
                            RefreshBadges();
                            AppSettings.Save(_settings);
                            Log($"[Setting] Key {btn.Index} Macro -> {item.Uid} (Preset)");
                            return;
                        }

                        // 신규 입력 (기본 항목)
                        // 간단한 입력 다이얼로그 표시
                        string currentText = (btn.TargetLayer == InputExecutor.ACTION_TEXT_MACRO) ? btn.ProgramPath : "";
                        string input = ShowInputDialog(
                            LanguageManager.GetString(_settings.Language, "TitleInputText"),
                            LanguageManager.GetString(_settings.Language, "MsgEnterText"),
                            currentText);

                        if (input != null) // 취소가 아닐 때
                        {
                            btn.ProgramPath = input; // 텍스트 내용을 ProgramPath에 저장
                            btn.TargetLayer = targetLayer;
                            
                            // [추가] 입력한 상용구 내용을 키 이름으로 자동 설정 (목록에서 확인 가능)
                            btn.Name = input;
                            _osd.UpdateNames(_settings.Buttons, _currentLayer);
                            RefreshBadges();
                            
                            item.ToolTip = input;
                            AppSettings.Save(_settings);
                            Log($"[Setting] Key {btn.Index} Macro -> {input}");
                            
                            // 목록 갱신 (새로운 상용구 추가) 및 재선택
                            RefreshTargetLayerList();
                            UpdateEditControls();
                        }
                        else
                        {
                            UpdateEditControls(); // 취소 시 복구
                            return;
                        }
                    }
                    else
                    {
                        // 일반 레이어 이동 또는 마이크
                        if (btn.TargetLayer != targetLayer || btn.ProgramPath != null)
                        {
                            btn.TargetLayer = targetLayer;
                            btn.ProgramPath = null; // 프로그램 실행 해제
                            AppSettings.Save(_settings);
                            Log($"[Setting] Key {btn.Index} Action -> {item.Content}");
                        }
                    }
                }
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
            if (_rawInput != null)
                _rawInput.ProcessMessage(msg, lParam);
            
            return IntPtr.Zero;
        }

        // 데이터에서 고유한 패턴(Signature)을 추출하는 메서드
        private string GetSignature(byte[] data)
        {
            if (data == null || data.Length == 0) return "";

            // 긴 패킷 (일반/매크로): 인덱스 8부터 12바이트(Type, Key, Val, Padding 등)를 패턴으로 사용
            if (data.Length > 10)
            {
                int len = Math.Min(data.Length - 8, 12); 
                return BitConverter.ToString(data, 8, len).Replace("-", " ");
            }
            // 짧은 패킷 (멀티미디어): 전체 데이터를 사용하여 고유성 확보
            // 인덱스 2부터 자르면 구분이 안 되거나 길이가 부족할 수 있음
            return BitConverter.ToString(data).Replace("-", " ");
        }

        private void OnHidDataReceived(byte[] data)
        {
            string hex = BitConverter.ToString(data).Replace("-", " ");
            string signature = GetSignature(data);

            // 입력 감지 모드일 때 처리
            if (_isAutoDetecting)
            {
                HandleAutoDetect(data, hex);
                return;
            }

            // 1. 버튼 찾기
            var btn = FindMappedButton(signature);
            
            if (btn == null)
            {
                // 매핑되지 않은 신호 로그
                LogUnknownSignal(data, hex);
                return;
            }

            // 2. 기능 실행 및 레이어 변경 계산
            int newLayer = ExecuteButtonLogic(btn, out bool? micState);

            // 3. 레이어 변경 적용
            if (_currentLayer != newLayer)
            {
                ChangeLayer(newLayer);
            }

            // 4. OSD 표시 및 로그
            HandleOsdAndLog(data, hex, btn, micState);
        }

        // [리팩토링] 자동 감지 처리
        private void HandleAutoDetect(byte[] data, string hex)
        {
            if (_candidateCount >= 10) return;

            Dispatcher.Invoke(() => {
                AddLog(data, hex, "Candidate Signal (Double-click to map)", true);
                _candidateCount++;
                if (_candidateCount >= 10)
                {
                    BtnAutoDetect.Content = "감지 완료 (선택하세요)";
                }
            });
        }

        // [리팩토링] 매핑된 버튼 찾기
        private ButtonConfig FindMappedButton(string signature)
        {
            // 1. 현재 레이어에서 먼저 검색
            var btn = _settings.Buttons.FirstOrDefault(b => b.TriggerPattern == signature && b.Layer == _currentLayer);
            // 2. 없으면 전체 레이어에서 검색
            if (btn == null)
                btn = _settings.Buttons.FirstOrDefault(b => b.TriggerPattern == signature);
            return btn;
        }

        // [리팩토링] 버튼 기능 실행 로직
        private int ExecuteButtonLogic(ButtonConfig btn, out bool? micState)
        {
            int newLayer = _currentLayer;
            micState = null;

            // 1순위: 타겟 레이어/기능 설정 확인
            if (btn.TargetLayer >= 0 && btn.TargetLayer <= 4)
            {
                newLayer = btn.TargetLayer;
            }
            else if (btn.TargetLayer == InputExecutor.LAYER_MIC_MUTE)
            {
                micState = _audioController.ToggleMicMute();
            }
            else if (btn.TargetLayer >= 101 && btn.TargetLayer <= 106)
            {
                _inputExecutor.ExecuteMediaKey(btn.TargetLayer);
            }
            else if (btn.TargetLayer == InputExecutor.ACTION_TEXT_MACRO && !string.IsNullOrEmpty(btn.ProgramPath))
            {
                _inputExecutor.ExecuteMacro(btn.ProgramPath);
            }
            else if (btn.TargetLayer == InputExecutor.ACTION_AUDIO_CYCLE)
            {
                string newDeviceName = _audioController.CycleAudioDevice();
                if (!string.IsNullOrEmpty(newDeviceName))
                {
                    btn.Name = newDeviceName;
                    AppSettings.Save(_settings);
                    Dispatcher.Invoke(() => {
                        _osd.UpdateNames(_settings.Buttons, _currentLayer);
                        RefreshBadges();
                    });
                }
            }
            // 2순위: 다른 레이어의 키라면 해당 레이어로 이동 (하드웨어 동기화)
            else if (_currentLayer != btn.Layer)
            {
                newLayer = btn.Layer;
            }

            // 프로그램 실행 (매크로가 아닐 때)
            if (!string.IsNullOrEmpty(btn.ProgramPath) && btn.TargetLayer != InputExecutor.ACTION_TEXT_MACRO)
            {
                _inputExecutor.ExecuteProgram(btn.ProgramPath);
            }

            return newLayer;
        }

        // [리팩토링] 레이어 변경 적용
        private void ChangeLayer(int newLayer)
        {
            _currentLayer = newLayer;
            _settings.LastLayerIndex = _currentLayer;
            AppSettings.Save(_settings);
            Dispatcher.Invoke(() => {
                _osd.UpdateNames(_settings.Buttons, _currentLayer);
                UpdateLayerRadioButton(_currentLayer);
            });
        }

        // [리팩토링] OSD 표시 및 로그 기록
        private void HandleOsdAndLog(byte[] data, string hex, ButtonConfig btn, bool? micState)
        {
            // 1~12번 키인 경우 OSD 표시 (로그 일시정지와 무관하게 작동)
            if (btn.Index >= 1 && btn.Index <= 12)
            {
                LogManager.Write($"[Main] Key {btn.Index} detected. Triggering OSD.");
                LogManager.Write($"[Main] [L{_currentLayer}] Key {btn.Index} detected. Triggering OSD.");
                
                Dispatcher.Invoke(() => _osd.HighlightKey(btn.Index, micState));
            }

            // 로그 기록
            // 노이즈 필터링: C6로 시작하는 신호는 로그에 남기지 않음
            if (hex.StartsWith("C6")) return;

            // 힌트 메시지: 0x81로 시작하는 긴 패킷은 보통 Key Up(뗌) 신호임
            string hint = "";
            if (data.Length > 10 && data[8] == 0x81) hint = " (Key Up?)";

            Dispatcher.Invoke(() => {
                string msg = $"[L{_currentLayer}] [Key {btn.Index}] Matched{hint}";
                AddLog(data, hex, msg);
            });
        }

        private void LogUnknownSignal(byte[] data, string hex)
        {
            if (hex.StartsWith("C6")) return;
            string hint = (data.Length > 10 && data[8] == 0x81) ? " (Key Up?)" : "";
            Dispatcher.Invoke(() => AddLog(data, hex, $"[L{_currentLayer}] Unknown Signal{hint}"));
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
            LstLog.Items.Add(entry);
            if (LstLog.Items.Count > 1000) LstLog.Items.RemoveAt(0);

            // 스크롤만 최소화 상태가 아닐 때 수행 (UI 부하 방지)
            if (this.WindowState != WindowState.Minimized)
            {
                LstLog.ScrollIntoView(entry);
            }

            // 파일에도 기록
            string logMsg = msg;
            if (rawKey != 0) logMsg = $"[Key: {rawKey:X2}] {msg}";
            LogManager.Write(logMsg); // [수정] LogManager 사용
        }

        private void Log(string msg)
        {
            // 일반 텍스트 로그도 리스트에 추가
            AddLog(null, "", msg);
        }

        private void BtnAutoDetect_Click(object sender, RoutedEventArgs e)
        {
            if (_isAutoDetecting)
            {
                // 취소
                _isAutoDetecting = false;
                BtnAutoDetect.Content = LanguageManager.GetString(_settings.Language, "BtnAutoDetect");
                return;
            }

            // 감지 시작
            _isAutoDetecting = true;
            _candidateCount = 0;
            LstLog.Items.Clear(); // 로그 초기화
            BtnAutoDetect.Content = LanguageManager.GetString(_settings.Language, "MsgDetecting");
            AddLog(null, "", "--- Auto Detect Started (Press keys) ---");
        }

        private void LstLog_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // 로그 더블 클릭 시 매핑
            if (LstLog.SelectedItem is LogEntry entry && entry.RawBytes != null)
            {
                string signature = GetSignature(entry.RawBytes);
                if (!string.IsNullOrEmpty(signature))
                {
                    PerformAutoMapping(signature);
                    
                    // 매핑 후 자동 감지 모드 종료
                    _isAutoDetecting = false;
                    BtnAutoDetect.Content = LanguageManager.GetString(_settings.Language, "BtnAutoDetect");
                }
            }
        }

        private void BtnUnmap_Click(object sender, RoutedEventArgs e)
        {
            int layer = _currentLayer;
            int slotIndex = _selectedSlotIndex;

            var btn = _settings.Buttons.FirstOrDefault(b => b.Layer == layer && b.Index == slotIndex);
            if (btn != null)
            {
                if (System.Windows.MessageBox.Show($"Key {slotIndex} (Layer {layer})의 매핑 정보를 초기화하시겠습니까?", 
                    "매핑 해제", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    btn.TriggerPattern = null;
                    btn.TargetLayer = -1;
                    btn.Name = $"Key {slotIndex}"; // 기본 이름으로 복구
                    btn.ProgramPath = null;

                    AppSettings.Save(_settings);

                    _osd.UpdateNames(_settings.Buttons, layer);
                    RefreshBadges(); // 배지 갱신

                    System.Windows.MessageBox.Show(LanguageManager.GetString(_settings.Language, "MsgUnmapped"));
                }
            }
        }

        private void PerformAutoMapping(string signature)
        {
            int layer = _currentLayer;

            // 중복 방지: 같은 레이어 내에서만 중복 체크
            foreach (var b in _settings.Buttons)
            {
                if (b.Layer == layer && b.TriggerPattern == signature) b.TriggerPattern = null;
            }

            int slotIndex = _selectedSlotIndex;
            var btn = _settings.Buttons.FirstOrDefault(b => b.Layer == layer && b.Index == slotIndex);
            if (btn != null)
            {
                btn.TriggerPattern = signature;
                // 이름은 이미 TextBox에서 수정되어 저장되었으므로 여기서는 덮어쓰지 않거나 현재 값을 유지
                
                // 타겟 레이어 저장
                if (CboTargetLayer.SelectedItem is System.Windows.Controls.ComboBoxItem item && 
                    int.TryParse(item.Tag.ToString(), out int targetLayer))
                {
                    btn.TargetLayer = targetLayer;
                }
                else btn.TargetLayer = -1;

                AppSettings.Save(_settings);

                _osd.UpdateNames(_settings.Buttons, layer);
                _osd.HighlightKey(slotIndex);
                RefreshBadges();

                System.Windows.MessageBox.Show($"Key {slotIndex}에 패턴이 매핑되었습니다.\n패턴: {signature}");
            }
        }

        private void BtnOpenSettings_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow(_settings, _rawInput, _osd);
            settingsWindow.Owner = this;
            settingsWindow.Show();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            AppSettings.Save(_settings);
            System.Windows.MessageBox.Show("설정이 저장되었습니다. (settings.json)");
        }

        private void BtnHide_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void ChkStartWithWindows_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
                {
                    if (ChkStartWithWindows.IsChecked == true)
                    {
                        string path = Environment.ProcessPath; // 현재 실행 파일 경로 (.exe)
                        if (!string.IsNullOrEmpty(path))
                            key.SetValue("SayoOSD", $"\"{path}\" --tray");
                    }
                    else
                    {
                        key.DeleteValue("SayoOSD", false);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"레지스트리 설정 실패: {ex.Message}");
            }
        }

        private void ChkEnableFileLog_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (_settings != null)
            {
                _settings.EnableFileLog = ChkEnableFileLog.IsChecked == true;
                AppSettings.Save(_settings);
                LogManager.Enabled = _settings.EnableFileLog; // [추가] 실시간 설정 반영
            }
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

        private void CboLanguage_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_settings == null || CboLanguage == null) return;
            
            if (CboLanguage.SelectedItem is System.Windows.Controls.ComboBoxItem item)
            {
                string lang = item.Tag.ToString();
                if (_settings.Language != lang)
                {
                    _settings.Language = lang;
                    UpdateLanguage();
                    AppSettings.Save(_settings);
                }
            }
        }

        private void UpdateLanguage()
        {
            string lang = _settings.Language;

            this.Title = LanguageManager.GetString(lang, "Title");

            if (GrpMap != null) GrpMap.Header = LanguageManager.GetString(lang, "GrpMap");
            if (ColTime != null) ColTime.Header = LanguageManager.GetString(lang, "ColTime");
            if (ColKey != null) ColKey.Header = LanguageManager.GetString(lang, "ColKey");
            if (ColData != null) ColData.Header = LanguageManager.GetString(lang, "ColData");
            if (MnuCopy != null) MnuCopy.Header = LanguageManager.GetString(lang, "MnuCopy");

            if (LblLayer != null) LblLayer.Text = LanguageManager.GetString(lang, "LblLayer");
            if (LblTarget != null) LblTarget.Text = LanguageManager.GetString(lang, "LblTarget");
            
            if (CboTargetLayer != null)
            {
                RefreshTargetLayerList();
                // 선택 상태 복구
                if (_selectedSlotIndex > 0) UpdateEditControls();
            }

            if (BtnAutoDetect != null) BtnAutoDetect.Content = LanguageManager.GetString(lang, "BtnAutoDetect");
            if (BtnUnmap != null) BtnUnmap.Content = LanguageManager.GetString(lang, "BtnUnmap");

            if (ChkEnableFileLog != null) ChkEnableFileLog.Content = LanguageManager.GetString(lang, "ChkEnableFileLog");
            if (ChkStartWithWindows != null) ChkStartWithWindows.Content = LanguageManager.GetString(lang, "ChkStartWithWindows");
            if (BtnSave != null) BtnSave.Content = LanguageManager.GetString(lang, "BtnSave");
            if (BtnHide != null) BtnHide.Content = LanguageManager.GetString(lang, "BtnHide");
            if (BtnOpenSettings != null) BtnOpenSettings.Content = LanguageManager.GetString(lang, "BtnOpenSettings");
        }

        private void RefreshTargetLayerList()
        {
            if (CboTargetLayer == null) return;
            string lang = _settings.Language;

            // 이벤트 핸들러 임시 제거 (갱신 중 발생 방지)
            CboTargetLayer.SelectionChanged -= CboTargetLayer_SelectionChanged;
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
            }
            finally
            {
                CboTargetLayer.SelectionChanged += CboTargetLayer_SelectionChanged;
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
                            btn.Name = $"Key {btn.Index}"; // 이름도 기본값으로 복구
                            changed = true;
                        }
                    }

                    if (changed)
                    {
                        AppSettings.Save(_settings);
                        
                        // UI 및 OSD 갱신
                        _osd.UpdateNames(_settings.Buttons, _currentLayer);
                        RefreshBadges();
                        
                        // 목록 갱신 (삭제된 항목 제거됨)
                        RefreshTargetLayerList();
                        UpdateEditControls(); // 현재 선택된 슬롯의 콤보박스 상태 업데이트
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
    }
}
