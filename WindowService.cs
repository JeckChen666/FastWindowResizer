using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using static FastWindowResizer.NativeMethods;

namespace FastWindowResizer;

internal sealed record WindowEntry(nint Handle, uint ProcessId, uint ThreadId, string Title);

internal sealed class WindowService : IDisposable
{
    private readonly IVirtualDesktopManager? desktops;
    internal WindowService()
    {
        try
        {
            var type = Type.GetTypeFromCLSID(new Guid("AA509086-5CA9-4C25-8F95-589D3C07B48A"));
            if (type is not null) desktops = (IVirtualDesktopManager?)Activator.CreateInstance(type);
        }
        catch (COMException) { }
    }

    internal List<WindowEntry> Enumerate(uint? excludedProcess = null)
    {
        uint excluded = excludedProcess ?? (uint)Environment.ProcessId;
        var windows = new List<WindowEntry>();
        var seen = new HashSet<nint>();
        EnumWindows((window, _) =>
        {
            if (!IsWindowVisible(window)) return true;
            uint thread = GetWindowThreadProcessId(window, out uint process);
            if (process == excluded || thread == 0) return true;
            long extended = GetWindowLongPtr(window, -20).ToInt64();
            bool appWindow = (extended & 0x40000) != 0;
            if (!appWindow)
            {
                if ((extended & (0x80 | 0x08000000)) != 0) return true; // tool/no-activate
                nint root = GetAncestor(window, 3);
                if (root != window) return true;
            }
            if (DwmGetWindowAttribute(window, 14, out int cloaked, sizeof(int)) == 0 && cloaked != 0) return true;
            try
            {
                if (desktops is not null && desktops.IsWindowOnCurrentVirtualDesktop(window, out bool current) == 0 && !current) return true;
            }
            catch (COMException) { }
            var className = new StringBuilder(256);
            GetClassName(window, className, className.Capacity);
            if (className.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") return true;
            nint target = ResolvePopup(window);
            if (!seen.Add(target)) return true;
            thread = GetWindowThreadProcessId(target, out process);
            var title = new StringBuilder(512);
            GetWindowText(target, title, title.Capacity);
            string label = title.ToString().Trim();
            if (label.Length == 0)
            {
                try { using var p = Process.GetProcessById((int)process); label = p.ProcessName; }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { label = "未命名窗口"; }
            }
            windows.Add(new WindowEntry(target, process, thread, label));
            return true;
        }, 0);
        return windows;
    }

    private static nint ResolvePopup(nint window)
    {
        var visited = new HashSet<nint>();
        while (!IsWindowEnabled(window) && visited.Add(window))
        {
            nint popup = GetLastActivePopup(window);
            if (popup == window || !IsWindowVisible(popup)) break;
            window = popup;
        }
        return window;
    }

    internal static bool Matches(WindowEntry entry) => IsWindow(entry.Handle)
        && GetWindowThreadProcessId(entry.Handle, out uint process) == entry.ThreadId && process == entry.ProcessId;

    internal static Bitmap GetIcon(WindowEntry entry)
    {
        if (Matches(entry))
        {
            SendMessageTimeout(entry.Handle, 0x7f, 2, 0, 0x23, 40, out nuint icon);
            nint handle = (nint)icon;
            if (handle == 0) handle = GetClassLongPtr(entry.Handle, -34);
            if (handle == 0) handle = GetClassLongPtr(entry.Handle, -14);
            if (handle != 0)
            {
                try { using var borrowed = Icon.FromHandle(handle); using var copy = (Icon)borrowed.Clone(); return copy.ToBitmap(); }
                catch (ArgumentException) { }
            }
        }
        return SystemIcons.Application.ToBitmap();
    }

    internal static async Task<string?> RescueAsync(WindowEntry entry, string monitor, Point fallback)
    {
        if (!Matches(entry)) return "窗口已经关闭，请重新打开列表。";
        nint target = ResolvePopup(entry.Handle);
        uint thread = GetWindowThreadProcessId(target, out uint process);
        entry = entry with { Handle = target, ThreadId = thread, ProcessId = process };
        bool responsive = await Task.Run(() => SendMessageTimeout(target, 0, 0, 0, 0x23, 250, out _) != 0);
        if (!responsive) return "目标应用未响应或操作受限。若它以管理员身份运行，请尝试以管理员身份运行本工具。";
        if (!Matches(entry)) return "窗口已经关闭。";
        if (IsIconic(target) || IsZoomed(target))
        {
            ShowWindowAsync(target, 9);
            for (int i = 0; i < 12 && (IsIconic(target) || IsZoomed(target)); i++) await Task.Delay(40);
            if (!Matches(entry)) return "窗口已经关闭。";
            if (IsIconic(target) || IsZoomed(target)) return "应用未能还原窗口。";
        }
        if (!GetWindowRect(target, out var rect)) return "无法读取窗口位置。";
        bool resizable = (GetWindowLongPtr(target, -16).ToInt64() & 0x40000) != 0;
        Screen screen = Screen.AllScreens.FirstOrDefault(s => s.DeviceName == monitor) ?? Screen.FromPoint(fallback);
        Rectangle desired = WindowGeometry.Fit(rect.Rectangle, screen.WorkingArea, resizable, GetDpiForWindow(target));
        uint flags = 0x4000 | 0x4 | 0x10 | 0x200; // async, preserve Z order, no activate/owner reorder
        if (!resizable) flags |= 1;
        if (!SetWindowPos(target, 0, desired.X, desired.Y, desired.Width, desired.Height, flags))
            return $"窗口调整失败（错误 {Marshal.GetLastWin32Error()}），可能受到权限限制。";
        // Cross-monitor DPI handling can change the size. Center once more using the actual resulting size.
        await Task.Delay(160);
        if (!Matches(entry)) return "窗口已经关闭。";
        if (GetWindowRect(target, out var actual))
        {
            screen = Screen.AllScreens.FirstOrDefault(s => s.DeviceName == monitor) ?? Screen.FromPoint(fallback);
            Rectangle corrected = WindowGeometry.Fit(actual.Rectangle, screen.WorkingArea, resizable, GetDpiForWindow(target));
            SetWindowPos(target, 0, corrected.X, corrected.Y, corrected.Width, corrected.Height, flags);
        }
        SetForegroundWindow(target);
        await Task.Delay(120);
        if (!Matches(entry)) return "窗口已经关闭。";
        if (!GetWindowRect(target, out var final) || !screen.WorkingArea.IntersectsWith(final.Rectangle))
            return "应用未接受位置调整，窗口仍未回到目标屏幕。";
        var expected = WindowGeometry.Fit(final.Rectangle, screen.WorkingArea, false, GetDpiForWindow(target));
        if (Math.Abs(final.Left - expected.Left) > 24 || Math.Abs(final.Top - expected.Top) > 24)
            return "应用限制了窗口位置，未能完成居中。";
        return null;
    }

    public void Dispose()
    {
        if (desktops is not null) Marshal.FinalReleaseComObject(desktops);
    }
}
