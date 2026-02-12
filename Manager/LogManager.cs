using System;
using System.IO;
using SayoOSD.Managers;

namespace SayoOSD.Managers
{
    public static class LogManager
    {
        // 로그 저장 활성화 여부
        public static bool Enabled { get; set; } = false;

        public static void Write(string message)
        {
            if (!Enabled) return;

            try
            {
                string path = Path.Combine(AppContext.BaseDirectory, "log.txt");
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                File.AppendAllText(path, $"[{timestamp}] {message}{Environment.NewLine}");
            }
            catch { /* 파일 쓰기 실패 시 무시 (충돌 방지) */ }
        }
    }
}