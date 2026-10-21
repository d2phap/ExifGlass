/*
ExifGlass - AI & EXIF metadata viewer
Copyright (C) 2023 - 2026 DUONG DIEU PHAP
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
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ExifGlass.Services;

namespace ExifGlass.Windows;

public partial class MainWindow
{
    #region Fields and constants

    // a drag that leaves one control enters the next in the same pass; only a leave nothing follows ends it
    private static readonly TimeSpan DragLeaveGrace = TimeSpan.FromMilliseconds(60);

    private bool _dragOver;
    private int _dragGeneration;

    #endregion // Fields and constants


    #region Properties

    /// <summary>
    /// Gets whether a file is being dragged over the window.
    /// </summary>
    public bool IsDragOver => _dragOver;

    #endregion // Properties


    #region Private methods

    private void InitializeInput()
    {
        // shortcuts tunnel, so a focused text box can't swallow them; Esc bubbles, so menus, dialogs and the search box use it first
        AddHandler(KeyDownEvent, Window_PreviewKeyDown, RoutingStrategies.Tunnel);
        AddHandler(KeyDownEvent, Window_KeyDown, RoutingStrategies.Bubble);

        AddHandler(DragDrop.DragEnterEvent, Window_DragOver);
        AddHandler(DragDrop.DragOverEvent, Window_DragOver);
        AddHandler(DragDrop.DragLeaveEvent, Window_DragLeave);
        AddHandler(DragDrop.DropEvent, Window_Drop);
    }


    private void SetDragOver(bool over)
    {
        if (_dragOver == over) return;

        _dragOver = over;
        if (over && ViewModel.HasDocument) Scrim.Show();
        else Scrim.Hide();

        Table.EmptyCard.IsDragOver = over && !ViewModel.HasDocument;
    }


    private static (List<string> Files, int Folders) ReadDropped(DragEventArgs e)
    {
        var files = new List<string>();
        var folders = 0;
        foreach (var item in e.DataTransfer.TryGetFiles() ?? [])
        {
            if (item is IStorageFolder) folders++;
            else if (item.TryGetLocalPath() is { } path) files.Add(path);
        }

        return (files, folders);
    }

    #endregion // Private methods


    #region Event handlers

    private void Window_PreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (Shortcuts.Is(e, Key.K)) ViewModel.FocusSearchCommand.Execute(null);
        else if (Shortcuts.Is(e, Key.O)) ViewModel.OpenCommand.Execute(null);
        else if (Shortcuts.Is(e, Key.OemComma)) ViewModel.OpenSettingsCommand.Execute(null);
        else if (e.Key == Key.F1 && e.KeyModifiers == KeyModifiers.None) ViewModel.OpenAboutCommand.Execute(null);
        else return;

        e.Handled = true;
    }


    // DS 7.6 Esc chain after the focused control's own use: close a pushed panel, then clear the search; Esc never closes the window
    private void Window_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || e.KeyModifiers != KeyModifiers.None) return;

        e.Handled = ViewModel.Escape();
    }


    private void Window_DragOver(object? sender, DragEventArgs e)
    {
        if (!e.DataTransfer.Contains(DataFormat.File))
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        e.DragEffects = DragDropEffects.Copy;
        _dragGeneration++;
        SetDragOver(true);
    }


    private void Window_DragLeave(object? sender, DragEventArgs e)
    {
        var generation = ++_dragGeneration;
        DispatcherTimer.RunOnce(() =>
        {
            if (generation == _dragGeneration) SetDragOver(false);
        }, DragLeaveGrace);
    }


    private void Window_Drop(object? sender, DragEventArgs e)
    {
        _dragGeneration++;
        SetDragOver(false);

        var (files, folders) = ReadDropped(e);
        ViewModel.OpenDropped(files, folders);
        e.Handled = true;
    }

    #endregion // Event handlers
}
