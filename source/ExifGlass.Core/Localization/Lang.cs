/*
ExifGlass - AI & EXIF metadata viewer
Copyright (C) 2023 - 2027 DUONG DIEU PHAP
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
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace ExifGlass.Core.Localization;

/// <summary>
/// What installing language packs did: the files copied, and the names of those made for another version.
/// </summary>
public sealed record LangInstallResult(IReadOnlyList<string> Installed, IReadOnlyList<string> Incompatible);


/// <summary>
/// A language pack (<c>*.eglang.json</c>, as ImageGlass's <c>*.iglang.json</c>): <c>_Metadata</c> and the <c>Items</c> it translates; a key it lacks shows the built-in English.
/// </summary>
public sealed partial class Lang
{
    #region Fields and constants

    public const string FILE_EXTENSION = ".eglang.json";
    public const string FOLDER = "_lang";

    /// <summary>
    /// The Language setting of the built-in English, which has no file.
    /// </summary>
    public const string BUILT_IN = "English";

    /// <summary>
    /// A pack must declare at least this <c>MinVersion</c> to be listed or loaded.
    /// </summary>
    public const float SPEC_VERSION = 3;

    private const string METADATA = "_Metadata";
    private const string ITEMS = "Items";

    private static readonly JsonDocumentOptions READ_OPTIONS = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    private static Lang _current = new();

    #endregion // Fields and constants


    #region Constructors

    static Lang()
    {
        DefaultLangMap = new Dictionary<LangId, string>(_defaultLangList).ToFrozenDictionary();
    }


    /// <summary>
    /// The built-in English.
    /// </summary>
    public Lang() { }


    private Lang(string filePath, LangMetadata metadata, FrozenDictionary<LangId, string> items)
    {
        FilePath = filePath;
        Metadata = metadata;
        Items = items;
    }

    #endregion // Constructors


    #region Properties

    /// <summary>
    /// The language every window shows. Set it on the UI thread: <see cref="CurrentChanged"/> updates the controls.
    /// </summary>
    public static Lang Current
    {
        get => _current;
        set
        {
            if (ReferenceEquals(value, _current)) return;

            _current = value;
            CurrentChanged?.Invoke(null, EventArgs.Empty);
        }
    }

    /// <summary>
    /// The built-in English of every key.
    /// </summary>
    public static FrozenDictionary<LangId, string> DefaultLangMap { get; }

    /// <summary>
    /// Built-in packs ship in the app's own folder.
    /// </summary>
    public static string BuiltInDirectory => Path.Combine(AppContext.BaseDirectory, FOLDER);

    /// <summary>
    /// Installed packs live next to the settings; null until the app knows where they are.
    /// </summary>
    public static string? UserDirectory { get; set; }

    public LangMetadata Metadata { get; } = new();

    public FrozenDictionary<LangId, string> Items { get; } = FrozenDictionary<LangId, string>.Empty;

    /// <summary>
    /// The pack's file; empty for the built-in English.
    /// </summary>
    public string FilePath { get; } = string.Empty;

    public string FileName => Path.GetFileName(FilePath);

    public bool IsBuiltIn => FilePath.Length == 0;

    /// <summary>
    /// What the Language setting stores for this pack: its file name, or <see cref="BUILT_IN"/>.
    /// </summary>
    public string SettingValue => IsBuiltIn ? BUILT_IN : FileName;

    public string this[LangId key] => Get(key);

    public string this[LangId key, params object?[] args] => Get(key, args);

    #endregion // Properties


    #region Events

    /// <summary>
    /// Occurs on the UI thread when <see cref="Current"/> changes.
    /// </summary>
    public static event EventHandler? CurrentChanged;

    #endregion // Events


    #region Public methods

    /// <summary>
    /// The pack's text for the key, else the built-in English.
    /// </summary>
    public string Get(LangId key) => Items.TryGetValue(key, out var value) ? value : DefaultLangMap.GetValueOrDefault(key, string.Empty);


    /// <summary>
    /// The text with its arguments filled in; counts and sizes format the same in every language (the app runs with invariant globalization).
    /// </summary>
    public string Get(LangId key, params object?[] args)
    {
        var value = Get(key);
        if (args.Length == 0) return value;

        try
        {
            return string.Format(CultureInfo.InvariantCulture, value, args);
        }
        catch (FormatException)
        {
            // a translation with a broken placeholder must not take a window down
            return string.Format(CultureInfo.InvariantCulture, DefaultLangMap.GetValueOrDefault(key, string.Empty), args);
        }
    }


    /// <summary>
    /// Reads a pack file; null when it can't be read or was made for an older version.
    /// </summary>
    public static Lang? Load(string filePath)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllBytes(filePath), READ_OPTIONS);
            if (doc.RootElement.ValueKind != JsonValueKind.Object || MinVersionOf(doc.RootElement) < SPEC_VERSION) return null;

            var metadata = ReadMetadata(doc.RootElement);
            var items = new Dictionary<LangId, string>();
            if (doc.RootElement.TryGetProperty(ITEMS, out var list) && list.ValueKind == JsonValueKind.Object)
            {
                // keys from another version are skipped, not mapped onto a wrong string
                foreach (var item in list.EnumerateObject())
                {
                    if (item.Value.ValueKind == JsonValueKind.String && Enum.TryParse<LangId>(item.Name, ignoreCase: true, out var key) && Enum.IsDefined(key))
                    {
                        items[key] = item.Value.GetString()!;
                    }
                }
            }

            return new Lang(Path.GetFullPath(filePath), metadata, items.ToFrozenDictionary());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }


    /// <summary>
    /// The pack a Language setting names: a file in the user folder first, then the app's; the built-in English when it's missing or too old.
    /// </summary>
    public static Lang FromSetting(string? setting)
    {
        if (string.IsNullOrWhiteSpace(setting) || setting.Equals(BUILT_IN, StringComparison.OrdinalIgnoreCase)) return new Lang();

        return ResolveFilePath(setting) is { } path && Load(path) is { } lang ? lang : new Lang();
    }


    /// <summary>
    /// The built-in English, then every compatible pack by local name; a user pack hides an app pack of the same file name.
    /// </summary>
    public static IReadOnlyList<Lang> LoadAll()
    {
        var found = new Dictionary<string, Lang>(StringComparer.OrdinalIgnoreCase);
        foreach (var dir in new[] { BuiltInDirectory, UserDirectory })
        {
            if (dir is null || !Directory.Exists(dir)) continue;

            foreach (var file in EnumeratePacks(dir))
            {
                if (Load(file) is { } lang) found[lang.FileName] = lang;
            }
        }

        return [new Lang(), .. found.Values.OrderBy(l => l.Metadata.LocalName, StringComparer.OrdinalIgnoreCase)];
    }


    /// <summary>
    /// Copies the packs made for this version into the user folder.
    /// </summary>
    public static LangInstallResult Install(IEnumerable<string> filePaths)
    {
        if (UserDirectory is not { } target) return new LangInstallResult([], []);

        Directory.CreateDirectory(target);
        var installed = new List<string>();
        var incompatible = new List<string>();
        foreach (var file in filePaths)
        {
            if (Load(file) is null)
            {
                incompatible.Add(PackName(file));
                continue;
            }

            try
            {
                File.Copy(file, Path.Combine(target, Path.GetFileName(file)), overwrite: true);
                installed.Add(Path.GetFileName(file));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                incompatible.Add(PackName(file));
            }
        }

        return new LangInstallResult(installed, incompatible);
    }


    /// <summary>
    /// Writes the pack with every key in order, English where it has no text; the built-in English becomes a template to translate.
    /// </summary>
    public void Export(Stream stream)
    {
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        writer.WriteStartObject();
        writer.WriteStartObject(METADATA);
        writer.WriteString(nameof(LangMetadata.Code), IsBuiltIn ? "<language-code>" : Metadata.Code);
        writer.WriteString(nameof(LangMetadata.EnglishName), IsBuiltIn ? "<Your language name in English>" : Metadata.EnglishName);
        writer.WriteString(nameof(LangMetadata.LocalName), IsBuiltIn ? "<The language name in your language>" : Metadata.LocalName);
        writer.WriteStartArray(nameof(LangMetadata.Contributors));
        IReadOnlyList<string> contributors = IsBuiltIn ? ["<Your name>"] : Metadata.Contributors;
        foreach (var name in contributors) writer.WriteStringValue(name);
        writer.WriteEndArray();
        writer.WriteNumber(nameof(LangMetadata.MinVersion), SPEC_VERSION);
        writer.WriteEndObject();

        writer.WriteStartObject(ITEMS);
        foreach (var key in Enum.GetValues<LangId>()) writer.WriteString(key.ToString(), Get(key));
        writer.WriteEndObject();
        writer.WriteEndObject();
    }


    /// <summary>
    /// The file a Language setting names, or null when neither folder has it.
    /// </summary>
    public static string? ResolveFilePath(string setting)
    {
        if (Path.IsPathRooted(setting)) return File.Exists(setting) ? setting : null;

        foreach (var dir in new[] { UserDirectory, BuiltInDirectory })
        {
            if (dir is not null && Path.Combine(dir, setting) is var path && File.Exists(path)) return path;
        }

        return null;
    }

    #endregion // Public methods


    #region Private methods

    private static IEnumerable<string> EnumeratePacks(string dir)
    {
        try
        {
            return Directory.GetFiles(dir, "*" + FILE_EXTENSION);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }


    // the raw value, so a pack without _Metadata can't pass as current through the class default
    private static float MinVersionOf(JsonElement root)
    {
        if (!root.TryGetProperty(METADATA, out var meta) || meta.ValueKind != JsonValueKind.Object
            || !meta.TryGetProperty(nameof(LangMetadata.MinVersion), out var version)) return 0;

        if (version.ValueKind == JsonValueKind.Number && version.TryGetSingle(out var number)) return number;
        if (version.ValueKind == JsonValueKind.String && float.TryParse(version.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var text)) return text;

        return 0;
    }


    private static LangMetadata ReadMetadata(JsonElement root)
    {
        var meta = root.GetProperty(METADATA);
        var result = new LangMetadata { MinVersion = MinVersionOf(root), Contributors = [] };
        if (Text(meta, nameof(LangMetadata.Code)) is { } code) result.Code = code;
        if (Text(meta, nameof(LangMetadata.EnglishName)) is { } english) result.EnglishName = english;
        result.LocalName = Text(meta, nameof(LangMetadata.LocalName)) ?? result.EnglishName;

        if (meta.TryGetProperty(nameof(LangMetadata.Contributors), out var people))
        {
            // a single name is read as a list of one
            if (people.ValueKind == JsonValueKind.String && people.GetString() is { Length: > 0 } one) result.Contributors.Add(one.Trim());
            else if (people.ValueKind == JsonValueKind.Array)
            {
                foreach (var person in people.EnumerateArray())
                {
                    if (person.ValueKind == JsonValueKind.String && person.GetString()?.Trim() is { Length: > 0 } name) result.Contributors.Add(name);
                }
            }
        }

        return result;
    }


    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString()?.Trim() is { Length: > 0 } text ? text : null;


    private static string PackName(string filePath)
    {
        var name = Path.GetFileName(filePath);
        return name.EndsWith(FILE_EXTENSION, StringComparison.OrdinalIgnoreCase) ? name[..^FILE_EXTENSION.Length] : name;
    }

    #endregion // Private methods
}
