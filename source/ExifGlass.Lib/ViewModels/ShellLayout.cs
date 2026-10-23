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
namespace ExifGlass.ViewModels;

/// <summary>
/// How a side panel shows at the current width.
/// </summary>
public enum PanelMode
{
    /// <summary>
    /// In its own column beside the table.
    /// </summary>
    Docked,

    /// <summary>
    /// Off the edge until shown; then it slides in and pushes the table aside, which keeps its width (owner at G4 review).
    /// </summary>
    Push,
}


/// <summary>
/// What the app toolbar shows at the current width.
/// </summary>
public enum ToolbarMode
{
    /// <summary>
    /// Every button, the "Open" label and the search hint.
    /// </summary>
    Full,

    /// <summary>
    /// Theme and About hidden (reachable from Settings, Appendix B item 4); Pin stays (owner at G4 review).
    /// </summary>
    Compact,

    /// <summary>
    /// As Compact, and the "Open" label and the search hint hidden too.
    /// </summary>
    IconOnly,
}


/// <summary>
/// The window-width bands of DS 2.1 (plan P4), each with a hysteresis so a live resize at a threshold doesn't flip back and forth.
/// </summary>
public sealed class ShellLayout
{
    #region Fields and constants

    /// <summary>
    /// A panel docks while the table keeps this much beside it (owner at the P6 review; DS 2: the table is fully usable at 420).
    /// </summary>
    public const double MIN_TABLE_WIDTH = 420;

    // the layout grid (DS 2): content padding 8 each side, the rail's and inspector's columns with their 8 gap
    public const double CONTENT_PADDING = 16;
    public const double PANEL_GAP = 8;

    // a drag in the gap sizes a panel within these; the rail starts at its narrowest (owner at G7)
    public const double RAIL_MIN_WIDTH = 200;
    public const double RAIL_MAX_WIDTH = 360;
    public const double DEFAULT_RAIL_WIDTH = RAIL_MIN_WIDTH;
    public const double INSPECTOR_MIN_WIDTH = 280;
    public const double INSPECTOR_MAX_WIDTH = 480;
    public const double DEFAULT_INSPECTOR_WIDTH = 304;

    public const double TOOLBAR_FULL_WIDTH = 900;
    public const double TOOLBAR_LABEL_WIDTH = 620;

    /// <summary>
    /// A band that was reached stays until the width falls this far below its threshold.
    /// </summary>
    public const double HYSTERESIS = 24;

    private bool _measured;

    #endregion // Fields and constants


    #region Properties

    public PanelMode Rail { get; private set; } = PanelMode.Docked;

    /// <summary>
    /// Gets or sets the rail's column, its width and gap (208 by default).
    /// </summary>
    public double RailColumn { get; set; } = DEFAULT_RAIL_WIDTH + PANEL_GAP;

    public double InspectorColumn { get; set; } = DEFAULT_INSPECTOR_WIDTH + PANEL_GAP;

    public PanelMode Inspector { get; private set; } = PanelMode.Docked;

    public ToolbarMode Toolbar { get; private set; } = ToolbarMode.Full;

    #endregion // Properties


    #region Public methods

    /// <summary>
    /// Places the panels and the toolbar for a window width; the rail docks first, and the inspector counts the rail's column only while the rail is open. The first call, or a fresh one, has no history, so it uses the thresholds alone. Returns whether anything changed.
    /// </summary>
    public bool Update(double windowWidth, bool railOpen = true, bool fresh = false)
    {
        if (fresh) _measured = false;

        var railRoom = windowWidth - CONTENT_PADDING - RailColumn;
        var rail = Reached(railRoom, Rail == PanelMode.Docked, MIN_TABLE_WIDTH) ? PanelMode.Docked : PanelMode.Push;
        var inspectorRoom = windowWidth - CONTENT_PADDING - InspectorColumn - (rail == PanelMode.Docked && railOpen ? RailColumn : 0);
        var inspector = Reached(inspectorRoom, Inspector == PanelMode.Docked, MIN_TABLE_WIDTH) ? PanelMode.Docked : PanelMode.Push;
        var toolbar = Reached(windowWidth, Toolbar == ToolbarMode.Full, TOOLBAR_FULL_WIDTH) ? ToolbarMode.Full
            : Reached(windowWidth, Toolbar != ToolbarMode.IconOnly, TOOLBAR_LABEL_WIDTH) ? ToolbarMode.Compact
            : ToolbarMode.IconOnly;

        _measured = true;
        if (rail == Rail && inspector == Inspector && toolbar == Toolbar) return false;

        Rail = rail;
        Inspector = inspector;
        Toolbar = toolbar;
        return true;
    }


