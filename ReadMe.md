# Minimal SQL Export

A simple tool to export data from SQL Server databases in various formats. Perfect for scheduled reports, data transfers, or one-off exports.

![Screenshot of Minimal SQL Export](https://github.com/hawkinslabdev/minimalsqlexport/raw/main/Source/example.png)

## Getting Started

### Install It

1. Download the latest release from the releases page
2. Extract the ZIP file to a folder of your choice (e.g., C:\Tools\MinimalSqlExport)
3. Run `MinimalSqlExport.exe` - this creates default settings files in a `profiles` folder

### Your First Export

After installation, the easiest way to get started is:

1. Open Command Prompt or PowerShell
2. Navigate to your installation folder: `cd C:\Tools\MinimalSqlExport`
3. Run: `MinimalSqlExport`
4. Follow the on-screen prompts to select options

### Create Your Own Profile

Profiles make it easy to save your export settings:

1. Find the `profiles` folder that was created during first run
2. Copy `default.json` and rename it (e.g., `customers.json`)
3. Edit the file with Notepad or any text editor:
   - Change `Name` to match your file name (without .json)
   - Update `ConnectionString` to your database 
   - Change `Query` to your SQL statement
   - Set `Format` to CSV, JSON, XML, TAB, YAML, or AUTO
   - Update `OutputDirectory` to where you want files saved

Example profile (customers.json):
```json
{
  "Name": "customers",
  "ConnectionString": "Server=MyServer;Database=Sales;Integrated Security=True;Encrypt=False;TrustServerCertificate=True;",
  "Query": "SELECT TOP 100 * FROM Customers WHERE Region = @Region",
  "Format": "CSV",
  "OutputDirectory": "C:\\Reports\\Daily",
  "CommandTimeout": 30,
  "Parameters": [
    {
      "Name": "@Region",
      "Value": "East",
      "Type": "NVarChar"
    }
  ]
}
```

The `Parameters` section lets you safely include variables in your SQL query, which helps prevent SQL injection attacks and makes your profiles more flexible.

## Command Line Usage

Run a saved profile:
```
MinimalSqlExport -p customers
```

See all available profiles:
```
MinimalSqlExport -l
```

Run a profile but change the query:
```
MinimalSqlExport -p customers -q "SELECT * FROM Customers WHERE JoinDate > '2023-01-01'"
```

Change output format on the fly:
```
MinimalSqlExport -p customers -f JSON
```

Specify custom output file:
```
MinimalSqlExport -p customers -o "C:\Reports\customers-march.csv"
```

Enable email notifications for errors:
```
MinimalSqlExport -p customers -n
```

## New Features

### Post-Processing with Stored Procedures

After completing an export, you can now automatically run a stored procedure to perform additional tasks such as:

- Recording the export in an audit table
- Moving or processing the output file
- Triggering additional business processes
- Sending notifications

Example profile with post-processing:
```json
{
  "Name": "customers-with-postprocess",
  "ConnectionString": "Server=MyServer;Database=Sales;Integrated Security=True;",
  "Query": "SELECT * FROM Customers",
  "Format": "CSV",
  "OutputDirectory": "C:\\Reports\\Daily",
  "PostProcess": {
    "Name": "sp_LogExportCompletion",
    "Schema": "dbo",
    "RequireSuccess": true,
    "CaptureReturnValue": true,
    "CommandTimeout": 30,
    "Parameters": [
      {
        "Name": "ExportFileName",
        "Type": "varchar",
        "Size": 255,
        "Value": "${ExportFileName}"
      },
      {
        "Name": "ExportPath",
        "Type": "varchar",
        "Size": 500,
        "Value": "${ExportFilePath}"
      },
      {
        "Name": "RowCount",
        "Type": "int",
        "Value": "${RowCount}"
      },
      {
        "Name": "ExportTimestamp",
        "Type": "datetime",
        "Value": "${Timestamp}"
      }
    ]
  }
}
```

#### Special Variables for Post-Processing

You can use the following variables in your stored procedure parameters:

- `${ExportFilePath}` - Full path of the exported file
- `${ExportFileName}` - Only the filename portion  
- `${RowCount}` - Number of rows exported
- `${Timestamp}` - Current date/time (format: yyyy-MM-dd HH:mm:ss)

### Enhanced Email Notifications

Configure email notifications through a dedicated config file (`mailconfig.json`):

```json
{
  "SmtpServer": "smtp.example.com",
  "SmtpPort": 587,
  "UseSsl": true,
  "Username": "notifications@example.com",
  "Password": "your_secure_password",
  "From": "sql-exports@example.com",
  "To": "admin@example.com",
  "Subject": "[MinimalSqlExport] SQL Export Notification",
  "NotifyOnlyErrors": true
}
```

Enable notifications for a specific profile by setting:
```json
"EnableMailNotification": true
```

### Dapper Integration for Improved Performance

MinimalSqlExport now uses Dapper for database interactions, which provides:
- Better performance and query efficiency
- Improved parameter handling
- Robust type conversions
- More reliable connection management

### Customizable Logging

Control application logging through the `logsettings.json` file:
```json
{
  "LogLevel": "Information"
}
```

Available log levels: Debug, Information, Warning, Error

## Set Up Scheduled Reports

### Basic Windows Task

1. Open Task Scheduler (search in Start menu)
2. Click "Create Basic Task"
3. Name it (e.g., "Daily Customer Export")
4. Set when to run it (daily, weekly, etc.)
5. Choose "Start a program"
6. Browse to your MinimalSqlExport.exe location
7. Add arguments: `-p customers -f CSV -n`
8. Click Finish

### For Multiple Reports

Create a batch file (daily_exports.bat):
```batch
@echo off
cd /d "C:\Tools\MinimalSqlExport"

echo Exporting customers...
MinimalSqlExport -p customers -f CSV -n

echo Exporting orders...
MinimalSqlExport -p orders -f JSON -n

echo Done!
```

Then schedule this batch file instead.

## Advanced Features & Options

### Export Formats

- **CSV**: Standard comma-separated values
- **JSON**: JavaScript Object Notation
- **XML**: Extensible Markup Language
- **TAB**: Tab-separated values
- **YAML**: YAML Ain't Markup Language
- **AUTO**: Auto-detect from SQL Server's output format

### Format-Specific Settings

You can customize how each format behaves in your profile:

#### CSV/TAB Settings
```json
"CSV": {
  "Header": true,
  "Delimiter": ",",
  "Separator": ",",
  "Decimal": "."
}
```

#### XML Settings
```json
"XML": {
  "AppendHeader": true,
  "RootNode": "Customers",
  "RowNode": "Customer"
}
```

#### JSON Settings
```json
"JSON": {
  "WriteIndented": true
}
```

#### YAML Settings
```json
"YAML": {
  "IncludeHeader": true,
  "IndentationLevel": 2,
  "EmitDefaults": false
}
```

### Security Features

Run in secure mode to validate queries before execution (helps prevent potentially dangerous SQL):
```
MinimalSqlExport -p profile -s
```

### Error Notifications

Get email when exports fail by enabling notifications:
```
MinimalSqlExport -p profile -n
```

You can also use PowerShell's `Send-MailMessage` in a batch file for more control over notifications:
```batch
powershell -Command "Send-MailMessage -From 'alerts@mycompany.com' -To 'user@mycompany.com' -Subject 'SQL Export Completed' -Body 'The export has completed.' -SmtpServer 'smtp.mycompany.com'"
```

## License

[MIT License](LICENSE)
