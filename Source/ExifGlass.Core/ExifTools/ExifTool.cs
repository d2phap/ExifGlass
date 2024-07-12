/*
ExifGlass - Standalone Exif tool for ImageGlass
Copyright (C) 2023-2024 DUONG DIEU PHAP
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
using Avalonia.Platform.Storage;
using CliWrap;
using CliWrap.Buffered;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ExifGlass.ExifTools;

/// <summary>
/// Initialize new instance of <see cref="ExifTool"/>.
/// </summary>
public class ExifTool(string toolPath = "") : List<ExifTagItem>
{
    /// <summary>
    /// Gets, sets the path of Exiftool executable file.
    /// If empty, it will use the default "exiftool.exe" file
    /// </summary>
    public string ExifToolPath { get; set; } = toolPath;


    /// <summary>
    /// Gets the current path of ExifTool.
    /// </summary>
    public string CurrentExifToolPath => string.IsNullOrWhiteSpace(ExifToolPath) ? DefaultExifToolPath : ExifToolPath;

    /// <summary>
    /// Gets default commands to pass to Exiftool.
    /// </summary>
    public static string DefaultCommands => "-fast -G -t -m -q -H";

    /// <summary>
    /// Gets the default exiftool executable path.
    /// </summary>
    public static string DefaultExifToolPath => Path.Combine(AppContext.BaseDirectory, "exiftool.exe");

    /// <summary>
    /// Gets the original file path.
    /// </summary>
    public string OriginalFilePath { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the actual file path where EXIF metadata fetched from.
    /// </summary>
    public string CleanedFilePath { get; private set; } = string.Empty;

    /// <summary>
    /// Check if file path contains unsupported characters.
    /// </summary>
    public bool IsFilePathDirty { get; private set; } = false;


    // Public methods
    #region Public methods

    /// <summary>
    /// Reads file's metadata.
    /// </summary>
    /// <param name="filePath">Path of file to read.</param>
    /// <param name="exifToolCmd">Additional commands for Exiftool.</param>
    public async Task ReadAsync(
        string filePath,
        CancellationToken cancelToken = default,
        params string[] exifToolCmd)
    {
        DeleteTempFiles();

        IsFilePathDirty = CheckAndPurifyUnicodePath(filePath, out var cleanPath);
        OriginalFilePath = filePath;
        CleanedFilePath = cleanPath;


        var cmd = Cli.Wrap(CurrentExifToolPath);
        var cmdResult = await cmd
            .WithArguments($"{DefaultCommands} {string.Join(" ", exifToolCmd)} \"{cleanPath}\"")
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync(Encoding.UTF8, cancelToken);

        var cmdOutput = cmdResult.StandardOutput;

        if (!string.IsNullOrEmpty(cmdResult.StandardError)
            && !cmdResult.StandardError.StartsWith("-- press ENTER --\r\n"))
        {
            throw new Exception(cmdResult.StandardError);
        }

        ParseExifTags(cmdOutput);
    }


    /// <summary>
    /// Deletes the <see cref="CleanedFilePath"/>.
    /// </summary>
    public void DeleteTempFiles()
    {
        if (IsFilePathDirty && OriginalFilePath != CleanedFilePath)
        {
            // delete temporary file
            try
            {
                File.Delete(CleanedFilePath);
            }
            catch { }
        }
    }


    /// <summary>
    /// Exports Exif metadata to file.
    /// </summary>
    public async Task ExportAs(ExportFileType fileType, IStorageFile? destFile)
    {
        if (destFile == null) return;

        var fileContent = string.Empty;
        if (fileType == ExportFileType.Text)
        {
            fileContent = ToText();
        }
        else if (fileType == ExportFileType.CSV)
        {
            fileContent = ToCsv();
        }
        else if (fileType == ExportFileType.JSON)
        {
            fileContent = ToJson();
        }

        await WriteTextFileAsync(fileContent, destFile);
    }


    /// <summary>
    /// Exports as text content.
    /// </summary>
    public string ToText()
    {
        var contentBuilder = new StringBuilder();

        // find the longest Tag Name in the list
        var propMaxLength = this.Max(item => item.TagName.Length);
        var currentGroup = "";

        foreach (var item in this)
        {
            // append group heading
            if (item.TagGroup != currentGroup)
            {
                var groupLine = $"[{item.TagGroup}]";
                if (currentGroup.Length > 0)
                {
                    groupLine = "\n" + groupLine;
                }

                contentBuilder.AppendLine(groupLine);
                currentGroup = item.TagGroup;
            }

            // append exif item
            contentBuilder.AppendLine(
                item.TagId + "\t" +
                item.TagName + "\t" +
                item.TagValue);
        }

        return contentBuilder.ToString();
    }


    /// <summary>
    /// Exports as CSV content.
    /// </summary>
    public string ToCsv()
    {
        var csvHeader = $"\"{nameof(ExifTagItem.Index)}\"," +
            $"\"{nameof(ExifTagItem.TagGroup)}\"," +
            $"\"{nameof(ExifTagItem.TagId)}\"," +
            $"\"{nameof(ExifTagItem.TagName)}\"," +
            $"\"{nameof(ExifTagItem.TagValue)}\"\r\n";

        var csvRows = this
            .Select(i => $"\"{i.Index}\",\"{i.TagGroup}\",\"{i.TagId}\",\"{i.TagName}\",\"{i.TagValue}\"");
        var csvContent = string.Join("\r\n", csvRows);


        return $"{csvHeader}{csvContent}";
    }


    /// <summary>
    /// Exports as JSON content.
    /// </summary>
    public string ToJson()
    {
        return JsonEx.ToJson(this);
    }


    /// <summary>
    /// Extracts tag binary data to file.
    /// </summary>
    /// <exception cref="Exception"></exception>
    public async Task ExtractTagAsync(string tagName, string destFilePath)
    {
        if (string.IsNullOrWhiteSpace(tagName)) return;

        var tagNameNoSpace = tagName.Replace(" ", "");
        var extractedDir = Path.Combine(Config.ConfigDir, "ExtractOutput");

        var cmd = Cli.Wrap(CurrentExifToolPath);
        var cmdResult = await cmd
            .WithArguments($"-{tagNameNoSpace} -b -w! \"{extractedDir}\\%f_{tagNameNoSpace}\" \"{CleanedFilePath}\"")
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync();

        if (!string.IsNullOrEmpty(cmdResult.StandardError))
        {
            throw new Exception(cmdResult.StandardError);
        }

        var extractedFileName = $"{Path.GetFileNameWithoutExtension(CleanedFilePath)}_{tagNameNoSpace}";
        var extractedFilePath = Path.Combine(extractedDir, extractedFileName);

        File.Move(extractedFilePath, destFilePath);
    }


    #endregion // Public methods


    // Private methods
    #region Private methods

    /// <summary>
    /// Parses Exiftool's command-line output.
    /// </summary>
    private void ParseExifTags(string cmdOutput)
    {
        var hasError = false;
        var index = 0;
        var originalFileName = Path.GetFileName(OriginalFilePath);
        Clear();

        while (cmdOutput.Length > 0)
        {
            var epos = cmdOutput.IndexOf('\r');
            if (epos < 0) epos = cmdOutput.Length;

            var tmp = cmdOutput[..epos];
            var tpos1 = tmp.IndexOf('\t');
            var tpos2 = tmp.IndexOf('\t', tpos1 + 1);
            var tpos3 = tmp.IndexOf('\t', tpos2 + 1);

            if (tpos1 > 0 && tpos2 > 0 && tpos3 > 0)
            {
                var tagGroup = tmp[..tpos1];
                ++tpos1;

                var tagId = tmp[tpos1..tpos2];
                ++tpos2;

                var tagName = tmp[tpos2..tpos3];
                ++tpos3;

                var tagValue = tmp[tpos3..];

                // special processing for tags with binary data 
                tpos1 = tagValue.IndexOf(", use -b option to extract");
                if (tpos1 >= 0)
                    _ = tagValue.Remove(tpos1, 26);

                // 
                if (tagName.Equals("File Name")) tagValue = originalFileName;

                Add(new ExifTagItem()
                {
                    Index = index + 1,
                    TagId = tagId,
                    TagName = tagName,
                    TagValue = tagValue,
                    TagGroup = tagGroup,
                });

                index++;
            }
            else
            {
                hasError = true;
            }


            // is \r followed by \n ?
            if (epos < cmdOutput.Length)
                epos += (cmdOutput[epos + 1] == '\n') ? 2 : 1;

            cmdOutput = cmdOutput[epos..];
        }


        if (hasError && Count == 0)
        {
            throw new Exception("ExifGlass encountered an error while parsing the output of ExifTool. Please ensure that the command-line arguments for ExifTool are correct.");
        }
    }


    /// <summary>
    /// Writes the <paramref name="fileContent"/> to <paramref name="destFile"/>.
    /// </summary>
    private static async Task WriteTextFileAsync(string? fileContent, IStorageFile? destFile)
    {
        if (string.IsNullOrEmpty(fileContent) || destFile == null) return;

        using var writer = await destFile.OpenWriteAsync();
        var fileBuffer = Encoding.UTF8.GetBytes(fileContent);

        await writer.WriteAsync(new ReadOnlyMemory<byte>(fileBuffer));
        await writer.FlushAsync();
    }


    /// <summary>
    /// Purifies <paramref name="filePath"/> if it contains unicode character.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the <paramref name="filePath"/> contains unicode and is purified.
    /// </returns>
    private static bool CheckAndPurifyUnicodePath(string filePath, out string cleanPath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            cleanPath = string.Empty;
            return false;
        }

        const int MAX_ANSICODE = 255;


        // exiftool does not support unicode filename
        var dirPath = Path.GetDirectoryName(filePath) ?? "";
        var fileNameNoExt = Path.GetFileNameWithoutExtension(filePath);
        var ext = Path.GetExtension(filePath);


        // directory has unicode char
        if (filePath.Any(c => c > MAX_ANSICODE))
        {
            // copy and rename it
            try
            {
                cleanPath = Path.GetTempFileName() + ext;
                File.Copy(filePath, cleanPath, true);

                return true;
            }
            catch (Exception) { }
        }

        cleanPath = filePath;
        return false;
    }

    #endregion // Private methods

}
