# PowerShell script to export admin table data to CSV format

Write-Host "Exporting admin table data to CSV format..." -ForegroundColor Green

# Set the connection string
$env:CONNECTION_STRING = "Server=tctapps-development.database.windows.net;Database=TCTBackOffice;User Id=tctdev;Password=TCTapps4pp5;"

try {
    # First, get the column headers
    Write-Host "Getting table structure..." -ForegroundColor Yellow
    $headers = sqlcmd -S "tctapps-development.database.windows.net" -d "TCTBackOffice" -U "tctdev" -P "TCTapps4pp5" -Q "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'admin' ORDER BY ORDINAL_POSITION" -h -1 -W
    
    # Get the data
    Write-Host "Retrieving data..." -ForegroundColor Yellow
    $data = sqlcmd -S "tctapps-development.database.windows.net" -d "TCTBackOffice" -U "tctdev" -P "TCTapps4pp5" -Q "SELECT * FROM admin" -h -1 -W
    
    # Create CSV content
    $csvContent = @()
    
    # Add headers
    $csvContent += ($headers -join ",")
    
    # Add data rows
    foreach ($row in $data) {
        if ($row.Trim() -ne "") {
            $csvContent += $row
        }
    }
    
    # Save to file
    $csvContent | Out-File -FilePath "admin_data.csv" -Encoding UTF8
    
    Write-Host "Data exported to admin_data.csv" -ForegroundColor Green
    Write-Host "File location: $(Get-Location)\admin_data.csv" -ForegroundColor Cyan
    
    # Display first few rows
    Write-Host "`nFirst 5 rows of data:" -ForegroundColor Yellow
    $csvContent | Select-Object -First 6 | ForEach-Object { Write-Host $_ -ForegroundColor White }
    
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`nExport completed." -ForegroundColor Green
