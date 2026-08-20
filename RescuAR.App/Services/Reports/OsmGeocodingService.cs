using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace RescuAR.App.Services.Reports
{
    public class OsmSearchResult
    {
        public string DisplayName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string Type { get; set; } = string.Empty;
    }

    public interface IOsmGeocodingService
    {
        Task<List<OsmSearchResult>> SearchLocationsAsync(string query);
    }

    public class OsmGeocodingService : IOsmGeocodingService
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        static OsmGeocodingService()
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "RescuAR.App/1.0 (Emergency Response MAUI App)");
            _httpClient.Timeout = TimeSpan.FromSeconds(6);
        }

        public async Task<List<OsmSearchResult>> SearchLocationsAsync(string query)
        {
            var results = new List<OsmSearchResult>();
            if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
            {
                return results;
            }

            try
            {
                // Prioritize Marikina / Philippines in OSM Nominatim query
                string searchUrl = $"https://nominatim.openstreetmap.org/search?format=json&q={Uri.EscapeDataString(query.Trim())}&countrycodes=ph&limit=10&addressdetails=1";

                var response = await _httpClient.GetAsync(searchUrl);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var array = doc.RootElement;

                    if (array.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in array.EnumerateArray())
                        {
                            string displayName = item.TryGetProperty("display_name", out var dn) ? dn.GetString() ?? "" : "";
                            string latStr = item.TryGetProperty("lat", out var lt) ? lt.GetString() ?? "0" : "0";
                            string lonStr = item.TryGetProperty("lon", out var ln) ? ln.GetString() ?? "0" : "0";
                            string typeStr = item.TryGetProperty("type", out var tp) ? tp.GetString() ?? "location" : "location";
                            string nameStr = item.TryGetProperty("name", out var nm) ? nm.GetString() ?? "" : "";

                            if (double.TryParse(latStr, out double lat) && double.TryParse(lonStr, out double lon))
                            {
                                results.Add(new OsmSearchResult
                                {
                                    DisplayName = displayName,
                                    Name = string.IsNullOrWhiteSpace(nameStr) ? displayName : nameStr,
                                    Latitude = lat,
                                    Longitude = lon,
                                    Type = typeStr
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OSM Search Error: {ex.Message}");
            }

            return results;
        }
    }
}
