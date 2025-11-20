using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using SupplyRiskScanner.Models;
using SupplyRiskScanner.Utils;

namespace SupplyRiskScanner.Collectors
{
    public class PyPiCollector
    {
        private readonly HttpClient _http;
        private readonly FileCache _cache;
        public PyPiCollector(HttpClient http, FileCache cache)
        {
            _http = http;
            _cache = cache;
            _http.Timeout = TimeSpan.FromSeconds(10);
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("SupplyRiskScanner/0.2 (+https://github.com/your)");
        }

        public async Task<PackageInfo> GetPackageInfoAsync(string packageName)
        {
            var url = $"https://pypi.org/pypi/{packageName}/json";
            if (_cache.TryGet(url, out var cached))
            {
                return ParsePackageInfo(packageName, cached);
            }

            try
            {
                var resp = await _http.GetAsync(url);
                if (!resp.IsSuccessStatusCode)
                {
                    return new PackageInfo { Name = packageName, RawJson = "", Source = "pypi" };
                }
                var text = await resp.Content.ReadAsStringAsync();
                _cache.Set(url, text);
                return ParsePackageInfo(packageName, text);
            }
            catch
            {
                return new PackageInfo { Name = packageName, RawJson = "", Source = "pypi" };
            }
        }

        private PackageInfo ParsePackageInfo(string name, string json)
        {
            var info = new PackageInfo { Name = name, RawJson = json, Source = "pypi" };
            if (string.IsNullOrEmpty(json)) return info;

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("info", out var infoEl))
                {
                    if (infoEl.TryGetProperty("home_page", out var hp) && hp.ValueKind == JsonValueKind.String)
                        info.RepoUrl = hp.GetString() ?? "";
                    if (infoEl.TryGetProperty("version", out var ver) && ver.ValueKind == JsonValueKind.String)
                        info.Version = ver.GetString() ?? "";
                }
                if (root.TryGetProperty("releases", out var releases))
                {
                    info.NumVersions = releases.EnumerateObject().Count();
                    DateTimeOffset? latest = null;
                    foreach (var r in releases.EnumerateObject())
                    {
                        foreach (var item in r.Value.EnumerateArray())
                        {
                            if (item.TryGetProperty("upload_time_iso_8601", out var t) && t.ValueKind == JsonValueKind.String)
                            {
                                if (DateTimeOffset.TryParse(t.GetString(), out var dto))
                                {
                                    if (latest == null || dto > latest) latest = dto;
                                }
                            }
                        }
                    }
                    info.LastRelease = latest;
                }
            }
            catch
            {
            }
            return info;
        }
    }
}
