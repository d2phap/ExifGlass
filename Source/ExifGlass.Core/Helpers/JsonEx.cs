﻿/*
ExifGlass - Standalone Exif tool for ImageGlass
Copyright (C) 2023-2025 DUONG DIEU PHAP
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
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace ExifGlass;

public partial class JsonEx
{
    private static JsonSerializerOptions JsonOptions => new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,

        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),

        Converters =
        {
            // Write enum value as string
            new JsonStringEnumConverter(),

            new CustomDateTimeConverter("yyyy/MM/dd HH:mm:ss"),
        },

        // ignoring policy
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault | JsonIgnoreCondition.WhenWritingNull,
        IgnoreReadOnlyProperties = true,
        IgnoreReadOnlyFields = true,
    };


    /// <summary>
    /// Parse object to JSON string
    /// </summary>
    public static string ToJson<T>(T obj)
    {
        return JsonSerializer.Serialize(obj, JsonOptions);
    }


    /// <summary>
    /// Parse JSON from a stream
    /// </summary>
    public static async Task<T?> ParseJson<T>(Stream stream)
    {
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions);
    }


    /// <summary>
    /// Writes an object value to JSON file
    /// </summary>
    public static async Task WriteJsonAsync(string jsonFilePath, object? value, CancellationToken token = default)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);

        await File.WriteAllTextAsync(jsonFilePath, json, Encoding.UTF8, token);
    }
}


public class CustomDateTimeConverter(string format) : JsonConverter<DateTime>
{
    private readonly string Format = format;


    public override void Write(Utf8JsonWriter writer, DateTime date, JsonSerializerOptions options)
    {
        writer.WriteStringValue(date.ToString(Format));
    }


    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return DateTime.ParseExact(reader.GetString() ?? "", Format, null);
    }
}