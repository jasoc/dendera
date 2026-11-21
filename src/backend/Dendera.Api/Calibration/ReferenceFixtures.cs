using System.Globalization;
using System.IO.Compression;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using Dendera.Core.Calibration;
using Hour = Dendera.Sources.OpenMeteoWeatherProvider.Hour;

namespace Dendera.Api.Calibration;

/// <summary>
/// The external history the ritual reads, stored once in src/backend/calibration so the ritual runs offline:
/// Kp from GFZ Potsdam (CC BY 4.0) and hourly weather from the Open-Meteo archive (ERA5, CC BY 4.0) for every
/// reference climate zone, over the WMO climate normal 1991–2020. The files are gitignored and downloaded on demand
/// (`fetch-reference`); only their content hashes are committed (<see cref="FixtureManifest"/>).
/// </summary>
public static class ReferenceFixtures
{
    public const string KpFile = "kp.csv.gz";
    public static string WeatherFile(ClimateZone zone) => $"weather-{zone.Id}.csv.gz";

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static IReadOnlyList<string> Names => [KpFile, .. ClimateZones.All.Select(WeatherFile)];

    /// <summary>SHA-256 of a fixture's decompressed content.</summary>
    public static string ContentHash(string path)
    {
        using var gz = new GZipStream(File.OpenRead(path), CompressionMode.Decompress);
        using var buffer = new MemoryStream();
        gz.CopyTo(buffer);
        return CalibrationHash.Content(buffer.ToArray());
    }

    /// <summary>Hashes of the fixtures on disk (they must all be there).</summary>
    public static FixtureManifest Compute(string dir) => new()
    {
        Files = new SortedDictionary<string, string>(
            Names.ToDictionary(n => n, n => ContentHash(Path.Combine(dir, n))), StringComparer.Ordinal),
    };

    public static string ManifestPath(string dir) => Path.Combine(dir, FixtureManifest.FileName);

    /// <summary>
    /// The committed manifest, after checking that every fixture is present and matches it. Throws with what to do
    /// otherwise: download them, or accept new data from the sources (then the tables change too).
    /// </summary>
    public static FixtureManifest Verify(string dir)
    {
        var path = ManifestPath(dir);
        if (!File.Exists(path))
            throw new InvalidOperationException($"No {path}: run `dotnet run --project Dendera.Api -- fetch-reference --update-manifest`");
        var manifest = FixtureManifest.Parse(File.ReadAllText(path));
        var missing = Names.Where(n => !File.Exists(Path.Combine(dir, n))).ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException(
                $"Reference fixtures missing ({string.Join(", ", missing)}): run `dotnet run --project Dendera.Api -- fetch-reference`");
        var differ = Names.Where(n => manifest.Files.GetValueOrDefault(n) != ContentHash(Path.Combine(dir, n))).ToList();
        if (differ.Count > 0)
            throw new InvalidOperationException(
                $"Reference fixtures differ from {FixtureManifest.FileName} ({string.Join(", ", differ)}): the sources changed their data. " +
                "Accept it with `fetch-reference --update-manifest`, then run the ritual and commit manifest and tables together.");
        return manifest;
    }

