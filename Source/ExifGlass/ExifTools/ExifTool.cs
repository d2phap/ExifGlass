/*
ExifGlass - Standalone Exif tool for ImageGlass
Copyright (C) 2023 DUONG DIEU PHAP
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

public class ExifTool : List<ExifTagItem>
{
    /// <summary>
    /// Gets, sets the path of Exiftool executable file.
    /// </summary>
    public string ExifToolPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets default commands to pass to Exiftool.
    /// </summary>
    public static string DefaultCommands => "-fast -G -t -m -q -H";


    /// <summary>
    /// Initialize new instance of <see cref="ExifTool"/>.
    /// </summary>
    public ExifTool(string toolPath = "")
    {
        ExifToolPath = toolPath;
    }


    // Public methods
    #region Public methods

    /// <summary>
    /// Tests if the <see cref="ExifToolPath"/> is valid.
    /// </summary>
    public async Task<bool> Test()
    {
        var cmd = Cli.Wrap(ExifToolPath);

        var cmdResult = await cmd
                .WithArguments("-ver")
                .ExecuteBufferedAsync(Encoding.UTF8);

        //var cmdOutput = cmdResult.StandardOutput;

        return true;
    }


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
        var cmdOutput = string.Empty;
        var pathContainsUnicode = CheckAndPurifyUnicodePath(filePath, out var cleanPath);

        try
        {
            var cmd = Cli.Wrap(ExifToolPath);
            var cmdResult = await cmd
                .WithArguments($"{DefaultCommands} \"{cleanPath}\" {string.Join(" ", exifToolCmd)}")
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync(Encoding.UTF8, cancelToken);

            cmdOutput = cmdResult.StandardOutput;
        }
        finally
        {
            // delete temporary file
            if (pathContainsUnicode)
            {
                try
                {
                    File.Delete(cleanPath);
                }
                catch { }
            }
        }

        ParseExifTags(cmdOutput, Path.GetFileName(filePath));
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
        var propMaxLength = this.Max(item => item.Name.Length);
        var currentGroup = "";

        foreach (var item in this)
        {
            // append group heading
            if (item.Group != currentGroup)
            {
                var groupLine = item.Group.PadRight(propMaxLength + 5, '-') + ":";
                if (currentGroup.Length > 0)
                {
                    groupLine = "\n" + groupLine;
                }

                contentBuilder.AppendLine(groupLine);

                currentGroup = item.Group;
            }

            // append exif item
            contentBuilder.AppendLine(item.Name.PadRight(propMaxLength + 5) + ":".PadRight(4) + item.Value);
        }

        return contentBuilder.ToString();
    }


    /// <summary>
    /// Exports as CSV content.
    /// </summary>
    public string ToCsv()
    {
        var csvHeader = $"\"{nameof(ExifTagItem.Index)}\"," +
            $"\"{nameof(ExifTagItem.TagId)}\"," +
            $"\"{nameof(ExifTagItem.Group)}\"," +
            $"\"{nameof(ExifTagItem.Name)}\"," +
            $"\"{nameof(ExifTagItem.Value)}\"\r\n";

        var csvRows = this
            .Select(i => $"\"{i.Index}\",\"{i.TagId}\",\"{i.Group}\",\"{i.Name}\",\"{i.Value}\"");
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


    #endregion // Public methods


    // Private methods
    #region Private methods

    /// <summary>
    /// Parses Exiftool's command-line output.
    /// </summary>
    private void ParseExifTags(string cmdOutput, string originalFileName)
    {
        var index = 0;
        Clear();

        while (cmdOutput.Length > 0)
        {
            var epos = cmdOutput.IndexOf('\r');
            if (epos < 0) epos = cmdOutput.Length;

            var tmp = cmdOutput[..epos];
            var tpos1 = tmp.IndexOf('\t');
            var tpos2 = tmp.IndexOf('\t', tpos1 + 1);
            var tpos3 = tmp.IndexOf('\t', tpos2 + 1);

            if (tpos1 > 0 && tpos2 > 0)
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
                    Name = tagName,
                    Value = tagValue,
                    Group = tagGroup,
                });

                index++;
            }

            // is \r followed by \n ?
            if (epos < cmdOutput.Length)
                epos += (cmdOutput[epos + 1] == '\n') ? 2 : 1;

            cmdOutput = cmdOutput[epos..];
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
