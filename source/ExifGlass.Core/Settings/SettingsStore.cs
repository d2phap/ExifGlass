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
using System.Text.Json;

namespace ExifGlass.Core.Settings;

/// <summary>
/// Loads and saves settings: defaults, then the file (migrated from v2 when only that exists), then -p: overrides kept in memory.
/// </summary>
public sealed class SettingsStore : IAsyncDisposable
{
    #region Fields and constants

    public const string FILE_NAME = "exifglass.settings.json";

    /// <summary>
    /// v2's file in the same folder; read once to migrate.
    /// </summary>
    public const string V2_FILE_NAME = "exifglass.config.json";

    private static readonly TimeSpan DEBOUNCE = TimeSpan.FromMilliseconds(400);

    private readonly Lock _lock = new();
    private readonly string _directory;
    private CancellationTokenSource? _pendingSave;
    private Task _saveTask = Task.CompletedTask;
    private AppSettings _persisted = new();

    #endregion // Fields and constants


    #region Constructors

    public SettingsStore(string directory) => _directory = directory;

    #endregion // Constructors


    #region Properties

    public string DirectoryPath => _directory;

    public string FilePath => Path.Combine(_directory, FILE_NAME);

    /// <summary>
    /// The live settings: what the app reads, overrides included.
    /// </summary>
    public AppSettings Current { get; private set; } = new();

    /// <summary>
    /// The keys -p: overrode for this run; they are never written to the file.
    /// </summary>
    public IReadOnlySet<string> OverriddenKeys { get; private set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// True when this run migrated v2's settings.
    /// </summary>
    public bool Migrated { get; private set; }

    #endregion // Properties


    #region Public methods

    /// <summary>
    /// The per-user settings folder: %LOCALAPPDATA%\ExifGlass, ~/.config/ExifGlass, ~/Library/Application Support/ExifGlass.
    /// </summary>
    public static string DefaultDirectory()
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrEmpty(baseDir)) baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        return Path.Combine(baseDir, Common.AppInfo.PRODUCT_NAME);
    }


    /// <summary>
    /// Loads the file, or migrates v2's, or starts from defaults when neither reads; then applies the overrides.
    /// </summary>
    public AppSettings Load(IReadOnlyDictionary<string, string>? overrides = null)
    {
        _persisted = ReadFile() ?? Migrate() ?? new AppSettings();
        if (_persisted.Version < AppSettings.CURRENT_VERSION) _persisted.Version = AppSettings.CURRENT_VERSION;

        var live = _persisted.Clone();
        OverriddenKeys = overrides is null ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) : SettingsOverrides.Apply(live, overrides);
        Current = live;

        if (Migrated) ScheduleSave();
        return Current;
    }


    /// <summary>
    /// Changes settings and saves them a moment later; overridden keys keep their file value.
    /// </summary>
    public void Update(Action<AppSettings> change)
    {
        lock (_lock)
        {
            change(Current);
            var persisted = Current.Clone();
            SettingsOverrides.Restore(persisted, _persisted, OverriddenKeys);
            _persisted = persisted;
        }

        ScheduleSave();
    }


    /// <summary>
    /// Writes now: temp file then rename, so a crash never leaves half a file.
    /// </summary>
    public void SaveNow()
    {
        AppSettings snapshot;
        lock (_lock) snapshot = _persisted.Clone();

        Directory.CreateDirectory(_directory);
        var tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(snapshot, SettingsJsonContext.Default.AppSettings));
        File.Move(tmp, FilePath, overwrite: true);
    }


    /// <summary>
    /// Waits for a pending save, then saves anything left.
    /// </summary>
    public async Task FlushAsync()
    {
        CancellationTokenSource? pending;
        lock (_lock)
        {
            pending = _pendingSave;
            _pendingSave = null;
        }

        if (pending is not null)
        {
            await pending.CancelAsync().ConfigureAwait(false);
            pending.Dispose();
            await SafeSaveAsync().ConfigureAwait(false);
        }

        await _saveTask.ConfigureAwait(false);
    }


    public async ValueTask DisposeAsync() => await FlushAsync().ConfigureAwait(false);

    #endregion // Public methods


    #region Private methods

    private AppSettings? ReadFile()
    {
        try
        {
            if (!File.Exists(FilePath)) return null;
            return JsonSerializer.Deserialize(File.ReadAllText(FilePath), SettingsJsonContext.Default.AppSettings);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // a broken file starts from defaults rather than failing the launch; the next save replaces it
            return null;
        }
    }


    private AppSettings? Migrate()
    {
        var v2 = Path.Combine(_directory, V2_FILE_NAME);
        try
        {
            if (!File.Exists(v2)) return null;

            var old = JsonSerializer.Deserialize(File.ReadAllText(v2), SettingsJsonContext.Default.V2Settings);
            if (old is null) return null;

            Migrated = true;
            return V2Migration.ToV3(old);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return null;
        }
    }


    private void ScheduleSave()
    {
        CancellationTokenSource cts;
        lock (_lock)
        {
            _pendingSave?.Cancel();
            _pendingSave?.Dispose();
            _pendingSave = cts = new CancellationTokenSource();
        }

        _saveTask = DebouncedSaveAsync(cts.Token);
    }


    private async Task DebouncedSaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(DEBOUNCE, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        lock (_lock)
        {
            if (_pendingSave?.Token == cancellationToken)
            {
                _pendingSave.Dispose();
                _pendingSave = null;
            }
        }

        await SafeSaveAsync().ConfigureAwait(false);
    }


    private Task SafeSaveAsync() => Task.Run(() =>
    {
        try
        {
            SaveNow();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // a read-only profile keeps the settings for this run only
        }
    });

    #endregion // Private methods
}
