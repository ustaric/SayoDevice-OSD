﻿using System;
using System.Windows;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Collections.Generic; // 리스트 사용을 위해 추가
using System.Linq;
using System.Threading;
using System.Security.Principal; // 관리자 권한 확인용
using SayoOSD.Managers;
using SayoOSD;

namespace SayoOSD
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private const string ShowMessageName = "SayoOSD_Show_Window";
        // Mutex가 GC에 의해 해제되지 않도록 반드시 static으로 선언해야 합니다.
        private static Mutex _mutex;
        
        // 시작 로그를 메모리에 저장할 리스트 (MainWindow에서 가져다 씀)
        public static List<string> StartupLogs = new List<string>() { "[App] 리스트 초기화됨 (Field Init)" };

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern uint RegisterWindowMessage(string lpString);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        private const int HWND_BROADCAST = 0xFFFF;

        // [추가] 로그 기록용 정적 메서드 (어디서든 접근 가능)
        public static void Log(string msg)
        {
            StartupLogs.Add($"[App] {msg}");
        }

        // [수정] 정적 생성자: 클래스 로드 시 최초 1회 실행됨 (가장 확실한 초기화 시점)
        static App()
        {
            Log("========================================");
            Log("=== 프로그램 시작 (Static Constructor) ===");
            
            // [수정] 기존 실행 중인 인스턴스(vFinal)와 호환되도록 Mutex 이름 복구
            const string mutexName = "Global\\SayoOSD_SingleInstance_Mutex_vFinal";
            bool createdNew = false;

            try 
            {
                // [핵심] Mutex 생성 및 소유권 확인
                _mutex = new Mutex(true, mutexName, out createdNew);
                if (!createdNew)
                {
                    Log(">> [중복 감지] 이미 실행 중입니다. (Mutex 소유 실패)");
                    // GUI 생성 전 즉시 프로세스 종료 (FailFast는 가장 빠르게 종료됨)
                    Environment.Exit(0);
                }
                else
                {
                    Log(">> [정상 실행] Mutex 획득 성공.");
                }
            }
            catch (Exception ex)
            {
                Log($"[오류] Mutex 확인 중 에러: {ex.Message}");
                // 안전장치: 프로세스 이름으로 확인
                if (IsProcessRunning())
                {
                    Log(">> [중복 감지] 프로세스 리스트 확인됨. 종료합니다.");
                    Environment.Exit(0);
                }
            }
        }

        // [추가] 인스턴스 생성자
        public App()
        {
            Log("App 인스턴스 생성자 호출");
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            Log("OnStartup 이벤트 발생");
            
            // 관리자 권한 여부 확인
            bool isAdmin = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
            Log($"관리자 권한: {isAdmin}");

            Log(">> 정상 실행 시작.");

            base.OnStartup(e);
        }

        private static bool IsProcessRunning()
        {
            try
            {
                var current = Process.GetCurrentProcess();
                return Process.GetProcessesByName(current.ProcessName).Any(p => p.Id != current.Id);
            }
            catch { return false; }
        }
    }
}