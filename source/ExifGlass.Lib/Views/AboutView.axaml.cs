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
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using ExifGlass.UI.Tokens;
using ExifGlass.ViewModels;
using System.ComponentModel;

namespace ExifGlass.Views;

public partial class AboutView : UserControl
{
    #region Fields and constants

    // Proto: the logo springs in from scale 0.6 and -10 degrees, 60 ms after the window opens
    private static readonly TransformOperations LOGO_FROM = TransformOperations.Parse("scale(0.6) rotate(-10deg)");
    private static readonly TransformOperations LOGO_TO = TransformOperations.Parse("none");
    private static readonly TimeSpan LogoDelay = TimeSpan.FromMilliseconds(60);

    private AboutViewModel? _viewModel;

    #endregion // Fields and constants


    #region Constructors

    public AboutView()
    {
        InitializeComponent();
    }

    #endregion // Constructors


    #region Protected methods

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_viewModel is not null)
        {
            _viewModel.ToastRequested -= ViewModel_ToastRequested;
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        _viewModel = DataContext as AboutViewModel;
        if (_viewModel is null) return;

        _viewModel.ToastRequested += ViewModel_ToastRequested;
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
    }


    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        SpringInLogo();
    }

    #endregion // Protected methods


    #region Private methods

    private void SpringInLogo()
    {
        if (MotionTokens.IsFrozen) return;

        Logo.Transitions = null;
        Logo.RenderTransform = LOGO_FROM;
        Dispatcher.UIThread.Post(() =>
        {
            Logo.Transitions =
            [
                new TransformOperationsTransition { Property = RenderTransformProperty, Duration = MotionTokens.LogoSpringIn, Easing = MotionTokens.LogoSpringInEasing, Delay = LogoDelay },
            ];
            Logo.RenderTransform = LOGO_TO;
        }, DispatcherPriority.Render);
    }

    #endregion // Private methods


    #region Event handlers

    private void ViewModel_ToastRequested(object? sender, string message) => Toasts.Show(message);


    // each notice starts at its top with Back focused, as the row that opened it hides with its page
    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(AboutViewModel.OpenNotice) || _viewModel is not { IsNoticeOpen: true }) return;

        NoticeScroller.Offset = default;
        Dispatcher.UIThread.Post(() => NoticeBack.Focus(), DispatcherPriority.Loaded);
    }

    #endregion // Event handlers
}
