using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using SupplyRiskScanner.Models;
using SupplyRiskScanner.Utils;

namespace SupplyRiskScanner.Collectors
{
    public class NuGetCollector
    {
        private readonly HttpClient _http;
        private readonly FileCache _cache;
        public NuGetCollector(HttpClient http, FileCache cache)
        {
            _http = http;
            _cache = cache;
            _http.Timeout = TimeSpan.FromSeconds(10);
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("SupplyRiskScanner/0.2 (+https://github.com/your)"); 
        }

        // Uses NuGet V3 registration endpoint
        public async Task<PackageInfo> GetPackageInfoAsync(string packageName)
        {
            var url = $"https://api.nuget.org/v3/registration5-gz-semver2/{packageName.ToLowerInvariant()}/index.json";
            if (_cache.TryGet(url, out var cached))
            {
                return ParsePackageInfo(packageName, cached);
            }
            try
            {
                var resp = await _http.GetAsync(url);
                if (!resp.IsSuccessStatusCode) return new PackageInfo { Name = packageName, RawJson = "", Source = "nuget" };
                var text = await resp.Content.ReadAsStringAsync();
                _cache.Set(url, text);
                return ParsePackageInfo(packageName, text);
            }
            catch
            {
                return new PackageInfo { Name = packageName, RawJson = "", Source = "nuget" };
            }
        }

        private PackageInfo ParsePackageInfo(string name, string json)
        {
            var info = new PackageInfo { Name = name, RawJson = json, Source = "nuget" };
            if (string.IsNullOrEmpty(json)) return info;
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                // registration pages -> items -> items -> catalogEntry
                if (root.TryGetProperty("items", out var pages))
                {
                    int versions = 0;
                    DateTimeOffset? latest = null;
                    foreach (var page in pages.EnumerateArray())
                    {
                        if (page.TryGetProperty("items", out var items))
                        {
                            foreach (var item in items.EnumerateArray())
                            {
                                if (item.TryGetProperty("catalogEntry", out var entry))
                                {
                                    versions++;
                                    if (entry.TryGetProperty("published", out var pub) && pub.ValueKind == JsonValueKind.String)
                                    {
                                        if (DateTimeOffset.TryParse(pub.GetString(), out var dto))
                                        {
                                            if (latest == null || dto > latest) latest = dto;
                                        }
                                    }
                                    if (string.IsNullOrEmpty(info.RepoUrl))
                                    {
                                        if (entry.TryGetProperty("projectUrl", out var purl) && purl.ValueKind == JsonValueKind.String)
                                            info.RepoUrl = purl.GetString() ?? "";
                                        if (entry.TryGetProperty("repository" , out var repo) && repo.ValueKind == JsonValueKind.Object)
                                        {
                                            if (repo.TryGetProperty("url", out var rurl) && rurl.ValueKind == JsonValueKind.String)
                                                info.RepoUrl = rurl.GetString() ?? info.RepoUrl;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    info.NumVersions = versions;
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
