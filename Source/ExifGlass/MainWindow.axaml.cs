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
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using ExifGlass.ExifTools;
using ImageGlass.Tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExifGlass;

public partial class MainWindow : Window
{
    // use executable path for server name
    private static string ServerName => $"{ImageGlassTool.PIPENAME_PREFIX}{Process.GetCurrentProcess().MainModule?.FileName}";
    private PipeClient? _client;


    private List<ExifTagItem> _exifTags = new();
    private string _filePath = string.Empty;


    public MainWindow()
    {
        InitializeComponent();


        // initialize pipe client
        InitializePipeClient();
        _ = _client?.ConnectAsync();


        // controls events
        GotFocus += MainWindow_GotFocus;
        LostFocus += MainWindow_LostFocus;
        AddHandler(DragDrop.DragOverEvent, OnFileDragOver);
        AddHandler(DragDrop.DropEvent, OnFileDrop);

        DtGrid.LoadingRowGroup += DtGrid_LoadingRowGroup;
        DtGrid.LoadingRow += DtGrid_LoadingRow;

        BtnSettings.Click += BtnSettings_Click;
        BtnOpenFile.Click += BtnOpenFile_Click;
        BtnCopy.Click += BtnCopy_Click;

        MnuExportText.Click += MnuExportText_Click;
        MnuExportCsv.Click += MnuExportCsv_Click;
        MnuExportJson.Click += MnuExportJson_Click;
    }


