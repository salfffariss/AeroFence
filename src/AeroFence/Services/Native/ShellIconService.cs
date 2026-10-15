using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AeroFence.Services.Native;

/// <summary>
/// Extracts genuine, high-resolution Windows icons from .exe, .lnk, .url, or filesystem paths.
/// Uses native shell32 SHGetFileInfo and SHGetImageList with automatic GDI handle cleanup and memory caching.
/// Automatically resolves .lnk shortcuts to genuine target binaries to avoid Windows shortcut arrow overlays.
/// </summary>
public class ShellIconService
{
    private static readonly ConcurrentDictionary<string, ImageSource> _iconCache = new(StringComparer.OrdinalIgnoreCase);

    private const uint SHGFI_ICON = 0x000000100;
    private const uint SHGFI_LARGEICON = 0x000000000; // 32x32 / High-DPI
    private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
    private const uint SHGFI_SYSICONINDEX = 0x000004000;

    private const int SHIL_LARGE = 0x0;       // 32x32
    private const int SHIL_EXTRALARGE = 0x2;  // 48x48
    private const int SHIL_JUMBO = 0x4;       // 256x256
    private const int ILD_TRANSPARENT = 0x1;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [ComImport]
    [Guid("46EB5926-582E-4017-9FDF-E8998DAA0950")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IImageList
    {
        [PreserveSig] int Add(IntPtr hbmImage, IntPtr hbmMask, ref int pi);
        [PreserveSig] int ReplaceIcon(int i, IntPtr hicon, ref int pi);
        [PreserveSig] int SetOverlayImage(int iImage, int iOverlay);
        [PreserveSig] int Replace(int i, IntPtr hbmImage, IntPtr hbmMask);
        [PreserveSig] int AddMasked(IntPtr hbmImage, int crMask, ref int pi);
        [PreserveSig] int Draw(ref object pimldp);
        [PreserveSig] int Remove(int i);
        [PreserveSig] int GetIcon(int i, int flags, ref IntPtr picon);
    }

    [DllImport("shell32.dll", EntryPoint = "#727")]
    private static extern int SHGetImageList(int iImageList, ref Guid riid, out IImageList ppv);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    /// <summary>
    /// Gets the native high-resolution icon for a file or application path.
    /// If the path is a shortcut (.lnk), extracts from the target executable to omit shortcut arrow badges.
    /// </summary>
    public ImageSource? GetIcon(string? targetPath)
    {
        if (string.IsNullOrWhiteSpace(targetPath)) return null;

        string cleanTarget = ResolveCleanTarget(targetPath);
        string resolvedPath = ResolveSystemPath(cleanTarget);

        if (_iconCache.TryGetValue(resolvedPath, out var cached))
        {
            return cached;
        }

        IntPtr hIcon = IntPtr.Zero;
        try
        {
            // 1. Try High-Resolution Jumbo / Extra Large image list for pristine rendering
            hIcon = ExtractHighResIcon(resolvedPath);

            // 2. Fallback to standard SHGetFileInfo if image list returned zero
            if (hIcon == IntPtr.Zero)
            {
                var shinfo = new SHFILEINFO();
                uint flags = SHGFI_ICON | SHGFI_LARGEICON;

                if (!File.Exists(resolvedPath) && !Directory.Exists(resolvedPath))
                {
                    flags |= SHGFI_USEFILEATTRIBUTES;
                }

                SHGetFileInfo(resolvedPath, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), flags);
                hIcon = shinfo.hIcon;
            }

            if (hIcon != IntPtr.Zero)
            {
                var bitmapSource = Imaging.CreateBitmapSourceFromHIcon(
                    hIcon,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());

                // Freeze bitmap source for thread-safety and optimal rendering performance
                bitmapSource.Freeze();
                _iconCache[resolvedPath] = bitmapSource;

                if (!string.Equals(targetPath, resolvedPath, StringComparison.OrdinalIgnoreCase))
                {
                    _iconCache[targetPath] = bitmapSource;
                }

                return bitmapSource;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ShellIconService] Failed to extract icon for {targetPath}: {ex.Message}");
        }
        finally
        {
            if (hIcon != IntPtr.Zero)
            {
                DestroyIcon(hIcon);
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts a high-res (256x256 Jumbo or 48x48 Extra-Large) HICON using the Windows Shell Image List.
    /// </summary>
    private static IntPtr ExtractHighResIcon(string path)
    {
        try
        {
            var shinfo = new SHFILEINFO();
            uint flags = SHGFI_SYSICONINDEX;
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                flags |= SHGFI_USEFILEATTRIBUTES;
            }

            SHGetFileInfo(path, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), flags);
            int iconIndex = shinfo.iIcon;
            if (iconIndex == 0 && !File.Exists(path) && !Directory.Exists(path))
            {
                return IntPtr.Zero;
            }

            Guid imageListGuid = new Guid("46EB5926-582E-4017-9FDF-E8998DAA0950");

            // Attempt Jumbo 256x256 first
            if (SHGetImageList(SHIL_JUMBO, ref imageListGuid, out var imageList) == 0 && imageList != null)
            {
                IntPtr hIcon = IntPtr.Zero;
                if (imageList.GetIcon(iconIndex, ILD_TRANSPARENT, ref hIcon) == 0 && hIcon != IntPtr.Zero)
                {
                    return hIcon;
                }
            }

            // Attempt Extra Large 48x48
            if (SHGetImageList(SHIL_EXTRALARGE, ref imageListGuid, out imageList) == 0 && imageList != null)
            {
                IntPtr hIcon = IntPtr.Zero;
                if (imageList.GetIcon(iconIndex, ILD_TRANSPARENT, ref hIcon) == 0 && hIcon != IntPtr.Zero)
                {
                    return hIcon;
                }
            }
        }
        catch { }

        return IntPtr.Zero;
    }

    /// <summary>
    /// Resolves .lnk shortcuts to genuine target binaries or icon locations, bypassing Windows shortcut overlay arrows.
    /// </summary>
    public static string ResolveCleanTarget(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return path;

        if (string.Equals(Path.GetExtension(path), ".lnk", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType != null)
                {
                    dynamic? shell = Activator.CreateInstance(shellType);
                    if (shell != null)
                    {
                        dynamic shortcut = shell.CreateShortcut(path);

                        // If explicit IconLocation exists and points to a valid file, use it
                        string iconLoc = shortcut.IconLocation;
                        if (!string.IsNullOrWhiteSpace(iconLoc))
                        {
                            string iconFile = iconLoc.Split(',')[0].Trim();
                            if (File.Exists(iconFile))
                            {
                                return iconFile;
                            }
                        }

                        // Otherwise extract directly from the target executable
                        string target = shortcut.TargetPath;
                        if (!string.IsNullOrWhiteSpace(target) && (File.Exists(target) || Directory.Exists(target)))
                        {
                            return target;
                        }
                    }
                }
            }
            catch { }
        }

        return path;
    }

    /// <summary>
    /// Resolves common Windows system binaries (e.g. calc.exe, notepad.exe, cmd.exe) to absolute paths.
    /// </summary>
    private static string ResolveSystemPath(string path)
    {
        if (File.Exists(path) || Path.IsPathRooted(path))
        {
            return path;
        }

        string sys32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
        string candidate = Path.Combine(sys32, path);
        if (File.Exists(candidate))
        {
            return candidate;
        }

        string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        candidate = Path.Combine(winDir, path);
        if (File.Exists(candidate))
        {
            return candidate;
        }

        return path;
    }
}
