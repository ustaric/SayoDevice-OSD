using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Media.Imaging; // For ImageSource
using SayoOSD.Models;
using SayoOSD.Helpers;
using SayoOSD.ViewModels;
using SayoOSD.Services; // InputExecutor

namespace SayoOSD.ViewModels
{
    /// <summary>
    /// MVVM 패턴의 일부로, 각 키 슬롯의 UI 상태와 데이터를 나타내는 ViewModel입니다.
    /// ButtonConfig 모델을 감싸고, IsSelected와 같은 UI 상태를 추가로 가집니다.
    /// </summary>
    public class KeySlotViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // 데이터 모델인 ButtonConfig를 내부적으로 가집니다.
        private readonly ButtonConfig _config;

        public KeySlotViewModel(ButtonConfig config)
        {
            _config = config;
            UpdateColor();
        }

        /// <summary>
        /// 키 슬롯의 고유 인덱스 (1~12)
        /// </summary>
        public int Index => _config.Index;

        /// <summary>
        /// 키 슬롯에 표시될 이름
        /// </summary>
        public string Name
        {
            get => _config.Name;
            set
            {
                if (_config.Name != value)
                {
                    _config.Name = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isSelected;
        /// <summary>
        /// 현재 이 키 슬롯이 UI에서 선택되었는지 여부
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isActive;
        /// <summary>
        /// 물리적 키가 눌렸을 때 잠시 활성화되는 효과를 위한 속성
        /// </summary>
        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive != value)
                {
                    _isActive = value;
                    OnPropertyChanged();
                }
            }
        }

        private System.Windows.Media.Brush _displayColor;
        /// <summary>
        /// 키 슬롯의 배경색 (예: 레이어 이동 키, 선택된 키 등)
        /// </summary>
        public System.Windows.Media.Brush DisplayColor
        {
            get => _displayColor;
            set
            {
                if (_displayColor != value)
                {
                    _displayColor = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 버튼의 기능(TargetLayer)
        /// </summary>
        public int TargetLayer
        {
            get => _config.TargetLayer;
            set
            {
                if (_config.TargetLayer != value)
                {
                    _config.TargetLayer = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsRunMode));
                    OnPropertyChanged(nameof(IsMacroMode));
                    OnPropertyChanged(nameof(UseClipboard)); // [추가]
                    UpdateColor();
                }
            }
        }

        /// <summary>
        /// 프로그램 실행 모드 여부 (UI 바인딩용)
        /// </summary>
        public bool IsRunMode => _config.TargetLayer == InputExecutor.ACTION_RUN_PROGRAM;

        /// <summary>
        /// 텍스트 매크로 모드 여부 (UI 바인딩용)
        /// </summary>
        public bool IsMacroMode => _config.TargetLayer == InputExecutor.ACTION_TEXT_MACRO || _config.TargetLayer == InputExecutor.ACTION_TEXT_MACRO_CLIPBOARD;

        /// <summary>
        /// [추가] 매크로 입력 시 클립보드 사용 여부
        /// </summary>
        public bool UseClipboard
        {
            get => _config.TargetLayer == InputExecutor.ACTION_TEXT_MACRO_CLIPBOARD;
            set
            {
                if (IsMacroMode && UseClipboard != value)
                {
                    TargetLayer = value ? InputExecutor.ACTION_TEXT_MACRO_CLIPBOARD : InputExecutor.ACTION_TEXT_MACRO;
                }
            }
        }

        /// <summary>
        /// 실행할 프로그램 경로 (전체 경로 + 인수)
        /// </summary>
        public string ProgramPath
        {
            get => _config.ProgramPath;
            set
            {
                if (_config.ProgramPath != value)
                {
                    _config.ProgramPath = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FilePath));
                    OnPropertyChanged(nameof(Arguments));
                    OnPropertyChanged(nameof(IconSource)); // 경로 변경 시 아이콘도 변경될 수 있음
                }
            }
        }

        /// <summary>
        /// UI 바인딩용: 파일 경로만 분리
        /// </summary>
        public string FilePath
        {
            get
            {
                ParsePathAndArgs(_config.ProgramPath, out string path, out string args);
                return path;
            }
            set
            {
                ParsePathAndArgs(_config.ProgramPath, out string path, out string args);
                if (path != value)
                {
                    UpdateProgramPath(value, args);
                }
            }
        }

        /// <summary>
        /// UI 바인딩용: 인수만 분리
        /// </summary>
        public string Arguments
        {
            get
            {
                ParsePathAndArgs(_config.ProgramPath, out string path, out string args);
                return args;
            }
            set
            {
                ParsePathAndArgs(_config.ProgramPath, out string path, out string args);
                if (args != value)
                {
                    UpdateProgramPath(path, value);
                }
            }
        }

        public string IconPath
        {
            get => _config.IconPath;
            set
            {
                if (_config.IconPath != value)
                {
                    _config.IconPath = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IconSource));
                }
            }
        }

        public ImageSource IconSource
        {
            get
            {
                if (!string.IsNullOrEmpty(IconPath)) return IconHelper.GetIconFromPath(IconPath);
                if (!string.IsNullOrEmpty(FilePath)) return IconHelper.GetIconFromPath(FilePath);
                return null;
            }
        }

        /// <summary>
        /// 버튼 설정에 따라 배경색을 업데이트합니다.
        /// </summary>
        public void UpdateColor()
        {
            bool isLayerMove = (_config.TargetLayer >= 0 && _config.TargetLayer <= 4) || _config.TargetLayer == 99; // 99: Mic Mute
            if (isLayerMove) DisplayColor = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xD0, 0xF0, 0xFF));
            else DisplayColor = System.Windows.Media.Brushes.LightGray;
        }

        private void UpdateProgramPath(string path, string args)
        {
            if (!string.IsNullOrEmpty(args))
            {
                if (path.Contains(" ") && !path.StartsWith("\"")) path = $"\"{path}\"";
                ProgramPath = $"{path} {args}";
            }
            else
            {
                ProgramPath = path;
            }
        }

        private void ParsePathAndArgs(string fullPath, out string path, out string args)
        {
            path = ""; args = "";
            if (string.IsNullOrEmpty(fullPath)) return;
            
            if (fullPath.StartsWith("\"")) {
                int end = fullPath.IndexOf("\"", 1);
                if (end > 0) { path = fullPath.Substring(1, end - 1); if (end + 1 < fullPath.Length) args = fullPath.Substring(end + 1).Trim(); return; }
            }
            int exeIdx = fullPath.IndexOf(".exe", System.StringComparison.OrdinalIgnoreCase);
            if (exeIdx > 0) {
                int split = exeIdx + 4;
                if (split < fullPath.Length && fullPath[split] == ' ') { path = fullPath.Substring(0, split); args = fullPath.Substring(split).Trim(); return; }
            }
            path = fullPath;
        }
    }
}