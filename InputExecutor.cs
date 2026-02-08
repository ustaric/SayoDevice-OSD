﻿using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms; // SendKeys 사용을 위해 필요

namespace SayoOSD
{
    public class InputExecutor
    {
        // Win32 API: 키보드 이벤트 시뮬레이션
        [DllImport("user32.dll")]
        static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

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

        // 로그 전달 이벤트
        public event Action<string> LogMessage;

        // 미디어 키 실행
        public void ExecuteMediaKey(int actionType)
        {
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
            }

            if (vkCode != 0)
            {
                try
                {
                    // 키 누름 (0) 및 뗌 (2) 시뮬레이션
                    keybd_event(vkCode, 0, 0, UIntPtr.Zero);
                    keybd_event(vkCode, 0, 2, UIntPtr.Zero);
                    
                    LogMessage?.Invoke($"[Action] {actionName} Triggered");
                }
                catch (Exception ex)
                {
                    LogMessage?.Invoke($"[Media Error] {ex.Message}");
                }
            }
        }

        // 텍스트 매크로 실행
        public void ExecuteMacro(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            try
            {
                // SendKeys를 사용하여 텍스트 입력 시뮬레이션
                SendKeys.SendWait(text);
                LogMessage?.Invoke($"[Macro] Text Sent: {text}");
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"[Macro Error] {ex.Message}");
            }
        }

        // 프로그램 실행
        public void ExecuteProgram(string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                // UseShellExecute=true를 사용하여 실행 (권한 문제 최소화)
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                LogMessage?.Invoke($"[Run] Executing: {path}");
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"[Run Error] {ex.Message}");
            }
        }
    }
}
