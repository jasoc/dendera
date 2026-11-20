using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Dendera.Sources;

public sealed record Place(string Name, string? Region, string? Country, double Latitude, double Longitude, string? TimeZone);

/// <summary>Place search via Open-Meteo geocoding (free, no key). Returns IANA time zones too.</summary>
public sealed class OpenMeteoGeocoder(HttpClient http)
{
    public const string BaseUrl = "https://geocoding-api.open-meteo.com/";

    public async Task<IReadOnlyList<Place>> SearchAsync(string query, string language, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2) return [];
        var url = $"v1/search?name={Uri.EscapeDataString(query.Trim())}&count=8&language={Uri.EscapeDataString(language)}&format=json";
        var data = await http.GetFromJsonAsync<Response>(url, ct);
        return data?.Results?
            .Select(r => new Place(r.Name ?? "", r.Admin1, r.Country, r.Latitude, r.Longitude, r.Timezone))
            .ToList() ?? [];
    }

    private sealed class Response
    {
        [JsonPropertyName("results")] public List<Result>? Results { get; set; }
    }

    private sealed class Result
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("admin1")] public string? Admin1 { get; set; }
        [JsonPropertyName("country")] public string? Country { get; set; }
        [JsonPropertyName("latitude")] public double Latitude { get; set; }
        [JsonPropertyName("longitude")] public double Longitude { get; set; }
        [JsonPropertyName("timezone")] public string? Timezone { get; set; }
    }
}
