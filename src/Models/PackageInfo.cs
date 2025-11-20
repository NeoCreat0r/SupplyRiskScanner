namespace SupplyRiskScanner.Models
{
    public class PackageInfo
    {
        public string Name { get; set; } = "";
        public string Version { get; set; } = "";
        public string RepoUrl { get; set; } = "";
        public int NumVersions { get; set; } = 0;
        public System.DateTimeOffset? Created { get; set; } = null;
        public System.DateTimeOffset? LastRelease { get; set; } = null;
        public string RawJson { get; set; } = "";
        // source: pypi or nuget indicator
        public string Source { get; set; } = "";
        public List<CveInfo> Cves { get; set; } = new();
    }
}
