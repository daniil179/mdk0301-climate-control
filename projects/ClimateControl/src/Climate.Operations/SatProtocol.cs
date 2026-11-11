using System.Text;
using System.Text.Json;
namespace Climate.Operations;
public sealed record SatCheck(string Id, string Description, string Status, string Evidence);
public static class SatProtocol
{
    public static string ToMarkdown(IReadOnlyList<SatCheck> checks)
    {
        string Clean(string s) => s.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
        var result = new StringBuilder("# Протокол программного стенда SAT\n\n");
        result.AppendLine("Протокол автоматических проверок. Не является актом приёмки оборудования у заказчика.\n");
        result.AppendLine("| ID | Проверка | Статус | Доказательство |\n|---|---|---|---|");
        foreach (var check in checks)
            result.AppendLine($"| {Clean(check.Id)} | {Clean(check.Description)} | {Clean(check.Status)} | {Clean(check.Evidence)} |");
        result.AppendLine("\nDPAPI и проверки на пользовательском Windows отложены; не включены в выполненный объём.");
        return result.ToString();
    }
    public static void ConvertJson(string input, string output)
    {
        var rows = JsonSerializer.Deserialize<List<SatCheck>>(File.ReadAllText(input)) ?? throw new InvalidDataException();
        if (rows.Any(x => x.Status is not ("PASS" or "FAIL" or "DEFERRED"))) throw new InvalidDataException("Unknown SAT status.");
        File.WriteAllText(output, ToMarkdown(rows));
    }
}
