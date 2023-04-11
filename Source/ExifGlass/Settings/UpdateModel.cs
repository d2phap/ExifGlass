/*
ExifGlass - EXIF metadata viewer
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
#nullable disable
using System;
using System.ComponentModel.DataAnnotations;

namespace ExifGlass;

public class UpdateModel
{
    public float ApiVersion { get; set; } = 1;

    public string Version { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";

    [DataType(DataType.Url)]
    public Uri ChangelogUrl { get; set; }

    [DataType(DataType.DateTime)]
    public DateTime PublishedDate { get; set; }

    public Uri DownloadUrl { get; set; }
}
