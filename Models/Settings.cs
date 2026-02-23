using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json; // System.Text.Json 패키지 필요 (최신 .NET 기본 포함)
using System.Windows.Media; // For Brush
using SayoOSD.Models;

namespace SayoOSD.Models
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

    // [추가] 하드웨어 레이어별 색상 스타일
    public class LayerStyle
    {
        public string BackgroundColor { get; set; }
        public string HighlightColor { get; set; }
        public string BorderColor { get; set; }
        public string FontFamily { get; set; } // [추가] 폰트
        public double? FontSize { get; set; }  // [추가] 크기
        public string FontWeight { get; set; } // [추가] 굵기
    }

    public class ButtonConfig
    {
        public int Index { get; set; } // 1 ~ 12
        public int Layer { get; set; } = 0; // 0 ~ 4 (Fn0 ~ Fn4)
        public int TargetLayer { get; set; } = -1; // -1: 이동 없음, 0~4: 레이어 이동, 99: 마이크 음소거
        public string Name { get; set; } = "Button";
        public string TriggerPattern { get; set; } // 매핑된 신호 패턴 (Hex String)
        public string ProgramPath { get; set; } // 실행할 프로그램 경로
        public string IconPath { get; set; } // [추가] 아이콘 경로 (별도 지정 시)
    }

    public class AppProfile
    {
        public string Name { get; set; } = "New Profile";
        public string ProcessName { get; set; } = ""; // 실행 파일명 (예: chrome.exe)
        public string ExecutablePath { get; set; } // [추가] 실행 파일 전체 경로 (아이콘 추출용)
        public double? CustomOsdLeft { get; set; } // [추가] 프로필별 OSD X 위치 (null이면 전역 설정 사용)
        public double? CustomOsdTop { get; set; }  // [추가] 프로필별 OSD Y 위치 (null이면 전역 설정 사용)
        public string CustomOsdBackgroundColor { get; set; } // [추가] 프로필별 배경색 (Hex)
        public string CustomOsdHighlightColor { get; set; } // [추가] 프로필별 강조색 (Hex)
        public string CustomOsdBorderColor { get; set; } // [추가] 프로필별 테두리색 (Hex)
        public string CustomOsdFontFamily { get; set; } // [추가] 프로필별 폰트
        public double? CustomOsdFontSize { get; set; }  // [추가] 프로필별 폰트 크기
        public string CustomOsdFontWeight { get; set; } // [추가] 프로필별 폰트 굵기
        public List<ButtonConfig> Buttons { get; set; } = new List<ButtonConfig>();

        public AppProfile()
        {
            // 가상 레이어용 버튼 12개 초기화
            for (int i = 1; i <= 12; i++)
            {
                // Layer 값을 -1로 설정하여 하드웨어 레이어(0~4)와 구분
                Buttons.Add(new ButtonConfig { Index = i, Layer = -1, Name = $"Key {i}" });
            }
        }
    }

    public class AppSettings
    {
        public string DeviceVid { get; set; } = "8089"; // SayoDevice 기본 VID (확인 필요)
        public string DevicePid { get; set; } = "000B"; // 사용자 요청 PID (000B)
        public double OsdOpacity { get; set; } = 0.8;   // 투명도 (0.1 ~ 1.0)
        public double OsdTimeout { get; set; } = 3.0;   // 표시 시간 (초)
        public int OsdMode { get; set; } = 0;           // 0:자동, 1:항상켜기, 2:항상끄기
        public double OsdTop { get; set; } = -1;        // OSD 창 Y 위치 (-1이면 기본값)
        public double OsdLeft { get; set; } = -1;       // OSD 창 X 위치 (-1이면 기본값)
        public double OsdWidth { get; set; } = -1;      // OSD 창 너비 (-1이면 기본값)
        public double OsdHeight { get; set; } = -1;     // OSD 창 높이 (-1이면 기본값)
        public int OsdBackgroundAlpha { get; set; } = 50; // 배경 투명도 (0~255)
        public bool OsdVertical { get; set; } = false;  // [추가] 세로 모드
        public bool OsdSwapRows { get; set; } = false;  // [추가] 1번줄/7번줄 위치 교체
        public int LastLayerIndex { get; set; } = 0;    // 마지막 사용 레이어 (0~4)
        public string Language { get; set; } = "KO";    // 언어 설정 (KO/EN)
        public bool EnableFileLog { get; set; } = false; // 로그 파일 저장 여부
        public int VolumeStep { get; set; } = 2;        // [추가] 볼륨 조절 단위 (기본 2)
        public string OsdBackgroundColor { get; set; } = "#32FFFFFF"; // [추가] 기본 배경색 (Alpha 50 White)
        public string OsdHighlightColor { get; set; } = "#FF007ACC"; // [추가] 기본 강조색 (Blue)
        public string OsdBorderColor { get; set; } = "#FFFFFF00"; // [추가] 기본 테두리색 (Yellow)
        public string OsdFontFamily { get; set; } = "Segoe UI"; // [추가] 기본 폰트
        public double OsdFontSize { get; set; } = 12.0; // [추가] 기본 폰트 크기
        public string OsdFontWeight { get; set; } = "Normal"; // [추가] 기본 폰트 굵기
        public double PaletteFontSize { get; set; } = 12.0; // [추가] 기능 팔레트 폰트 크기

        // [추가] 가상 레이어(앱 프로필) 관련 설정
        public bool EnableAppProfiles { get; set; } = false; // 가상 레이어 기능 활성화 여부
        public string FallbackProfileId { get; set; } = ""; // 매칭되는 앱이 없을 때 사용할 기본 프로필 이름 (비어있으면 하드웨어 레이어 유지)
        public string LastVirtualProfileName { get; set; } // [추가] 마지막으로 선택된 가상 프로필 이름 (상태 저장용)
        public List<AppProfile> AppProfiles { get; set; } = new List<AppProfile>();

        public List<LayerStyle> LayerStyles { get; set; } = new List<LayerStyle>(); // [추가] 레이어별 색상 (0~4)

        public List<ButtonConfig> Buttons { get; set; } = new List<ButtonConfig>();

        // [추가] 설정 저장 알림 이벤트
        public static event Action OnSettingsSaved;

        public AppSettings()
        {
            // 5개 레이어(0~4) * 12개 버튼 초기화
            for (int l = 0; l < 5; l++)
            {
                for (int i = 1; i <= 12; i++)
                {
                    Buttons.Add(new ButtonConfig { Index = i, Layer = l, Name = $"Key {i}" });
                }
            }

            // [추가] 레이어 스타일 초기화
            for (int i = 0; i < 5; i++)
            {
                LayerStyles.Add(new LayerStyle());
            }
        }

        public static void Save(AppSettings settings, string path = null)
        {
            if (string.IsNullOrEmpty(path))
                path = Path.Combine(AppContext.BaseDirectory, "settings.json");

            var options = new JsonSerializerOptions { WriteIndented = true };
            string jsonString = JsonSerializer.Serialize(settings, options);
            File.WriteAllText(path, jsonString);

            // [추가] 저장 이벤트 발생
            OnSettingsSaved?.Invoke();
        }

        public static AppSettings Load(string path = null)
        {
            if (string.IsNullOrEmpty(path))
                path = Path.Combine(AppContext.BaseDirectory, "settings.json");

            if (!File.Exists(path)) return new AppSettings();
            string jsonString = File.ReadAllText(path);
            var settings = JsonSerializer.Deserialize<AppSettings>(jsonString) ?? new AppSettings();

            // 로드 후 누락된 레이어/버튼이 있으면 추가 (기존 설정 파일 호환성)
            for (int l = 0; l < 5; l++)
            {
                for (int i = 1; i <= 12; i++)
                {
                    if (!settings.Buttons.Exists(b => b.Layer == l && b.Index == i))
                    {
                        settings.Buttons.Add(new ButtonConfig { Index = i, Layer = l, Name = $"Key {i}" });
                    }
                }
            }

            // [추가] 레이어 스타일 데이터 보정 (구버전 호환)
            if (settings.LayerStyles == null) settings.LayerStyles = new List<LayerStyle>();
            while (settings.LayerStyles.Count < 5)
            {
                settings.LayerStyles.Add(new LayerStyle());
            }

            return settings;
        }
    }
}
