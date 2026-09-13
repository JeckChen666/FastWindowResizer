using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using FastWindowResizer;
using Microsoft.Win32;

internal static class Checks
{
    private static int passed;
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.Contains("--startup-only"))
        {
            try { Startup(); Console.WriteLine($"PASS: {passed} startup checks"); }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            return;
        }
        using var runner = new Form { Text = "FastWindowResizer controlled test", ShowInTaskbar = false, Opacity = 0, Size = new Size(1, 1) };
        runner.Shown += async (_, _) =>
        {
            try
            {
                Geometry();
                Startup();
                await Windows();
                await UnresponsiveWindow();
                await MenuResources();
                Console.WriteLine($"PASS: {passed} checks");
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            finally { runner.Close(); }
        };
        Application.Run(runner);
    }

    private static void Assert(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("FAIL: " + label);
        passed++;
        Console.WriteLine("PASS: " + label);
    }

    private static void Geometry()
    {
        var negative = new Rectangle(-1920, -100, 1920, 1040);
        var fit = WindowGeometry.Fit(new Rectangle(6000, 0, 800, 600), negative, true, 96);
        Assert(fit == new Rectangle(-1360, 120, 800, 600), "negative monitor coordinates and centering");
        fit = WindowGeometry.Fit(new Rectangle(0, 0, 3000, 2000), negative, true, 96);
        Assert(fit == negative, "oversized window fits work area");
        fit = WindowGeometry.Fit(new Rectangle(0, 0, 300, 200), negative, true, 192);
        Assert(fit.Size == new Size(1536, 832), "DPI-scaled small-window threshold");
        fit = WindowGeometry.Fit(new Rectangle(0, 0, 60, 50), negative, false, 192);
        Assert(fit.Size == new Size(60, 50) && negative.Contains(fit), "fixed-size window keeps dimensions");
        fit = WindowGeometry.Fit(new Rectangle(0, 0, 4000, 3000), negative, false, 96);
        Assert(fit.Location == negative.Location, "oversized fixed window keeps title area reachable");
        fit = WindowGeometry.Fit(new Rectangle(0, 0, 10, 10), new Rectangle(20, 30, 100, 60), true, 144);
        Assert(fit == new Rectangle(30, 36, 80, 48), "small work area stays valid");
    }

    private static void Startup()
    {
        // This isolated key is not a Windows startup location. Never enable real autostart in tests.
        string keyPath = @"Software\FastWindowResizer.Tests\" + Guid.NewGuid().ToString("N");
        const string path = @"C:\Program Files\窗口找回工具\FastWindowResizer.exe";
        var settings = new StartupSettings(path, keyPath);
        try
        {
            Assert(!settings.IsEnabled, "startup disabled when no entry exists");
            settings.SetEnabled(true);
            Assert(new StartupSettings(path, keyPath).IsEnabled, "startup state persists across service instances");
            using (var key = Registry.CurrentUser.OpenSubKey(keyPath, writable: true)!)
            {
                Assert((string?)key.GetValue("FastWindowResizer") == $"\"{path}\"", "startup path with spaces and Unicode is quoted");
                Assert(key.GetValueKind("FastWindowResizer") == RegistryValueKind.String, "startup command uses REG_SZ");
                key.SetValue("OtherApplication", "unchanged");
                key.SetValue("FastWindowResizer", "\"C:\\Old\\FastWindowResizer.exe\"");
            }
            Assert(!settings.IsEnabled, "old executable location is not treated as current startup");
            settings.SetEnabled(true);
            Assert(settings.IsEnabled, "enabling repairs old executable location");
            settings.SetEnabled(false);
            using (var key = Registry.CurrentUser.OpenSubKey(keyPath)!)
            {
                Assert(key.GetValue("FastWindowResizer") is null, "disabling removes startup value");
                Assert((string?)key.GetValue("OtherApplication") == "unchanged", "disabling preserves unrelated entries");
            }
            settings.SetEnabled(false);
            Assert(!settings.IsEnabled, "disabling twice is safe");
            bool rejected = false;
            try { new StartupSettings(@"C:\" + new string('a', 260) + @"\app.exe", keyPath).SetEnabled(true); }
            catch (IOException) { rejected = true; }
            Assert(rejected && !settings.IsEnabled, "overlong startup path rejected without registration");
        }
        finally { Registry.CurrentUser.DeleteSubKeyTree(keyPath, throwOnMissingSubKey: false); }
    }

    private static WindowEntry Entry(Form form)
    {
        uint thread = NativeMethods.GetWindowThreadProcessId(form.Handle, out uint process);
        return new WindowEntry(form.Handle, process, thread, form.Text);
    }

    private static async Task Windows()
    {
        var screen = Screen.PrimaryScreen!;
        using var form = new Form { Text = "FWR test offscreen", StartPosition = FormStartPosition.Manual, Bounds = new Rectangle(30000, 30000, 800, 600) };
        form.Show();
        using var service = new WindowService();
        Assert(service.Enumerate(0).Any(w => w.Handle == form.Handle), "offscreen window stays in list");
        Assert(!service.Enumerate().Any(w => w.Handle == form.Handle), "own process excluded by default");
        string? error = await WindowService.RescueAsync(Entry(form), screen.DeviceName, screen.WorkingArea.Location);
        Assert(error is null && screen.WorkingArea.Contains(form.Bounds), "offscreen window restored");
        form.WindowState = FormWindowState.Minimized;
        await Task.Delay(80);
        Assert(service.Enumerate(0).Any(w => w.Handle == form.Handle), "minimized window stays in list");
        error = await WindowService.RescueAsync(Entry(form), screen.DeviceName, Point.Empty);
        Assert(error is null && form.WindowState == FormWindowState.Normal, "minimized window restored");
        form.WindowState = FormWindowState.Maximized;
        error = await WindowService.RescueAsync(Entry(form), screen.DeviceName, Point.Empty);
        Assert(error is null && form.WindowState == FormWindowState.Normal, "maximized window restored");
        form.Size = new Size(150, 80);
        error = await WindowService.RescueAsync(Entry(form), screen.DeviceName, Point.Empty);
        Assert(error is null && form.Width >= screen.WorkingArea.Width * .7, "tiny resizable window enlarged");
        form.FormBorderStyle = FormBorderStyle.FixedDialog;
        form.Size = new Size(300, 200);
        Size original = form.Size;
        form.Location = new Point(30000, 30000);
        error = await WindowService.RescueAsync(Entry(form), "disconnected-monitor", Point.Empty);
        Assert(error is null && form.Size == original && Screen.FromPoint(Point.Empty).WorkingArea.Contains(form.Bounds), "fixed dialog and missing-monitor fallback");
        using var other = new Form { Text = "FWR second window", Size = new Size(400, 300) };
        other.Show();
        var entries = service.Enumerate(0);
        Assert(entries.Any(w => w.Handle == form.Handle) && entries.Any(w => w.Handle == other.Handle), "multiple windows from same process");
        other.Hide();
        Assert(!service.Enumerate(0).Any(w => w.Handle == other.Handle), "hidden window excluded");
        using var dialog = new Form { Text = "FWR owned dialog", ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(30000, 30000) };
        dialog.Show(form);
        form.Enabled = false;
        dialog.Activate();
        await Task.Delay(80);
        entries = service.Enumerate(0);
        Assert(entries.Count(w => w.Handle == dialog.Handle) == 1 && !entries.Any(w => w.Handle == form.Handle), "disabled owner resolves to active dialog without duplicate");
        error = await WindowService.RescueAsync(Entry(form), screen.DeviceName, Point.Empty);
        Assert(error is null && screen.WorkingArea.Contains(dialog.Bounds), "rescue follows active owned dialog");
        form.Enabled = true;
        dialog.Close();
        var stale = Entry(other);
        other.Close();
        Assert(await WindowService.RescueAsync(stale, screen.DeviceName, Point.Empty) is not null, "closed window fails safely");
        var wrongIdentity = Entry(form) with { ProcessId = 0 };
        Assert(await WindowService.RescueAsync(wrongIdentity, screen.DeviceName, Point.Empty) is not null, "stale identity rejected");
        form.Close();
    }

    private static async Task MenuResources()
    {
        using var tray = new TrayApplicationContext();
        var type = typeof(TrayApplicationContext);
        var populate = type.GetMethod("PopulateMenu", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var clear = type.GetMethod("ClearMenu", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var menu = (ContextMenuStrip)type.GetField("menu", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(tray)!;
        // Exercise real menu layout in the controlled harness, including bottom-right placement.
        Rectangle work = Screen.PrimaryScreen!.WorkingArea;
        menu.Show(new Point(work.Right - 2, work.Bottom - 2));
        await Task.Delay(80);
        Assert(work.Contains(menu.Bounds), "popup menu remains within work area");
        using (var bitmap = new Bitmap(menu.Width, menu.Height))
        {
            menu.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            bitmap.Save(Path.Combine(AppContext.BaseDirectory, "menu-preview.png"));
        }
        menu.Close();
        await Task.Delay(80);
        for (int i = 0; i < 5; i++) { populate.Invoke(tray, null); clear.Invoke(tray, null); }
        await Task.Delay(100);
        using var process = Process.GetCurrentProcess();
        process.Refresh();
        int handles = process.HandleCount;
        uint gdi = GetGuiResources(process.Handle, 0), user = GetGuiResources(process.Handle, 1);
        for (int i = 0; i < 60; i++) { populate.Invoke(tray, null); clear.Invoke(tray, null); }
        await Task.Delay(100);
        process.Refresh();
        Console.WriteLine($"Menu stress: handles {handles}->{process.HandleCount}, GDI {gdi}->{GetGuiResources(process.Handle, 0)}, USER {user}->{GetGuiResources(process.Handle, 1)}");
        Assert(menu.Items.Count == 0, "menu items released");
        Assert(process.HandleCount - handles < 30 && GetGuiResources(process.Handle, 0) <= gdi + 5 && GetGuiResources(process.Handle, 1) <= user + 5, "60 menu refreshes do not leak handles");
    }

    private static async Task UnresponsiveWindow()
    {
        using var ready = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        WindowEntry? entry = null;
        var thread = new Thread(() =>
        {
            using var form = new Form { Text = "FWR blocked test", StartPosition = FormStartPosition.Manual, Location = new Point(30000, 30000) };
            form.Shown += (_, _) =>
            {
                entry = Entry(form);
                ready.Set();
                // A managed STA wait can pump sent messages; use a native non-pumping wait.
                WaitForSingleObject(release.WaitHandle.SafeWaitHandle.DangerousGetHandle(), 10000);
                form.Close();
            };
            Application.Run(form);
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        try
        {
            Assert(await Task.Run(() => ready.Wait(3000)), "unresponsive fixture started");
            var watch = Stopwatch.StartNew();
            string? error = await WindowService.RescueAsync(entry!, Screen.PrimaryScreen!.DeviceName, Point.Empty);
            Console.WriteLine($"Unresponsive check: {watch.ElapsedMilliseconds} ms, error={error ?? "none"}");
            Assert(error is not null && watch.ElapsedMilliseconds < 2000, "unresponsive window returns within bounded timeout");
        }
        finally { release.Set(); await Task.Run(() => thread.Join(3000)); }
    }

    [DllImport("user32.dll")] private static extern uint GetGuiResources(nint process, uint flags);
    [DllImport("kernel32.dll")] private static extern uint WaitForSingleObject(nint handle, uint milliseconds);
}
