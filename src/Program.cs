using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SupplyRiskScanner.Parsers;
using SupplyRiskScanner.Collectors;
using SupplyRiskScanner.Scoring;
using SupplyRiskScanner.Report;
using SupplyRiskScanner.Utils;

class Program
{
    static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || args.Contains("--help"))
        {
            Console.WriteLine("Usage: SupplyRiskScanner --path <project_path> [--out report.html] [--ecosystems pypi,nuget]");
            return 1;
        }

        string path = null;
        string outFile = "report.html";
        string ecosystems = "pypi,nuget";
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--path" && i + 1 < args.Length) path = args[i + 1];
            if (args[i] == "--out" && i + 1 < args.Length) outFile = args[i + 1];
            if (args[i] == "--ecosystems" && i + 1 < args.Length) ecosystems = args[i + 1];
        }

        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
        {
            Console.WriteLine("Error: valid --path is required.");
            return 2;
        }

        Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), ".cache"));

        var listResults = new System.Collections.Generic.List<SupplyRiskScanner.Report.PackageResult>();

        var cache = new FileCache(Path.Combine(Directory.GetCurrentDirectory(), ".cache"));
        var http = new System.Net.Http.HttpClient();
        var pypi = new PyPiCollector(http, cache);
        var nuget = new NuGetCollector(http, cache);

        var scorer = new Scorer();

        var ecos = ecosystems.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (ecos.Contains("pypi"))
        {
            var reqFile = Path.Combine(path, "requirements.txt");
            if (File.Exists(reqFile))
            {
                var parser = new RequirementsParser();
                var pkgs = parser.Parse(reqFile);
                Console.WriteLine($"Found {pkgs.Count} PyPI packages in requirements.txt");
                foreach (var pkg in pkgs)
                {
                    Console.WriteLine($"Collecting PyPI:{pkg}..."); var info = await pypi.GetPackageInfoAsync(pkg);
                    var score = scorer.Score(info);
                    listResults.Add(new SupplyRiskScanner.Report.PackageResult { Ecosystem = "pypi", Name = pkg, Info = info, Score = score });
                    await Task.Delay(200);
                }
            }
            else Console.WriteLine("No requirements.txt found for PyPI in provided path.");
        }

        if (ecos.Contains("nuget"))
        {
            // find csproj files in path
            var csprojFiles = Directory.GetFiles(path, "*.csproj", SearchOption.AllDirectories);
            if (csprojFiles.Length == 0) Console.WriteLine("No .csproj files found for NuGet in provided path.");
            else
            {
                var parser = new CsProjParser();
                var nugetPkgs = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var cs in csprojFiles)
                {
                    var names = parser.Parse(cs);
                    foreach (var n in names) nugetPkgs.Add(n);
                }
                Console.WriteLine($"Found {nugetPkgs.Count} NuGet packages from .csproj files");
                foreach (var pkg in nugetPkgs)
                {
                    Console.WriteLine($"Collecting NuGet:{pkg}..."); var info = await nuget.GetPackageInfoAsync(pkg);
                    var score = scorer.Score(info);
                    listResults.Add(new SupplyRiskScanner.Report.PackageResult { Ecosystem = "nuget", Name = pkg, Info = info, Score = score });
                    await Task.Delay(200);
                }
            }
        }

        var reportGen = new ReportGenerator();
        reportGen.GenerateHtmlReport(listResults, outFile);
        reportGen.GenerateJsonReport(listResults, Path.ChangeExtension(outFile, ".json"));

        int high = listResults.Count(r => r.Score.risk_level == "High" || r.Score.risk_level == "Critical");
        int med = listResults.Count(r => r.Score.risk_level == "Medium");
        int low = listResults.Count(r => r.Score.risk_level == "Low");

        Console.WriteLine($"Scanned {listResults.Count} packages. High/Critical: {high}, Medium: {med}, Low: {low}");
        Console.WriteLine($"Report saved to {outFile} and {Path.ChangeExtension(outFile, ".json")}");
        return 0;
    }
}
