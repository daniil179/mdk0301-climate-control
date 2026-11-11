using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CodeMetrics;

if (args.Length < 3) throw new ArgumentException("Arguments: source.cs dotnet-sdk-root output.json");
var referenceFiles = new List<string>();
foreach (string pack in new[] { "Microsoft.NETCore.App.Ref", "Microsoft.AspNetCore.App.Ref" })
{
    string folder = Directory.GetDirectories(Path.Combine(args[1], "packs", pack))
        .Where(x => Path.GetFileName(x).StartsWith("8.0.", StringComparison.Ordinal))
        .OrderByDescending(x => Version.Parse(Path.GetFileName(x))).First();
    referenceFiles.AddRange(Directory.EnumerateFiles(Path.Combine(folder, "ref", "net8.0"), "*.dll"));
}
var references = referenceFiles.DistinctBy(Path.GetFileName)
    .Select(f => MetadataReference.CreateFromFile(f)).ToArray();
var source = CSharpSyntaxTree.ParseText(File.ReadAllText(args[0]), path: args[0]);
var usings = CSharpSyntaxTree.ParseText("global using System; global using System.Collections.Generic; global using System.Linq;");
var compilation = CSharpCompilation.Create("MetricsSample", [source, usings], references,
    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
var errors = compilation.GetDiagnostics().Where(x => x.Severity == DiagnosticSeverity.Error).ToArray();
if (errors.Length > 0) throw new InvalidOperationException(string.Join(Environment.NewLine, errors.Select(x => x.ToString())));
var data = await CodeAnalysisMetricData.ComputeAsync(new CodeMetricsAnalysisContext(compilation, CancellationToken.None));
var rows = new List<object>();
void Visit(CodeAnalysisMetricData item)
{
    if (item.Symbol.Kind is SymbolKind.NamedType or SymbolKind.Method)
        rows.Add(new { symbol = item.Symbol.ToDisplayString(), kind = item.Symbol.Kind.ToString(),
            mi = item.MaintainabilityIndex, cc = item.CyclomaticComplexity,
            coupling = item.CoupledNamedTypes.Count, sourceLines = item.SourceLines,
            executableLines = item.ExecutableLines });
    foreach (var child in item.Children) Visit(child);
}
Visit(data);
string json = JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true });
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[2]))!);
File.WriteAllText(args[2], json); Console.WriteLine(json);
