using System;
using SupplyRiskScanner.Models;

namespace SupplyRiskScanner.Scoring
{
    public class Scorer
    {
        public dynamic Score(PackageInfo info)
        {
            int cveRisk = 0;
            int score = 0;
            var reasons = new System.Collections.Generic.List<string>();

            if (info.Cves != null && info.Cves.Any())
            {
                var maxCvss = info.Cves.Max(c => c.Cvss);
                if (maxCvss >= 9) cveRisk = 50;
                else if (maxCvss >= 7) cveRisk = 35;
                else if (maxCvss >= 4) cveRisk = 20;
                else cveRisk = 10;
            }

            if (info.LastRelease == null)
            {
                score += 3;
                reasons.Add("No release information available");
            }
            else
            {
                var monthsSinceLast = (DateTimeOffset.UtcNow - info.LastRelease.Value).TotalDays / 30.0;
                if (monthsSinceLast < 6)
                {
                    // fresh
                }
                else if (monthsSinceLast < 12)
                {
                    score += 1;
                    reasons.Add("Last release >6 months");
                }
                else if (monthsSinceLast < 24)
                {
                    score += 2;
                    reasons.Add("Last release >12 months");
                }
                else
                {
                    score += 4;
                    reasons.Add("Last release >24 months");
                }
            }

            if (info.NumVersions <= 1)
            {
                score += 3;
                reasons.Add("Only 1 or no released versions");
            }
            else if (info.NumVersions < 5)
            {
                score += 1;
                reasons.Add("Few versions (<5)");
            }

            if (string.IsNullOrWhiteSpace(info.RepoUrl))
            {
                score += 2;
                reasons.Add("No repository URL provided");
            }

            var norm = Math.Min(100, score * 10);
            string level = "Low";
            if (norm >= 70) level = "Critical";
            else if (norm >= 46) level = "High";
            else if (norm >= 21) level = "Medium";

            return new { total_score = norm, risk_level = level, reasons = reasons.ToArray() };
        }
    }
}
