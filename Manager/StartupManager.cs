using System;
using System.Diagnostics;
using System.Security.Principal;
using SayoOSD.Managers;

namespace SayoOSD.Managers
{
    /// <summary>
    /// 작업 스케줄러를 사용하여 프로그램의 관리자 권한 자동 시작을 관리합니다.
    /// </summary>
    public static class StartupManager
    {
        private const string TaskName = "SayoOSD";

        /// <summary>
        /// schtasks.exe를 실행하고 결과를 반환합니다.
        /// </summary>
        private static bool RunSchTasks(string args)
        {
            var psi = new ProcessStartInfo("schtasks.exe", args)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardError = true // 에러 확인용
            };

            using (var process = Process.Start(psi))
            {
                process.WaitForExit();
                return process.ExitCode == 0;
            }
        }

        /// <summary>
        /// 자동 시작 작업이 등록되어 있는지 확인합니다.
        /// </summary>
        public static bool IsStartupTaskEnabled()
        {
            // /Query 명령으로 해당 이름의 작업이 있는지 확인
            return RunSchTasks($"/Query /TN \"{TaskName}\"");
        }

        /// <summary>
        /// 자동 시작 작업을 등록하거나 해제합니다.
        /// </summary>
        public static void SetStartup(bool enable)
        {
            if (enable) CreateStartupTask();
            else DeleteStartupTask();
        }

        private static void CreateStartupTask()
        {
            string exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath)) return;

            // 1. 실행 경로와 인자 설정 (전체를 따옴표로 감싸서 공백 문제 해결)
            string taskAction = $"\"{exePath}\" --tray";

            // 2. 관리자 권한 앱이므로 무조건 /RL HIGHEST 사용
            // /Create: 생성, /F: 강제(기존 작업 덮어쓰기), /SC ONLOGON: 로그인 시 실행
            // /TN: 작업 이름, /TR: 실행할 경로 및 인자, /RL: 권한 수준
            string args = $"/Create /F /TN \"{TaskName}\" /TR \"{taskAction}\" /SC ONLOGON /RL HIGHEST";

            if (RunSchTasks(args))
            {
                // 3. 추가 설정: 배터리 모드에서도 실행되도록 설정 변경 (노트북 사용자 필수)
                // 작업 스케줄러 기본값은 'AC 전원이 연결된 경우에만 실행'이기 때문에 이를 꺼줘야 합니다.
                RunSchTasks($"/Change /TN \"{TaskName}\" /SET /AllowStartOnBatteries");
                RunSchTasks($"/Change /TN \"{TaskName}\" /SET /StopIfGoingOnBatteries");
            }
        }

        private static void DeleteStartupTask()
        {
            // /Delete: 삭제, /F: 확인 절차 없이 강제 삭제
            RunSchTasks($"/Delete /F /TN \"{TaskName}\"");
        }
    }
}