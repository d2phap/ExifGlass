/*
ExifGlass - Standalone Exif tool for ImageGlass
Copyright (C) 2023-2024 DUONG DIEU PHAP
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
using Avalonia.Threading;
using ExifGlass.Core;
using ExifGlass.ExifTools;
using ImageGlass.Tools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ExifGlass;

public partial class MainWindow : StyledWindow
{
    private readonly ImageGlassTool _igTool = new();

    private readonly ExifTool _exifTool = [];
    private string _filePath = string.Empty;


    public MainWindow()
    {
        InitializeComponent();

        // ImageGlass tool events
        _ = ConnectToImageGlassAsync();

        // controls events
        SizeChanged += MainWindow_SizeChanged;
        PropertyChanged += MainWindow_PropertyChanged;
        AddHandler(DragDrop.DragOverEvent, OnFileDragOver);
        AddHandler(DragDrop.DropEvent, OnFileDrop);

        DtGrid.LoadingRowGroup += DtGrid_LoadingRowGroup;
        DtGrid.LoadingRow += DtGrid_LoadingRow;
        DtGrid.KeyDown += DtGrid_KeyDown;
        DtGrid.CellPointerPressed += DtGrid_CellPointerPressed;

        BtnOpenFile.Click += BtnOpenFile_Click;
        BtnCopy.Click += BtnCopy_Click;


        MnuExportText.Click += MnuExportText_Click;
        MnuExportCsv.Click += MnuExportCsv_Click;
        MnuExportJson.Click += MnuExportJson_Click;

        MnuSettings.Click += MnuSettings_Click;
        MnuAbout.Click += MnuAbout_Click;
        MnuCheckForUpdate.Click += MnuCheckForUpdate_Click;
        MnuExit.Click += MnuExit_Click;
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

        Title = $"ExifGlass {Config.AppVersion}";

        var args = Environment.GetCommandLineArgs();
        if (args.Length >= 2)
        {
            // get path from params
            var filePath = args
                .Skip(1)
                .FirstOrDefault(i => !i.StartsWith('-'));

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
        SizeChanged -= MainWindow_SizeChanged;
        PropertyChanged -= MainWindow_PropertyChanged;
        RemoveHandler(DragDrop.DragOverEvent, OnFileDragOver);
        RemoveHandler(DragDrop.DropEvent, OnFileDrop);

        DtGrid.LoadingRowGroup -= DtGrid_LoadingRowGroup;
        DtGrid.LoadingRow -= DtGrid_LoadingRow;
        DtGrid.KeyDown -= DtGrid_KeyDown;
        DtGrid.CellPointerPressed -= DtGrid_CellPointerPressed;

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

        // delete temporary files
        _exifTool.DeleteTempFiles();
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
        // Escape
        else if (e.Key == Key.Escape)
        {
            MnuExit_Click(MnuExit, new RoutedEventArgs());
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

    private void DtGrid_CellPointerPressed(object? sender, DataGridCellPointerPressedEventArgs e)
    {
        // fixed: selected cell is not updated when context menu is open
        if (e.Column.Tag != DtGrid.CurrentColumn.Tag)
        {
            DtGrid.CurrentColumn = e.Column;
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


    private void MnuContext_Opening(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (sender is not ContextMenu mnu) return;
        if (DtGrid.SelectedItem is not ExifTagItem item) return;

        var header = DtGrid.CurrentColumn.Tag?.ToString() ?? string.Empty;
        var value = item.GetType().GetProperty(header)?.GetValue(item)?.ToString();
        if (value == null) return;

        if (mnu.ItemsView.FirstOrDefault(i => i is MenuItem
            {
                Name: nameof(MnuExtractData)
            }) is not MenuItem mnuItem)
            return;

        mnuItem.IsVisible = value.Contains("use -b option to extract", StringComparison.InvariantCultureIgnoreCase);
    }


    private void MnuExtractData_Click(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not MenuItem mnu) return;
        if (mnu.DataContext is not ExifTagItem item) return;

        _ = ExtractTagBinaryDataAsync(item.TagName);
    }


    private async Task ExtractTagBinaryDataAsync(string tagName)
    {
        var tagNameNoSpace = tagName.Replace(" ", "");
        var defaultFilename = $"{Path.GetFileNameWithoutExtension(_exifTool.OriginalFilePath)}_{tagNameNoSpace}.jpg";
        var typeChoices = new List<FilePickerFileType>
        {
            new("JPG file (*.jpg)")
            {
                Patterns = ["*.jpg"],
            },
            new("All files (*.*)")
            {
                Patterns = ["*.*"],
            }
        };

        var destFile = await StorageProvider.SaveFilePickerAsync(new()
        {
            ShowOverwritePrompt = true,
            SuggestedFileName = defaultFilename,
            DefaultExtension = Path.GetExtension(defaultFilename),
            FileTypeChoices = typeChoices,
        });
        if (destFile?.TryGetLocalPath() is not string destFilePath) return;


        try
        {
            await _exifTool.ExtractTagAsync(tagName, destFilePath);
        }
        catch (Exception ex)
        {
            _ = await Popup.ShowAsync(this, ex.Message, "❌ Error");
        }
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

        win.Show(this);
    }


    private void MnuCheckForUpdate_Click(object? sender, RoutedEventArgs e)
    {
        _ = App.CheckForUpdateAsync(true);
    }


    private void MnuExit_Click(object? sender, RoutedEventArgs e)
    {
        Close();
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
            Title = $"ExifGlass v{Config.AppVersion.ToString(2)}";
            DtGrid.ItemsSource = Enumerable.Empty<object>();
            return;
        }


        // show command preview
        _filePath = filePath;
        _exifTool.ExifToolPath = Config.ExifToolExecutable;

        Title = $"ExifGlass v{Config.AppVersion.ToString(2)} - {_filePath}";
        TxtCmd.Text = $"{_exifTool.CurrentExifToolPath} {ExifTool.DefaultCommands} {Config.ExifToolArguments} \"{filePath}\"";

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


            TxtError.Text = "\r\n❌ Error:\r\n";
            if (ex.Message.Contains("Target file or working directory doesn't exist"))
            {
                TxtError.Text += "\"exiftool.exe\" is not installed or ExifGlass could not find the path. To resolve this issue, please open the app settings and update the \"Excutable path\".";
            }
            else
            {
                TxtError.Text += ex.Message + "\r\n\r\nℹ️ Details:\r\n" +
                    ex.ToString();
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
                MimeTypes = ["text/plain"],
                Patterns = ["*.txt"],
            });
        }

        if (isExtEmpty || defaultExt.Equals(".csv", StringComparison.InvariantCultureIgnoreCase))
        {
            typeChoices.Add(new FilePickerFileType("CSV file (*.csv)")
            {
                MimeTypes = ["text/csv"],
                Patterns = ["*.csv"],
            });
        }

        if (isExtEmpty || defaultExt.Equals(".json", StringComparison.InvariantCultureIgnoreCase))
        {
            typeChoices.Add(new FilePickerFileType("JSON file (*.json)")
            {
                MimeTypes = ["application/json"],
                Patterns = ["*.json"],
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