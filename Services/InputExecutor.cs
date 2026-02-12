using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using SayoOSD.Services;
using NAudio.CoreAudioApi; // [추가] NAudio 사용
using SayoOSD.Managers; // [추가] LanguageManager 사용

namespace SayoOSD.Services
{
    public class InputExecutor
    {
        // Win32 API: 키보드 이벤트 시뮬레이션
        [DllImport("user32.dll")]
        static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        // [추가] 창 제어 API
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetShellWindow();

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("kernel32.dll")]
        static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("System.Windows.Forms.dll")]
        private static extern void SendKeys_SendWait(string keys);

        private const int SW_RESTORE = 9;
        private const int SW_MINIMIZE = 6;
        // 상수 정의 (MainWindow에서 이동됨)
        public const int ACTION_RUN_PROGRAM = 200;
        public const int ACTION_TEXT_MACRO = 201;
        public const int ACTION_AUDIO_CYCLE = 202; // [추가]
        public const int LAYER_MIC_MUTE = 99;      // [추가]
        
        public const int ACTION_MEDIA_PLAYPAUSE = 101;
        public const int ACTION_MEDIA_NEXT = 102;
        public const int ACTION_MEDIA_PREV = 103;
        public const int ACTION_VOL_UP = 104;
        public const int ACTION_VOL_DOWN = 105;
        public const int ACTION_VOL_MUTE = 106;
        public const int ACTION_ACTIVE_VOL_UP = 110;   // [추가] 활성 창 볼륨 증가
        public const int ACTION_ACTIVE_VOL_DOWN = 111; // [추가] 활성 창 볼륨 감소

        // [추가] 전역 볼륨 조절 단위 (기본 2) - MainWindow에서 설정값 동기화
        public static int GlobalVolumeStep = 2;
        
        // [추가] 현재 언어 설정 (OSD 피드백용)
        public static string CurrentLanguage = "KO";

        // 로그 전달 이벤트
        public event Action<string> LogMessage;
        public event Action<string, string, int> OsdFeedbackRequested; // [수정] OSD 피드백 요청 이벤트 (메시지, 아이콘경로, 키인덱스)

        // [추가] 전역 로그 전달 이벤트 (MainWindow에서 구독하여 로그 출력)
        public static event Action<string> GlobalLogMessage;

        // [추가] 내부 로그 헬퍼
        private void Log(string msg)
        {
            LogMessage?.Invoke(msg);
            GlobalLogMessage?.Invoke(msg);
        }

        // 미디어 키 실행
        public void ExecuteMediaKey(int actionType, int keyIndex = -1)
        {
            // [디버그] 활성 창 볼륨 조절 호출 여부 확인 로그
            if (actionType == ACTION_ACTIVE_VOL_UP || actionType == ACTION_ACTIVE_VOL_DOWN)
            {
                Log($"[Debug] ExecuteMediaKey Called: {actionType}");
            }

            byte vkCode = 0;
            string actionName = "Media Key";

            switch (actionType)
            {
                case ACTION_MEDIA_PLAYPAUSE: 
                    vkCode = 0xB3; // VK_MEDIA_PLAY_PAUSE
                    actionName = "Play/Pause"; 
                    break;
                case ACTION_MEDIA_NEXT: 
                    vkCode = 0xB0; // VK_MEDIA_NEXT_TRACK
                    break;
                case ACTION_MEDIA_PREV: 
                    vkCode = 0xB1; // VK_MEDIA_PREV_TRACK
                    break;
                case ACTION_VOL_MUTE: 
                    vkCode = 0xAD; // VK_VOLUME_MUTE
                    break;
                case ACTION_VOL_DOWN: 
                    vkCode = 0xAE; // VK_VOLUME_DOWN
                    actionName = "Vol -"; 
                    break;
                case ACTION_VOL_UP: 
                    vkCode = 0xAF; // VK_VOLUME_UP
                    actionName = "Vol +"; 
                    break;
                case ACTION_ACTIVE_VOL_UP:
                    AdjustActiveWindowVolume(true, keyIndex);
                    return; // 별도 로직 처리 후 종료
                case ACTION_ACTIVE_VOL_DOWN:
                    AdjustActiveWindowVolume(false, keyIndex);
                    return; // 별도 로직 처리 후 종료
            }

            if (vkCode != 0)
            {
                try
                {
                    // [수정] 볼륨 조절의 경우 설정된 Step만큼 반복 입력
                    // 윈도우 기본 볼륨 조절은 1회당 약 2%
                    int repeatCount = 1;
                    if (vkCode == 0xAE || vkCode == 0xAF) // Vol Up/Down
                    {
                        // 0~100 단위이므로 2로 나누어 반복 횟수 계산 (최소 1회)
                        repeatCount = Math.Max(1, GlobalVolumeStep / 2); 
                    }

                    for (int i = 0; i < repeatCount; i++)
                    {
                        keybd_event(vkCode, 0, 0, UIntPtr.Zero);
                        keybd_event(vkCode, 0, 2, UIntPtr.Zero);
                    }

                    // [추가] 볼륨 조절 시 현재 볼륨 OSD 표시
                    if (vkCode == 0xAE || vkCode == 0xAF)
                    {
                        int currentVol = GetMasterVolume();
                        string langKey = (vkCode == 0xAF) ? "ActionVolUp" : "ActionVolDown";
                        string name = LanguageManager.GetString(CurrentLanguage, langKey);
                        OsdFeedbackRequested?.Invoke($"{name} {currentVol}", null, keyIndex);
                    }
                    
                    Log($"[Action] {actionName} Triggered");
                }
                catch (Exception ex)
                {
                    Log($"[Media Error] {ex.Message}");
                }
            }
        }

        // [추가] 시스템 마스터 볼륨 가져오기
        private int GetMasterVolume()
        {
            try
            {
                using (var enumerator = new MMDeviceEnumerator())
                {
                    var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                    return (int)(device.AudioEndpointVolume.MasterVolumeLevelScalar * 100);
                }
            }
            catch { return 0; }
        }

        // [추가] 활성 창 볼륨 조절 로직 (NAudio)
        private void AdjustActiveWindowVolume(bool isUp, int keyIndex)
        {
            // UI 스레드 차단 방지 및 예외 격리를 위해 비동기 실행
            Task.Run(() =>
            {
            try
            {
                Log($"[Debug] AdjustActiveWindowVolume 진입. isUp: {isUp}");

                IntPtr hWnd = GetForegroundWindow();
                if (hWnd == IntPtr.Zero)
                {
                    Log("[ActiveVol] 활성 창 핸들(hWnd)을 찾을 수 없습니다.");
                    return;
                }
                Log($"[Debug] 활성 창 핸들: {hWnd}");

                uint pid;
                GetWindowThreadProcessId(hWnd, out pid);
                Log($"[Debug] 대상 PID: {pid}");
                
                // [추가] 활성 창의 프로세스 이름 가져오기 (PID 매칭 실패 시 이름으로 매칭 시도)
                string procName = "Unknown";
                string iconPath = null; // [추가] 아이콘 경로
                try { 
                    var proc = Process.GetProcessById((int)pid);
                    procName = proc.ProcessName;
                    try { iconPath = proc.MainModule.FileName; } catch { } // 권한 문제로 실패 가능
                    Log($"[Debug] 대상 프로세스 이름: {procName}");
                } catch (Exception pEx) {
                    Log($"[Debug] 프로세스 이름 가져오기 실패: {pEx.Message}");
                }

                var deviceEnumerator = new MMDeviceEnumerator();
                var device = deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                var sessions = device.AudioSessionManager.Sessions;
                
                Log($"[Debug] 현재 오디오 세션 수: {sessions.Count}");

                bool found = false;
                float finalVol = 0f;
                float step = GlobalVolumeStep / 100f; // 예: 5 -> 0.05 (5%)

                for (int i = 0; i < sessions.Count; i++)
                {
                    var session = sessions[i];
                    uint sessionPid = session.GetProcessID;
                    bool match = (sessionPid == pid);

                    // [추가] PID가 달라도 프로세스 이름이 같으면 제어 (Chrome 등 멀티 프로세스 앱 대응)
                    if (!match && !string.IsNullOrEmpty(procName))
                    {
                        try 
                        { 
                            var sessionProc = Process.GetProcessById((int)sessionPid);
                            if (sessionProc.ProcessName.Equals(procName, StringComparison.OrdinalIgnoreCase))
                            {
                                match = true;
                                Log($"[Debug] 이름으로 매칭 성공: {procName} (Session PID: {sessionPid})");
                            }
                        } catch { }
                    }

                    if (match)
                    {
                        float currentVol = session.SimpleAudioVolume.Volume;
                        float newVol = isUp ? currentVol + step : currentVol - step;
                        
                        // 범위 제한 (0.0 ~ 1.0)
                        if (newVol < 0f) newVol = 0f;
                        if (newVol > 1f) newVol = 1f;

                        session.SimpleAudioVolume.Volume = newVol;
                        found = true;
                        finalVol = newVol;
                        
                        Log($"[Debug] 볼륨 변경됨. PID: {sessionPid}, {currentVol:F2} -> {newVol:F2}");
                        
                        // 음소거 상태라면 해제
                        if (session.SimpleAudioVolume.Mute) 
                        {
                            session.SimpleAudioVolume.Mute = false;
                            Log($"[Debug] 음소거 해제됨 (PID: {sessionPid})");
                        }
                    }
                }

                string action = isUp ? "Up" : "Down";
                if (found) 
                {
                    Log($"[ActiveVol] {procName}({pid}) Volume {action} (Step {GlobalVolumeStep}%)");
                    // [추가] OSD 피드백 (활성 창 볼륨)
                    string langKey = isUp ? "ActionActiveVolUp" : "ActionActiveVolDown";
                    string name = LanguageManager.GetString(CurrentLanguage, langKey);
                    OsdFeedbackRequested?.Invoke($"{name} {(int)(finalVol * 100)}", iconPath, keyIndex);
                }
                else 
                    Log($"[ActiveVol] No audio session found for {procName}({pid})");
            }
            catch (Exception ex)
            {
                Log($"[ActiveVol Error] {ex.Message}");
            }
            });
        }

        // 텍스트 매크로 실행
        public void ExecuteMacro(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            try
            {
                // SendKeys를 사용하여 텍스트 입력 시뮬레이션
                System.Windows.Forms.SendKeys.SendWait(text);
                Log($"[Macro] Text Sent: {text}");
            }
            catch (Exception ex)
            {
                Log($"[Macro Error] {ex.Message}");
            }
        }

        // 프로그램 실행
        public void ExecuteProgram(string path, string iconPath = null)
        {
            Task.Run(() =>
            {
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                string exePath = path;
                string args = "";

                // [추가] 실행 파일 경로와 인수 분리 (따옴표 처리 및 .exe 기준)
                if (path.StartsWith("\""))
                {
                    int endQuote = path.IndexOf('\"', 1);
                    if (endQuote > 0)
                    {
                        exePath = path.Substring(1, endQuote - 1);
                        if (endQuote + 1 < path.Length)
                            args = path.Substring(endQuote + 1).Trim();
                    }
                }
                else if (!System.IO.File.Exists(path)) // 파일이 직접 존재하지 않으면 공백 분리 시도
                {
                    int exeIndex = path.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
                    if (exeIndex > 0)
                    {
                        int splitIndex = exeIndex + 4;
                        // .exe 뒤가 끝이거나 공백인 경우 분리
                        if (splitIndex == path.Length || path[splitIndex] == ' ')
                        {
                            exePath = path.Substring(0, splitIndex);
                            if (splitIndex < path.Length)
                                args = path.Substring(splitIndex).Trim();
                        }
                    }
                }

                // [수정] 제어할 프로세스 이름 결정
                // 아이콘 경로가 유효한 .exe 파일이면(예: 게임 실행 파일), 런처 대신 그 프로세스를 제어 대상으로 설정
                string processName;
                if (!string.IsNullOrEmpty(iconPath) && iconPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    processName = System.IO.Path.GetFileNameWithoutExtension(iconPath);
                }
                else
                {
                    processName = System.IO.Path.GetFileNameWithoutExtension(exePath);
                }

                Process[] processes = Process.GetProcessesByName(processName);
                Process target = null;

                // 메인 윈도우 핸들이 있는 프로세스 찾기
                foreach (var p in processes)
                {
                    if (p.MainWindowHandle != IntPtr.Zero)
                    {
                        target = p;
                        break;
                    }
                }

                if (target != null)
                {
                    // [수정] AttachThreadInput을 사용하여 게임 등 다른 프로세스와의 충돌을 최소화
                    IntPtr hWnd = target.MainWindowHandle;
                    IntPtr hForeWnd = GetForegroundWindow();
                    uint dwTargetID = GetWindowThreadProcessId(hWnd, out _);
                    uint dwForeID = GetWindowThreadProcessId(hForeWnd, out _);
                    uint dwCurID = GetCurrentThreadId();

                    try
                    {
                        // 현재 스레드의 입력 처리 메커니즘을 대상 스레드에 연결
                        AttachThreadInput(dwCurID, dwTargetID, true);
                        // 현재 활성 창의 스레드에도 연결하여 전환을 더 부드럽게 함
                        if (hForeWnd != IntPtr.Zero && dwForeID != dwCurID)
                            AttachThreadInput(dwForeID, dwCurID, true);

                        bool isMinimized = IsIconic(hWnd);
                        bool isForeground = (hForeWnd == hWnd);

                        if (isMinimized)
                        {
                            // 최소화 상태 -> 복원 및 활성화
                            ShowWindow(hWnd, SW_RESTORE);
                            SetForegroundWindow(hWnd);
                            Log($"[Run] Restored (Attach): {processName}");
                        }
                        else if (isForeground)
                        {
                            // 활성 상태 -> 최소화
                            ShowWindow(hWnd, SW_MINIMIZE);
                            Log($"[Run] Minimized (Attach): {processName}");
                        }
                        else
                        {
                            // 비활성 상태 -> 활성화
                            SetForegroundWindow(hWnd);
                            Log($"[Run] Activated (Attach): {processName}");
                        }
                    }
                    catch (Exception attachEx)
                    {
                        Log($"[Run Warning] AttachThreadInput Failed: {attachEx.Message}");
                        // 실패 시 기존 방식으로 단순 활성화 시도
                        SetForegroundWindow(hWnd);
                    }
                    finally
                    {
                        // 연결 해제 (매우 중요)
                        if (hForeWnd != IntPtr.Zero && dwForeID != dwCurID)
                            AttachThreadInput(dwForeID, dwCurID, false);
                        AttachThreadInput(dwCurID, dwTargetID, false);
                    }
                }
                else
                {
                    // 3. 실행 중이 아니면 프로그램을 시작한다.
                    // [수정] 제어 대상(processName)과 실행 대상(exePath)이 다를 수 있으므로,
                    // 실행할 파일을 결정하는 로직을 추가한다.
                    string fileToStart = exePath; // 기본값은 원래 경로
                    string argsToStart = args;    // 기본값은 원래 인수

                    // 아이콘 경로가 .exe이고, 제어 대상(processName)이 아이콘 경로에서 왔다면,
                    // 실행도 아이콘 경로의 .exe로 해야 한다.
                    if (!string.IsNullOrEmpty(iconPath) && iconPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                        processName.Equals(System.IO.Path.GetFileNameWithoutExtension(iconPath), StringComparison.OrdinalIgnoreCase))
                    {
                        fileToStart = iconPath;
                        argsToStart = ""; // 아이콘 경로에는 인수가 없다고 가정
                    }

                    var psi = new ProcessStartInfo(fileToStart);
                    if (!string.IsNullOrEmpty(argsToStart)) psi.Arguments = argsToStart;
                    psi.UseShellExecute = true;

                    Process.Start(psi);
                    Log($"[Run] Executing: {fileToStart} {argsToStart}");
                }
            }
            catch (Exception ex)
            {
                Log($"[Run Error] {ex.Message}");
            }
            });
        }
    }
}
