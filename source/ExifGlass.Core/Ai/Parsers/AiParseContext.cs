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
namespace ExifGlass.Core.Ai.Parsers;

/// <summary>
/// A parser for one generator family or standard.
/// </summary>
public interface IAiParser
{
    #region Methods

    /// <summary>
    /// Reads the tags it knows, claims them, and adds zero or more sources to the context.
    /// </summary>
    void Parse(AiParseContext context);

    #endregion // Methods
}


/// <summary>
/// What the parsers share during one parse: the tags, which of them earlier parsers claimed, and the sources found.
/// </summary>
public sealed class AiParseContext
{
    #region Fields and constants

    private readonly HashSet<AiTag> _claimed = new(ReferenceEqualityComparer.Instance);

    #endregion // Fields and constants


    #region Constructors

    public AiParseContext(AiTagSet tags, string? sidecarText = null, CancellationToken cancellationToken = default)
    {
        Tags = tags;
        SidecarText = sidecarText;
        CancellationToken = cancellationToken;
    }

    #endregion // Constructors


    #region Properties

    public AiTagSet Tags { get; }

    /// <summary>
    /// The A1111 / SD.Next .txt sidecar next to the image, read by the app (Tier 2).
    /// </summary>
    public string? SidecarText { get; }

    public CancellationToken CancellationToken { get; }

    public List<AiSourceInfo> Sources { get; } = [];

    public List<string> Warnings { get; } = [];

    /// <summary>
    /// True once a parser found prompts, parameters or a workflow.
    /// </summary>
    public bool HasGeneration => Sources.Any(s => s.IsGeneration);

    #endregion // Properties


    #region Public methods

    public bool IsClaimed(AiTag tag) => _claimed.Contains(tag);


    public void Claim(AiTag tag) => _claimed.Add(tag);


    /// <summary>
    /// The first unclaimed tag with this name in one of the groups.
    /// </summary>
    public AiTag? Free(string name, params ReadOnlySpan<string> groups)
    {
        foreach (var g in groups)
        {
            foreach (var t in Tags.InGroup(g))
            {
                if (!IsClaimed(t) && t.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) return t;
            }
        }

        return null;
    }


    /// <summary>
    /// Every unclaimed tag with this name, in any group.
    /// </summary>
    public IEnumerable<AiTag> FreeWithName(string name) => Tags.WithName(name).Where(t => !IsClaimed(t));

    #endregion // Public methods
}
