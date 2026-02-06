﻿﻿﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json; // System.Text.Json 패키지 필요 (최신 .NET 기본 포함)

namespace SayoOSD
{
    public class ButtonConfig
    {
        public int Index { get; set; } // 1 ~ 12
        public int Layer { get; set; } = 0; // 0 ~ 4 (Fn0 ~ Fn4)
        public int TargetLayer { get; set; } = -1; // -1: 이동 없음, 0~4: 해당 레이어로 이동
        public string Name { get; set; } = "Button";
        public string TriggerPattern { get; set; } // 매핑된 신호 패턴 (Hex String)
    }

    public class AppSettings
    {
        public string DeviceVid { get; set; } = "8089"; // SayoDevice 기본 VID (확인 필요)
        public string DevicePid { get; set; } = "000B"; // 사용자 요청 PID (000B)
        public double OsdOpacity { get; set; } = 0.8;   // 투명도 (0.1 ~ 1.0)
        public int OsdTimeout { get; set; } = 3;        // 표시 시간 (초)
        public int OsdMode { get; set; } = 0;           // 0:자동, 1:항상켜기, 2:항상끄기
        public double OsdTop { get; set; } = -1;        // OSD 창 Y 위치 (-1이면 기본값)
        public double OsdLeft { get; set; } = -1;       // OSD 창 X 위치 (-1이면 기본값)
        public double OsdWidth { get; set; } = -1;      // OSD 창 너비 (-1이면 기본값)
        public double OsdHeight { get; set; } = -1;     // OSD 창 높이 (-1이면 기본값)
        public int OsdBackgroundAlpha { get; set; } = 50; // 배경 투명도 (0~255)
        public int LastLayerIndex { get; set; } = 0;    // 마지막 사용 레이어 (0~4)
        public string Language { get; set; } = "KO";    // 언어 설정 (KO/EN)
        public bool EnableFileLog { get; set; } = false; // 로그 파일 저장 여부
        public List<ButtonConfig> Buttons { get; set; } = new List<ButtonConfig>();

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
        }

        public static void Save(AppSettings settings, string path = null)
        {
            if (string.IsNullOrEmpty(path))
                path = Path.Combine(AppContext.BaseDirectory, "settings.json");

            var options = new JsonSerializerOptions { WriteIndented = true };
            string jsonString = JsonSerializer.Serialize(settings, options);
            File.WriteAllText(path, jsonString);
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
            return settings;
        }
    }
}
