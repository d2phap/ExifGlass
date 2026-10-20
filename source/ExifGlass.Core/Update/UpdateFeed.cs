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
using System.Net.Http.Headers;

namespace ExifGlass.Core.Update;

/// <summary>
/// Downloads the release feed: the one network call the app makes (BannedSymbols.txt).
/// </summary>
public static class UpdateFeed
{
    #region Fields and constants

    private static readonly TimeSpan TIMEOUT = TimeSpan.FromSeconds(15);

    #endregion // Fields and constants


    #region Public methods

    /// <summary>
    /// GETs the feed without the cache; sends only a User-Agent with the app version.
    /// </summary>
    public static async Task<string> FetchAsync(string appVersion, CancellationToken cancellationToken)
    {
#pragma warning disable RS0030 // the update check is the only network use, by design
        using var http = new HttpClient { Timeout = TIMEOUT };
        using var request = new HttpRequestMessage(HttpMethod.Get, UpdateChecker.FEED_URL);

        request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue(Common.AppInfo.PRODUCT_NAME, appVersion));

        using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#pragma warning restore RS0030
    }

    #endregion // Public methods
}
