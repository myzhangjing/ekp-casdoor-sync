$connStr = "Server=sso.fzcsps.com;Port=3306;Database=casdoor;User Id=root;Password=zhangjing;SslMode=None;"

Add-Type -Path "C:\Users\ThinkPad\.nuget\packages\mysqlconnector\2.4.0\lib\net8.0\MySqlConnector.dll"

$conn = New-Object MySqlConnector.MySqlConnection($connStr)
$conn.Open()

$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT name, client_id, client_secret FROM application WHERE name = 'fzcsps-app' OR client_id = 'aecd00a352e5c560ffe6' LIMIT 5"

$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Host "Application: $($reader['name'])"
    Write-Host "Client ID: $($reader['client_id'])"
    Write-Host "Client Secret: $($reader['client_secret'])"
    Write-Host "---"
}
$reader.Close()
$conn.Close()