    /// <summary>
    /// The rail's width for a drag: within its limits, and never so wide that the table beside the docked panels falls under MIN_TABLE_WIDTH.
    /// </summary>
    public double FitRail(double width, double windowWidth, bool inspectorDocked) =>
        Fit(width, RAIL_MIN_WIDTH, RAIL_MAX_WIDTH, windowWidth - CONTENT_PADDING - MIN_TABLE_WIDTH - PANEL_GAP - (inspectorDocked ? InspectorColumn : 0));


    public double FitInspector(double width, double windowWidth, bool railDocked) =>
        Fit(width, INSPECTOR_MIN_WIDTH, INSPECTOR_MAX_WIDTH, windowWidth - CONTENT_PADDING - MIN_TABLE_WIDTH - PANEL_GAP - (railDocked ? RailColumn : 0));

    #endregion // Public methods


    #region Private methods

    private bool Reached(double width, bool wasReached, double threshold) =>
        width >= threshold || (_measured && wasReached && width >= threshold - HYSTERESIS);


    // a saved width out of range (an edited settings file) comes back within the limits; the room only caps growth
    private static double Fit(double width, double min, double max, double room) =>
        Math.Clamp(double.IsFinite(width) ? width : min, min, Math.Max(min, Math.Min(max, room)));

    #endregion // Private methods
}


/// <summary>
/// The table panel's width rules (DS 2.1, plan P4): labels, the auto-hidden columns, the narrow inset, the group count.
/// </summary>
public sealed class TableLayout
{
    #region Fields and constants

    public const double LABELS_WIDTH = 660;
    public const double ID_COLUMNS_WIDTH = 560;
    public const double GROUP_COUNT_WIDTH = 520;
    public const double NARROW_WIDTH = 460;
    public const double HYSTERESIS = 16;

    private bool _measured;

    #endregion // Fields and constants


    #region Properties

    /// <summary>
    /// Gets whether the table is wide enough for toolbar labels; P5 also hides them while any would clip or rows are selected.
    /// </summary>
    public bool LabelsFit { get; private set; } = true;

    /// <summary>
    /// Gets whether the # and Tag ID columns hide for width ("Hidden at this width").
    /// </summary>
    public bool IdColumnsHidden { get; private set; }

    /// <summary>
    /// Gets whether "in 8 groups" fits beside the tag count.
    /// </summary>
    public bool GroupCountFits { get; private set; } = true;

    /// <summary>
    /// Gets whether the table is narrow: first-column inset 28 (+16), Tag name 44 %, group descriptions hidden.
    /// </summary>
    public bool IsNarrow { get; private set; }

    #endregion // Properties


    #region Public methods

    /// <summary>
    /// Applies the table panel's width; returns whether any rule changed.
    /// </summary>
    public bool Update(double tableWidth)
    {
        var labels = Reached(tableWidth, LabelsFit, LABELS_WIDTH);
        var idHidden = !Reached(tableWidth, !IdColumnsHidden, ID_COLUMNS_WIDTH);
        var groupCount = Reached(tableWidth, GroupCountFits, GROUP_COUNT_WIDTH);
        var narrow = !Reached(tableWidth, !IsNarrow, NARROW_WIDTH);

        _measured = true;
        if (labels == LabelsFit && idHidden == IdColumnsHidden && groupCount == GroupCountFits && narrow == IsNarrow) return false;

        LabelsFit = labels;
        IdColumnsHidden = idHidden;
        GroupCountFits = groupCount;
        IsNarrow = narrow;
        return true;
    }

    #endregion // Public methods


    #region Private methods

    private bool Reached(double width, bool wasReached, double threshold) =>
        width >= threshold || (_measured && wasReached && width >= threshold - HYSTERESIS);

    #endregion // Private methods
}
