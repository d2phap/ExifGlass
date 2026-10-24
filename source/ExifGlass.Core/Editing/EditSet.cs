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
using ExifGlass.Core.Ai;
using System.Collections.Immutable;

namespace ExifGlass.Core.Editing;

/// <summary>
/// How a row looks with the staged edits: the Edited, Delete and New badges.
/// </summary>
public enum EditState
{
    None,
    Edited,
    Deleted,
    Added,

    /// <summary>
    /// Deleted by a bulk action (Remove GPS data, Remove AI generation data, Strip all): no Undo of its own.
    /// </summary>
    Removed,
}


/// <summary>
/// A staged change to an existing tag: a new value, or a delete when Value is null.
/// </summary>
public sealed record TagEdit(string Key, string WriteName, string? Value, string OriginalValue, bool IsRaw = false)
{
    #region Properties

    public bool IsDelete => Value is null;

    #endregion // Properties
}


/// <summary>
/// A tag the Add-tag card staged; Group is EXIF, XMP or IPTC.
/// </summary>
public sealed record AddedTag(string Group, string Name, string Value)
{
    #region Properties

    /// <summary>
    /// The row key of the new tag in the table.
    /// </summary>
    public string Key => $"{Group}:{Name}";

    #endregion // Properties
}


/// <summary>
/// Every staged change for one file; immutable, each change returns a new set.
/// </summary>
public sealed class EditSet
{
    #region Constructors

    private EditSet(ImmutableDictionary<string, TagEdit> edits, ImmutableList<AddedTag> added, bool removeGps, bool stripAll, AiRedactionPlan? aiRedaction)
    {
        Edits = edits;
        Added = added;
        RemoveGps = removeGps;
        StripAll = stripAll;
        AiRedaction = aiRedaction;
    }

    #endregion // Constructors


    #region Properties

    public static EditSet Empty { get; } = new(ImmutableDictionary<string, TagEdit>.Empty.WithComparers(StringComparer.Ordinal), [], false, false, null);

    public ImmutableDictionary<string, TagEdit> Edits { get; }

    public ImmutableList<AddedTag> Added { get; }

    /// <summary>
    /// Remove GPS data: -gps*=, every GPS tag in every writable group.
    /// </summary>
    public bool RemoveGps { get; }

    /// <summary>
    /// -all=
    /// </summary>
    public bool StripAll { get; }

    /// <summary>
    /// Remove AI generation data (D24).
    /// </summary>
    public AiRedactionPlan? AiRedaction { get; }

    public bool RemoveAi => AiRedaction is not null;

    /// <summary>
    /// The pending-bar count: each edit, each added tag and each bulk action.
    /// </summary>
    public int PendingCount => Edits.Count + Added.Count + (RemoveGps ? 1 : 0) + (StripAll ? 1 : 0) + (RemoveAi ? 1 : 0);

    public bool IsEmpty => PendingCount == 0;

    #endregion // Properties


    #region Public methods

    /// <summary>
    /// Stages a new value; staging the original value again removes the edit ("No change").
    /// </summary>
    public EditSet Set(string key, string writeName, string value, string originalValue, bool isRaw = false)
    {
        if (value == originalValue && !isRaw) return Revert(key);
        return With(edits: Edits.SetItem(key, new TagEdit(key, writeName, value, originalValue, isRaw)));
    }


    public EditSet Delete(string key, string writeName, string originalValue) =>
        With(edits: Edits.SetItem(key, new TagEdit(key, writeName, null, originalValue)));


    /// <summary>
    /// Drops the staged edit or delete of a tag: Undo for a delete, Revert for an edit.
    /// </summary>
    public EditSet Revert(string key) => Edits.ContainsKey(key) ? With(edits: Edits.Remove(key)) : this;


    public EditSet Add(AddedTag tag) => With(added: Added.RemoveAll(a => a.Key.Equals(tag.Key, StringComparison.OrdinalIgnoreCase)).Add(tag));


    public EditSet RemoveAdded(string key) => With(added: Added.RemoveAll(a => a.Key.Equals(key, StringComparison.OrdinalIgnoreCase)));


    public EditSet WithRemoveGps(bool on) => With(removeGps: on);


    public EditSet WithStripAll(bool on) => With(stripAll: on);


    /// <summary>
    /// Stages the AI redaction with its plan; null drops it.
    /// </summary>
    public EditSet WithRemoveAi(AiRedactionPlan? plan) => new(Edits, Added, RemoveGps, StripAll, plan);


    public EditState StateOf(string key) => Edits.TryGetValue(key, out var e) ? (e.IsDelete ? EditState.Deleted : EditState.Edited) : EditState.None;

    #endregion // Public methods


    #region Private methods

    private EditSet With(ImmutableDictionary<string, TagEdit>? edits = null, ImmutableList<AddedTag>? added = null, bool? removeGps = null, bool? stripAll = null) =>
        new(edits ?? Edits, added ?? Added, removeGps ?? RemoveGps, stripAll ?? StripAll, AiRedaction);

    #endregion // Private methods
}
