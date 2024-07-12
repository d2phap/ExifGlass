/*
ExifGlass - EXIF metadata viewer
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

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace ExifGlass;

public class UpdateService
{
    /// <summary>
    /// Gets the update information
    /// </summary>
    public UpdateModel? CurrentReleaseInfo { get; private set; }


    /// <summary>
    /// Gets the value indicates that the current app has a new update
    /// </summary>
    public bool HasNewUpdate
    {
        get
        {
            if (CurrentReleaseInfo == null)
            {
                return false;
            }

            var newVersion = new Version(CurrentReleaseInfo.Version);
            var currentVersion = Config.AppVersion;

            return newVersion > currentVersion;
        }
    }


    /// <summary>
    /// Gets the latest updates
    /// </summary>
    public async Task GetUpdatesAsync()
    {
        var url = "https://raw.githubusercontent.com/d2phap/ExifGlass/main/update.json";


        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue
        {
            NoCache = true,
        };
        var response = await httpClient.GetAsync(url);

        if (!response.IsSuccessStatusCode)
        {
            return;
        }

        var json = await response.Content.ReadAsStringAsync();
        CurrentReleaseInfo = UpdateModel.Deserialize(json);
    }
}
