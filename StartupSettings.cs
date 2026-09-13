using Microsoft.Win32;

namespace FastWindowResizer;

internal sealed class StartupSettings
{
    private const string ValueName = "FastWindowResizer";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private readonly string registryPath;
    private readonly string command;

    internal StartupSettings(string executablePath, string registryPath = RunKey)
    {
        if (!Path.IsPathFullyQualified(executablePath) || executablePath.Contains('"')
            || !executablePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("自启需要有效的 EXE 绝对路径。", nameof(executablePath));
        command = $"\"{executablePath}\"";
        this.registryPath = registryPath;
    }

    // Read on every menu opening so changes outside this process are reflected.
    internal bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(registryPath);
            return string.Equals(key?.GetValue(ValueName) as string, command, StringComparison.OrdinalIgnoreCase);
        }
    }

    internal void SetEnabled(bool enabled)
    {
        if (enabled)
        {
            // Windows documents a 260-character maximum for a Run command line.
            if (command.Length > 260) throw new IOException("程序路径过长，请移到较短的目录后重试。");
            using var key = Registry.CurrentUser.CreateSubKey(registryPath, writable: true);
            key.SetValue(ValueName, command, RegistryValueKind.String);
        }
        else
        {
            using var key = Registry.CurrentUser.OpenSubKey(registryPath, writable: true);
            key?.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
