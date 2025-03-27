# Minimal SQL Export (Tool)

A lightweight, profile-based SQL export utility that makes it easy to export SQL Server query results in multiple formats. Easy for people whom use SQL Server on Windows.

![alt text](https://github.com/hawkinslabdev/minimalsqlexport/raw/main/Source/example.png "Example")

## Quick Start Guide

### Installation

1. Download the latest release from the [Releases](https://github.com/yourusername/minimalsqlexport/releases) page
2. Extract the ZIP file to a location of your choice (e.g., `C:\Tools\MinimalSqlExport`)
3. The folder now contains a self-contained executable with no additional dependencies

### First Run

1. Open Command Prompt or PowerShell
2. Navigate to the folder containing MinimalSqlExport.exe:
   ```
   cd C:\Tools\MinimalSqlExport
   ```
3. Run the application in interactive mode:
   ```
   MinimalSqlExport
   ```
4. The application will create a default profile on first run
5. Follow the on-screen prompts to select a profile, customize queries, formats, and output paths

### Creating Your First Profile

1. Navigate to the `profiles` folder created by the application
2. Copy the `default.json` file and rename it (e.g., `mydb.json`)
3. Edit the file with any text editor and update:
   - The `Name` field to match your file name (without extension)
   - `ConnectionString` to point to your database
   - `Query` with your SQL query
   - `Format` to your preferred output format (CSV, JSON, XML, TAB, YAML, AUTO)
   - `OutputDirectory` to your preferred output location

Example profile (mydb.json):
```json
{
  "Name": "mydb",
  "ConnectionString": "Server=MyServer;Database=MyDB;Integrated Security=True;Encrypt=False;TrustServerCertificate=True;",
  "Query": "SELECT TOP 100 * FROM dbo.customers",
  "Format": "CSV",
  "OutputDirectory": "C:\\Exports\\MyDB",
  "CommandTimeout": 30,
  "OutputProperties": {
    "CSV": {
      "Header": true,
      "Delimiter": ",",
      "Separator": ",",
      "Decimal": "."
    }
  },
  "EnableMailNotification": true
}
```

### Running From Command Line

Execute a profile:
```
MinimalSqlExport -p mydb
```

List all available profiles:
```
MinimalSqlExport -l
```

Override the query:
```
MinimalSqlExport -p mydb -q "SELECT * FROM orders WHERE order_date > '2023-01-01'"
```

Change the output format:
```
MinimalSqlExport -p mydb -f JSON
```

Specify a custom output file:
```
MinimalSqlExport -p mydb -o "C:\Exports\customers_20250325.csv"
```

## Scheduling with Task Scheduler

### Basic Setup

1. Open Windows Task Scheduler (search for "Task Scheduler" in the Start menu)
2. Click "Create Basic Task..." in the right-side Actions panel
3. Enter a name (e.g., "Daily SQL Export") and description
4. Set your trigger (Daily, Weekly, etc.) and configure when it should run
5. Select "Start a program" as the action
6. Browse to select your MinimalSqlExport.exe location
7. Add arguments in the "Add arguments" field, for example:
   ```
   -p mydb -f CSV
   ```
8. Complete the wizard by clicking "Finish"

### Advanced Configuration

For more advanced scheduling:

1. Create a new task with "Create Task..." instead of "Create Basic Task..."
2. Configure General settings, including "Run whether user is logged on or not" for unattended execution
3. Add triggers as needed (multiple schedules possible)
4. In the Actions tab, create a new action pointing to MinimalSqlExport.exe with your arguments
5. Set the "Start in" field to your MinimalSqlExport directory
6. Configure additional conditions and settings as needed

### Using Batch Files for Complex Scenarios

For multiple exports or more complex scenarios, create a batch file:

1. Create a new text file with a `.bat` extension (e.g., `daily_exports.bat`)
2. Add your export commands:
   ```batch
   @echo off
   cd /d "C:\Tools\MinimalSqlExport"
   
   echo Exporting customers data...
   MinimalSqlExport -p customers -f CSV
   
   echo Exporting orders data...
   MinimalSqlExport -p orders -f JSON
   
   echo Exports completed!
   ```
3. Schedule this batch file in Task Scheduler instead of the direct executable

### Email Notifications on Completion

To receive email notifications when exports complete:

1. Add the following to your batch file:
   ```batch
   @echo off
   cd /d "C:\Tools\MinimalSqlExport"
   
   MinimalSqlExport -p mydb -f CSV
   
   powershell -Command "Send-MailMessage -From 'alerts@mycompany.com' -To 'user@mycompany.com' -Subject 'SQL Export Completed' -Body 'The scheduled SQL export has completed successfully.' -SmtpServer 'smtp.mycompany.com'"
   ```
2. Schedule this batch file using Task Scheduler

## Features

- Multiple output formats: JSON, XML, CSV, TAB, YAML, or AUTO detection
- Smart format detection for SQL Server's `FOR XML` and `FOR JSON` output
- Command-line and interactive modes
- Customizable output settings per profile
- Comprehensive error handling with detailed logging
- Simple, but effective error notifications (only supports SMTP)

## Profiles

Profiles are JSON files stored in the `profiles` folder. Each profile defines connection details, query, output format, and format-specific settings.

### Example Profiles

#### Auto-Detecting SQL Server FOR XML/JSON

```json
{
  "Name": "generic-export",
  "ConnectionString": "Server=MyServer;Database=MyDB;Integrated Security=True;Encrypt=False;TrustServerCertificate=True;",
  "Query": "SELECT * FROM dbo.customers FOR XML PATH('Customer'), ROOT('Customers')",
  "Format": "AUTO",
  "OutputDirectory": ".\\output\\generic",
  "CommandTimeout": 60,
  "EnableMailNotification": true
}
```

## Format Settings

### CSV/TAB Settings

```json
"CSV": {
  "Header": true,
  "Delimiter": ",",
  "Separator": ",",
  "Decimal": "."
}
```

### XML Settings

```json
"XML": {
  "AppendHeader": true,
  "RootNode": "Root",
  "RowNode": "Row"
}
```

### JSON Settings

```json
"JSON": {
  "WriteIndented": true
}
```

### YAML Settings

```json
"YAML": {
  "IncludeHeader": true,
  "IndentationLevel": 2,
  "EmitDefaults": false
}
```

## License

[MIT License](LICENSE)
