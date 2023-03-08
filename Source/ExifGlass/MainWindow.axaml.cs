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
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Themes.Fluent;
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
    }


    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);


        var args = Environment.GetCommandLineArgs();
        if (args.Length > 1)
        {
            _ = LoadExifInfoAsync(args[1]);
        }
    }


    /// <summary>
    /// Loads EXIF metadata.
    /// </summary>
    private async Task LoadExifInfoAsync(string? filePath)
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

            Dispatcher.UIThread.Post(() => _ = LoadExifInfoAsync(filePath));

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

            _ = LoadExifInfoAsync(filePath);
        }
    }

    #endregion // File drag-n-drop


    // Control events
    #region Control events

    private void MainWindow_GotFocus(object? sender, GotFocusEventArgs e)
    {
        TransparencyLevelHint = WindowTransparencyLevel.Mica;
    }

    private void MainWindow_LostFocus(object? sender, RoutedEventArgs e)
    {
        TransparencyLevelHint = WindowTransparencyLevel.None;
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


    private async void BtnOpenFile_Click(object? sender, RoutedEventArgs e)
    {
        var openFd = new OpenFileDialog();

        if (await openFd.ShowAsync(this) is string[] files
            && files.Length > 0)
        {
            _ = LoadExifInfoAsync(files[0]);
        }
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

    #endregion // Control events


}