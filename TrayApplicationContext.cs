namespace FastWindowResizer;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly WindowService service = new();
    private readonly StartupSettings startup = new(Environment.ProcessPath!);
    private readonly NotifyIcon tray;
    private readonly ContextMenuStrip menu = new() { ShowImageMargin = true };
    private readonly Icon icon;
    private bool busy;
    private bool disposed;
    private string monitor = "";
    private Point anchor;

    internal TrayApplicationContext()
    {
        using var stream = typeof(TrayApplicationContext).Assembly.GetManifestResourceStream("FastWindowResizer.Assets.app.ico")!;
        icon = new Icon(stream);
        tray = new NotifyIcon { Icon = icon, Text = "FastWindowResizer · 右键找回窗口", ContextMenuStrip = menu, Visible = true };
        // NotifyIcon recreates its shell icon on the registered TaskbarCreated message.
        menu.Opening += (_, _) => PopulateMenu();
        menu.Closed += (_, _) =>
        {
            // Defer disposal until a clicked menu item's handler has run.
            if (menu.IsHandleCreated) menu.BeginInvoke((Action)(() => { if (!disposed && !menu.Visible) ClearMenu(); }));
        };
    }

    private void ClearMenu()
    {
        while (menu.Items.Count > 0)
        {
            ToolStripItem item = menu.Items[0];
            menu.Items.RemoveAt(0);
            item.Image?.Dispose();
            item.Dispose();
        }
    }

    private void PopulateMenu()
    {
        ClearMenu();
        anchor = Cursor.Position;
        var screen = Screen.FromPoint(anchor);
        monitor = screen.DeviceName;
        menu.MaximumSize = new Size(Math.Min(560, screen.WorkingArea.Width), Math.Max(100, screen.WorkingArea.Height - 16));
        menu.Items.Add(CreateStartupItem());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem(busy ? "正在找回窗口…" : "点击窗口，找回到此屏幕") { Enabled = false });
        menu.Items.Add(new ToolStripSeparator());
        if (!busy)
        {
            try
            {
                var entries = service.Enumerate();
                if (entries.Count == 0) menu.Items.Add(new ToolStripMenuItem("没有可找回的窗口") { Enabled = false });
                // Bound total icon-fetch latency even when many apps are unresponsive.
                var budget = System.Diagnostics.Stopwatch.StartNew();
                foreach (var entry in entries)
                {
                    string title = entry.Title.Replace('\r', ' ').Replace('\n', ' ');
                    if (title.Length > 65) title = title[..62] + "…";
                    var item = new ToolStripMenuItem(title.Replace("&", "&&"))
                    {
                        ToolTipText = entry.Title,
                        Image = budget.ElapsedMilliseconds < 180 ? WindowService.GetIcon(entry) : SystemIcons.Application.ToBitmap()
                    };
                    item.Click += async (_, _) => await Rescue(entry);
                    menu.Items.Add(item);
                }
            }
            catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or System.ComponentModel.Win32Exception)
            {
                menu.Items.Add(new ToolStripMenuItem("读取窗口失败，请重试") { Enabled = false });
            }
        }
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => ExitThread());
    }

    private ToolStripMenuItem CreateStartupItem()
    {
        var item = new ToolStripMenuItem("开机自启")
        {
            ToolTipText = "当前用户登录 Windows 后自动运行；勾选表示已为此程序位置配置自启。"
        };
        try { item.Checked = startup.IsEnabled; }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            item.Enabled = false;
            item.ToolTipText = "无法读取开机自启配置。";
        }
        item.Click += (_, _) =>
        {
            try
            {
                bool enabled = !startup.IsEnabled;
                startup.SetEnabled(enabled);
                item.Checked = startup.IsEnabled;
                if (item.Checked != enabled) throw new IOException("开机自启配置未生效，请重试。");
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
            {
                tray.ShowBalloonTip(4000, "无法修改开机自启", ex is IOException ? ex.Message : "当前用户无法修改启动配置。", ToolTipIcon.Warning);
            }
        };
        return item;
    }

    private async Task Rescue(WindowEntry entry)
    {
        if (busy) return;
        busy = true;
        string selectedMonitor = monitor;
        Point selectedAnchor = anchor;
        try
        {
            string? error = await WindowService.RescueAsync(entry, selectedMonitor, selectedAnchor);
            if (!disposed && error is not null) tray.ShowBalloonTip(4000, "未能找回窗口", error, ToolTipIcon.Warning);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            if (!disposed) tray.ShowBalloonTip(4000, "未能找回窗口", "窗口状态已变化，请重新尝试。", ToolTipIcon.Warning);
        }
        finally { busy = false; }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed)
        {
            disposed = true;
            tray.Visible = false;
            tray.Dispose();
            ClearMenu();
            menu.Dispose();
            icon.Dispose();
            service.Dispose();
        }
        base.Dispose(disposing);
    }
}
