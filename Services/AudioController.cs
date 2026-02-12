using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using System.Threading.Tasks;
using SayoOSD.Services;

namespace SayoOSD.Services
{
    public class AudioController : IDisposable
    {
        private MMDevice _micDevice;
        private MMDevice _speakerDevice;
        private MMDeviceEnumerator _enumerator;
        private AudioNotificationClient _notificationClient;
        private readonly object _lock = new object();

        // 상태 변경 및 로그 전달 이벤트
        public event Action<bool> MicMuteChanged;
        public event Action<bool> SpeakerMuteChanged;
        public event Action<string> LogMessage;
        public event Action<string> AudioDeviceChanged;

        public void Initialize()
        {
            _enumerator = new MMDeviceEnumerator();
            _notificationClient = new AudioNotificationClient();
            _notificationClient.DefaultDeviceChanged += OnSystemDefaultDeviceChanged;
            _notificationClient.DeviceStateChanged += OnDeviceStateChanged;
            _enumerator.RegisterEndpointNotificationCallback(_notificationClient);

            InitializeMicListener();
            InitializeSpeakerListener();
        }

        private void InitializeMicListener()
        {
            try
            {
                var enumerator = new MMDeviceEnumerator();
                // 기본 통신 장치(마이크) 가져오기
                _micDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
                
                // 초기 상태 알림
                MicMuteChanged?.Invoke(_micDevice.AudioEndpointVolume.Mute);

                // 이벤트 구독
                _micDevice.AudioEndpointVolume.OnVolumeNotification += AudioEndpointVolume_OnVolumeNotification;
                LogMessage?.Invoke("[Mic] 마이크 상태 감지 시작 (Event Mode)");
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"[Mic Error] 마이크 장치를 찾을 수 없거나 초기화 실패: {ex.Message}");
            }
        }

        private void InitializeSpeakerListener()
        {
            lock (_lock)
            {
                // 기존 리스너 해제 (장치 전환 시 중복 방지)
                if (_speakerDevice != null)
                {
                    try { _speakerDevice.AudioEndpointVolume.OnVolumeNotification -= Speaker_OnVolumeNotification; } catch { }
                    _speakerDevice.Dispose();
                    _speakerDevice = null;
                }

                try
                {
                    var enumerator = new MMDeviceEnumerator();
                    // 기본 렌더 장치(스피커) 가져오기
                    _speakerDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                    
                    // 초기 상태 알림
                    SpeakerMuteChanged?.Invoke(_speakerDevice.AudioEndpointVolume.Mute);

                    // 이벤트 구독
                    _speakerDevice.AudioEndpointVolume.OnVolumeNotification += Speaker_OnVolumeNotification;
                    LogMessage?.Invoke("[Speaker] 스피커 상태 감지 시작");
                }
                catch (Exception ex)
                {
                    LogMessage?.Invoke($"[Speaker Error] 스피커 장치를 찾을 수 없거나 초기화 실패: {ex.Message}");
                }
            }
        }

