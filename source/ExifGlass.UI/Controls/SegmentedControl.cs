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
using Avalonia.Controls.Primitives;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using ExifGlass.UI.Tokens;

namespace ExifGlass.UI.Controls;

/// <summary>
/// The segmented control (DS 6.5): 32 high, outer radius 14, padding 3; the thumb (radius 11) slides with the soft spring in 420 ms.
/// </summary>
public class SegmentedControl : ListBox
{
    #region Fields and constants

    private readonly DoubleTransition _widthTransition = new() { Property = WidthProperty, Duration = MotionTokens.SegmentedThumb, Easing = MotionTokens.SegmentedThumbEasing };
    private readonly TransformOperationsTransition _moveTransition = new() { Property = RenderTransformProperty, Duration = MotionTokens.SegmentedThumb, Easing = MotionTokens.SegmentedThumbEasing };
    private Control? _indicator;
    private Visual? _host;
    private bool _hasPlaced;
    private Rect _target;

    #endregion // Fields and constants


    #region Properties

    protected override Type StyleKeyOverride => typeof(SegmentedControl);

    #endregion // Properties


    #region Protected methods

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey) => NeedsContainer<SegmentedItem>(item, out recycleKey);

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) => new SegmentedItem();


    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _indicator = e.NameScope.Find<Control>("PART_Indicator");
        _host = e.NameScope.Find<Visual>("PART_IndicatorHost");
        _hasPlaced = false;
        _target = default;

        if (_indicator is not null) _indicator.Transitions = [_widthTransition, _moveTransition];
    }


    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SelectedIndexProperty) PlaceIndicator(animate: _hasPlaced);
    }


    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);

        // containers have their final bounds only after arrange; a resize follows without motion
        Dispatcher.UIThread.Post(() => PlaceIndicator(animate: false), DispatcherPriority.Render);
        return size;
    }

    #endregion // Protected methods


    #region Private methods

    private void PlaceIndicator(bool animate)
    {
        if (_indicator is null || _host is null) return;

        var container = SelectedIndex >= 0 ? ContainerFromIndex(SelectedIndex) : null;
        if (container is null || container.Bounds.Width <= 0 || container.TranslatePoint(default, _host) is not { } origin)
        {
            _indicator.IsVisible = false;
            return;
        }

        var target = new Rect(origin, container.Bounds.Size);
        if (_hasPlaced && target == _target) return;

        var instant = !animate || MotionTokens.IsFrozen;
        _widthTransition.Duration = instant ? TimeSpan.Zero : MotionTokens.SegmentedThumb;
        _moveTransition.Duration = instant ? TimeSpan.Zero : MotionTokens.SegmentedThumb;

        _target = target;
        _indicator.IsVisible = true;
        _indicator.Height = target.Height;
        _indicator.Width = target.Width;
        _indicator.RenderTransform = TransformOperations.Parse(FormattableString.Invariant($"translate({target.X}px, {target.Y}px)"));
        _hasPlaced = true;
    }

    #endregion // Private methods
}


/// <summary>
/// One option of a SegmentedControl: icon 14 and a label, Medium and TextPrimary when selected.
/// </summary>
public class SegmentedItem : ListBoxItem
{
    #region Fields and constants

    public static readonly StyledProperty<IconId?> IconProperty =
        AvaloniaProperty.Register<SegmentedItem, IconId?>(nameof(Icon));

    #endregion // Fields and constants


    #region Properties

    public IconId? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(SegmentedItem);

    #endregion // Properties


    #region Protected methods

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IconProperty) PseudoClasses.Set(":icon", Icon is not null);
    }

    #endregion // Protected methods
}
