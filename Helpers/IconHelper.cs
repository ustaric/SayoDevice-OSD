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

        // [추가] 경로 자동 보정 메서드 (버전 폴더 변경 대응)
        public static string ResolvePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (System.IO.File.Exists(path)) return path;

            try
            {
                var fileInfo = new System.IO.FileInfo(path);
                var dir = fileInfo.Directory;

                // 디렉토리가 없으면 상위 디렉토리에서 검색
                // 예: .../Discord/app-1.0.9224/Discord.exe -> .../Discord/app-1.0.9225/Discord.exe
                if (dir != null && !dir.Exists && dir.Parent != null && dir.Parent.Exists)
                {
                    string fileName = fileInfo.Name;
                    string dirName = dir.Name;
                    System.IO.DirectoryInfo[] subDirs;

                    // "app-"로 시작하는 폴더였던 경우 (Discord 패턴)
                    if (dirName.StartsWith("app-", StringComparison.OrdinalIgnoreCase))
                        subDirs = dir.Parent.GetDirectories("app-*");
                    else
                        subDirs = dir.Parent.GetDirectories();

                    // 생성 시간 내림차순 정렬 (최신 폴더 우선)
                    var sortedDirs = subDirs.OrderByDescending(d => d.CreationTime);

                    foreach (var subDir in sortedDirs)
                    {
                        string newPath = System.IO.Path.Combine(subDir.FullName, fileName);
                        if (System.IO.File.Exists(newPath)) return newPath;
                    }
                }
            }
            catch { }
            return null;
        }

        public static ImageSource GetIconFromPath(string path, Action<string> logger = null)
        {
            if (string.IsNullOrEmpty(path)) return null;

            // [수정] 파일이 없으면 경로 보정 시도
            if (!System.IO.File.Exists(path))
            {
                string resolved = ResolvePath(path);
                if (resolved != null) path = resolved;
                else return null;
            }

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