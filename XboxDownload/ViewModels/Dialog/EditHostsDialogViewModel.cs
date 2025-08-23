using System;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XboxDownload.Helpers.IO;

namespace XboxDownload.ViewModels.Dialog;

public partial class  EditHostsDialogViewModel : ObservableObject
{
    public EditHostsDialogViewModel()
    {
        _ = ReadHostsAsync();
    }
    
    public Action? CloseDialog { get; init; }
    
    [ObservableProperty]
    private string _content = string.Empty;
    
    [RelayCommand]
    private async Task SaveHostsAsync()
    {
        await File.WriteAllTextAsync(PathHelper.SystemHostsPath, Content.Trim() + Environment.NewLine);
        CloseDialog?.Invoke();
    }
    
    [RelayCommand]
    private async Task ReadHostsAsync()
    {
        Content = await File.ReadAllTextAsync(PathHelper.SystemHostsPath);
    }
}