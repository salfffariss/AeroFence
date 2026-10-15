using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using AeroFence.Models;

namespace AeroFence.Services.Tracking;

/// <summary>
/// Monitors running games, standard Win32 apps, modern UWP stub launchers (Notepad, Calculator),
/// and File Explorer folder windows, tracking real-time playtime duration.
/// </summary>
public class PlaytimeTrackerService : IDisposable
{
    private class ActiveSession
    {
        public ItemModel Item { get; }
        public Process? CurrentProcess { get; set; }
        public string? FolderPath { get; }
        public string TargetHint { get; }
        public int InitialPid { get; }
        public DateTime StartTime { get; }
        public DateTime LastTick { get; set; }
        public Action? OnSave { get; }

        public ActiveSession(ItemModel item, Process? process, string? folderPath, string targetHint, Action? onSave)
        {
            Item = item;
            CurrentProcess = process;
            FolderPath = folderPath;
            TargetHint = targetHint;
            InitialPid = process?.Id ?? 0;
            StartTime = DateTime.Now;
            LastTick = DateTime.Now;
            OnSave = onSave;
        }

        public void AdoptProcess(Process newProcess, EventHandler exitedHandler)
        {
            CurrentProcess = newProcess;
            try
            {
                newProcess.EnableRaisingEvents = true;
                newProcess.Exited += exitedHandler;
            }
            catch { }
        }
    }

    private readonly ConcurrentDictionary<string, ActiveSession> _sessions = new();
    private readonly System.Timers.Timer _tickTimer;
    private bool _disposed = false;

    // Win32 Toolhelp32 process snapshot P/Invokes
    private const uint TH32CS_SNAPPROCESS = 0x00000002;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct PROCESSENTRY32
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    public PlaytimeTrackerService()
    {
        // Periodically tick every 3 seconds to update active playtime
        _tickTimer = new System.Timers.Timer(3000);
        _tickTimer.Elapsed += (s, e) => TickActiveSessions();
        _tickTimer.AutoReset = true;
        _tickTimer.Start();
    }

