using Avalonia.Controls;
using XboxDownload.ViewModels.Dialog;

namespace XboxDownload.Views.Dialog;

public partial class StartupSettingsDialog : Window
{
    public StartupSettingsDialog()
    {
        InitializeComponent();
        
        var vm = new StartupSettingsDialogViewModel()
        {
            CloseDialog = () => Close(null)
        };
        
        DataContext = vm;
    }
}