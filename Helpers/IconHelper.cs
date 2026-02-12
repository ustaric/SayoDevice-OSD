using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SayoOSD.Helpers;

namespace SayoOSD.Helpers
{
    public static class IconHelper
    {
        // [추가] 고해상도 아이콘 추출을 위한 Win32 API
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int PrivateExtractIcons(string lpszFile, int nIconIndex, int cxIcon, int cyIcon, IntPtr[] phicon, int[] piconid, int nIcons, int flags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        public static ImageSource GetIconFromPath(string path, Action<string> logger = null)
        {
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return null;

            // 1. [신규] EXE/DLL에서 고해상도(256x256) 아이콘 추출 시도 (PrivateExtractIcons)
            try
            {
                IntPtr[] phicon = new IntPtr[1];
                int[] piconid = new int[1];

                // 256x256 크기로 1개 추출 요청
                int count = PrivateExtractIcons(path, 0, 256, 256, phicon, piconid, 1, 0);

                if (count > 0 && phicon[0] != IntPtr.Zero)
                {
                    var imageSource = Imaging.CreateBitmapSourceFromHIcon(
                        phicon[0],
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());

                    imageSource.Freeze();
                    DestroyIcon(phicon[0]); // 핸들 해제 (메모리 누수 방지)

                    string msg = $"고해상도 아이콘 추출 성공 (EXE): {imageSource.PixelWidth}x{imageSource.PixelHeight}";
                    Debug.WriteLine(msg);
                    logger?.Invoke(msg);

                    return imageSource;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PrivateExtractIcons 실패: {ex.Message}");
            }

            // 2. [기존] .ico 파일 등을 위한 디코더 방식
            try
            {
                var uri = new Uri(path);
                var decoder = new IconBitmapDecoder(
                    uri,
                    BitmapCreateOptions.None,
                    BitmapCacheOption.OnLoad);

                var hiResFrame = decoder.Frames
                    .OrderByDescending(f => f.Width)
                    .FirstOrDefault();

                if (hiResFrame != null)
                {
                    string msg = $"고해상도 아이콘 추출 성공 (Decoder): {hiResFrame.Width}x{hiResFrame.Height}";
                    Debug.WriteLine(msg);
                    logger?.Invoke(msg);
                    hiResFrame.Freeze(); // 스레드 간 공유를 위해 프리즈
                    return hiResFrame;
                }
            }
            catch
            {
                // EXE 파일은 여기서 에러가 발생하므로 조용히 넘어감
            }

            // 3. [Fallback] 최후의 수단 (ExtractAssociatedIcon - 보통 32x32)
            try
            {
                using (var icon = System.Drawing.Icon.ExtractAssociatedIcon(path))
                {
                    if (icon == null) return null;

                    string msg = $"Fallback 아이콘 추출 성공 ({path}): {icon.Width}x{icon.Height}";
                    Debug.WriteLine(msg);
                    logger?.Invoke(msg);

                    var imageSource = Imaging.CreateBitmapSourceFromHIcon(
                        icon.Handle,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());

                    imageSource.Freeze();
                    return imageSource;
                }
            }
            catch
            {
                return null; // Fallback도 실패하면 null 반환
            }
        }
    }
}