    // Protected methods
    #region Protected methods

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);


        var args = Environment.GetCommandLineArgs();
        if (args.Length > 1)
        {
            _ = LoadExifMetadatAsync(args[1]);
        }
    }


    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);

        // controls events
        GotFocus -= MainWindow_GotFocus;
        LostFocus -= MainWindow_LostFocus;
        RemoveHandler(DragDrop.DragOverEvent, OnFileDragOver);
        RemoveHandler(DragDrop.DropEvent, OnFileDrop);

        DtGrid.LoadingRowGroup -= DtGrid_LoadingRowGroup;
        DtGrid.LoadingRow -= DtGrid_LoadingRow;

        BtnSettings.Click -= BtnSettings_Click;
        BtnOpenFile.Click -= BtnOpenFile_Click;
        BtnCopy.Click -= BtnCopy_Click;

        MnuExportText.Click -= MnuExportText_Click;
        MnuExportCsv.Click -= MnuExportCsv_Click;
        MnuExportJson.Click -= MnuExportJson_Click;
    }


    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.KeyModifiers == KeyModifiers.Control)
        {
            if (e.Key == Key.D1)
            {
                MnuExportText_Click(MnuExportText, new RoutedEventArgs());
            }
            else if (e.Key == Key.D3)
            {
                MnuExportCsv_Click(MnuExportCsv, new RoutedEventArgs());
            }
            else if (e.Key == Key.D2)
            {
                MnuExportJson_Click(MnuExportJson, new RoutedEventArgs());
            }
        }
    }

    #endregion // Protected methods


    /// <summary>
    /// Loads EXIF metadata.
    /// </summary>
    private async Task LoadExifMetadatAsync(string? filePath)
    {
        filePath ??= string.Empty;
        var toolPath = @"exiftool";

        // show command preview
        _filePath = filePath;
        TxtCmd.Text = $"{toolPath} {ExifTool.DefaultCommands} \"{filePath}\"";

        var exif = new ExifTool(toolPath);
        try
        {
            _exifTags = await exif.ReadAsync(filePath);
        }
        catch
        {
            _exifTags = new();
        }


        // create groups
        var groupView = new DataGridCollectionView(_exifTags);
        groupView.GroupDescriptions.Add(new DataGridPathGroupDescription(nameof(ExifTagItem.Group)));

        // load results into grid
        DtGrid.Items = groupView;


        if (_exifTags.Any())
        {
            BtnCopy.IsEnabled = BtnExport.IsEnabled = true;
        }
        else
        {
            BtnCopy.IsEnabled = BtnExport.IsEnabled = false;
        }
    }



    // ImageGlass server connection
    #region ImageGlass server connection

    private void InitializePipeClient()
    {
        _client = new PipeClient(ServerName, PipeDirection.InOut);
        _client.MessageReceived += Client_MessageReceived;
        _client.Disconnected += (_, _) => Dispatcher.UIThread.Post(Close);
    }

    private void Client_MessageReceived(object? sender, MessageReceivedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.MessageName)) return;


        // terminate slideshow
        if (e.MessageName == ToolServerMsgs.TOOL_TERMINATE)
        {
            Dispatcher.UIThread.Post(Close);
            return;
        }


        if (string.IsNullOrEmpty(e.MessageData)) return;


        // update image list
        if (e.MessageName.Equals(ToolServerMsgs.IMAGE_LOADING, StringComparison.InvariantCultureIgnoreCase))
        {
            var obj = JsonEx.ParseJson<ExpandoObject>(e.MessageData) as dynamic;
            if (obj == null) return;

            var filePath = obj.FilePath.ToString();

            Dispatcher.UIThread.Post(() => _ = LoadExifMetadatAsync(filePath));

            return;
        }
    }

    #endregion // ImageGlass server connection


    // File drag-n-drop
    #region File drag-n-drop
    private void OnFileDragOver(object? sender, DragEventArgs e)
    {
        // Only allow Copy or Link as Drop Operations.
        e.DragEffects = DragDropEffects.Copy | DragDropEffects.Link;

        // block if dragged data do not contain file path
        if (!e.Data.Contains(DataFormats.FileNames))
        {
            e.DragEffects = DragDropEffects.None;
        }
        else if (e.Data.GetFileNames()?.FirstOrDefault() is string filePath)
        {
            // block if the path is a dirrectory, not a file
            var attrs = File.GetAttributes(filePath);
            if (attrs.HasFlag(FileAttributes.Directory))
            {
                e.DragEffects = DragDropEffects.None;
            }
        }
    }

    private void OnFileDrop(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains(DataFormats.FileNames)
            && e.Data.GetFileNames()?.FirstOrDefault() is string filePath)
        {
            // check if the path is a dirrectory or a file
            var attrs = File.GetAttributes(filePath);
            if (attrs.HasFlag(FileAttributes.Directory)) return;

            _ = LoadExifMetadatAsync(filePath);
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


    private void DtGrid_LoadingRow(object? sender, DataGridRowEventArgs e)
    {
        if (e.Row.DataContext is not ExifTagItem item) return;
        e.Row.Padding = new Thickness(6);
        e.Row.Height = e.Row.FontSize + e.Row.Padding.Top + e.Row.Padding.Bottom;

        if (item.Name.Equals("File Name", StringComparison.InvariantCultureIgnoreCase))
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

        await LoadExifMetadatAsync(file.Path.AbsolutePath);
    }


    private void BtnCopy_Click(object? sender, RoutedEventArgs e)
    {
        if (DtGrid.SelectedItem is not ExifTagItem item) return;

        var header = DtGrid.CurrentColumn.Header?.ToString() ?? string.Empty;
        var value = item.GetType().GetProperty(header)?.GetValue(item)?.ToString();
        if (value == null) return;

        _ = Application.Current?.Clipboard?.SetTextAsync(value);
    }


    private void BtnSettings_Click(object? sender, RoutedEventArgs e)
    {
        if (Application.Current is not Application app) return;
    }


    private void MnuExportText_Click(object? sender, RoutedEventArgs e)
    {

    }

    
    private void MnuExportCsv_Click(object? sender, RoutedEventArgs e)
    {
        _ = ExportAsCsvFileAsync();
    }

    private void MnuExportJson_Click(object? sender, RoutedEventArgs e)
    {
        _ = ExportAsJsonFileAsync();
    }


    private async Task ExportAsCsvFileAsync()
    {
        var csvHeader = $"\"{nameof(ExifTagItem.Index)}\"," +
            $"\"{nameof(ExifTagItem.TagId)}\"," +
            $"\"{nameof(ExifTagItem.Group)}\"," +
            $"\"{nameof(ExifTagItem.Name)}\"," +
            $"\"{nameof(ExifTagItem.Value)}\"\r\n";

        var csvRows = _exifTags
            .Select(i => $"\"{i.Index}\",\"{i.TagId}\",\"{i.Group}\",\"{i.Name}\",\"{i.Value}\"");
        var csvContent = string.Join("\r\n", csvRows);

        await ExportToFileAsync(new FilePickerFileType("CSV file (*.csv)")
        {
            Patterns = new string[] { "*.csv" },
        }, $"{csvHeader}{csvContent}");
    }


    private async Task ExportAsJsonFileAsync()
    {
        var json = JsonEx.ToJson(_exifTags);

        await ExportToFileAsync(new FilePickerFileType("JSON file (*.json)")
        {
            Patterns = new string[] { "*.json" },
        }, json);
    }


    private async Task ExportToFileAsync(FilePickerFileType fileType, string fileContent)
    {
        var fileSaver = await StorageProvider.SaveFilePickerAsync(new()
        {
            ShowOverwritePrompt = true,
            DefaultExtension = fileType.Patterns?.SingleOrDefault(),
            FileTypeChoices = new FilePickerFileType[] { fileType },
        });
        if (fileSaver == null) return;


        using var writer = await fileSaver.OpenWriteAsync();
        var fileBuffer = Encoding.UTF8.GetBytes(fileContent);

        await writer.WriteAsync(new ReadOnlyMemory<byte>(fileBuffer));
        await writer.FlushAsync();
    }

    #endregion // Control events


}