using Avalonia;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace XboxDownload;

sealed class Program
{
    // 全局唯一的唤醒窗口消息 ID
    private static uint _showWindowMsg;

    private const string MutexName = $"Global\\{nameof(XboxDownload)}_Mutex";

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string lpString);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [STAThread]
    public static void Main(string[] args)
    {
        // 注册全局唯一的消息（第一个实例和后续实例都要注册）
        _showWindowMsg = RegisterWindowMessage("XboxDownload_ShowWindow");

        using var mutex = new Mutex(true, MutexName, out var createdNew);

        if (!createdNew)
        {
            if (OperatingSystem.IsWindows())
            {
                // 给第一个实例发送唤醒消息
                PostMessage((IntPtr)0xFFFF, _showWindowMsg, IntPtr.Zero, IntPtr.Zero); // HWND_BROADCAST
            }
            else
            {
                Console.WriteLine("程序已在运行。");
            }
            return;
        }

        // 把消息 ID 传给 App，用于监听
        App.ShowWindowMessageId = _showWindowMsg;

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}