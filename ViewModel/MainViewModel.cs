using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Collections.Generic; // For Queue, HashSet
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows; // For Application.Current.Dispatcher
using System.Threading.Tasks; // For Task
using System.Diagnostics;
using SayoOSD.Models;
using SayoOSD.Services;
using SayoOSD.Helpers;
using SayoOSD.Managers;
using SayoOSD.ViewModels;

namespace SayoOSD.ViewModels
{
    /// <summary>
    /// MainWindow의 모든 UI 상태와 로직을 관리하는 메인 ViewModel입니다.
    /// </summary>
    public class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        #region Services
        private readonly AppSettings _settings;
        private readonly HidService _hidService;
        private readonly AudioController _audioController;
        private readonly InputExecutor _inputExecutor;
        private RawInputReceiver _rawInput; // View가 로드된 후 초기화되어야 함
        public RawInputReceiver RawInput => _rawInput; // [추가] 외부(SettingsWindow 등)에서 접근 가능하도록 공개

        // OSD 업데이트 요청 이벤트 (View에서 구독)
        public event Action RequestOsdUpdate;

        // [추가] View에 UI 액션을 요청하는 이벤트
        public event Action<int, bool?> RequestOsdHighlight;
        public event Action<bool> RequestSpeakerUpdate; // [추가] 스피커 상태 업데이트 요청
        public event Action<string> RequestAutoMappingConfirmation;
        public event Action<string, string, int> RequestOsdFeedback; // [수정] 아이콘 경로, 키 인덱스 추가
        public event Action<int> RequestLayerChange; // UI(라디오버튼) 업데이트 요청

        // [추가] HID 입력 처리 관련 필드
        private readonly Queue<Tuple<byte[], DateTime>> _recentPackets = new Queue<Tuple<byte[], DateTime>>();
        private readonly object _packetLock = new object();
        private bool _isManualDetecting = false;
        private int _candidateCount = 0;
        private readonly HashSet<string> _ignoredSignatures = new HashSet<string>();
        private System.Windows.Threading.DispatcherTimer _layerSyncDebounceTimer;
        #endregion

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion

        #region Properties
        private int _currentLayer;
        public int CurrentLayer
        {
            get => _currentLayer;
            set
            {
                if (_currentLayer != value)
                {
                    _currentLayer = value;
                    OnPropertyChanged();
                    // 레이어 변경 시 슬롯 목록 갱신
                    LoadKeySlotsForLayer(value);
                    RequestOsdUpdate?.Invoke();
                }
            }
        }

        // [추가] 가상 레이어(앱 프로필) 관련 프로퍼티
        public ObservableCollection<AppProfile> AppProfiles { get; private set; }

        private AppProfile _selectedAppProfile;
        public AppProfile SelectedAppProfile
        {
            get => _selectedAppProfile;
            set
            {
                if (_selectedAppProfile != value)
                {
                    _selectedAppProfile = value;
                    OnPropertyChanged();
                    if (_selectedAppProfile != null)
                    {
                        IsVirtualLayerMode = true;
                        _settings.LastVirtualProfileName = _selectedAppProfile.Name; // [추가] 선택된 프로필 상태 저장
                        LoadKeySlotsFromProfile(_selectedAppProfile);
                    }
                }
            }
        }

        private bool _isVirtualLayerMode;
        public bool IsVirtualLayerMode
        {
            get => _isVirtualLayerMode;
            set
            {
                if (_isVirtualLayerMode != value)
                {
                    _isVirtualLayerMode = value;
                    OnPropertyChanged();
                    // 가상 모드가 해제되면 하드웨어 레이어로 복귀
                    if (!_isVirtualLayerMode)
                    {
                        _selectedAppProfile = null; // 내부 필드만 초기화 (프로퍼티 세터 로직 방지)
                        _settings.LastVirtualProfileName = null; // [추가] 가상 모드 해제 시 상태 초기화
                        OnPropertyChanged(nameof(SelectedAppProfile));
                        LoadKeySlotsForLayer(CurrentLayer);
                        RequestOsdUpdate?.Invoke();
                    }
                }
            }
        }

        public ObservableCollection<LogEntry> LogEntries { get; }

        public ObservableCollection<KeySlotViewModel> KeySlots { get; }

