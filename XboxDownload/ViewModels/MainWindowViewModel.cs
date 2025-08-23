﻿using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.Input;
using System;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData;
using XboxDownload.Helpers.Network;
using XboxDownload.Helpers.Resources;
using XboxDownload.Services;

namespace XboxDownload.ViewModels;

public partial class MainWindowViewModel(
    ServiceViewModel serviceViewModel,
    SpeedTestViewModel speedTestViewModel,
    HostViewModel hostViewModel,
    CdnViewModel cdnViewModel,
    StorageViewModel storageViewModel,
    StoreViewModel storeViewModel,
    ToolsViewModel toolsViewModel)
    : ObservableObject
{
    public ServiceViewModel ServiceViewModel { get; } = serviceViewModel;
    public SpeedTestViewModel SpeedTestViewModel { get; } = speedTestViewModel;
    public HostViewModel HostViewModel { get; } = hostViewModel;
    public CdnViewModel CdnViewModel { get; } = cdnViewModel;
    public StorageViewModel StorageViewModel { get; } = storageViewModel;
    public StoreViewModel StoreViewModel { get; } = storeViewModel;
    public ToolsViewModel ToolsViewModel { get; } = toolsViewModel;

    [ObservableProperty]
    private int _selectedTabIndex;
    
    public static bool IsWindows => OperatingSystem.IsWindows();
    
    partial void OnSelectedTabIndexChanged(int value)
    {
        if (value == 5)
        {
            StoreViewModel.ReloadGamePass();
        }
        else if (value == 6)
        {
            if (ToolsViewModel.DrivePaths.Count == 0)
                _ = ToolsViewModel.RefreshDrives();
            ToolsViewModel.EnableUsbWatcherIfNeeded();
        }
    }
    
    
    [RelayCommand]
    private void SwitchLanguage(string culture)
    {
        if (Application.Current is not App app || string.Equals(App.Settings.Culture, culture))
            return;
        
        var dictionaries = app.Resources.MergedDictionaries;
        
        var oldLang = dictionaries
            .OfType<ResourceInclude>()
            .FirstOrDefault(x => x.Source?.ToString().Contains("/Resources/Languages/") == true);
        
        if(oldLang != null)
            dictionaries.Remove(oldLang);

        var langDict = new ResourceInclude(new Uri($"resm:Styles?assembly={nameof(XboxDownload)}"))
        {
            Source = new Uri($"avares://{nameof(XboxDownload)}/Resources/Languages/{culture}.axaml")
        };
        
        dictionaries.Add(langDict);
        
        App.Settings.Culture = culture;
        SettingsManager.Save(App.Settings);
        
        // 更新 ObservableCollection 语言
        foreach (var filter in SpeedTestViewModel.LocationFilters)
        {
            filter.Display = ResourceHelper.GetString($"SpeedTest.{filter.Key}");
        }

        foreach (var option in SpeedTestViewModel.ImportOptions)
        {
            option.Display = ResourceHelper.GetString($"SpeedTest.ImportIp.{option.Key}.Display");
            option.Hint = ResourceHelper.GetString($"SpeedTest.ImportIp.{option.Key}.Hint");
            
            if (option.Key != SpeedTestViewModel.SelectedImportOption?.Key) continue;
            SpeedTestViewModel.HeaderText = option.Display;
            SpeedTestViewModel.WatermarkText = option.Hint;
        }

        foreach (var file in SpeedTestViewModel.SpeedTestFiles)
        {
            file.Display = ResourceHelper.GetString($"SpeedTest.TestFile.{file.Key}");
        }
        
        if (string.IsNullOrEmpty(SpeedTestViewModel.SelectedImportOption?.Target))
        {
            SpeedTestViewModel.IpSource.Clear();
            SpeedTestViewModel.TargetTestUrl = string.Empty;
            foreach (var option in SpeedTestViewModel.LocationFilters)
            {
                option.IsVisible = App.Settings.Culture == "zh-Hans"
                    ? option.Key is "ChinaTelecom" or "ChinaUnicom" or "ChinaMobile"
                    : option.Key is not ("ChinaTelecom" or "ChinaUnicom" or "ChinaMobile");
            }
        }
        
        ServiceViewModel.LanguageChanged();
        SpeedTestViewModel.UploadAkamaiIpsVisible = SpeedTestViewModel.SelectedImportOption!.Key.StartsWith("Akamai") && culture == "zh-Hans";
        StoreViewModel.LanguageChanged();
        ToolsViewModel.LanguageChanged();
        
        App.TrayIconService.UpdateToolTip();
        
    }
    
    [RelayCommand]
    private void SwitchTheme(string theme)
    {
        if (Application.Current is App app)
        {
            var themeVariant = theme switch
            {
                "Light" => ThemeVariant.Light,
                "Dark" => ThemeVariant.Dark,
                _ => ThemeVariant.Default
            };

            app.RequestedThemeVariant = themeVariant;
        }
        
        App.Settings.Theme = theme;
        SettingsManager.Save(App.Settings);
        
        foreach (var item in StoreViewModel.PlatformDownloadInfo)
        {
            item.NotifyThemeChanged();
        }
    }
    
    [RelayCommand]
    private static async Task ExitAsync()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
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
        }
    }
        
    [RelayCommand]
    private static async Task CheckUpdate()
    {
        await UpdateService.Start();
    }
    
    [RelayCommand]
    private static void AppDownload(string parameter)
    {
        HttpClientHelper.OpenUrl(parameter);
    }
}