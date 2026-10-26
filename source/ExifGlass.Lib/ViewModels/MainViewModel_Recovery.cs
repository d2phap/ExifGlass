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
using ExifGlass.Core.Editing;
using ExifGlass.UI.Controls;

namespace ExifGlass.ViewModels;

public sealed partial class MainViewModel
{
    #region Fields and constants

    // a save running at SHUTDOWN gets this long; ExifTool's own stop then gives its write a little more (R21)
    private static readonly TimeSpan SAVE_WAIT = TimeSpan.FromSeconds(1);

    #endregion // Fields and constants


    #region Public methods

    /// <summary>
    /// D29 at ImageGlass's SHUTDOWN: no dialog, since ImageGlass kills the app 3 s later; what is still staged goes to the recovery file. True when it wrote one.
    /// </summary>
    public async Task<bool> KeepForRecoveryAsync()
    {
        if (_saving is { IsCompleted: false } saving) await Task.WhenAny(saving, Task.Delay(SAVE_WAIT));
        if (!HasPending || Document is not { } doc) return false;

        try
        {
            RecoveryFile.Save(_settings.DirectoryPath, RecoveryFile.Create(doc.SourcePath, doc.FileSize, doc.FileModified, _edits, DateTimeOffset.UtcNow));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }


    /// <summary>
    /// D29 at the next start: offers the edits a SHUTDOWN kept. Restore stages them again, Discard drops them, Not now asks at the next start. True when they are staged.
    /// </summary>
    public async Task<bool> OfferRecoveryAsync()
    {
        var directory = _settings.DirectoryPath;
        if (Shell is null || RecoveryFile.Load(directory) is not { } recovery) return false;

        if (Exists(recovery.SourcePath) is not { } file)
        {
            RecoveryFile.Delete(directory);
            Shell.ShowToast(AppText.TOAST_RECOVERY_GONE, AppText.RecoveryGone(recovery.FileName), ToastKind.Danger);
            return false;
        }

        var changed = recovery.FileChanged(file.Length, file.LastWriteTimeUtc);
        var answer = await Shell.AskAsync(new DialogRequest(AppText.RecoveryTitle(recovery.FileName), AppText.RecoveryBody(recovery.PendingCount, changed),
            AppText.RESTORE, AppText.DISCARD, AppText.NOT_NOW, Icon: IconId.History));

        if (answer == DialogAnswer.Secondary) RecoveryFile.Delete(directory);
        if (answer != DialogAnswer.Primary) return false;

        // a read that fails keeps the file for the next start
        if (!await RestoreAsync(recovery)) return false;

        RecoveryFile.Delete(directory);
        return true;
    }

    #endregion // Public methods


    #region Private methods

    private static FileInfo? Exists(string path)
    {
        try
        {
            var file = new FileInfo(path);
            return file.Exists ? file : null;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }


    private async Task<bool> RestoreAsync(RecoveryFile recovery)
    {
        await OpenFileAsync(recovery.SourcePath);
        if (Loader.Document is not { } doc || !string.Equals(doc.SourcePath, recovery.SourcePath, StringComparison.Ordinal)) return false;

        // a new read clears what is staged when its rows arrive, so the edits wait for them
        if (!doc.IsSameRead(Document)) await RunQueryAsync(doc);

        Stage(recovery.ToEditSet(key => doc.GetByKey(key) is not null, out var dropped));
        Shell?.ShowToast(AppText.Restored(PendingCount), dropped > 0 ? AppText.RestoreDropped(dropped) : AppText.RESTORED_SUB);
        return true;
    }

    #endregion // Private methods
}
