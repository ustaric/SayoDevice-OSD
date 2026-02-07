﻿using System;
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

        public MainWindow()
        {
            // [핵심] App 클래스의 정적 생성자를 강제로 실행시켜 중복 검사를 가장 먼저 수행
            // MainWindow가 StartupObject일 경우, InitializeComponent보다 먼저 실행되어야 창이 뜨지 않고 종료됨
            var _ = App.StartupLogs; 

            InitializeComponent();
            _osd = new OsdWindow();
            _osd.DebugLog += (msg) => AppendLogFile(msg); // OSD 로그를 파일로 저장
            
            // 설정 로드
            _settings = AppSettings.Load();
            _currentLayer = _settings.LastLayerIndex; // 저장된 마지막 레이어 불러오기

            // 로그 파일 저장 설정 적용 및 이벤트 연결
            ChkEnableFileLog.IsChecked = _settings.EnableFileLog;
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

            contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

            // 3. 종료
            contextMenu.Items.Add("종료", null, (s, e) => Dispatcher.Invoke(() => this.Close()));

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
                        bool isLayerMove = btn != null && btn.TargetLayer >= 0 && btn.TargetLayer <= 4;

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
                // [수정] 프로그램 시작 직후 키 로그가 쏟아져 App 로그가 묻히는 것을 방지하기 위해 5초 대기
                await System.Threading.Tasks.Task.Delay(5000);

                // 윈도우 핸들을 얻은 후 Raw Input 등록
                IntPtr hwnd = new WindowInteropHelper(this).Handle;
                _rawInput = new RawInputReceiver(hwnd, _settings.DeviceVid, _settings.DevicePid);
                _rawInput.HidDataReceived += OnHidDataReceived;
                _rawInput.DebugLog += (msg) => Dispatcher.Invoke(() => Log(msg)); // 디버그 로그 연결

                // 로그 연결 후 장치 검색 시작
                _rawInput.Initialize();

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
                    int targetIndex = 0; // 기본값: 이동 없음
                    if (btn.TargetLayer >= 0 && btn.TargetLayer <= 4)
                    {
                        targetIndex = btn.TargetLayer + 1; // 0번 인덱스가 '이동 없음'이므로 +1
                    }
                    if (targetIndex < CboTargetLayer.Items.Count)
                    {
                        CboTargetLayer.SelectedIndex = targetIndex;
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
                    if (btn.TargetLayer != targetLayer)
                    {
                        btn.TargetLayer = targetLayer;
                        AppSettings.Save(_settings);
                        Log($"[Setting] Key {btn.Index} Target Layer -> {item.Content}");
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
                if (_candidateCount >= 10) return; // 10개 수집 후 정지

                Dispatcher.Invoke(() => {
                    // 후보군은 파란색으로 표시
                    AddLog(data, hex, "Candidate Signal (Double-click to map)", true);
                    _candidateCount++;
                    if (_candidateCount >= 10)
                    {
                        BtnAutoDetect.Content = "감지 완료 (선택하세요)";
                    }
                });
                return; // 감지 중에는 일반 로직 건너뜀
            }

            byte keyIndex = 0; // 0이면 매핑 안됨

            // 설정된 매핑 확인
            // 1. 현재 레이어에서 먼저 검색
            var mappedBtn = _settings.Buttons.FirstOrDefault(b => b.TriggerPattern == signature && b.Layer == _currentLayer);
            // 2. 없으면 전체 레이어에서 검색 (다른 레이어의 고유 키일 경우)
            if (mappedBtn == null)
                mappedBtn = _settings.Buttons.FirstOrDefault(b => b.TriggerPattern == signature);

            if (mappedBtn != null)
            {
                keyIndex = (byte)mappedBtn.Index;
                int newLayer = _currentLayer;
                
                // 레이어 이동 로직
                // 1순위: 타겟 레이어가 설정되어 있으면 그곳으로 이동
                if (mappedBtn.TargetLayer >= 0 && mappedBtn.TargetLayer <= 4)
                {
                    newLayer = mappedBtn.TargetLayer;
                }
                // 2순위: 타겟 설정은 없지만, 다른 레이어에만 존재하는 키라면 해당 레이어로 이동 (하드웨어 동기화)
                else if (_currentLayer != mappedBtn.Layer)
                {
                    newLayer = mappedBtn.Layer;
                }

                // 레이어가 변경되었다면 적용 및 저장
                if (_currentLayer != newLayer)
                {
                    _currentLayer = newLayer;
                    _settings.LastLayerIndex = _currentLayer;
                    AppSettings.Save(_settings); // 변경된 레이어 저장
                    Dispatcher.Invoke(() => {
                        _osd.UpdateNames(_settings.Buttons, _currentLayer);
                        UpdateLayerRadioButton(_currentLayer); // UI 동기화
                    });
                }
            }

            // 1~12번 키인 경우 OSD 표시 (로그 일시정지와 무관하게 작동)
            if (keyIndex >= 1 && keyIndex <= 12)
            {
                AppendLogFile($"[Main] Key {keyIndex} detected. Triggering OSD.");
                Dispatcher.Invoke(() => _osd.HighlightKey(keyIndex));
            }

            // 노이즈 필터링: C6로 시작하는 신호는 로그에 남기지 않음
            if (hex.StartsWith("C6")) return;

            // 힌트 메시지: 0x81로 시작하는 긴 패킷은 보통 Key Up(뗌) 신호임
            string hint = "";
            if (data.Length > 10 && data[8] == 0x81) hint = " (Key Up?)";

            Dispatcher.Invoke(() => {
                if (keyIndex >= 1 && keyIndex <= 12)
                    AddLog(data, hex, $"[Key {keyIndex}] Matched{hint}");
                else
                    AddLog(data, hex, $"Unknown Signal{hint}");
            });
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
            AppendLogFile(logMsg);
        }

        private void AppendLogFile(string message)
        {
            // 파일 로그 저장 체크박스가 꺼져있으면 기록하지 않음
            if (ChkEnableFileLog.IsChecked != true) return;

            try
            {
                string path = System.IO.Path.Combine(AppContext.BaseDirectory, "log.txt");
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                System.IO.File.AppendAllText(path, $"[{timestamp}] {message}{Environment.NewLine}");
            }
            catch { /* 파일 쓰기 실패 시 무시 (충돌 방지) */ }
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
            
            if (CboTargetLayer != null && CboTargetLayer.Items.Count > 0)
            {
                (CboTargetLayer.Items[0] as System.Windows.Controls.ComboBoxItem).Content = LanguageManager.GetString(lang, "TargetNone");
            }

            if (BtnAutoDetect != null) BtnAutoDetect.Content = LanguageManager.GetString(lang, "BtnAutoDetect");
            if (BtnUnmap != null) BtnUnmap.Content = LanguageManager.GetString(lang, "BtnUnmap");

            if (ChkEnableFileLog != null) ChkEnableFileLog.Content = LanguageManager.GetString(lang, "ChkEnableFileLog");
            if (ChkStartWithWindows != null) ChkStartWithWindows.Content = LanguageManager.GetString(lang, "ChkStartWithWindows");
            if (BtnSave != null) BtnSave.Content = LanguageManager.GetString(lang, "BtnSave");
            if (BtnHide != null) BtnHide.Content = LanguageManager.GetString(lang, "BtnHide");
            if (BtnOpenSettings != null) BtnOpenSettings.Content = LanguageManager.GetString(lang, "BtnOpenSettings");
        }
    }
}
