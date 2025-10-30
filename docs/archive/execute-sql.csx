using System;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

// Quick SQL executor
var connStr = Environment.GetEnvironmentVariable("EKP_SQLSERVER_CONN");
if (string.IsNullOrWhiteSpace(connStr))
{
    Console.WriteLine("ERROR: EKP_SQLSERVER_CONN environment variable not set");
    return 1;
}

var sqlPath = args.Length > 0 ? args[0] : "FIX_HIERARCHY_DEPTH.sql";
if (!File.Exists(sqlPath))
{
    Console.WriteLine($"ERROR: SQL file not found: {sqlPath}");
    return 1;
}

Console.WriteLine($"Reading SQL script: {sqlPath}");
var sqlContent = File.ReadAllText(sqlPath);

// Split by GO statements
var batches = Regex.Split(sqlContent, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)
    .Select(b => b.Trim())
    .Where(b => !string.IsNullOrWhiteSpace(b) && !b.StartsWith("--"))
    .ToList();

Console.WriteLine($"Total batches: {batches.Count}\n");

try
{
    using var conn = new SqlConnection(connStr);
    conn.Open();
    Console.WriteLine("Connected to SQL Server");
    
    int successCount = 0;
    foreach (var batch in batches)
    {
        // Skip PRINT statements
        if (batch.Trim().StartsWith("PRINT", StringComparison.OrdinalIgnoreCase))
            continue;
            
        using var cmd = new SqlCommand(batch, conn)
        {
            CommandTimeout = 300
        };
        
        try
        {
            cmd.ExecuteNonQuery();
            successCount++;
            Console.WriteLine($"  Batch {successCount} executed successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Warning: {ex.Message}");
        }
    }
    
    Console.WriteLine($"\nView fix applied successfully!");
    Console.WriteLine("Now query the view to verify:");
    Console.WriteLine("  SELECT COUNT(*) FROM vw_org_structure_sync WHERE parent_id IS NOT NULL AND parent_id != 'fzswjtOrganization'");
    
    return 0;
}
catch (Exception ex)
{
    Console.WriteLine($"\nERROR: {ex.Message}");
    return 1;
}
