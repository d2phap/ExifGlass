using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using System.Threading.Tasks;

namespace ExifGlass;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();

        GotFocus += SettingsWindow_GotFocus;
        LostFocus += SettingsWindow_LostFocus;

        BtnSelectExecutable.Click += BtnSelectExecutable_Click;
        BtnOK.Click += BtnOK_Click;
        BtnCancel.Click += BtnCancel_Click;
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


    private void BtnSelectExecutable_Click(object? sender, RoutedEventArgs e)
    {
        _ = OpenFilePickerAsync();
    }

    private async Task OpenFilePickerAsync()
    {
        var filePicker = await StorageProvider.OpenFilePickerAsync(new()
        {
            AllowMultiple = false,
            FileTypeFilter = new FilePickerFileType[]
            {
                new FilePickerFileType("ExifTool's binary file")
                {
                    Patterns = new[] { "*.exe" }
                },
            },
        });
        if (filePicker == null || filePicker.Count == 0) return;

        TxtExecutable.Text = filePicker[0].Path.LocalPath;
    }

    private void BtnOK_Click(object? sender, RoutedEventArgs e)
    {

    }

    private void BtnCancel_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }


    #endregion // Control events



}