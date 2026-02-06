using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
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
    }

    public partial class MainWindow : Window
    {
        private RawInputReceiver _rawInput;
        private OsdWindow _osd;
        private AppSettings _settings;
        private System.Windows.Forms.NotifyIcon _notifyIcon; // 트레이 아이콘
        private int _currentLayer = 0; // 현재 활성화된 레이어
        private bool _isListening = false; // 입력 감지 모드 여부
        private string _lastSignature = ""; // 마지막 수신된 신호 (노이즈 필터링용)
        private string _startSignature = ""; // 감지 시작 시점의 노이즈 신호
        private Window _candidateWindow; // 신호 선택 창
        private System.Windows.Controls.ListBox _candidateListBox; // 신호 목록

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        extern static bool DestroyIcon(IntPtr handle);

        public MainWindow()
        {
            InitializeComponent();
            _osd = new OsdWindow();
            _osd.DebugLog += (msg) => AppendLogFile(msg); // OSD 로그를 파일로 저장
            
            // 설정 로드
            _settings = AppSettings.Load();
            _currentLayer = _settings.LastLayerIndex; // 저장된 마지막 레이어 불러오기
            TxtVid.Text = _settings.DeviceVid;
            TxtPid.Text = _settings.DevicePid;
            SldOpacity.Value = _settings.OsdOpacity;
            TxtTimeout.Text = _settings.OsdTimeout.ToString();
            CboMode.SelectedIndex = _settings.OsdMode;

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
            string pngPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.png");
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
                string iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico");
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
            modeItem.DropDownItems.Add("자동", null, (s, e) => Dispatcher.Invoke(() => CboMode.SelectedIndex = 0));
            modeItem.DropDownItems.Add("항상 켜기", null, (s, e) => Dispatcher.Invoke(() => CboMode.SelectedIndex = 1));
            modeItem.DropDownItems.Add("항상 끄기", null, (s, e) => Dispatcher.Invoke(() => CboMode.SelectedIndex = 2));
            contextMenu.Items.Add(modeItem);

            // 2. OSD 위치 이동
            contextMenu.Items.Add("OSD 위치 이동 허용", null, (s, e) => Dispatcher.Invoke(() => ChkMoveOsd.IsChecked = !ChkMoveOsd.IsChecked));

            contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

            // 3. 종료
            contextMenu.Items.Add("종료", null, (s, e) => Dispatcher.Invoke(() => this.Close()));

            _notifyIcon.ContextMenuStrip = contextMenu;

            // UI 콤보박스도 저장된 레이어로 동기화
            CboLayer.SelectedIndex = _currentLayer;

            // 매핑 슬롯 콤보박스 초기화 (1~12번)
            if (CboMapSlot.Items.Count == 0) RefreshSlotList();

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

        private void RefreshSlotList()
        {
            if (CboLayer == null || CboMapSlot == null) return;

            int savedIndex = CboMapSlot.SelectedIndex;
            int layer = CboLayer.SelectedIndex < 0 ? 0 : CboLayer.SelectedIndex;

            CboMapSlot.Items.Clear();
            // 선택된 레이어의 버튼만 필터링하여 표시
            foreach (var btn in _settings.Buttons.Where(b => b.Layer == layer).OrderBy(b => b.Index))
            {
                CboMapSlot.Items.Add($"{btn.Index}: {btn.Name}");
            }
            
            if (savedIndex >= 0 && savedIndex < CboMapSlot.Items.Count)
                CboMapSlot.SelectedIndex = savedIndex;
            else
                CboMapSlot.SelectedIndex = 0;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 윈도우 핸들을 얻은 후 Raw Input 등록
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            _rawInput = new RawInputReceiver(hwnd, _settings.DeviceVid, _settings.DevicePid);
            _rawInput.HidDataReceived += OnHidDataReceived;
            _rawInput.DebugLog += (msg) => Dispatcher.Invoke(() => Log(msg)); // 디버그 로그 연결

            // 로그 연결 후 장치 검색 시작
            _rawInput.Initialize();

            // 장치 목록 검색 및 콤보박스 갱신
            RefreshDeviceList();

            // 시작 시 OSD가 잘 뜨는지 테스트
            _osd.ShowBriefly();
            Log("프로그램 시작됨. OSD 테스트 표시.");

            // 메시지 루프 훅 추가
            HwndSource source = HwndSource.FromHwnd(hwnd);
            source.AddHook(WndProc);
        }

        private void RefreshDeviceList()
        {
            if (_rawInput == null) return;

            string vid = TxtVid.Text;
            var devices = _rawInput.GetAvailableDevices(vid);
            CboDevices.ItemsSource = devices;

            if (devices.Count > 0)
            {
                // 현재 PID와 일치하는 장치가 있으면 선택, 없으면 첫 번째 선택
                var current = devices.FirstOrDefault(d => d.Pid.Equals(TxtPid.Text, StringComparison.OrdinalIgnoreCase));
                if (current != null)
                    CboDevices.SelectedItem = current;
                else
                    CboDevices.SelectedIndex = 0;
            }
        }

        private void BtnScan_Click(object sender, RoutedEventArgs e)
        {
            RefreshDeviceList();
        }

        private void CboDevices_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (CboDevices.SelectedItem is DeviceInfo info)
            {
                TxtPid.Text = info.Pid;
            }
        }

        private void SldOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_settings != null && _osd != null)
            {
                _settings.OsdOpacity = e.NewValue;
                _osd.Opacity = e.NewValue; // OSD 창에 즉시 적용
                AppSettings.Save(_settings); // 설정 파일에 즉시 저장
            }
        }

        private void CboMode_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_settings != null && _osd != null)
            {
                _settings.OsdMode = CboMode.SelectedIndex;
                _osd.UpdateSettings(_settings); // OSD에 즉시 반영
                AppSettings.Save(_settings);
            }
        }

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            if (this.WindowState == WindowState.Minimized)
                this.Hide(); // 최소화 시 작업표시줄에서 숨김 (트레이로 이동)
        }

        private void ChkMoveOsd_CheckedChanged(object sender, RoutedEventArgs e)
        {
            _osd.SetMoveMode(ChkMoveOsd.IsChecked == true);
        }

        private void BtnResetSize_Click(object sender, RoutedEventArgs e)
        {
            _osd.ResetSize();
            AppSettings.Save(_settings);
        }

        private void CboLayer_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_settings == null) return;

            int layer = CboLayer.SelectedIndex;
            if (layer < 0) return;

            _currentLayer = layer;
            _settings.LastLayerIndex = layer;
            AppSettings.Save(_settings); // 레이어 변경 시 즉시 저장

            if (_osd != null) _osd.UpdateNames(_settings.Buttons, layer);
            RefreshSlotList();
        }

        private void CboMapSlot_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            int index = CboMapSlot.SelectedIndex;
            int layer = CboLayer.SelectedIndex < 0 ? 0 : CboLayer.SelectedIndex;
            var btn = _settings.Buttons.FirstOrDefault(b => b.Layer == layer && b.Index == index + 1);
            if (btn != null)
            {
                TxtKeyName.Text = btn.Name;
                
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

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
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
            _lastSignature = signature; // 마지막 신호 갱신

            // 입력 감지 모드일 때 처리
            if (_isListening)
            {
                // 감지 시작 시점의 노이즈와 다르고, 키 뗌(81 00) 신호가 아니면 목록에 추가
                if (signature != _startSignature && !signature.Replace(" ", "").StartsWith("8100"))
                {
                    Dispatcher.Invoke(() => {
                        if (_candidateListBox != null && !_candidateListBox.Items.Contains(signature))
                        {
                            _candidateListBox.Items.Add(signature);
                        }
                    });
                }
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
                        CboLayer.SelectedIndex = _currentLayer; // UI 동기화
                    });
                }
            }

            // 1~12번 키인 경우 OSD 표시 (로그 일시정지와 무관하게 작동)
            if (keyIndex >= 1 && keyIndex <= 12)
            {
                AppendLogFile($"[Main] Key {keyIndex} detected. Triggering OSD.");
                Dispatcher.Invoke(() => _osd.HighlightKey(keyIndex));
            }

            // 로그 일시정지면 로그 기록만 건너뜀
            if (ChkPauseLog.IsChecked == true) return;

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

        private void AddLog(byte[] data, string dataHex, string msg)
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
                Data = dataHex
            };

            // UI 업데이트: 최소화 상태가 아닐 때만 수행 (최적화)
            if (this.WindowState != WindowState.Minimized)
            {
                LstLog.Items.Add(entry);
                if (LstLog.Items.Count > 1000) LstLog.Items.RemoveAt(0); // 로그 보존 개수 증가 (100 -> 1000)
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
                string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log.txt");
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

        private void BtnRename_Click(object sender, RoutedEventArgs e)
        {
            int slotIndex = CboMapSlot.SelectedIndex + 1;
            int layer = CboLayer.SelectedIndex;
            var btn = _settings.Buttons.FirstOrDefault(b => b.Layer == layer && b.Index == slotIndex);

            if (btn != null)
            {
                btn.Name = TxtKeyName.Text;
                AppSettings.Save(_settings);
                _osd.UpdateNames(_settings.Buttons, layer);
                RefreshSlotList(); // 콤보박스 이름 갱신
                string msg = LanguageManager.GetString(_settings.Language, "MsgNameChanged");
                System.Windows.MessageBox.Show(msg);
            }
        }

        private void BtnAutoDetect_Click(object sender, RoutedEventArgs e)
        {
            if (_candidateWindow != null)
            {
                _candidateWindow.Activate();
                return;
            }

            if (CboMapSlot.SelectedIndex < 0)
            {
                string msg = LanguageManager.GetString(_settings.Language, "MsgSelectSlot");
                System.Windows.MessageBox.Show(msg);
                return;
            }

            _startSignature = _lastSignature; // 현재 노이즈(기본 신호) 저장
            _isListening = true;
            BtnAutoDetect.Content = LanguageManager.GetString(_settings.Language, "MsgDetecting");

            // 신호 선택 창 생성
            _candidateWindow = new Window
            {
                Title = LanguageManager.GetString(_settings.Language, "TitleSelectSignal"),
                Width = 350,
                Height = 400,
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize
            };

            var grid = new System.Windows.Controls.Grid { Margin = new Thickness(10) };
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });

            _candidateListBox = new System.Windows.Controls.ListBox { Margin = new Thickness(0, 0, 0, 10) };
            System.Windows.Controls.Grid.SetRow(_candidateListBox, 0);

            var btnSelect = new System.Windows.Controls.Button
            {
                Content = "선택한 신호로 매핑",
                Padding = new Thickness(10, 5, 10, 5),
                Height = 30
            };
            System.Windows.Controls.Grid.SetRow(btnSelect, 1);

            btnSelect.Click += (s, args) =>
            {
                if (_candidateListBox.SelectedItem != null)
                {
                    string selectedSignature = _candidateListBox.SelectedItem.ToString();
                    PerformAutoMapping(selectedSignature);
                    _candidateWindow.Close();
                }
                else
                {
                    System.Windows.MessageBox.Show("목록에서 신호를 선택해주세요.");
                }
            };

            grid.Children.Add(_candidateListBox);
            grid.Children.Add(btnSelect);
            _candidateWindow.Content = grid;

            _candidateWindow.Closed += (s, args) =>
            {
                _isListening = false;
                BtnAutoDetect.Content = LanguageManager.GetString(_settings.Language, "BtnAutoDetect");
                _candidateWindow = null;
                _candidateListBox = null;
            };

            _candidateWindow.Show();
        }

        private void BtnUnmap_Click(object sender, RoutedEventArgs e)
        {
            if (CboMapSlot.SelectedIndex < 0)
            {
                System.Windows.MessageBox.Show("매핑을 해제할 슬롯을 선택해주세요.");
                return;
            }

            int layer = CboLayer.SelectedIndex < 0 ? 0 : CboLayer.SelectedIndex;
            int slotIndex = CboMapSlot.SelectedIndex + 1;

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
                    RefreshSlotList(); // 콤보박스 목록 갱신 및 UI 업데이트

                    System.Windows.MessageBox.Show(LanguageManager.GetString(_settings.Language, "MsgUnmapped"));
                }
            }
        }

        private void PerformAutoMapping(string signature)
        {
            int layer = CboLayer.SelectedIndex < 0 ? 0 : CboLayer.SelectedIndex;

            // 중복 방지: 같은 레이어 내에서만 중복 체크
            foreach (var b in _settings.Buttons)
            {
                if (b.Layer == layer && b.TriggerPattern == signature) b.TriggerPattern = null;
            }

            int slotIndex = CboMapSlot.SelectedIndex + 1;
            var btn = _settings.Buttons.FirstOrDefault(b => b.Layer == layer && b.Index == slotIndex);
            if (btn != null)
            {
                btn.TriggerPattern = signature;
                btn.Name = TxtKeyName.Text;
                
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
                RefreshSlotList();

                System.Windows.MessageBox.Show($"Key {slotIndex}에 패턴이 매핑되었습니다.\n패턴: {signature}");
            }
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            _settings.DeviceVid = TxtVid.Text;
            _settings.DevicePid = TxtPid.Text;
            _settings.OsdOpacity = SldOpacity.Value;
            _settings.OsdMode = CboMode.SelectedIndex;
            if (int.TryParse(TxtTimeout.Text, out int timeout))
                _settings.OsdTimeout = timeout;

            _rawInput.UpdateTargetDevice(_settings.DeviceVid, _settings.DevicePid);
            _osd.UpdateSettings(_settings);
            
            AppSettings.Save(_settings);
            System.Windows.MessageBox.Show("VID/PID가 적용되었습니다.");
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            _settings.OsdOpacity = SldOpacity.Value;
            _settings.OsdMode = CboMode.SelectedIndex;
            if (int.TryParse(TxtTimeout.Text, out int timeout))
                _settings.OsdTimeout = timeout;
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
                            key.SetValue("SayoOSD", path);
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

            if (GrpDevice != null) GrpDevice.Header = LanguageManager.GetString(lang, "GrpDevice");
            if (BtnScan != null) BtnScan.Content = LanguageManager.GetString(lang, "BtnScan");
            if (BtnApply != null) BtnApply.Content = LanguageManager.GetString(lang, "BtnApply");

            if (GrpOsd != null) GrpOsd.Header = LanguageManager.GetString(lang, "GrpOsd");
            if (LblOpacity != null) LblOpacity.Text = LanguageManager.GetString(lang, "LblOpacity");
            if (LblTimeout != null) LblTimeout.Text = LanguageManager.GetString(lang, "LblTimeout");
            if (LblMode != null) LblMode.Text = LanguageManager.GetString(lang, "LblMode");
            
            if (CboMode != null && CboMode.Items.Count >= 3)
            {
                (CboMode.Items[0] as System.Windows.Controls.ComboBoxItem).Content = LanguageManager.GetString(lang, "ModeAuto");
                (CboMode.Items[1] as System.Windows.Controls.ComboBoxItem).Content = LanguageManager.GetString(lang, "ModeOn");
                (CboMode.Items[2] as System.Windows.Controls.ComboBoxItem).Content = LanguageManager.GetString(lang, "ModeOff");
            }

            if (ChkMoveOsd != null) ChkMoveOsd.Content = LanguageManager.GetString(lang, "ChkMoveOsd");
            if (BtnResetSize != null) BtnResetSize.Content = LanguageManager.GetString(lang, "BtnResetSize");

            if (GrpMap != null) GrpMap.Header = LanguageManager.GetString(lang, "GrpMap");
            if (ColTime != null) ColTime.Header = LanguageManager.GetString(lang, "ColTime");
            if (ColKey != null) ColKey.Header = LanguageManager.GetString(lang, "ColKey");
            if (ColData != null) ColData.Header = LanguageManager.GetString(lang, "ColData");
            if (MnuCopy != null) MnuCopy.Header = LanguageManager.GetString(lang, "MnuCopy");

            if (ChkPauseLog != null) ChkPauseLog.Content = LanguageManager.GetString(lang, "ChkPauseLog");
            if (LblLayer != null) LblLayer.Text = LanguageManager.GetString(lang, "LblLayer");
            if (LblSlot != null) LblSlot.Text = LanguageManager.GetString(lang, "LblSlot");
            if (LblName != null) LblName.Text = LanguageManager.GetString(lang, "LblName");
            if (BtnRename != null) BtnRename.Content = LanguageManager.GetString(lang, "BtnRename");
            if (LblTarget != null) LblTarget.Text = LanguageManager.GetString(lang, "LblTarget");
            
            if (CboTargetLayer != null && CboTargetLayer.Items.Count > 0)
            {
                (CboTargetLayer.Items[0] as System.Windows.Controls.ComboBoxItem).Content = LanguageManager.GetString(lang, "TargetNone");
            }

            if (LblSignal != null) LblSignal.Text = LanguageManager.GetString(lang, "LblSignal");
            if (BtnAutoDetect != null) BtnAutoDetect.Content = LanguageManager.GetString(lang, "BtnAutoDetect");
            if (BtnUnmap != null) BtnUnmap.Content = LanguageManager.GetString(lang, "BtnUnmap");

            if (ChkEnableFileLog != null) ChkEnableFileLog.Content = LanguageManager.GetString(lang, "ChkEnableFileLog");
            if (ChkStartWithWindows != null) ChkStartWithWindows.Content = LanguageManager.GetString(lang, "ChkStartWithWindows");
            if (BtnSave != null) BtnSave.Content = LanguageManager.GetString(lang, "BtnSave");
            if (BtnHide != null) BtnHide.Content = LanguageManager.GetString(lang, "BtnHide");
        }
    }
}