    /// <summary>Kp per 3-hour slot start (UTC).</summary>
    public static IReadOnlyDictionary<DateTimeOffset, double> LoadKp(string dir, ReferenceSet set)
    {
        var start = new DateTimeOffset(set.DataFromYear, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var result = new Dictionary<DateTimeOffset, double>();
        var i = 0;
        foreach (var line in ReadLines(Path.Combine(dir, KpFile)))
        {
            if (line.Length > 0) result[start.AddHours(3 * i)] = double.Parse(line, Inv);
            i++;
        }
        return result;
    }

    /// <summary>Hourly weather of a zone (UTC), from the first day of the span.</summary>
    public static IReadOnlyDictionary<DateTimeOffset, Hour> LoadWeather(string dir, ClimateZone zone, ReferenceSet set)
    {
        var start = new DateTimeOffset(set.DataFromYear, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var result = new Dictionary<DateTimeOffset, Hour>();
        var i = 0;
        foreach (var line in ReadLines(Path.Combine(dir, WeatherFile(zone))))
        {
            var f = line.Split(',');
            double? V(int k) => k < f.Length && f[k].Length > 0 ? double.Parse(f[k], Inv) : null;
            result[start.AddHours(i++)] = new Hour(V(0), V(1), V(2), V(3), V(4));
        }
        return result;
    }

    private static IEnumerable<string> ReadLines(string path)
    {
        using var gz = new GZipStream(File.OpenRead(path), CompressionMode.Decompress);
        using var reader = new StreamReader(gz, Encoding.UTF8);
        while (reader.ReadLine() is { } line)
            if (!line.StartsWith('#')) yield return line;
    }

    // ---------- download (network; run by hand, results are committed) ----------

    /// <summary>
    /// Downloads the fixtures, then compares them with the committed manifest. With <paramref name="updateManifest"/>
    /// (or no manifest yet) the manifest is rewritten to the downloaded data; otherwise a difference is reported and
    /// the manifest is left alone. Returns false when the data differs and the manifest was not updated.
    /// </summary>
    public static async Task<bool> FetchAsync(string dir, ReferenceSet set, bool updateManifest, TextWriter log)
    {
        await DownloadAsync(dir, set, log);
        var downloaded = Compute(dir);
        var path = ManifestPath(dir);
        if (updateManifest || !File.Exists(path))
        {
            File.WriteAllText(path, downloaded.ToJson(), new UTF8Encoding(false));
            log.WriteLine($"Wrote {path}: run the ritual and commit it with the tables");
            return true;
        }
        var committed = FixtureManifest.Parse(File.ReadAllText(path));
        var differ = Names.Where(n => committed.Files.GetValueOrDefault(n) != downloaded.Files[n]).ToList();
        if (differ.Count == 0)
        {
            log.WriteLine("Fixtures match the manifest");
            return true;
        }
        log.WriteLine($"Downloaded data differs from the manifest: {string.Join(", ", differ)}. The sources changed their data; " +
                      "to accept it run `fetch-reference --update-manifest`, then the ritual.");
        return false;
    }

    private static async Task DownloadAsync(string dir, ReferenceSet set, TextWriter log)
    {
        Directory.CreateDirectory(dir);
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Dendera/0.1 (self-hosted)");

        log.WriteLine("Kp: GFZ Potsdam, Kp_ap_since_1932.txt");
        var text = await http.GetStringAsync("https://kp.gfz.de/app/files/Kp_ap_since_1932.txt");
        var kp = new SortedDictionary<DateTimeOffset, double>();
        foreach (var line in text.Split('\n'))
        {
            if (line.StartsWith('#') || line.Length < 50) continue;
            var c = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var at = new DateTimeOffset(int.Parse(c[0], Inv), int.Parse(c[1], Inv), int.Parse(c[2], Inv),
                (int)double.Parse(c[3], Inv), 0, 0, TimeSpan.Zero);
            if (at.Year < set.DataFromYear || at.Year > set.DataToYear) continue;
            var value = double.Parse(c[7], Inv);
            if (value >= 0) kp[at] = value;
        }
        var start = new DateTimeOffset(set.DataFromYear, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(set.DataToYear + 1, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var kpLines = new List<string>();
        for (var t = start; t < end; t = t.AddHours(3))
            kpLines.Add(kp.TryGetValue(t, out var v) ? v.ToString("0.000", Inv) : "");
        Write(Path.Combine(dir, KpFile),
            $"# Kp, GFZ Potsdam (https://kp.gfz.de, CC BY 4.0). One value per 3-hour slot from {start:yyyy-MM-ddTHH:mmZ}, empty = missing.",
            kpLines);

        foreach (var zone in ClimateZones.All)
        {
            if (File.Exists(Path.Combine(dir, WeatherFile(zone))))
            {
                log.WriteLine($"Weather: {zone.Name} already present (delete the file to download it again)");
                continue;
            }
            log.WriteLine($"Weather: {zone.Name}");
            var lines = new List<string>();
            for (var y = set.DataFromYear; y <= set.DataToYear; y += 5)
            {
                var to = Math.Min(set.DataToYear, y + 4);
                var url = "https://archive-api.open-meteo.com/v1/archive?" +
                          $"latitude={zone.Latitude.ToString(Inv)}&longitude={zone.Longitude.ToString(Inv)}" +
                          $"&start_date={y}-01-01&end_date={to}-12-31&timezone=UTC" +
                          "&hourly=pressure_msl,temperature_2m,relative_humidity_2m,wind_speed_10m,wind_gusts_10m";
                var data = await GetWithRetry(http, url, log);
                var h = data.Hourly ?? throw new InvalidDataException(url);
                for (var i = 0; i < h.Time!.Count; i++)
                    lines.Add(string.Join(",", F(h.Pressure, i), F(h.Temperature, i), F(h.Humidity, i), F(h.Wind, i), F(h.Gusts, i)));
                await Task.Delay(500);
            }
            Write(Path.Combine(dir, WeatherFile(zone)),
                $"# {zone.Name} ({zone.Koppen}), {zone.Latitude.ToString(Inv)},{zone.Longitude.ToString(Inv)}. Open-Meteo historical " +
                $"weather API (ERA5, CC BY 4.0). One line per hour from {start:yyyy-MM-ddTHH:mmZ}: pressure_msl hPa, " +
                "temperature_2m °C, relative_humidity_2m %, wind_speed_10m km/h, wind_gusts_10m km/h; empty = missing.",
                lines);
        }
    }

    /// <summary>Open-Meteo limits calls per minute (weighted by size): on 429 wait and retry.</summary>
    private static async Task<ArchiveResponse> GetWithRetry(HttpClient http, string url, TextWriter log)
    {
        for (var attempt = 1; ; attempt++)
        {
            using var response = await http.GetAsync(url);
            if ((int)response.StatusCode == 429 && attempt < 8)
            {
                log.WriteLine("  rate limited, waiting 70 s");
                await Task.Delay(TimeSpan.FromSeconds(70));
                continue;
            }
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ArchiveResponse>() ?? throw new InvalidDataException(url);
        }
    }

    private static string F(List<double?>? list, int i) =>
        list is not null && i < list.Count && list[i] is { } v ? Math.Round(v, 1).ToString("0.#", Inv) : "";

    private static void Write(string path, string header, IEnumerable<string> lines)
    {
        using var file = File.Create(path);
        using var gz = new GZipStream(file, CompressionLevel.SmallestSize);
        using var w = new StreamWriter(gz, new UTF8Encoding(false)) { NewLine = "\n" };
        w.WriteLine(header);
        foreach (var l in lines) w.WriteLine(l);
    }

    private sealed class ArchiveResponse
    {
        [JsonPropertyName("hourly")] public HourlyData? Hourly { get; set; }
    }

    private sealed class HourlyData
    {
        [JsonPropertyName("time")] public List<string>? Time { get; set; }
        [JsonPropertyName("pressure_msl")] public List<double?>? Pressure { get; set; }
        [JsonPropertyName("temperature_2m")] public List<double?>? Temperature { get; set; }
        [JsonPropertyName("relative_humidity_2m")] public List<double?>? Humidity { get; set; }
        [JsonPropertyName("wind_speed_10m")] public List<double?>? Wind { get; set; }
        [JsonPropertyName("wind_gusts_10m")] public List<double?>? Gusts { get; set; }
    }
}
