using Microsoft.Data.SqlClient;
using System.Text.Json;
using System.Globalization;

// 1. YOUR CONNECTION STRING
string connString = "Server=spdbssrep004.healthbc.org;Database=StorageServices;Integrated Security=True;TrustServerCertificate=True;";
var masterExport = new Dictionary<string, object>();

Console.WriteLine("Connecting to SQL Server...");

try 
{
    // 2. PROCESS TIER 1 (Main Storage)
    Console.WriteLine("Fetching Tier 1 Data...");
    masterExport["tier1"] = await GetData(
        new[] { "KDCXP01", "KDCXP02", "KDCALLETRA01", "KDCALLETRA02", "KDCALLETRA03", "KDCPRIM02", "KDCPRIM03" }, 
        "Tier 1 Capacity % Used", 
        "Tier 1 Capacity (GB)"
    );

    // 3. PROCESS COHESITY
    Console.WriteLine("Fetching Cohesity Data...");
    masterExport["cohesity"] = await GetData(
        new[] { "COHCCH01", "COHCCH02", "COHCW01", "COHFCE01", "COHJPN01", "COHKCE01", "COHKCE04", "COHKDC01", "COHKDC04", "COHKEL01", "COHPCE01", "COHSMH01", "COHVCE01" }, 
        "Total Capacity % Used", 
        "Total Capacity (GB)"
    );

    // 4. SAVE TO JSON
    var options = new JsonSerializerOptions { WriteIndented = true };
    string jsonOutput = JsonSerializer.Serialize(masterExport, options);
    File.WriteAllText("data.json", jsonOutput);

    Console.WriteLine("--------------------------------------------------");
    Console.WriteLine("SUCCESS! data.json has been created successfully.");
    Console.WriteLine("You can now upload this file to GitHub.");
    Console.WriteLine("--------------------------------------------------");
}
catch (Exception ex)
{
    Console.WriteLine("ERROR: " + ex.Message);
}

// --- HELPER METHOD ---
async Task<Dictionary<string, List<object>>> GetData(string[] arrays, string pctM, string gbM) 
{
    var history = new Dictionary<string, List<object>>();
    using var conn = new SqlConnection(connString);
    await conn.OpenAsync();
    
    var sql = $@"SELECT e.EquipmentName, CAST(r.ReportDate AS DATE) as d, m.MetricName, AVG(CAST(r.ReportValue AS FLOAT)) as val
                 FROM dbo.Reports r JOIN dbo.Equipment e ON r.EquipmentID = e.EquipmentID JOIN dbo.Metrics m ON r.MetricID = m.MetricID
                 WHERE e.EquipmentName IN ('{string.Join("','", arrays)}') AND m.MetricName IN ('{pctM}', '{gbM}') AND r.ReportDate >= DATEADD(year, -1, GETDATE())
                 GROUP BY e.EquipmentName, CAST(r.ReportDate AS DATE), m.MetricName ORDER BY d ASC";
    
    using var cmd = new SqlCommand(sql, conn);
    using var reader = await cmd.ExecuteReaderAsync();
    var pivot = new Dictionary<(string, DateTime), (double Tot, double Pct)>();
    
    while (await reader.ReadAsync()) {
        var k = (reader.GetString(0), reader.GetDateTime(1));
        if (!pivot.ContainsKey(k)) pivot[k] = (0, 0);
        if (reader.GetString(2) == pctM) pivot[k] = (pivot[k].Tot, reader.GetDouble(3));
        else pivot[k] = (reader.GetDouble(3), pivot[k].Pct);
    }
    
    foreach (var n in pivot.Keys.Select(k => k.Item1).Distinct()) {
        history[n] = new List<object>();
        foreach (var k in pivot.Keys.Where(x => x.Item1 == n).OrderBy(x => x.Item2)) {
            double tb = pivot[k].Tot / 1024.0;
            double u = tb * (pivot[k].Pct / 100.0);
            history[n].Add(new { 
                Date = k.Item2.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture).ToUpper(), 
                Total = Math.Round(tb, 2), 
                Percent = Math.Round(pivot[k].Pct, 2), 
                Used = Math.Round(u, 2), 
                Free = Math.Round(tb - u, 2) 
            });
        }
    }
    return history;
}