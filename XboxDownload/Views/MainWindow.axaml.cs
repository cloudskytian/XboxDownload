using System;
using System.IO;
using System.Net.Sockets;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using XboxDownload.ViewModels;

namespace XboxDownload.Views;

public partial class MainWindow : Window
{
    private bool _isSystemShutdown;
    
    public MainWindow()
    {
        InitializeComponent();
        
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownRequested += async (_, e) =>
            {
                _isSystemShutdown = true;
                
                e.Cancel = true;
                
                if (desktop.MainWindow?.DataContext is MainWindowViewModel mainVm)
                {
                    var serviceVm = mainVm.ServiceViewModel;
                    if (serviceVm.IsListening)
                    {
                        await serviceVm.ToggleListeningAsync();
                    }
                    
                    var toolsVm = mainVm.ToolsViewModel;
                    toolsVm.Dispose();
                }
                
                // macOS/Linux 退出时清理 Socket 文件
                if (!OperatingSystem.IsWindows())
                {
                    try
                    {
                        using var client = new Socket(AddressFamily.Unix, SocketType.Stream, 0);
                        var path = Path.Combine(Path.GetTempPath(), $"{nameof(XboxDownload)}.sock");
                        // ReSharper disable once MethodHasAsyncOverload
                        client.Connect(new UnixDomainSocketEndPoint(path));
                    }
                    catch
                    {
                        // ignored
                    }

                    try
                    {
                        if (Program.Listener is not null)
                        {
                            try { Program.Listener.Shutdown(SocketShutdown.Both); }
                            catch
                            {
                                // ignored
                            }
                            Program.Listener?.Close();
                            Program.Listener = null;
                        }
                    }
                    catch
                    {
                        // ignored
                    }
                
                    try
                    {
                        var socketPath = Path.Combine(Path.GetTempPath(), $"{nameof(XboxDownload)}.sock");
                        if (File.Exists(socketPath))
                            File.Delete(socketPath);
                    }
                    catch
                    {
                        // ignored
                    }
                }
                
                desktop.Shutdown();
            };
        }
    }
    
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (_isSystemShutdown || !OperatingSystem.IsWindows())
        {
            // Allow actual shutdown on system exit
            base.OnClosing(e);
            return;
        }

        // User clicked close button → hide the window instead of closing
        e.Cancel = true;
        Hide();
    }
    
    private async void ShowStartupSettingsDialogAsync(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (VisualRoot is not Window window) return;
            
            var dialog = new Dialog.StartupSettingsDialog();
            await dialog.ShowDialog(window);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in show dialog: {ex}");
        }
    }
    
    private async void ShowAboutDialogAsync(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (VisualRoot is not Window window) return;
            
            var dialog = new Dialog.AboutDialog();
            await dialog.ShowDialog(window);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in show dialog: {ex}");
        }
    }
}