using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.IO;
using SupplyRiskScanner.Models;

namespace SupplyRiskScanner.Report
{
    public class PackageResult
    {
        public string Ecosystem { get; set; }
        public string Name { get; set; }
        public PackageInfo Info { get; set; }
        public dynamic Score { get; set; }
    }

    public class ReportGenerator
    {
        public void GenerateJsonReport(IEnumerable<PackageResult> results, string outPath)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var wrapper = new
            {
                generated_at = DateTimeOffset.UtcNow,
                packages = results
            };
            var json = JsonSerializer.Serialize(wrapper, options);
            File.WriteAllText(outPath, json);
        }

        public void GenerateHtmlReport(IEnumerable<PackageResult> results, string outPath)
        {
            var items = new List<PackageResult>(results);
            var html = new StringBuilder();

            // HEADER + CSS
            html.AppendLine(@"<!doctype html>
<html>
<head>
<meta charset='utf-8'>
<title>SupplyRiskScanner Report</title>
<style>

body {
    font-family: Arial, Helvetica, sans-serif;
    background: #f3f4f7;
    margin: 0;
    padding: 0;
    color: #333;
}

h1 {
    text-align: center;
    margin-top: 30px;
    font-size: 32px;
}

.report-meta {
    text-align: center;
    margin-bottom: 25px;
    color: #666;
}

table {
    width: 95%;
    margin: auto;
    border-collapse: collapse;
    margin-bottom: 40px;
    box-shadow: 0 3px 12px rgba(0,0,0,0.08);
    border-radius: 10px;
    overflow: hidden;
}

th {
    background: #222;
    color: #fff;
    text-align: left;
    padding: 10px 12px;
    font-size: 14px;
}

td {
    padding: 10px 12px;
    font-size: 14px;
    background: #fff;
    border-bottom: 1px solid #eee;
}

tr:nth-child(even) td {
    background: #f9fafc;
}

/* RISK COLORS */
.low { color: #1cc88a; font-weight: bold; }
.medium { color: #f6c23e; font-weight: bold; }
.high { color: #e74a3b; font-weight: bold; }
.critical { color: darkred; font-weight: bold; }

</style>
</head>
<body>

<h1>SupplyRiskScanner Report</h1>
<div class='report-meta'>Generated at " + DateTimeOffset.UtcNow.ToString("u") + @"</div>

<table>
<tr>
    <th>Ecosystem</th>
    <th>Package</th>
    <th>Version</th>
    <th>Last Release</th>
    <th>Versions</th>
    <th>Repo</th>
    <th>Score</th>
    <th>Level</th>
    <th>Reasons</th>
</tr>
");

            foreach (var p in items)
            {
                var info = p.Info ?? new PackageInfo();
                var score = p.Score ?? new { total_score = 0, risk_level = "Low", reasons = new string[0] };

                string lvl = (string)score.risk_level;
                string css = lvl.ToLowerInvariant();
                string last = info.LastRelease?.ToString("u") ?? "-";
                string repo = string.IsNullOrWhiteSpace(info.RepoUrl)
                    ? "-"
                    : $"<a href='{System.Web.HttpUtility.HtmlEncode(info.RepoUrl)}' target='_blank'>link</a>";
                string reasons = System.Web.HttpUtility.HtmlEncode(string.Join("; ", (string[])score.reasons));

                html.AppendLine($@"
<tr>
    <td>{System.Web.HttpUtility.HtmlEncode(p.Ecosystem)}</td>
    <td>{System.Web.HttpUtility.HtmlEncode(p.Name)}</td>
    <td>{System.Web.HttpUtility.HtmlEncode(info.Version)}</td>
    <td>{last}</td>
    <td>{info.NumVersions}</td>
    <td>{repo}</td>
    <td>{score.total_score}</td>
    <td class='{css}'>{lvl}</td>
    <td>{reasons}</td>
</tr>");
            }

            html.AppendLine("</table>");
            html.AppendLine("</body></html>");

            File.WriteAllText(outPath, html.ToString());
        }
    }
}
