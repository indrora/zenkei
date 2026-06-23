using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Zenkei.Views;

public partial class PropertiesView : UserControl
{
    private const double WideThreshold = 260;

    public PropertiesView()
    {
        InitializeComponent();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        ApplyAdaptiveLayout(e.NewSize.Width);
    }

    private void ApplyAdaptiveLayout(double width)
    {
        var nudsPanel = this.FindControl<StackPanel>("NudsPanel");
        if (nudsPanel is null) return;

        bool wide = width >= WideThreshold;

        Grid.SetColumn(nudsPanel, wide ? 1 : 0);
        Grid.SetRow(nudsPanel, wide ? 0 : 1);
        nudsPanel.Margin = wide ? new Thickness(6, 0, 0, 0) : new Thickness(0, 4, 0, 0);
        nudsPanel.VerticalAlignment = wide ? VerticalAlignment.Center : VerticalAlignment.Top;
    }
}
