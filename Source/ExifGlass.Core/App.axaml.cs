using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using System;
using System.Globalization;
using System.Threading.Tasks;

namespace ExifGlass;

public partial class App : Application
{
    public static readonly UpdateService Updater = new();


    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }


    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // load user configs
            Config.Load();
            _ = CheckAndRunAutoUpdateAsync();

            // load Theme mode
            Config.ApplyThemeMode(Config.ThemeMode);


            desktop.MainWindow = new MainWindow()
            {
                Position = new PixelPoint(Config.WindowPositionX, Config.WindowPositionY),
                Width = Config.WindowWidth,
                Height = Config.WindowHeight,
                WindowState = Config.WindowState,
                Topmost = Config.EnableWindowTopMost,
            };
            desktop.ShutdownRequested += Desktop_ShutdownRequested;
        }

        base.OnFrameworkInitializationCompleted();
    }


    private void Desktop_ShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        _ = Config.SaveAsync();
    }


    /// <summary>
    /// Checks and runs auto-update.
    /// </summary>
    public static async Task CheckAndRunAutoUpdateAsync()
    {
        var shouldCheckForUpdate = false;

        if (Config.AutoUpdate != "0")
        {
            if (DateTime.TryParseExact(
                Config.AutoUpdate,
                Config.DATETIME_FORMAT,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var lastUpdate))
            {
                // Check for update every 5 days
                if (DateTime.UtcNow.Subtract(lastUpdate).TotalDays > 5)
                {
                    shouldCheckForUpdate = true;
                }
            }
            else
            {
                shouldCheckForUpdate = true;
            }
        }


        if (shouldCheckForUpdate == true)
        {
            await CheckForUpdateAsync(false);
        }
    }


    /// <summary>
    /// Check for updatae
    /// </summary>
    /// <param name="alwaysShowUI">
    /// Set to <c>true</c> if you want to show the Update dialog. Default value is <c>false</c>.
    /// </param>
    public static async Task CheckForUpdateAsync(bool? alwaysShowUI = null)
    {
        await Updater.GetUpdatesAsync();

        // save last update
        Config.AutoUpdate = DateTime.UtcNow.ToString(Config.DATETIME_FORMAT);


        alwaysShowUI ??= false;
        if (Updater.HasNewUpdate || alwaysShowUI.Value)
        {
            // show update window
            var win = new UpdateWindow()
            {
                WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterScreen,
            };
            win.Show();
        }
    }

}