    /// <summary>
    /// Attaches tracking to a started process with support for stub launchers (Notepad, Calculator).
    /// </summary>
    public void TrackProcess(ItemModel item, Process process, string targetHint, Action? onSave)
    {
        if (item == null || process == null) return;

        try
        {
            item.IsRunning = true;
            item.Stats.LaunchCount++;
            item.Stats.LastPlayed = DateTime.Now;
            onSave?.Invoke();

            var session = new ActiveSession(item, process, null, targetHint, onSave);
            _sessions[item.Id] = session;

            process.EnableRaisingEvents = true;
            process.Exited += (s, e) => HandleProcessExited(item.Id, session);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PlaytimeTracker] Error attaching to process: {ex.Message}");
        }
    }

    /// <summary>
    /// Attaches tracking to an open File Explorer folder window.
    /// </summary>
    public void TrackFolder(ItemModel item, string folderPath, Action? onSave)
    {
        if (item == null || string.IsNullOrWhiteSpace(folderPath)) return;

        try
        {
            item.IsRunning = true;
            item.Stats.LaunchCount++;
            item.Stats.LastPlayed = DateTime.Now;
            onSave?.Invoke();

            string normalizedPath = Path.GetFullPath(folderPath).TrimEnd('\\');
            var session = new ActiveSession(item, null, normalizedPath, Path.GetFileName(normalizedPath), onSave);
            _sessions[item.Id] = session;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PlaytimeTracker] Error tracking folder: {ex.Message}");
        }
    }

    private void HandleProcessExited(string itemId, ActiveSession session)
    {
        Task.Run(async () =>
        {
            // If the initial process exited in under 4 seconds, it might be a modern Windows stub launcher (e.g. Notepad, Calc)
            if ((DateTime.Now - session.StartTime).TotalSeconds < 4.0)
            {
                for (int retry = 0; retry < 6; retry++)
                {
                    await Task.Delay(250);
                    var child = FindChildProcess(session.InitialPid, session.TargetHint);
                    if (child != null && !child.HasExited)
                    {
                        session.AdoptProcess(child, (s, e) => HandleProcessExited(itemId, session));
                        return; // Successfully transitioned to real application process!
                    }
                }
            }

            EndSession(itemId);
        });
    }

    private void TickActiveSessions()
    {
        if (_sessions.IsEmpty) return;

        var now = DateTime.Now;

        foreach (var kvp in _sessions.ToArray())
        {
            var session = kvp.Value;
            try
            {
                // 1. Folder tracking via Explorer windows
                if (!string.IsNullOrEmpty(session.FolderPath))
                {
                    if (!IsFolderWindowOpen(session.FolderPath))
                    {
                        EndSession(kvp.Key);
                        continue;
                    }

                    int elapsedSeconds = (int)(now - session.LastTick).TotalSeconds;
                    if (elapsedSeconds >= 1)
                    {
                        session.Item.Stats.TotalPlaytimeSeconds += elapsedSeconds;
                        session.LastTick = now;
                    }
                    continue;
                }

                // 2. Process tracking
                if (session.CurrentProcess != null)
                {
                    if (session.CurrentProcess.HasExited)
                    {
                        HandleProcessExited(kvp.Key, session);
                        continue;
                    }

                    int elapsedSeconds = (int)(now - session.LastTick).TotalSeconds;
                    if (elapsedSeconds >= 1)
                    {
                        session.Item.Stats.TotalPlaytimeSeconds += elapsedSeconds;
                        session.LastTick = now;
                    }
                }
            }
            catch
            {
                EndSession(kvp.Key);
            }
        }
    }

    private static Process? FindChildProcess(int parentPid, string targetHint)
    {
        // 1. Toolhelp32 snapshot for exact ParentProcessId match
        if (parentPid > 0)
        {
            try
            {
                IntPtr hSnapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
                if (hSnapshot != IntPtr.Zero && hSnapshot.ToInt64() != -1)
                {
                    try
                    {
                        var pe = new PROCESSENTRY32();
                        pe.dwSize = (uint)Marshal.SizeOf(typeof(PROCESSENTRY32));

                        if (Process32First(hSnapshot, ref pe))
                        {
                            do
                            {
                                if (pe.th32ParentProcessID == parentPid)
                                {
                                    try
                                    {
                                        var child = Process.GetProcessById((int)pe.th32ProcessID);
                                        if (!child.HasExited)
                                        {
                                            return child;
                                        }
                                    }
                                    catch { }
                                }
                            } while (Process32Next(hSnapshot, ref pe));
                        }
                    }
                    finally
                    {
                        CloseHandle(hSnapshot);
                    }
                }
            }
            catch { }
        }

        // 2. Name-based match for modern packaged Windows apps
        try
        {
            if (!string.IsNullOrWhiteSpace(targetHint))
            {
                string hint = targetHint.ToLowerInvariant();
                var candidateNames = new List<string> { hint };
                if (hint == "calc" || hint == "calculator") candidateNames.Add("calculatorapp");
                if (hint == "notepad") candidateNames.Add("notepad");

                foreach (var name in candidateNames)
                {
                    var procs = Process.GetProcessesByName(name);
                    foreach (var p in procs)
                    {
                        try
                        {
                            if (!p.HasExited && (DateTime.Now - p.StartTime).TotalSeconds < 12)
                            {
                                return p;
                            }
                        }
                        catch { }
                    }
                }
            }
        }
        catch { }

        return null;
    }

    private static bool IsFolderWindowOpen(string folderPath)
    {
        try
        {
            Type? shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType == null) return false;

            object? shell = Activator.CreateInstance(shellType);
            if (shell == null) return false;

            object? windows = shellType.InvokeMember("Windows", System.Reflection.BindingFlags.InvokeMethod, null, shell, null);
            if (windows == null) return false;

            int count = (int)windows.GetType().InvokeMember("Count", System.Reflection.BindingFlags.GetProperty, null, windows, null)!;
            string normalizedTarget = Path.GetFullPath(folderPath).TrimEnd('\\').ToLowerInvariant();

            for (int i = 0; i < count; i++)
            {
                try
                {
                    object? win = windows.GetType().InvokeMember("Item", System.Reflection.BindingFlags.InvokeMethod, null, windows, new object[] { i });
                    if (win == null) continue;

                    string? locationUrl = win.GetType().InvokeMember("LocationURL", System.Reflection.BindingFlags.GetProperty, null, win, null) as string;
                    if (!string.IsNullOrWhiteSpace(locationUrl) && locationUrl.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
                    {
                        var uri = new Uri(locationUrl);
                        string winPath = Path.GetFullPath(uri.LocalPath).TrimEnd('\\').ToLowerInvariant();
                        if (winPath == normalizedTarget)
                        {
                            return true;
                        }
                    }
                }
                catch { }
            }
        }
        catch { }
        return false;
    }

    private void EndSession(string itemId)
    {
        if (_sessions.TryRemove(itemId, out var session))
        {
            try
            {
                var now = DateTime.Now;
                int remainingSeconds = (int)(now - session.LastTick).TotalSeconds;
                if (remainingSeconds > 0)
                {
                    session.Item.Stats.TotalPlaytimeSeconds += remainingSeconds;
                }

                session.Item.IsRunning = false;
                session.OnSave?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PlaytimeTracker] Error ending session: {ex.Message}");
            }
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _tickTimer.Stop();
            _tickTimer.Dispose();

            // End any remaining active sessions cleanly
            foreach (var key in _sessions.Keys.ToArray())
            {
                EndSession(key);
            }
        }
    }
}
