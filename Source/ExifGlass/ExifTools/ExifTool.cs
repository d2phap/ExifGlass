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
using CliWrap;
using CliWrap.Buffered;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ExifGlass;

public class ExifTool
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
    public async Task<List<ExifTagItem>> ReadAsync(
        string filePath,
        CancellationToken cancelToken = default,
        params string[] exifToolCmd)
    {
        var cmd = Cli.Wrap(ExifToolPath);
        var cmdResult = await cmd
            .WithArguments($"{DefaultCommands} \"{filePath}\" {string.Join(" ", exifToolCmd)}")
            .ExecuteBufferedAsync(Encoding.UTF8, cancelToken);

        var cmdOutput = cmdResult.StandardOutput;
        
        return ParseExifTags(cmdOutput);
    }


    /// <summary>
    /// Parses Exiftool's command-line output.
    /// </summary>
    private static List<ExifTagItem> ParseExifTags(string cmdOutput)
    {
        var result = new List<ExifTagItem>();
        var index = 0;


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

                result.Add(new ExifTagItem()
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


        return result;
    }
}
