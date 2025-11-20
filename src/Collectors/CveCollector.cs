using System.Net.Http.Json;
using SupplyRiskScanner.Models;

namespace SupplyRiskScanner.Collectors
{
    public class CveCollector
    {
        private static readonly HttpClient _http = new HttpClient();

        private const string NvdApi =
            "https://services.nvd.nist.gov/rest/json/cves/2.0?keywordSearch={0}";

        public async Task<List<CveInfo>> GetCvesAsync(string packageName)
        {
            try
            {
                // В NVD много шумных данных → минимальная обработка
                var url = string.Format(NvdApi, Uri.EscapeDataString(packageName));

                var response = await _http.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return new();

                var json = await response.Content.ReadFromJsonAsync<NvdResponse>();
                if (json == null || json.Vulnerabilities == null)
                    return new();

                var result = new List<CveInfo>();

                foreach (var item in json.Vulnerabilities)
                {
                    var cve = item.Cve;
                    if (cve == null) continue;

                    double score = 0;

                    var metrics = cve.Metrics?.CvssMetricV31?.FirstOrDefault();
                    if (metrics != null)
                        score = metrics.CvssData.BaseScore;

                    result.Add(new CveInfo
                    {
                        Id = cve.Id,
                        Description = cve.Descriptions?.FirstOrDefault()?.Value ?? "",
                        Cvss = score
                    });
                }

                return result;
            }
            catch
            {
                return new();
            }
        }

        // === внутренние модели ===
        public class NvdResponse
        {
            public List<VulnWrapper>? Vulnerabilities { get; set; }
        }

        public class VulnWrapper
        {
            public CveRecord? Cve { get; set; }
        }

        public class CveRecord
        {
            public string Id { get; set; }
            public List<Desc>? Descriptions { get; set; }
            public Metrics? Metrics { get; set; }
        }

        public class Desc { public string Value { get; set; } }

        public class Metrics
        {
            public List<CvssMetric>? CvssMetricV31 { get; set; }
        }

        public class CvssMetric
        {
            public CvssData CvssData { get; set; }
        }

        public class CvssData
        {
            public double BaseScore { get; set; }
        }
    }
}
