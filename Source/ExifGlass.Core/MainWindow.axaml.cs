/*
ExifGlass - Standalone Exif tool for ImageGlass
Copyright (C) 2023 DUONG DIEU PHAP
Project homepage: https://github.com/d2phap/ExifGlass

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using ExifGlass.ExifTools;
using ImageGlass.Tools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ExifGlass;

public partial class MainWindow : Window
{
    private readonly ImageGlassTool _igTool = new();

    private readonly ExifTool _exifTool = new("exiftool");
    private string _filePath = string.Empty;


    public MainWindow()
    {
        InitializeComponent();

        // ImageGlass tool events
        _ = ConnectToImageGlassAsync();

        // controls events
        GotFocus += MainWindow_GotFocus;
        LostFocus += MainWindow_LostFocus;
        SizeChanged += MainWindow_SizeChanged;
        PropertyChanged += MainWindow_PropertyChanged;
        AddHandler(DragDrop.DragOverEvent, OnFileDragOver);
        AddHandler(DragDrop.DropEvent, OnFileDrop);

        DtGrid.LoadingRowGroup += DtGrid_LoadingRowGroup;
        DtGrid.LoadingRow += DtGrid_LoadingRow;
        DtGrid.KeyDown += DtGrid_KeyDown;

        BtnOpenFile.Click += BtnOpenFile_Click;
        BtnCopy.Click += BtnCopy_Click;

        MnuExportText.Click += MnuExportText_Click;
        MnuExportCsv.Click += MnuExportCsv_Click;
        MnuExportJson.Click += MnuExportJson_Click;

        MnuSettings.Click += MnuSettings_Click;
        MnuAbout.Click += MnuAbout_Click;
        MnuCheckForUpdate.Click += MnuCheckForUpdate_Click;
    }


    private void MainWindow_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (WindowState == WindowState.Normal
            && e.PreviousSize.Width > 0
            && e.PreviousSize.Height > 0)
        {
            Config.WindowWidth = (int)e.PreviousSize.Width;
            Config.WindowHeight = (int)e.PreviousSize.Height;
        }
    }

    private void MainWindow_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property.Name == nameof(WindowState))
        {
            // restore
            if (WindowState == WindowState.Normal && Config.WindowState == WindowState.Maximized)
            {
                Position = new PixelPoint(Config.WindowPositionX, Config.WindowPositionY);
                Width = Config.WindowWidth;
                Height = Config.WindowHeight;
            }

            Config.WindowState = WindowState;
        }
    }


    // Protected methods
    #region Protected methods

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        var args = Environment.GetCommandLineArgs();
        if (args.Length >= 2)
        {
            // get path from params
            var filePath = args
                .Skip(1)
                .FirstOrDefault(i => !i.StartsWith("-"));

            filePath ??= string.Empty;

            var protocol = "exifglass:";
            if (filePath.StartsWith(protocol, StringComparison.InvariantCultureIgnoreCase))
            {
                filePath = filePath[protocol.Length..];
            }

            _ = LoadExifMetadatAsync(filePath);
        }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);

        // controls events
        GotFocus -= MainWindow_GotFocus;
        LostFocus -= MainWindow_LostFocus;
        SizeChanged -= MainWindow_SizeChanged;
        PropertyChanged -= MainWindow_PropertyChanged;
        RemoveHandler(DragDrop.DragOverEvent, OnFileDragOver);
        RemoveHandler(DragDrop.DropEvent, OnFileDrop);

        DtGrid.LoadingRowGroup -= DtGrid_LoadingRowGroup;
        DtGrid.LoadingRow -= DtGrid_LoadingRow;
        DtGrid.KeyDown -= DtGrid_KeyDown;

        BtnOpenFile.Click -= BtnOpenFile_Click;
        BtnCopy.Click -= BtnCopy_Click;

        MnuExportText.Click -= MnuExportText_Click;
        MnuExportCsv.Click -= MnuExportCsv_Click;
        MnuExportJson.Click -= MnuExportJson_Click;

        MnuSettings.Click -= MnuSettings_Click;
        MnuAbout.Click -= MnuAbout_Click;


        // save configs
        Config.WindowState = WindowState == WindowState.Maximized
                ? WindowState.Maximized
                : WindowState.Normal;

        if (WindowState == WindowState.Normal)
        {
            Config.WindowPositionX = Position.X;
            Config.WindowPositionY = Position.Y;
            Config.WindowWidth = (int)Width;
            Config.WindowHeight = (int)Height;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.KeyModifiers == KeyModifiers.Control)
        {
            // Ctrl + 1
            if (e.Key == Key.D1)
            {
                MnuExportText_Click(MnuExportText, new RoutedEventArgs());
            }
            // Ctrl + 2
            else if (e.Key == Key.D2)
            {
                MnuExportCsv_Click(MnuExportCsv, new RoutedEventArgs());
            }
            // Ctrl + 3
            else if (e.Key == Key.D3)
            {
                MnuExportJson_Click(MnuExportJson, new RoutedEventArgs());
            }
            // Ctrl + ,
            else if (e.Key == Key.OemComma)
            {
                MnuSettings_Click(MnuSettings, new RoutedEventArgs());
            }
        }
        // F1
        else if (e.Key == Key.F1)
        {
            MnuAbout_Click(MnuAbout, new RoutedEventArgs());
        }
    }

    #endregion // Protected methods


    // ImageGlassTool connection
    #region ImageGlassTool connection

    private async Task ConnectToImageGlassAsync()
    {
        _igTool.ToolMessageReceived += IgTool_ToolMessageReceived;
        _igTool.ToolClosingRequest += IgTool_ToolClosingRequest;
        await _igTool.ConnectAsync();
    }


    private void IgTool_ToolClosingRequest(object? sender, DisconnectedEventArgs e)
    {
        Dispatcher.UIThread.Post(Close);
    }


    private void IgTool_ToolMessageReceived(object? sender, MessageReceivedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.MessageData)) return;


        // update image list
        if (e.MessageName.Equals(ImageGlassEvents.IMAGE_LOADING, StringComparison.InvariantCultureIgnoreCase))
        {
            var obj = JsonSerializer.Deserialize(e.MessageData, IgImageLoadingEventArgsJsonContext.Default.IgImageLoadingEventArgs);
            if (obj == null) return;


            Dispatcher.UIThread.Post(async delegate
            {
                await LoadExifMetadatAsync(obj.FilePath);
            });

            return;
        }
    }


    #endregion // ImageGlassTool connection


    // File drag-n-drop
    #region File drag-n-drop

    private void OnFileDragOver(object? sender, DragEventArgs e)
    {
        // check if the drag data contains a file
        if (e.Data.GetFiles()?.FirstOrDefault() is IStorageItem sFile
            && !File.GetAttributes(sFile.Path.LocalPath).HasFlag(FileAttributes.Directory))
        {
            e.DragEffects = DragDropEffects.Copy | DragDropEffects.Link;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }


    private void OnFileDrop(object? sender, DragEventArgs e)
    {
        // check if the drag data contains a file
        if (e.Data.GetFiles()?.FirstOrDefault() is IStorageItem sFile
            && !File.GetAttributes(sFile.Path.LocalPath).HasFlag(FileAttributes.Directory))
        {
            _ = LoadExifMetadatAsync(sFile.Path.LocalPath);
        }
    }

    #endregion // File drag-n-drop


    // Control events
    #region Control events

    private void MainWindow_GotFocus(object? sender, GotFocusEventArgs e)
    {
        this.SetDynamicResource(BackgroundProperty, "SystemAltMediumHighColor");
    }

    private void MainWindow_LostFocus(object? sender, RoutedEventArgs e)
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


    private void DtGrid_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers == KeyModifiers.Control)
        {
            // Ctrl + C
            if (e.Key == Key.C)
            {
                BtnCopy_Click(BtnCopy, new RoutedEventArgs());
            }
        }
    }


    private void DtGrid_LoadingRow(object? sender, DataGridRowEventArgs e)
    {
        if (e.Row.DataContext is not ExifTagItem item) return;
        e.Row.Padding = new Thickness(6);
        e.Row.Height = e.Row.FontSize + e.Row.Padding.Top + e.Row.Padding.Bottom;

        if (item.TagName.Equals("File Name", StringComparison.InvariantCultureIgnoreCase))
        {
            e.Row.FontWeight = FontWeight.SemiBold;
        }
        else
        {
            e.Row.FontWeight = FontWeight.Normal;
        }
    }

    private void DtGrid_LoadingRowGroup(object? sender, DataGridRowGroupHeaderEventArgs e)
    {
        e.RowGroupHeader.FontSize = 14;
        e.RowGroupHeader.FontWeight = FontWeight.SemiBold;
        e.RowGroupHeader.IsItemCountVisible = true;
    }


    private void BtnOpenFile_Click(object? sender, RoutedEventArgs e)
    {
        _ = PickFileAndLoadExifMetadataAsync();
    }

    private async Task PickFileAndLoadExifMetadataAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new());
        if (files.SingleOrDefault() is not IStorageFile file) return;

        await LoadExifMetadatAsync(file.Path.LocalPath);
    }


    private void BtnCopy_Click(object? sender, RoutedEventArgs e)
    {
        if (DtGrid.SelectedItem is not ExifTagItem item) return;

        var header = DtGrid.CurrentColumn.Tag?.ToString() ?? string.Empty;
        var value = item.GetType().GetProperty(header)?.GetValue(item)?.ToString();
        if (value == null) return;

        var clipboard = GetTopLevel(this)?.Clipboard;
        _ = clipboard?.SetTextAsync(value);
    }


    private void MnuExportText_Click(object? sender, RoutedEventArgs e)
    {
        _ = ExportToFileAsync(ExportFileType.Text);
    }


    private void MnuExportCsv_Click(object? sender, RoutedEventArgs e)
    {
        _ = ExportToFileAsync(ExportFileType.CSV);
    }


    private void MnuExportJson_Click(object? sender, RoutedEventArgs e)
    {
        _ = ExportToFileAsync(ExportFileType.JSON);
    }


    private void MnuSettings_Click(object? sender, RoutedEventArgs e)
    {
        _ = ShowSettingsAsync();
    }


    private void MnuAbout_Click(object? sender, RoutedEventArgs e)
    {
        var win = new AboutWindow()
        {
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Topmost = Config.EnableWindowTopMost,
        };

        _ = win.ShowDialog(this);
    }


    private void MnuCheckForUpdate_Click(object? sender, RoutedEventArgs e)
    {
        _ = App.CheckForUpdateAsync(true);
    }


    private async Task ShowSettingsAsync()
    {
        if (Screens.ScreenFromVisual(this) is not Screen screen) return;


        var win = new SettingsWindow()
        {
            WindowStartupLocation = WindowStartupLocation.Manual,
        };

        // calculate the best position to show dialog
        var ownerActualWidth = (int)(Width * screen.Scaling);
        var childActualWidth = (int)(win.Width * screen.Scaling);
        var minChildVisibleWidth = (int)(childActualWidth / 1.5f);

        var ownerLeft = Position.X;
        var ownerRight = ownerLeft + ownerActualWidth;
        var childPosition = new PixelPoint(
            Position.X + ownerActualWidth,
            Position.Y);

        var leftGap = ownerLeft - screen.WorkingArea.X;
        var rightGap = screen.WorkingArea.Right - ownerRight;

        if (leftGap < minChildVisibleWidth && rightGap < minChildVisibleWidth)
        {
            win.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
        else if (rightGap > leftGap)
        {
            childPosition = childPosition.WithX(Math.Min(screen.WorkingArea.Right - childActualWidth, ownerRight));
        }
        else
        {
            var x = ownerLeft - childActualWidth;
            childPosition = childPosition.WithX(Math.Max(screen.WorkingArea.X, x));
        }


        win.Position = childPosition;
        await win.ShowDialog(this);

        // apply settings
        if (win.Result == SettingsResult.OK)
        {
            Topmost = Config.EnableWindowTopMost;

            // reload exif metadata
            await LoadExifMetadatAsync(_filePath);
        }
    }

    #endregion // Control events


    // Private methods
    #region Private methods

    /// <summary>
    /// Loads EXIF metadata.
    /// </summary>
    private async Task LoadExifMetadatAsync(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            Title = "ExifGlass";
            DtGrid.ItemsSource = Enumerable.Empty<object>();
            return;
        }


        // show command preview
        _filePath = filePath;
        Title = $"ExifGlass - {_filePath}";
        TxtCmd.Text = $"{Config.ExifToolExecutable} {ExifTool.DefaultCommands} {Config.ExifToolArguments} \"{filePath}\"";

        _exifTool.ExifToolPath = Config.ExifToolExecutable;
        try
        {
            await _exifTool.ReadAsync(filePath, default, Config.ExifToolArguments);

            BoxExifGrid.IsVisible = true;
            BoxError.IsVisible = false;
        }
        catch (Exception ex)
        {
            BoxExifGrid.IsVisible = false;
            BoxError.IsVisible = true;

            if (ex.Message.Contains("Target file or working directory doesn't exist"))
            {
                TxtError.Text = "Error:\nExifGlass was unable to locate the path to the ExifTool executable. To resolve this issue, please navigate to the Settings menu (using Ctrl+Comma) and update the path to ExifTool as necessary.";
            }
            else
            {
                TxtError.Text = ex.Message;
            }
        }


        // create groups
        var groupView = new DataGridCollectionView(_exifTool);
        groupView.GroupDescriptions.Add(new DataGridPathGroupDescription(nameof(ExifTagItem.TagGroup)));

        // load results into grid
        DtGrid.ItemsSource = groupView;


        BtnCopy.IsEnabled = BtnExport.IsEnabled = _exifTool.Any();
    }


    /// <summary>
    /// Exports Exif metadata to file.
    /// </summary>
    private async Task ExportToFileAsync(ExportFileType fileType)
    {
        var extension = fileType switch
        {
            ExportFileType.Text => ".txt",
            ExportFileType.CSV => ".csv",
            ExportFileType.JSON => ".json",
            _ => "",
        };

        if (await OpenSaveFileDialog(extension) is not IStorageFile destFile) return;

        await _exifTool.ExportAs(fileType, destFile);
    }


    /// <summary>
    /// Opens Save file dialog.
    /// </summary>
    private async Task<IStorageFile?> OpenSaveFileDialog(string defaultExt = "")
    {
        var isExtEmpty = string.IsNullOrEmpty(defaultExt);
        var fileName = Path.GetFileNameWithoutExtension(_filePath);
        var defaultFilename = $"{fileName}{defaultExt}";
        var typeChoices = new List<FilePickerFileType>();

        if (isExtEmpty || defaultExt.Equals(".txt", StringComparison.InvariantCultureIgnoreCase))
        {
            typeChoices.Add(new FilePickerFileType("Text file (*.txt)")
            {
                MimeTypes = new string[] { "text/plain" },
                Patterns = new string[] { "*.txt" },
            });
        }

        if (isExtEmpty || defaultExt.Equals(".csv", StringComparison.InvariantCultureIgnoreCase))
        {
            typeChoices.Add(new FilePickerFileType("CSV file (*.csv)")
            {
                MimeTypes = new string[] { "text/csv" },
                Patterns = new string[] { "*.csv" },
            });
        }

        if (isExtEmpty || defaultExt.Equals(".json", StringComparison.InvariantCultureIgnoreCase))
        {
            typeChoices.Add(new FilePickerFileType("JSON file (*.json)")
            {
                MimeTypes = new string[] { "application/json" },
                Patterns = new string[] { "*.json" },
            });
        }

        var fileSaver = await StorageProvider.SaveFilePickerAsync(new()
        {
            ShowOverwritePrompt = true,
            SuggestedFileName = defaultFilename,
            DefaultExtension = defaultExt,
            FileTypeChoices = typeChoices,
        });

        return fileSaver;
    }


    #endregion // Private methods


}