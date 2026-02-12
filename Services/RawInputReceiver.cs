using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using SayoOSD.Services;

namespace SayoOSD.Services
{
    public class DeviceInfo
    {
        public string Name { get; set; }
        public string Pid { get; set; }
        public override string ToString() => $"{Name} (PID: {Pid})";
    }

    public class RawInputReceiver
    {
        private const int RIM_TYPEKEYBOARD = 1;
        private const int RIM_TYPEHID = 2;
        private const int WM_INPUT = 0x00FF;
        private const int RIDEV_INPUTSINK = 0x00000100;
        
        // Win32 API 구조체 및 메서드
        [StructLayout(LayoutKind.Sequential)]
        internal struct RAWINPUTDEVICE
        {
            public ushort usUsagePage;
            public ushort usUsage;
            public uint dwFlags;
            public IntPtr hwndTarget;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct RAWINPUTDEVICELIST
        {
            public IntPtr hDevice;
            public uint dwType;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct RAWINPUTHEADER
        {
            public uint dwType;
            public uint dwSize;
            public IntPtr hDevice;
            public IntPtr wParam;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct RAWKEYBOARD
        {
            public ushort MakeCode;
            public ushort Flags;
            public ushort Reserved;
            public ushort VKey;
            public uint Message;
            public uint ExtraInformation;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct RAWHID
        {
            public uint dwSizeHid;
            public uint dwCount;
            // 데이터는 이 뒤에 이어짐 (Variable length)
        }

        [StructLayout(LayoutKind.Explicit)]
        internal struct RAWINPUT
        {
            [FieldOffset(0)] public RAWINPUTHEADER header;
            [FieldOffset(24)] public RAWKEYBOARD keyboard;
            [FieldOffset(24)] public RAWHID hid;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct HIDP_CAPS
        {
            public ushort Usage;
            public ushort UsagePage;
            public ushort InputReportByteLength;
            public ushort OutputReportByteLength;
            public ushort FeatureReportByteLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
            public ushort[] Reserved;
            public ushort NumberLinkCollectionNodes;
            public ushort NumberInputButtonCaps;
            public ushort NumberInputValueCaps;
            public ushort NumberInputDataIndices;
            public ushort NumberOutputButtonCaps;
            public ushort NumberOutputValueCaps;
            public ushort NumberOutputDataIndices;
            public ushort NumberFeatureButtonCaps;
            public ushort NumberFeatureValueCaps;
            public ushort NumberFeatureDataIndices;
        }

        private const int HIDP_STATUS_SUCCESS = 0x00110000;

        [DllImport("User32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        extern static uint GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);

        [DllImport("User32.dll")]
        extern static bool RegisterRawInputDevices(RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, uint cbSize);

        [DllImport("User32.dll", SetLastError = true)]
        extern static uint GetRawInputDeviceList([In, Out] RAWINPUTDEVICELIST[] rawInputDeviceList, ref uint puiNumDevices, uint cbSize);

        [DllImport("hid.dll", SetLastError = true)]
        internal static extern int HidP_GetCaps(IntPtr PreparsedData, out HIDP_CAPS Capabilities);

        private const uint RIDI_PREPARSEDDATA = 0x20000005;

        [DllImport("User32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        extern static uint GetRawInputDeviceInfo(IntPtr hDevice, uint uiCommand, IntPtr pData, ref uint pcbSize);

        private IntPtr _hwnd;
        private string _targetVid;
        private string _targetPid;
        
        public event Action<byte[]> HidDataReceived; // HID Raw Data 전달
        public event Action<string> DebugLog; // 디버그 로그 전달 (추가)

        public RawInputReceiver(IntPtr hwnd, string vid, string pid)
        {
            _hwnd = hwnd;
            _targetVid = vid.ToLower();
            _targetPid = pid.ToLower();
        }

        public void UpdateTargetDevice(string vid, string pid)
        {
            _targetVid = vid.ToLower();
            _targetPid = pid.ToLower();
            // Re-register devices with new VID/PID
            RegisterInput();
        }

        public List<DeviceInfo> GetAvailableDevices(string vid)
        {
            var list = new List<DeviceInfo>();
            uint deviceCount = 0;
            GetRawInputDeviceList(null, ref deviceCount, (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICELIST)));

            if (deviceCount == 0) return list;

            var deviceList = new RAWINPUTDEVICELIST[deviceCount];
            GetRawInputDeviceList(deviceList, ref deviceCount, (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICELIST)));

            var seenPids = new HashSet<string>();

            foreach (var device in deviceList)
            {
                string devicePath = GetDeviceName(device.hDevice);
                if (string.IsNullOrEmpty(devicePath)) continue;

                if (devicePath.IndexOf(vid, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    string pid = "Unknown";
                    int pidIndex = devicePath.IndexOf("PID_", StringComparison.OrdinalIgnoreCase);
                    if (pidIndex >= 0 && pidIndex + 8 <= devicePath.Length)
                    {
                        pid = devicePath.Substring(pidIndex + 4, 4).ToUpper();
                    }

                    if (!seenPids.Contains(pid))
                    {
                        seenPids.Add(pid);
                        list.Add(new DeviceInfo { Name = "SayoDevice", Pid = pid });
                    }
                }
            }
            return list;
        }

        public void Initialize()
        {
            RegisterInput();
        }

        private void RegisterInput()
        {
            uint deviceCount = 0;
            GetRawInputDeviceList(null, ref deviceCount, (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICELIST)));

            if (deviceCount == 0) return;

            var deviceList = new RAWINPUTDEVICELIST[deviceCount];
            GetRawInputDeviceList(deviceList, ref deviceCount, (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICELIST)));

            var devicesToRegister = new List<RAWINPUTDEVICE>();

            foreach (var device in deviceList)
            {
                string deviceName = GetDeviceName(device.hDevice);
                DebugLog?.Invoke($"[Scan] Device: {deviceName}"); // 디버깅: 검색된 장치 이름 출력
                if (string.IsNullOrEmpty(deviceName)) continue;

                if (deviceName.ToLower().Contains(_targetVid) && deviceName.ToLower().Contains(_targetPid))
                {
                    uint pcbSize = 0;
                    GetRawInputDeviceInfo(device.hDevice, RIDI_PREPARSEDDATA, IntPtr.Zero, ref pcbSize);
                    if (pcbSize <= 0) continue;

                    IntPtr pPreparsedData = Marshal.AllocHGlobal((int)pcbSize);
                    try
                    {
                        if (GetRawInputDeviceInfo(device.hDevice, RIDI_PREPARSEDDATA, pPreparsedData, ref pcbSize) == uint.MaxValue) continue;

                        if (HidP_GetCaps(pPreparsedData, out HIDP_CAPS caps) == HIDP_STATUS_SUCCESS)
                        {
                            // Vendor-defined 페이지(커스텀 데이터용)와 키보드 페이지(디버깅용)만 등록
                            if (caps.UsagePage >= 0xFF00 || (caps.UsagePage == 1 && caps.Usage == 6))
                            {
                                var rid = new RAWINPUTDEVICE
                                {
                                    usUsagePage = caps.UsagePage,
                                    usUsage = caps.Usage,
                                    dwFlags = RIDEV_INPUTSINK,
                                    hwndTarget = _hwnd
                                };
                                devicesToRegister.Add(rid);
                                DebugLog?.Invoke($"[Auto-Detect] Found: UsagePage=0x{caps.UsagePage:X4}, Usage=0x{caps.Usage:X2}. Registering...");
                            }
                        }
                        else
                        {
                            // DebugLog?.Invoke($"[Error] HidP_GetCaps failed for {deviceName}");
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(pPreparsedData);
                    }
                }
            }

            if (devicesToRegister.Count > 0)
            {
                if (!RegisterRawInputDevices(devicesToRegister.ToArray(), (uint)devicesToRegister.Count, (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE))))
                {
                    Debug.WriteLine("RawInput registration failed for detected devices.");
                }
                else
                {
                    Debug.WriteLine($"Successfully registered {devicesToRegister.Count} devices.");
                }
            }
            else
            {
                DebugLog?.Invoke($"[Auto-Detect] No matching devices found for VID:{_targetVid} PID:{_targetPid}");
            }
        }

        public void ProcessMessage(int msg, IntPtr lParam)
        {
            if (msg != WM_INPUT) return;

            uint dwSize = 0;
            GetRawInputData(lParam, 0x10000003, IntPtr.Zero, ref dwSize, (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER)));

            IntPtr buffer = Marshal.AllocHGlobal((int)dwSize);
            try
            {
                if (GetRawInputData(lParam, 0x10000003, buffer, ref dwSize, (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER))) != dwSize)
                    return;

                RAWINPUT raw = Marshal.PtrToStructure<RAWINPUT>(buffer);

                // 장치 확인 (VID/PID)
                string deviceName = GetDeviceName(raw.header.hDevice);
                bool isTarget = !string.IsNullOrEmpty(deviceName) && 
                                deviceName.ToLower().Contains(_targetVid) && 
                                deviceName.ToLower().Contains(_targetPid);

                if (isTarget)
                {
                    // [디버그] RawInput 메시지 수신 확인 (장치로부터 신호가 오는지 확인)
                    // DebugLog?.Invoke($"[RawInput] WM_INPUT received from target");

                    if (raw.header.dwType == RIM_TYPEHID)
                    {
                        // HID 데이터 추출
                        int length = (int)raw.hid.dwSizeHid * (int)raw.hid.dwCount;
                        byte[] rawData = new byte[length];

                        // RAWINPUT 구조체 크기(헤더 포함) 뒤에 데이터가 위치함
                        // x64/x86에 따라 오프셋이 다를 수 있으므로 안전하게 계산
                        IntPtr dataPtr = new IntPtr(buffer.ToInt64() + Marshal.SizeOf(typeof(RAWINPUTHEADER)) + 8); 
                        // +8은 RAWHID의 dwSizeHid, dwCount 크기
                        
                        // 간단하게 GetRawInputData를 다시 호출하여 Raw Data만 가져오는 것이 안전함
                        if (GetRawInputData(lParam, 0x10000003, buffer, ref dwSize, (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER))) == dwSize)
                        {
                            // 구조체에서 직접 복사 (위의 포인터 계산 대신 구조체 필드 이후 메모리 복사)
                            // RAWHID 구조체 바로 뒤부터 데이터 시작
                            int headerSize = Marshal.SizeOf(typeof(RAWINPUTHEADER));
                            int hidStructSize = 8; // uint * 2
                            IntPtr start = new IntPtr(buffer.ToInt64() + headerSize + hidStructSize);
                            
                            Marshal.Copy(start, rawData, 0, length);
                            HidDataReceived?.Invoke(rawData);
                        }
                    }
                    else
                    {
                        // SayoDevice는 맞는데 HID 데이터가 아님 (예: 일반 키보드 입력)
                        DebugLog?.Invoke($"[Device Found] Type: {raw.header.dwType} (Not HID - Check Usage Page)");
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private string GetDeviceName(IntPtr hDevice)
        {
            uint pcbSize = 0;
            GetRawInputDeviceInfo(hDevice, 0x20000007, IntPtr.Zero, ref pcbSize); // RIDI_DEVICENAME

            if (pcbSize <= 0) return "";

            IntPtr pData = Marshal.AllocHGlobal((int)pcbSize * 2); // Unicode 문자 크기(2바이트) 고려
            try
            {
                GetRawInputDeviceInfo(hDevice, 0x20000007, pData, ref pcbSize);
                return Marshal.PtrToStringUni(pData);
            }
            finally
            {
                Marshal.FreeHGlobal(pData);
            }
        }
    }
}
