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
using Avalonia.Controls;
using Avalonia.Input;
using System.Threading.Tasks;

namespace ExifGlass.Core;

public partial class Popup : StyledWindow
{
    private TaskCompletionSource<PopupResult>? _result = null;


    public Popup()
    {
        InitializeComponent();
    }


    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);

        if (_result == null)
        {
            _result = new();
            _ = _result.TrySetResult(PopupResult.Cancel);
        }
    }


    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None)
        {
            Accept();
        }
        else if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None)
        {
            Cancel();
        }
    }


    private void BtnOK_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Accept();
    }


    private void Accept()
    {
        _result ??= new();
        _ = _result.TrySetResult(PopupResult.OK);
        Close();
    }


    private void Cancel()
    {
        Close();
    }


    /// <summary>
    /// Shows popup.
    /// </summary>
    public async Task<PopupResult> ShowPopupAsync(Window? parent,
        string? content = null,
        string? heading = null,
        string? title = null)
    {
        Title = title ?? string.Empty;

        if (this.FindControl<TextBlock>(nameof(TxtHeading)) is TextBlock txtHeading)
        {
            txtHeading.Text = heading ?? string.Empty;
        }
        if (this.FindControl<SelectableTextBlock>(nameof(TxtContent)) is SelectableTextBlock txtContent)
        {
            txtContent.Text = content ?? string.Empty;
        }


        if (parent != null)
        {
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Topmost = parent.Topmost;

            await ShowDialog(parent);
        }
        else
        {
            Show();
        }

        return await (_result?.Task ?? Task.FromResult(PopupResult.Cancel));
    }


    /// <summary>
    /// Show popup.
    /// </summary>
    public static async Task<PopupResult> ShowAsync(Window? parent,
        string? content = null,
        string? heading = null,
        string? title = null)
    {
        var popup = new Popup();

        return await popup.ShowPopupAsync(parent, content, heading, title);
    }

}


public enum PopupResult
{
    OK,
    Cancel,
}