        private void OnSystemDefaultDeviceChanged(DataFlow flow, Role role, string id)
        {
            // 기본 출력 장치(Console)가 변경된 경우
            if (flow == DataFlow.Render && role == Role.Console)
            {
                // [Fix] Deadlock prevention: Handle on a separate thread
                Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(200); // Wait for OS to settle

                        InitializeSpeakerListener(); // 리스너 갱신

                        using (var enumerator = new MMDeviceEnumerator())
                        {
                            var dev = enumerator.GetDevice(id);
                            string name = dev.FriendlyName;
                            LogMessage?.Invoke($"[Audio] System Default Changed: {name}");
                            AudioDeviceChanged?.Invoke(name);
                        }
                    }
                    catch { }
                });
            }
        }

        private void OnDeviceStateChanged(string deviceId, DeviceState newState)
        {
            Task.Run(async () =>
            {
                try
                {
                    // 장치 상태 변경 시 윈도우가 목록을 갱신할 시간을 줌
                    await Task.Delay(500);

                    if (newState == DeviceState.Active)
                    {
                        // 장치 연결됨 (Active): 해당 장치를 기본 장치로 강제 전환
                        using (var enumerator = new MMDeviceEnumerator())
                        {
                            var device = enumerator.GetDevice(deviceId);
                            if (device.DataFlow == DataFlow.Render)
                            {
                                var policy = new PolicyConfigClient();
                                policy.SetDefaultEndpoint(deviceId, Role.Console);
                                policy.SetDefaultEndpoint(deviceId, Role.Multimedia);
                                LogMessage?.Invoke($"[Audio] Jack Plugged: {device.FriendlyName} -> Switched to Default");
                            }
                        }
                    }
                    else if (newState == DeviceState.Unplugged)
                    {
                        // 장치 해제됨 (Unplugged): 윈도우가 자동으로 전환한 기본 장치 이름을 OSD에 반영
                        using (var enumerator = new MMDeviceEnumerator())
                        {
                            var defaultDev = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
                            string name = defaultDev.FriendlyName;
                            LogMessage?.Invoke($"[Audio] Jack Unplugged. Current: {name}");
                            AudioDeviceChanged?.Invoke(name);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogMessage?.Invoke($"[Audio State Error] {ex.Message}");
                }
            });
        }

        private void AudioEndpointVolume_OnVolumeNotification(AudioVolumeNotificationData data)
        {
            MicMuteChanged?.Invoke(data.Muted);
        }

        private void Speaker_OnVolumeNotification(AudioVolumeNotificationData data)
        {
            SpeakerMuteChanged?.Invoke(data.Muted);
        }

        public bool ToggleMicMute()
        {
            try
            {
                using (var enumerator = new MMDeviceEnumerator())
                {
                    var commDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
                    commDevice.AudioEndpointVolume.Mute = !commDevice.AudioEndpointVolume.Mute;
                    bool isMuted = commDevice.AudioEndpointVolume.Mute;
                    LogMessage?.Invoke($"[Mic] Mute Toggled: {isMuted}");
                    return isMuted;
                }
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"[Mic Error] {ex.Message}");
                return false;
            }
        }

        public string CycleAudioDevice()
        {
            try
            {
                var enumerator = new MMDeviceEnumerator();
                // 활성화된 출력 장치 목록 가져오기
                var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).ToList();
                var defaultDev = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
                
                int currentIdx = devices.FindIndex(d => d.ID == defaultDev.ID);
                int nextIdx = (currentIdx + 1) % devices.Count;
                var nextDev = devices[nextIdx];

                // 기본 장치 변경 (Console & Multimedia)
                var policy = new PolicyConfigClient();
                policy.SetDefaultEndpoint(nextDev.ID, Role.Console);
                policy.SetDefaultEndpoint(nextDev.ID, Role.Multimedia);

                // 장치가 변경되었으므로 리스너를 재설정하여 새 장치의 상태를 반영
                InitializeSpeakerListener();

                LogMessage?.Invoke($"[Audio] Switched to: {nextDev.FriendlyName}");
                return nextDev.FriendlyName;
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"[Audio Error] {ex.Message}");
                return null;
            }
        }

        public void Dispose()
        {
            if (_enumerator != null && _notificationClient != null)
            {
                try { _enumerator.UnregisterEndpointNotificationCallback(_notificationClient); } catch { }
                _enumerator.Dispose();
            }
            if (_micDevice != null)
            {
                try { _micDevice.AudioEndpointVolume.OnVolumeNotification -= AudioEndpointVolume_OnVolumeNotification; } catch { }
                _micDevice.Dispose();
                _micDevice = null;
            }
            lock (_lock)
            {
                if (_speakerDevice != null)
                {
                    try { _speakerDevice.AudioEndpointVolume.OnVolumeNotification -= Speaker_OnVolumeNotification; } catch { }
                    _speakerDevice.Dispose();
                    _speakerDevice = null;
                }
            }
        }
    }

    // 시스템 오디오 변경 알림 클라이언트
    public class AudioNotificationClient : IMMNotificationClient
    {
        public event Action<DataFlow, Role, string> DefaultDeviceChanged;
        public event Action<string, DeviceState> DeviceStateChanged;

        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
        {
            DefaultDeviceChanged?.Invoke(flow, role, defaultDeviceId);
        }

        public void OnDeviceAdded(string pwstrDeviceId) { }
        public void OnDeviceRemoved(string deviceId) { }
        public void OnDeviceStateChanged(string deviceId, DeviceState newState) 
        {
            DeviceStateChanged?.Invoke(deviceId, newState);
        }
        public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key) { }
    }

    // COM 인터페이스 및 클라이언트 (MainWindow에서 이동됨)
    [ComImport, Guid("f8679f50-850a-41cf-9c72-430f290290c8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IPolicyConfig
    {
        [PreserveSig] int GetMixFormat(string pszDeviceName, out IntPtr ppFormat);
        [PreserveSig] int GetDeviceFormat(string pszDeviceName, int bDefault, out IntPtr ppFormat);
        [PreserveSig] int ResetDeviceFormat(string pszDeviceName);
        [PreserveSig] int SetDeviceFormat(string pszDeviceName, IntPtr pEndpointFormat, IntPtr mixFormat);
        [PreserveSig] int GetProcessingPeriod(string pszDeviceName, int bDefault, out long pmftDefault, out long pmftMinimum);
        [PreserveSig] int SetProcessingPeriod(string pszDeviceName, long pmftDefault);
        [PreserveSig] int GetShareMode(string pszDeviceName, out IntPtr pMode);
        [PreserveSig] int SetShareMode(string pszDeviceName, IntPtr mode);
        [PreserveSig] int GetPropertyValue(string pszDeviceName, IntPtr key, out IntPtr value);
        [PreserveSig] int SetPropertyValue(string pszDeviceName, IntPtr key, IntPtr value);
        [PreserveSig] int SetDefaultEndpoint(string pszDeviceName, int role);
        [PreserveSig] int SetEndpointVisibility(string pszDeviceName, int bVisible);
    }

    class PolicyConfigClient
    {
        private readonly IPolicyConfig _policyConfig;
        public PolicyConfigClient()
        {
            _policyConfig = (IPolicyConfig)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9")));
        }
        public void SetDefaultEndpoint(string id, Role role)
        {
            _policyConfig.SetDefaultEndpoint(id, (int)role);
        }
    }
}
