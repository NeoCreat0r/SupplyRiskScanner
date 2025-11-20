namespace SupplyRiskScanner.Models
{
    public class CveInfo
    {
        public string Id { get; set; } = "";
        public string Description { get; set; } = "";
        public double Cvss { get; set; }
        public string Url => $"https://nvd.nist.gov/vuln/detail/{Id}";
    }
}
