using System.Windows;
using System.Windows.Controls;

namespace NusaLite.Views;

public partial class IdleView : UserControl
{
    public static readonly DependencyProperty ShowAltHintProperty = DependencyProperty.Register(
        nameof(ShowAltHint), typeof(bool), typeof(IdleView), new PropertyMetadata(false));

    public bool ShowAltHint
    {
        get => (bool)GetValue(ShowAltHintProperty);
        set => SetValue(ShowAltHintProperty, value);
    }

    public IdleView()
    {
        InitializeComponent();
    }
}
