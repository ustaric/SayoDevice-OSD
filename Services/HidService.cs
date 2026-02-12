using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using HidSharp;
using HidSharp.Reports;
using SayoOSD.Services;

namespace SayoOSD.Services
{
    public class HidService
    {
        private int _vid;
        private int _pid;
        public event Action<string> LogMessage;
        public event Action<int> LayerDetected; // 레이어 번호를 전달할 이벤트 추가

        public HidService(string vidHex, string pidHex)
        {
            UpdateTarget(vidHex, pidHex);
        }

        public void UpdateTarget(string vidHex, string pidHex)
        {
            try
            {
                _vid = Convert.ToInt32(vidHex, 16);
                _pid = Convert.ToInt32(pidHex, 16);
            }
            catch
            {
                _vid = 0x8089;
                _pid = 0x000B;
            }
        }

        // 레이어 상태 요청 및 읽기 (비동기)
        public void RequestLayerState()
        {
            Task.Run(() =>
            {
                try
                {
                    var loader = new HidDeviceLoader();
                    var devices = loader.GetDevices(_vid, _pid).ToList();

                    if (devices.Count == 0)
                    {
                        // LogMessage?.Invoke($"[HidSharp] 장치를 찾을 수 없습니다 (VID: {_vid:X4}, PID: {_pid:X4})");
                        return;
                    }

                    // col02 인터페이스 우선 순위 정렬 (사용자 분석 권장)
                    devices.Sort((a, b) =>
                    {
                        bool aCol02 = a.DevicePath.IndexOf("col02", StringComparison.OrdinalIgnoreCase) >= 0;
                        bool bCol02 = b.DevicePath.IndexOf("col02", StringComparison.OrdinalIgnoreCase) >= 0;
                        return bCol02.CompareTo(aCol02);
                    });

                    int? detectedLayer = null;

                    foreach (var device in devices)
                    {
                        if (!device.TryOpen(out HidStream stream)) continue;

                        using (stream)
                        {
                            try
                            {
                                // 1단계: 질의 명령 준비 (웹 로그의 Sending report와 동일)
                                byte[] query = new byte[64];
                                query[0] = 0x21;
                                query[1] = 0x12;
                                query[2] = 0x25;
                                query[3] = 0x12;
                                query[4] = 0x04;
                                // 나머지 00...

                                // 명령 전송
                                stream.Write(query);

                                // 응답 수신
                                byte[] response = new byte[64];
                                stream.ReadTimeout = 200; 
                                int count = stream.Read(response);

                                if (count > 18 && response[0] == 0x21 && response[1] == 0x12 && response[2] == 0x96)
                                {
                                    // [분석 결과 적용] 인덱스 18번(8+10)이 레이어 값
                                    detectedLayer = response[18];
                                    break; // 성공 시 루프 종료 (using 블록을 벗어나며 Dispose 호출되어 장치 닫힘)
                                }
                            }
                            catch
                            {
                                // 실패 시 다음 장치 시도
                            }
                        }
                    }

                    if (detectedLayer.HasValue)
                    {
                        LayerDetected?.Invoke(detectedLayer.Value);
                    }
                }
                catch (Exception ex)
                {
                    LogMessage?.Invoke($"[Error] {ex.Message}");
                }
            });
        }
    }
}