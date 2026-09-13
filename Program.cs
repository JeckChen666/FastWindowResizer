namespace FastWindowResizer;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(true, @"Local\FastWindowResizer", out bool firstInstance);
        if (!firstInstance) return;
        ApplicationConfiguration.Initialize();
        using var context = new TrayApplicationContext();
        Application.Run(context);
    }
}
