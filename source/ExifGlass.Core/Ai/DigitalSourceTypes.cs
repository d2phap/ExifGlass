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
using System.Collections.Frozen;

namespace ExifGlass.Core.Ai;

/// <summary>
/// The IPTC digital source type vocabulary (Appendix E.2): ExifTool prints the raw URI, so labels and kinds are ours.
/// </summary>
public static class DigitalSourceTypes
{
    #region Fields and constants

    private static readonly FrozenDictionary<string, (string Label, AiKind Kind)> TERMS = new Dictionary<string, (string, AiKind)>(StringComparer.OrdinalIgnoreCase)
    {
        ["trainedAlgorithmicMedia"] = ("Trained algorithmic media", AiKind.Generated),
        ["algorithmicMedia"] = ("Pure algorithmic media", AiKind.Generated),
        ["trainedAlgorithmicData"] = ("Trained algorithmic data", AiKind.Generated),
        ["compositeWithTrainedAlgorithmicMedia"] = ("Composite with trained algorithmic media", AiKind.Composite),
        ["compositeSynthetic"] = ("Composite of synthetic elements", AiKind.Composite),
        ["algorithmicallyEnhanced"] = ("Algorithmically enhanced", AiKind.EditedWithAi),
        ["humanEdits"] = ("Human edits", AiKind.Captured),
        ["digitalCapture"] = ("Digital capture", AiKind.Captured),
        ["computationalCapture"] = ("Computational capture", AiKind.Captured),
        ["negativeFilm"] = ("Digitised from a negative", AiKind.Captured),
        ["positiveFilm"] = ("Digitised from a transparency", AiKind.Captured),
        ["print"] = ("Digitised from a print", AiKind.Captured),
        ["screenCapture"] = ("Screen capture", AiKind.Captured),
        ["virtualRecording"] = ("Virtual recording", AiKind.Captured),
        ["composite"] = ("Composite", AiKind.Captured),
        ["compositeCapture"] = ("Composite of captured elements", AiKind.Captured),
        ["digitalCreation"] = ("Digital creation", AiKind.Captured),
        ["dataDrivenMedia"] = ("Data-driven media", AiKind.Captured),
        ["minorHumanEdits"] = ("Minor human edits", AiKind.Captured),
        ["softwareImage"] = ("Software image", AiKind.Captured),
        ["digitalArt"] = ("Digital art", AiKind.Captured),
        ["empty"] = ("Empty", AiKind.None),
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    #endregion // Fields and constants


    #region Public methods

    /// <summary>
    /// The term at the end of an http or https IPTC or C2PA URI: "trainedAlgorithmicMedia".
    /// </summary>
    public static string Term(string uri)
    {
        var u = uri.Trim().TrimEnd('/');
        var slash = u.LastIndexOf('/');
        return slash >= 0 ? u[(slash + 1)..] : u;
    }


    /// <summary>
    /// The IPTC label of a source type URI, or the term itself when it isn't in the vocabulary.
    /// </summary>
    public static string Label(string uri) => TERMS.TryGetValue(Term(uri), out var t) ? t.Label : Term(uri);


    /// <summary>
    /// The kind a source type stands for; None for an unknown term.
    /// </summary>
    public static AiKind Kind(string? uri) => uri is not null && TERMS.TryGetValue(Term(uri), out var t) ? t.Kind : AiKind.None;

    #endregion // Public methods
}
