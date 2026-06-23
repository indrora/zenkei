using Avalonia.Controls;
using Zenkei.Controls;
using Zenkei.ViewModels;

namespace Zenkei.Views;

public partial class PropertiesView : UserControl
{
    public PropertiesView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        var grid = this.FindControl<ZenkeiPropertyGrid>("PropGrid");
        if (grid != null)
            grid.SceneNames = (DataContext as PropertiesViewModel)?.DocumentSceneNames;
    }
}