        private KeySlotViewModel _selectedSlot;
        public KeySlotViewModel SelectedSlot
        {
            get => _selectedSlot;
            set
            {
                if (_selectedSlot != value)
                {
                    if (_selectedSlot != null)
                    {
                        _selectedSlot.IsSelected = false;
                        _selectedSlot.PropertyChanged -= SelectedSlot_PropertyChanged;
                    }
                    _selectedSlot = value;
                    
                    if (_selectedSlot != null)
                    {
                        _selectedSlot.IsSelected = true;
                        _selectedSlot.PropertyChanged += SelectedSlot_PropertyChanged;
                    }
                    
                    OnPropertyChanged();
                    UpdateDetailPanelVisibility(); // 슬롯 변경 시 패널 표시 여부 갱신
                }
            }
        }

        private bool _isAutoDetecting;
        public bool IsAutoDetecting
        {
            get => _isAutoDetecting;
            set
            {
                if (_isAutoDetecting != value)
                {
                    _isAutoDetecting = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsDetectionActive));
                }
            }
        }

        private bool _isDetailPanelVisible;
        public bool IsDetailPanelVisible
        {
            get => _isDetailPanelVisible;
            set
            {
                if (_isDetailPanelVisible != value)
                {
                    _isDetailPanelVisible = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsLogPanelVisible));
                }
            }
        }

        public bool IsLogPanelVisible => !_isDetailPanelVisible;

        // [추가] 버튼 텍스트 바인딩용 프로퍼티
        private string _autoDetectButtonText;
        public string AutoDetectButtonText
        {
            get => _autoDetectButtonText;
            set { if (_autoDetectButtonText != value) { _autoDetectButtonText = value; OnPropertyChanged(); } }
        }

        private string _manualDetectButtonText;
        public string ManualDetectButtonText
        {
            get => _manualDetectButtonText;
            set { if (_manualDetectButtonText != value) { _manualDetectButtonText = value; OnPropertyChanged(); } }
        }

        public bool IsManualDetecting
        {
            get => _isManualDetecting;
            set
            {
                if (_isManualDetecting != value)
                {
                    _isManualDetecting = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsDetectionActive));
                }
            }
        }

        public bool IsDetectionActive => IsAutoDetecting || IsManualDetecting;

        public string PathLabelText => (SelectedSlot != null && SelectedSlot.IsMacroMode)
            ? LanguageManager.GetString(_settings.Language, "TitleInputText")
            : LanguageManager.GetString(_settings.Language, "LblPath");

        public void RefreshLocalization() => OnPropertyChanged(nameof(PathLabelText));

        #endregion

        #region Commands
        public ICommand ChangeLayerCommand { get; }
        public ICommand AutoDetectCommand { get; }
        public ICommand UnmapCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand SavePanelCommand { get; }
        public ICommand CancelPanelCommand { get; }
        public ICommand BrowseCommand { get; }
        public ICommand ChangeIconCommand { get; }
        public ICommand StartAutoDetectCommand { get; }
        public ICommand StartManualDetectCommand { get; }
        #endregion

        public MainViewModel(AppSettings settings)
        {
            // 1. 설정 및 서비스 초기화
            _settings = settings;
            _inputExecutor = new InputExecutor();
            _audioController = new AudioController();
            _hidService = new HidService(_settings.DeviceVid, _settings.DevicePid);

            // 2. 컬렉션 초기화
            LogEntries = new ObservableCollection<LogEntry>();
            KeySlots = new ObservableCollection<KeySlotViewModel>();

            // [추가] 앱 프로필 컬렉션 초기화
            AppProfiles = new ObservableCollection<AppProfile>(_settings.AppProfiles);

            // [추가] 마지막 가상 프로필 상태 복원
            if (!string.IsNullOrEmpty(_settings.LastVirtualProfileName))
            {
                var profile = AppProfiles.FirstOrDefault(p => p.Name == _settings.LastVirtualProfileName);
                if (profile != null)
                {
                    SelectedAppProfile = profile;
                }
            }

            // 3. 속성 초기화
            _currentLayer = _settings.LastLayerIndex;
            LoadKeySlotsForLayer(_currentLayer);
            InputExecutor.CurrentLanguage = _settings.Language; // [추가] 초기 언어 설정 동기화
            if (KeySlots.Any())
            {
                SelectedSlot = KeySlots.First(); // 첫 번째 슬롯을 기본으로 선택
            }

            // 버튼 텍스트 초기화
            AutoDetectButtonText = LanguageManager.GetString(_settings.Language, "BtnAutoDetect");
            ManualDetectButtonText = LanguageManager.GetString(_settings.Language, "BtnManualDetect");

            // 4. 커맨드 초기화
            ChangeLayerCommand = new RelayCommand(p =>
            {
                if (p != null && int.TryParse(p.ToString(), out int layer))
                {
                    // [추가] 하드웨어 레이어 버튼 클릭 시 가상 모드 해제
                    IsVirtualLayerMode = false;
                    CurrentLayer = layer;
                    _settings.LastLayerIndex = layer;
                }
            });

            AutoDetectCommand = new RelayCommand(p => IsAutoDetecting = !IsAutoDetecting);
            UnmapCommand = new RelayCommand(p => { /* TODO: 매핑 해제 로직 */ }, p => SelectedSlot != null);
            SaveCommand = new RelayCommand(p => AppSettings.Save(_settings));

            // 패널 저장: 설정 저장 및 OSD 업데이트 후 패널 닫기
            SavePanelCommand = new RelayCommand(p => 
            {
                AppSettings.Save(_settings);
                RequestOsdUpdate?.Invoke();
                IsDetailPanelVisible = false;
            });

            // 패널 취소: 패널 닫기
            CancelPanelCommand = new RelayCommand(p => IsDetailPanelVisible = false);

            // 파일 찾아보기
            BrowseCommand = new RelayCommand(p => 
            {
                var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Executables (*.exe)|*.exe|All files (*.*)|*.*" };
                if (dlg.ShowDialog() == true && SelectedSlot != null)
                {
                    SelectedSlot.FilePath = dlg.FileName;
                }
            });

            // 아이콘 변경
            ChangeIconCommand = new RelayCommand(p => 
            {
                var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Icon/Exe (*.ico;*.exe)|*.ico;*.exe|All files (*.*)|*.*" };
                if (dlg.ShowDialog() == true && SelectedSlot != null)
                {
                    SelectedSlot.IconPath = dlg.FileName;
                }
            });

            StartAutoDetectCommand = new RelayCommand(p => ToggleAutoDetect());
            StartManualDetectCommand = new RelayCommand(p => ToggleManualDetect());
        }

        /// <summary>
        /// View가 로드된 후 호출되어 Window Handle이 필요한 서비스들을 초기화합니다.
        /// </summary>
        public void Initialize(IntPtr windowHandle)
        {
            _rawInput = new RawInputReceiver(windowHandle, _settings.DeviceVid, _settings.DevicePid);
            _rawInput.HidDataReceived += OnHidDataReceived;
            _rawInput.DebugLog += (msg) => AddLog($"[RAW-DBG] {msg}");
            _rawInput.Initialize();

            _audioController.LogMessage += (msg) => AddLog($"[AUDIO] {msg}");
            _audioController.MicMuteChanged += (muted) => 
                System.Windows.Application.Current.Dispatcher.Invoke(() => RequestOsdHighlight?.Invoke(-1, muted));
            _audioController.SpeakerMuteChanged += (muted) =>
                System.Windows.Application.Current.Dispatcher.Invoke(() => RequestSpeakerUpdate?.Invoke(muted));
            
            // [추가] 시스템 오디오 장치 변경 시 OSD 이름 자동 업데이트
            _audioController.AudioDeviceChanged += (deviceName) => 
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => 
                {
                    var buttons = _settings.Buttons.Where(b => b.TargetLayer == InputExecutor.ACTION_AUDIO_CYCLE).ToList();
                    if (buttons.Count > 0)
                    {
                        foreach (var btn in buttons) btn.Name = deviceName;
                        AppSettings.Save(_settings);
                        RequestOsdUpdate?.Invoke();
                        LoadKeySlotsForLayer(CurrentLayer); // VM 갱신
                        AddLog($"[Audio] OSD updated to: {deviceName}");
                    }
                });
            };

            _audioController.Initialize();

            _hidService.LogMessage += (msg) => AddLog($"[HID] {msg}");
            _hidService.LayerDetected += (layer) =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    AddLog($"[Sync] Device Layer Changed to {layer}");
                    HandleLayerChangeRequest(layer);
                });
            };
            _hidService.RequestLayerState();

            _inputExecutor.LogMessage += (msg) => AddLog($"[EXEC] {msg}");
            // [추가] OSD 피드백 요청 처리 (볼륨 표시 등)
            _inputExecutor.OsdFeedbackRequested += (msg, icon, idx) => 
                System.Windows.Application.Current.Dispatcher.Invoke(() => RequestOsdFeedback?.Invoke(msg, icon, idx));

            AddLog("ViewModel Initialized.");
        }

        /// <summary>
        /// MainWindow의 WndProc에서 수신한 RawInput 메시지를 처리합니다.
        /// </summary>
        public void ProcessRawInputMessage(int msg, IntPtr lParam)
        {
            _rawInput?.ProcessMessage(msg, lParam);
        }

        private void AddLog(string message)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() => LogEntries.Add(new LogEntry { Time = DateTime.Now.ToString("HH:mm:ss"), Data = message }));
        }

        public void LoadKeySlotsForLayer(int layer)
        {
            // [수정] 현재 선택된 인덱스 기억
            int previousIndex = SelectedSlot?.Index ?? 1;

            KeySlots.Clear();
            var buttonConfigs = _settings.Buttons.Where(b => b.Layer == layer).OrderBy(b => b.Index);
            foreach (var config in buttonConfigs) KeySlots.Add(new KeySlotViewModel(config));
            // [수정] 이전 선택 복구
            SelectedSlot = KeySlots.FirstOrDefault(k => k.Index == previousIndex) ?? KeySlots.FirstOrDefault();
        }

        // [추가] 프로필에서 키 슬롯 로드
        public void LoadKeySlotsFromProfile(AppProfile profile)
        {
            // [수정] 현재 선택된 인덱스 기억
            int previousIndex = SelectedSlot?.Index ?? 1;

            KeySlots.Clear();
            if (profile != null)
            {
                // 버튼이 없으면 초기화 (안전장치)
                if (profile.Buttons.Count == 0)
                {
                    for (int i = 1; i <= 12; i++) profile.Buttons.Add(new ButtonConfig { Index = i, Layer = -1, Name = $"Key {i}" });
                }

                foreach (var config in profile.Buttons.OrderBy(b => b.Index))
                {
                    KeySlots.Add(new KeySlotViewModel(config));
                }
            }
            // [수정] 이전 선택 복구
            SelectedSlot = KeySlots.FirstOrDefault(k => k.Index == previousIndex) ?? KeySlots.FirstOrDefault();
        }

        // [추가] 설정 변경 시 프로필 목록 갱신
        public void RefreshAppProfiles()
        {
            // [수정] 목록이 동일하면 갱신하지 않음 (선택 상태 유지)
            if (AppProfiles.Count == _settings.AppProfiles.Count && 
                !AppProfiles.Where((t, i) => t != _settings.AppProfiles[i]).Any())
            {
                return;
            }

            AppProfiles.Clear();
            foreach (var p in _settings.AppProfiles)
            {
                AppProfiles.Add(p);
            }
        }

        private void SelectedSlot_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(KeySlotViewModel.TargetLayer))
            {
                UpdateDetailPanelVisibility();
            }
        }

        private void UpdateDetailPanelVisibility()
        {
            if (SelectedSlot != null && 
                (SelectedSlot.TargetLayer == InputExecutor.ACTION_RUN_PROGRAM || SelectedSlot.IsMacroMode)) // [수정] 매크로(201, 203) 포함
            {
                IsDetailPanelVisible = true;
            }
            else
            {
                IsDetailPanelVisible = false;
            }
            OnPropertyChanged(nameof(PathLabelText));
        }

        public void Dispose()
        {
            _audioController?.Dispose();
        }

        #region HID Input Processing Logic (Moved from MainWindow.xaml.cs)

        private string GetSignature(byte[] data)
        {
            if (data == null || data.Length == 0) return "";
            return BitConverter.ToString(data).Replace("-", " ");
        }

        private void OnHidDataReceived(byte[] data)
        {
            lock (_packetLock)
            {
                _recentPackets.Enqueue(new Tuple<byte[], DateTime>(data, DateTime.Now));
                if (_recentPackets.Count > 20) _recentPackets.Dequeue();
            }

            // [추가] 3번째 바이트(Index 2)가 0x37(Release)인 경우 무시 (0x36 Press만 처리)
            if (data.Length > 2 && data[2] == 0x37)
            {
                return;
            }

            string hex = BitConverter.ToString(data).Replace("-", " ");
            string signature = GetSignature(data);

            if (data.Length > 8 && data[0] == 0x21)
            {
                int detectedLayer = -1;
                if (data.Length > 17 && data[15] == 0x52)
                {
                    detectedLayer = data[17];
                }

                if (detectedLayer >= 0 && detectedLayer <= 4)
                {
                    HandleLayerChangeRequest(detectedLayer);
                }
            }

            if (IsAutoDetecting)
            {
                if (data.Length > 10 && data[10] >= 0xC0)
                {
                    AddLog($"[Auto] Ignored (Keep-alive: {data[10]:X2}) | {hex}");
                    return;
                }

                if (hex.StartsWith("C6")) return;

                string currentKey = (data.Length > 10) ? data[10].ToString("X2") : "";
                if (_ignoredSignatures.Contains(currentKey))
                {
                    AddLog($"[Auto] Background ({currentKey}) | {hex}");
                    return;
                }

                StopDetection();
                AddLog($"[Auto] *** MAPPED *** | {hex}");
                PerformAutoMapping(signature);
                return;
            }
            else if (IsManualDetecting)
            {
                if (hex.StartsWith("C6")) return;
                HandleManualDetect(data, hex);
                return;
            }

            // [추가] 가상 레이어 모드일 때: 가상 프로필에 직접 매핑된 키가 있는지 우선 확인
            // 하드웨어 레이어 매핑 여부와 관계없이 가상 레이어 설정을 독립적으로 동작하게 함
            if (IsVirtualLayerMode && SelectedAppProfile != null)
            {
                var directVBtn = SelectedAppProfile.Buttons.FirstOrDefault(b => b.TriggerPattern == signature);
                if (directVBtn != null)
                {
                    ExecuteButtonLogic(directVBtn, out bool? vMicState);
                    HandleOsdAndLog(data, hex, directVBtn, vMicState);
                    return;
                }
            }

            var btn = FindMappedButton(signature);

            if (btn == null)
            {
                LogUnknownSignal(data, hex);
                return;
            }

            // [추가] 가상 레이어 모드일 경우 인터셉트 처리
            if (IsVirtualLayerMode && SelectedAppProfile != null)
            {
                // 하드웨어 버튼의 인덱스와 일치하는 가상 버튼 찾기
                var virtualBtn = SelectedAppProfile.Buttons.FirstOrDefault(b => b.Index == btn.Index);
                if (virtualBtn != null)
                {
                    // 가상 버튼 로직 실행 (하드웨어 레이어 변경은 무시)
                    ExecuteButtonLogic(virtualBtn, out bool? vMicState);
                    
                    // OSD 및 로그 업데이트
                    HandleOsdAndLog(data, hex, virtualBtn, vMicState);
                }
                // [수정] 가상 레이어 모드에서는 가상 버튼 존재 여부와 상관없이 하드웨어 레이어 동작 차단
                return;
            }

            int newLayer = ExecuteButtonLogic(btn, out bool? micState);

            if (CurrentLayer != newLayer)
            {
                ChangeLayer(newLayer);
            }

            HandleOsdAndLog(data, hex, btn, micState);
        }

        private void HandleManualDetect(byte[] data, string hex)
        {
            if (_candidateCount >= 10) return;

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var entry = new LogEntry
                {
                    Time = DateTime.Now.ToString("HH:mm:ss"),
                    RawKeyHex = (data != null && data.Length > 10) ? data[10].ToString("X2") : "-",
                    RawBytes = data,
                    Data = $"{hex} (Candidate Signal (Double-click to map))",
                    Foreground = System.Windows.Media.Brushes.Blue
                };
                LogEntries.Add(entry);
                if (LogEntries.Count > 1000) LogEntries.RemoveAt(0);
            });

            _candidateCount++;
            if (_candidateCount >= 10)
            {
                ManualDetectButtonText = LanguageManager.GetString(_settings.Language, "MsgDetectionDone");
            }
        }

        private ButtonConfig FindMappedButton(string signature)
        {
            var btn = _settings.Buttons.FirstOrDefault(b => b.TriggerPattern == signature && b.Layer == CurrentLayer);
            if (btn == null)
                btn = _settings.Buttons.FirstOrDefault(b => b.TriggerPattern == signature);
            return btn;
        }

        private int ExecuteButtonLogic(ButtonConfig btn, out bool? micState)
        {
            int newLayer = CurrentLayer;
            micState = null;

            if (btn.TargetLayer >= 0 && btn.TargetLayer <= 4)
            {
                newLayer = btn.TargetLayer;
            }
            else if (btn.TargetLayer == InputExecutor.LAYER_MIC_MUTE)
            {
                micState = _audioController.ToggleMicMute();
            }
            else if ((btn.TargetLayer >= 101 && btn.TargetLayer <= 106) || 
                     btn.TargetLayer == InputExecutor.ACTION_ACTIVE_VOL_UP || 
                     btn.TargetLayer == InputExecutor.ACTION_ACTIVE_VOL_DOWN)
            {
                _inputExecutor.ExecuteMediaKey(btn.TargetLayer, btn.Index);
            }
            else if ((btn.TargetLayer == InputExecutor.ACTION_TEXT_MACRO || btn.TargetLayer == InputExecutor.ACTION_TEXT_MACRO_CLIPBOARD) && !string.IsNullOrEmpty(btn.ProgramPath))
            {
                _inputExecutor.ExecuteMacro(btn.ProgramPath, btn.TargetLayer == InputExecutor.ACTION_TEXT_MACRO_CLIPBOARD);
            }
            else if (btn.TargetLayer == InputExecutor.ACTION_AUDIO_CYCLE)
            {
                string newDeviceName = _audioController.CycleAudioDevice();
                if (!string.IsNullOrEmpty(newDeviceName))
                {
                    btn.Name = newDeviceName;
                    AppSettings.Save(_settings);
                    RequestOsdUpdate?.Invoke();
                }
            }
            else if (btn.TargetLayer == 300 /* ACTION_OSD_CYCLE */)
            {
                CycleOsdMode(btn);
            }
            else if (btn.TargetLayer == 301 /* ACTION_SWITCH_PROFILE */)
            {
                if (!string.IsNullOrEmpty(btn.ProgramPath))
                {
                    var profile = AppProfiles.FirstOrDefault(p => p.Name == btn.ProgramPath);
                    if (profile != null) ActivateVirtualProfile(profile);
                }
            }
            else if (btn.TargetLayer == 302 /* ACTION_PROFILE_CYCLE */)
            {
                CycleAppProfiles();
            }
            // [수정] 가상 버튼(Layer -1)일 경우 레이어 변경 로직을 수행하지 않음
            else if (btn.Layer != -1 && CurrentLayer != btn.Layer)
            {
                newLayer = btn.Layer;
            }

            if (!string.IsNullOrEmpty(btn.ProgramPath) && btn.TargetLayer != InputExecutor.ACTION_TEXT_MACRO)
            {
                _inputExecutor.ExecuteProgram(btn.ProgramPath, btn.IconPath);
            }

            return newLayer;
        }

        private void CycleOsdMode(ButtonConfig btn = null)
        {
            int nextMode = 0;
            string modeName = "";

            if (_settings.OsdMode == 3) nextMode = 1;
            else if (_settings.OsdMode == 1) nextMode = 0;
            else nextMode = 3;

            if (nextMode == 3) modeName = LanguageManager.GetString(_settings.Language, "ModeBottom");
            else if (nextMode == 1) modeName = LanguageManager.GetString(_settings.Language, "ModeOn");
            else modeName = LanguageManager.GetString(_settings.Language, "ModeAuto");

            if (btn != null)
            {
                btn.Name = modeName;
            }

            _settings.OsdMode = nextMode;
            AppSettings.Save(_settings);

            RequestOsdUpdate?.Invoke();
            RequestOsdFeedback?.Invoke(modeName, null, -1);

            AddLog($"[OSD] Mode changed to {nextMode} ({modeName})");
        }

        private void CycleAppProfiles()
        {
            if (AppProfiles == null || AppProfiles.Count == 0) return;

            int nextIndex = 0;
            // 현재 가상 레이어 모드이고 프로필이 선택되어 있다면 다음 인덱스 계산
            if (IsVirtualLayerMode && SelectedAppProfile != null)
            {
                int currentIndex = AppProfiles.IndexOf(SelectedAppProfile);
                if (currentIndex >= 0)
                {
                    nextIndex = (currentIndex + 1) % AppProfiles.Count;
                }
            }
            
            ActivateVirtualProfile(AppProfiles[nextIndex]);
        }

        private void ChangeLayer(int newLayer)
        {
            CurrentLayer = newLayer;
            RequestLayerChange?.Invoke(newLayer);
        }

        private void HandleLayerChangeRequest(int layer)
        {
            if (_layerSyncDebounceTimer == null)
            {
                AddLog($"[Sync] Device Layer Change Request to {layer}");
                if (CurrentLayer != layer) ChangeLayer(layer);

                _layerSyncDebounceTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
                _layerSyncDebounceTimer.Tick += (s, args) =>
                {
                    _layerSyncDebounceTimer.Stop();
                    _layerSyncDebounceTimer = null;
                };
                _layerSyncDebounceTimer.Start();
            }
        }

        private void HandleOsdAndLog(byte[] data, string hex, ButtonConfig btn, bool? micState)
        {
            if (btn.Index >= 1 && btn.Index <= 12)
            {
                RequestOsdHighlight?.Invoke(btn.Index, micState);
            }

            if (hex.StartsWith("C6")) return;

            string hint = "";
            if (data.Length > 10 && data[8] == 0x81) hint = " (Key Up?)";

            AddLog($"[L{CurrentLayer}] [Key {btn.Index}] Matched{hint} | {hex}");
        }

        private void LogUnknownSignal(byte[] data, string hex)
        {
            if (data.Length > 10 && data[10] >= 0xC0)
            {
                AddLog($"[System] Keep-alive ({data[10]:X2}) | {hex}");
                return;
            }

            string hint = (data.Length > 10 && data[8] == 0x81) ? " (Key Up?)" : "";
            AddLog($"[L{CurrentLayer}] Unknown Signal{hint} | {hex}");
        }

        public void PerformAutoMapping(string signature)
        {
            if (SelectedSlot == null)
            {
                AddLog("[Error] 매핑할 슬롯이 선택되지 않았습니다.");
                return;
            }

            // [수정] 가상 레이어 모드일 경우: 가상 프로필에만 키 매핑 저장 (하드웨어 레이어 영향 없음)
            if (IsVirtualLayerMode && SelectedAppProfile != null)
            {
                // 현재 프로필 내에서 동일한 신호를 가진 키가 있다면 초기화 (중복 방지)
                foreach (var b in SelectedAppProfile.Buttons)
                {
                    if (b.TriggerPattern == signature) b.TriggerPattern = null;
                }

                var vBtn = SelectedAppProfile.Buttons.FirstOrDefault(b => b.Index == SelectedSlot.Index);
                if (vBtn != null)
                {
                    vBtn.TriggerPattern = signature;
                    // 가상 버튼은 이미 ViewModel과 바인딩되어 기능(TargetLayer)이 설정되어 있음
                    
                    RequestOsdUpdate?.Invoke();
                    RequestOsdHighlight?.Invoke(SelectedSlot.Index, null);
                    LoadKeySlotsFromProfile(SelectedAppProfile); // VM 갱신
                    
                    RequestAutoMappingConfirmation?.Invoke(signature);
                    StopDetection();
                }
                return;
            }

            foreach (var b in _settings.Buttons)
            {
                if (b.Layer == CurrentLayer && b.TriggerPattern == signature) b.TriggerPattern = null;
            }

            var btn = _settings.Buttons.FirstOrDefault(b => b.Layer == CurrentLayer && b.Index == SelectedSlot.Index);
            if (btn != null)
            {
                btn.TriggerPattern = signature;
                
                // [수정] 가상 레이어 모드일 때는 하드웨어 버튼의 기능(TargetLayer)을 덮어쓰지 않음
                // 오직 물리적 키 매핑(TriggerPattern)만 연결하여, 하드웨어 레이어의 기존 기능은 보존
                if (!IsVirtualLayerMode)
                {
                    btn.TargetLayer = SelectedSlot.TargetLayer;
                }

                // [수정] 매핑 시마다 자동 저장하지 않음 (메모리에만 적용)
                // 사용자가 '저장' 버튼을 누르거나 프로그램 종료 시 일괄 저장됨

                RequestOsdUpdate?.Invoke();
                RequestOsdHighlight?.Invoke(SelectedSlot.Index, null);
                
                // [수정] 가상 레이어 모드일 경우 해당 프로필 화면 유지
                if (IsVirtualLayerMode && SelectedAppProfile != null)
                {
                    LoadKeySlotsFromProfile(SelectedAppProfile);
                }
                else
                {
                    LoadKeySlotsForLayer(CurrentLayer);
                }

                RequestAutoMappingConfirmation?.Invoke(signature);
                StopDetection();
            }
        }

        public void ToggleAutoDetect()
        {
            if (IsAutoDetecting)
            {
                StopDetection();
                return;
            }
            StopDetection();
            IsAutoDetecting = true;
            _ignoredSignatures.Clear();
            lock (_packetLock)
            {
                var now = DateTime.Now;
                foreach (var item in _recentPackets)
                {
                    if ((now - item.Item2).TotalSeconds > 1.0) continue;
                    byte[] packet = item.Item1;
                    if (packet.Length > 10)
                    {
                        _ignoredSignatures.Add(packet[10].ToString("X2"));
                    }
                }
            }
            AutoDetectButtonText = LanguageManager.GetString(_settings.Language, "MsgDetecting");
            AddLog("--- Instant Detect Started (Waiting for input) ---");
        }

        public void ToggleManualDetect()
        {
            if (IsManualDetecting)
            {
                StopDetection();
                return;
            }
            StopDetection();
            IsManualDetecting = true;
            _candidateCount = 0;
            LogEntries.Clear();
            lock (_packetLock)
            {
                _recentPackets.Clear();
            }
            ManualDetectButtonText = LanguageManager.GetString(_settings.Language, "MsgDetecting");
            AddLog("--- Manual Detect Started (Press keys) ---");
        }

        private void StopDetection()
        {
            IsAutoDetecting = false;
            IsManualDetecting = false;
            AutoDetectButtonText = LanguageManager.GetString(_settings.Language, "BtnAutoDetect");
            ManualDetectButtonText = LanguageManager.GetString(_settings.Language, "BtnManualDetect");
        }

        // [수정] 활성 창 변경 처리 (아이콘 업데이트 및 프로필 자동 전환)
        public void HandleActiveWindowChange(string processPath)
        {
            // 1. 아이콘 업데이트 (기존 로직 유지)
            bool iconUpdated = false;

            // 현재 레이어의 버튼 중 '활성 창 볼륨 조절' 기능이 있는 버튼 찾기
            // 110: Active Vol Up, 111: Active Vol Down
            var activeVolButtons = _settings.Buttons
                .Where(b => b.Layer == CurrentLayer && (b.TargetLayer == 110 || b.TargetLayer == 111))
                .ToList();

            foreach (var btn in activeVolButtons)
            {
                if (btn.IconPath != processPath)
                {
                    btn.IconPath = processPath;
                    iconUpdated = true;
                }
            }

            if (iconUpdated)
            {
                // OSD 화면 갱신 요청
                System.Windows.Application.Current.Dispatcher.Invoke(() => RequestOsdUpdate?.Invoke());
            }

            // 2. 가상 레이어(앱 프로필) 자동 전환 로직
            if (!_settings.EnableAppProfiles) return;

            string processName = System.IO.Path.GetFileName(processPath);
            if (string.IsNullOrEmpty(processName)) return;

            // 매칭되는 프로필 검색
            var profile = _settings.AppProfiles.FirstOrDefault(p => p.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase));

            if (profile != null)
            {
                // 매칭 성공 -> 해당 프로필 활성화
                ActivateVirtualProfile(profile);
            }
            else
            {
                // 매칭 실패 -> Fallback 확인
                if (!string.IsNullOrEmpty(_settings.FallbackProfileId))
                {
                    var fallback = _settings.AppProfiles.FirstOrDefault(p => p.Name == _settings.FallbackProfileId);
                    if (fallback != null)
                    {
                        ActivateVirtualProfile(fallback);
                        return;
                    }
                }

                // Fallback도 없으면 -> 하드웨어 레이어로 복귀
                if (IsVirtualLayerMode)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() => IsVirtualLayerMode = false);
                }
            }
        }

        private void ActivateVirtualProfile(AppProfile profile)
        {
            if (IsVirtualLayerMode && SelectedAppProfile == profile) return;

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                SelectedAppProfile = profile; // 이 설정이 IsVirtualLayerMode = true로 만들고 슬롯을 로드함
                RequestOsdUpdate?.Invoke();
                RequestOsdFeedback?.Invoke(profile.Name, null, -1); // 프로필 이름 OSD 표시
            });
        }

        #endregion
    }
}