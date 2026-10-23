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
using Avalonia.Controls;
using ExifGlass.Core.Export;
using ExifGlass.Core.Query;
using ProDataGrid.SourceGeneration;

namespace ExifGlass.UI.Controls;

/// <summary>
/// One item of the table's flat model (S2): a group header or a row, made per view and pointing at the table that shows it.
/// </summary>
[GenerateDataGridColumns(ProviderName = "TableItemSchema", Strict = true, Discovery = DataGridColumnDiscovery.AttributedOnly)]
public sealed class TableItem
{
    #region Fields and constants

    public const string COLUMN_INDEX = "index";
    public const string COLUMN_ID = "id";
    public const string COLUMN_NAME = "name";
    public const string COLUMN_VALUE = "value";
    public const string COLUMN_ACTIONS = "actions";

    // a tag key never starts with a control character
    private const string HEADER_PREFIX = "\u0001";

    #endregion // Fields and constants


    #region Constructors

    internal TableItem(MetadataTable table, ViewRow row, int dataIndex)
    {
        Table = table;
        Row = row;
        Key = row.Key;
        Order = dataIndex;
    }


    internal TableItem(MetadataTable table, ViewGroup group, int groupIndex)
    {
        Table = table;
        Group = group;
        Key = HeaderKey(group.Name);
        Order = groupIndex;
    }

    #endregion // Constructors


    #region Properties

    [DataGridKey]
    public string Key { get; }

    [DataGridColumn(DataGridColumnKind.Template, Header = UiText.COLUMN_INDEX, ColumnKey = COLUMN_INDEX, Order = 0, Width = "52",
        IsReadOnly = true, CanUserSort = true, TemplateFactoryMethod = nameof(CreateIndexCell), ReuseCellContent = true)]
    public int Index => Row?.Index ?? 0;

    [DataGridColumn(DataGridColumnKind.Template, Header = UiText.COLUMN_TAG_ID, ColumnKey = COLUMN_ID, Order = 1, Width = "88",
        IsReadOnly = true, CanUserSort = true, TemplateFactoryMethod = nameof(CreateIdCell), ReuseCellContent = true)]
    public string TagId => Row?.DisplayId ?? string.Empty;

    [DataGridColumn(DataGridColumnKind.Template, Header = UiText.COLUMN_TAG_NAME, ColumnKey = COLUMN_NAME, Order = 2, Width = "260",
        IsReadOnly = true, CanUserSort = true, TemplateFactoryMethod = nameof(CreateNameCell), ReuseCellContent = true)]
    public string TagName => Row?.Name ?? string.Empty;

    [DataGridColumn(DataGridColumnKind.Template, Header = UiText.COLUMN_VALUE, ColumnKey = COLUMN_VALUE, Order = 3, Width = "*",
        IsReadOnly = true, CanUserSort = true, TemplateFactoryMethod = nameof(CreateValueCell), ReuseCellContent = true)]
    public string Value => Row?.Value ?? string.Empty;

    [DataGridColumn(DataGridColumnKind.Template, Header = "", ColumnKey = COLUMN_ACTIONS, Order = 4, Width = "36",
        IsReadOnly = true, TemplateFactoryMethod = nameof(CreateActionsCell), ReuseCellContent = true)]
    public string Actions => string.Empty;

    public ViewRow? Row { get; }

    public ViewGroup? Group { get; }

    public bool IsHeader => Group is not null;

    /// <summary>
    /// The row's place among the view's rows, or the header's among its groups: the first-load stagger.
    /// </summary>
    public int Order { get; }

    internal MetadataTable Table { get; }

    #endregion // Properties


    #region Public methods

    public static string HeaderKey(string group) => HEADER_PREFIX + group;


    /// <summary>
    /// The text a cell copies (Ctrl+C on a cell).
    /// </summary>
    public string CellText(TableColumns column) => Row is { } row ? Exporter.Cell(row, column) : string.Empty;

    #endregion // Public methods


    #region Recycling cell factories

    // no XAML, no bindings: the grid hands back the control it made before, and after a Reset only the DataContext changes (S2)
    public static Control CreateIndexCell(TableItem item, Control? existing)
    {
        var cell = existing as IndexCell ?? new IndexCell();
        cell.Update(item);
        return cell;
    }


    public static Control CreateIdCell(TableItem item, Control? existing)
    {
        var cell = existing as IdCell ?? new IdCell();
        cell.Update(item);
        return cell;
    }


    public static Control CreateNameCell(TableItem item, Control? existing)
    {
        var cell = existing as NameCell ?? new NameCell();
        cell.Update(item);
        return cell;
    }


    public static Control CreateValueCell(TableItem item, Control? existing)
    {
        var cell = existing as ValueCell ?? new ValueCell();
        cell.Update(item);
        return cell;
    }


    public static Control CreateActionsCell(TableItem item, Control? existing)
    {
        var cell = existing as CopyCell ?? new CopyCell();
        cell.Update(item);
        return cell;
    }

    #endregion // Recycling cell factories
}
