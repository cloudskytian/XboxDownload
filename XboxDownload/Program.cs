using Avalonia;
using System;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;

namespace XboxDownload;

sealed class Program
{
    private const string MutexName = $"Global\\{nameof(XboxDownload)}_Mutex";
    private static string SocketPath => Path.Combine(Path.GetTempPath(), $"{nameof(XboxDownload)}.sock");

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string lpString);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [STAThread]
    public static void Main(string[] args)
    {
        using var mutex = new Mutex(true, MutexName, out var createdNew);

        if (!createdNew)
        {
            if (OperatingSystem.IsWindows())
            {
                var msg = RegisterWindowMessage("XboxDownload_ShowWindow");
                PostMessage((IntPtr)0xFFFF, msg, IntPtr.Zero, IntPtr.Zero);
            }
            else
            {
                TrySendUnixWakeup();
            }
            return;
        }

        if (OperatingSystem.IsWindows())
        {
            App.ShowWindowMessageId = RegisterWindowMessage("XboxDownload_ShowWindow");
        }
        else
        {
            StartUnixSocketListener();
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static Socket? Listener;

    private static void StartUnixSocketListener()
    {
        if (File.Exists(SocketPath)) File.Delete(SocketPath);

        Listener = new Socket(AddressFamily.Unix, SocketType.Stream, 0);
        Listener.Bind(new UnixDomainSocketEndPoint(SocketPath));
        Listener.Listen(1);

        new Thread(() =>
            {
                while (true)
                {
                    try
                    {
                        using var client = Listener.Accept();
                        App.UnixWakeupRequested?.Invoke();
                    }
                    catch
                    {
                        break;
                    }
                }
            })
            { IsBackground = true }.Start();
    }

    private static void TrySendUnixWakeup()
    {
        try
        {
            using var client = new Socket(AddressFamily.Unix, SocketType.Stream, 0);
            client.Connect(new UnixDomainSocketEndPoint(SocketPath));
            client.Send(new byte[] { 1 });
        }
        catch
        {
            // ignored
        }
    }

    private static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
