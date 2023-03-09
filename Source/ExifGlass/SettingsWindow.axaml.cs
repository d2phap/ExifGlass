using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;

namespace ExifGlass;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();

        GotFocus += SettingsWindow_GotFocus;
        LostFocus += SettingsWindow_LostFocus;
    }


    // Control events
    #region Control events

    private void SettingsWindow_GotFocus(object? sender, GotFocusEventArgs e)
    {
        this.SetDynamicResource(BackgroundProperty, "SystemAltMediumHighColor");
    }

    private void SettingsWindow_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (Application.Current is not Application app) return;

        if (app.ActualThemeVariant == ThemeVariant.Dark)
        {
            Background = new SolidColorBrush(Color.FromRgb(32, 32, 32));
        }
        else
        {
            Background = new SolidColorBrush(Color.FromRgb(243, 243, 243));
        }
    }

    #endregion // Control events



}