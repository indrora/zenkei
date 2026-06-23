using System.Collections.ObjectModel;
using Dock.Model.Mvvm.Controls;
using Zenkei.Services;

namespace Zenkei.ViewModels;

public class OutputViewModel : Tool
{
    public ObservableCollection<LogEntry> Entries => AppLog.Entries;

    public OutputViewModel()
    {
        Id = "Output";
        Title = "Output";
        CanClose = false;
        CanPin = true;
        CanFloat = true;
    }
}
