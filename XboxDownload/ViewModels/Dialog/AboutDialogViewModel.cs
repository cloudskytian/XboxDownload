﻿using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XboxDownload.Helpers.Resources;

namespace XboxDownload.ViewModels.Dialog;

public partial class AboutDialogViewModel : ObservableObject
{
    [ObservableProperty]
    private string? _version = string.Format(ResourceHelper.GetString("About.Version"), 
        Assembly.GetEntryAssembly()?
        .GetCustomAttribute<AssemblyFileVersionAttribute>()?
        .Version);

    [ObservableProperty]
    private bool _isChineseUser = App.Settings.Culture == "zh-Hans";
    
    [RelayCommand]
    private async Task CopyAsync()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow?.Clipboard is not { } provider)
            return;

        await provider.SetTextAsync("TT9CzksU5KuXkkYaox2ifvF5tbGaQRmSZw");
    }
}