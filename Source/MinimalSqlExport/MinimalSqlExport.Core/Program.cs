using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.Data.SqlClient;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Text.Json.Serialization;
using Serilog;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Spectre.Console;

namespace MinimalSqlExport
{
    partial class Program
    {
        
        private enum ErrorCodes
        {
            Success = 0,
            GeneralError = 1,
            ProfileNotFound = 2,
            ConnectionError = 3,
            QueryExecutionError = 4,
            OutputFileError = 5,
            FormatError = 6,
            ConfigurationError = 7
        }

        const string ProfilesFolder = "profiles";
        static Dictionary<string, Profile> Profiles = new();

        static int Main(string[] args)
        {
            Directory.CreateDirectory("log");
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .WriteTo.File("log/log-.log", rollingInterval: RollingInterval.Day)
                .CreateLogger();

            try
            {
                LoadOrCreateProfiles();

                if (args.Length == 0)
                {
                    RunConsole();
                    return 0;
                }

                var rootCommand = new RootCommand("SQL Profile CLI")
                {
                    new Option<string>(
                        aliases: new[] { "--profile", "-p" },
                        description: "Profile name to use",
                        getDefaultValue: () => "default"),
                    
                    new Option<string>(
                        aliases: new[] { "--query", "-q" },
                        description: "SQL Query to execute (optional if defined in profile)"),
                    
                    new Option<string>(
                        aliases: new[] { "--format", "-f" },
                        description: "Output format: AUTO, JSON, XML, CSV, TAB, YAML (optional if defined in profile)")
                        .FromAmong("AUTO", "JSON", "XML", "CSV", "TAB", "YAML"),
                        
                    new Option<FileInfo>(
                        aliases: new[] { "--output", "-o" },
                        description: "Output file path (overrides profile setting)"),
                        
                    new Option<bool>(
                        aliases: new[] { "--list", "-l" },
                        description: "List available profiles",
                        getDefaultValue: () => false)
                };

                rootCommand.Description = "Run SQL queries per profile and export result in various formats";

                rootCommand.Handler = CommandHandler.Create<string, string, string, FileInfo, bool>(
                    (profile, query, format, output, list) => {
                        if (list)
                        {
                            DisplayProfiles();
                            return 0;
                        }
                        
                        return ExecuteQueryWithStatus(profile, query, format, output);
                    });

                return rootCommand.Invoke(args);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unhandled exception");
                return (int)ErrorCodes.GeneralError;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        static void LoadOrCreateProfiles()
        {
            if (!Directory.Exists(ProfilesFolder))
                Directory.CreateDirectory(ProfilesFolder);

            var profileFiles = Directory.GetFiles(ProfilesFolder, "*.json");

            if (profileFiles.Length == 0)
            {
                var demo = new Profile
                {
                    Name = "default",
                    ConnectionString = "Server=VM2K22;Database=600;Integrated Security=True;Encrypt=False;TrustServerCertificate=True;",
                    Query = "SELECT cmp_code, cmp_wwn, cmp_name FROM dbo.cicmpy",
                    Format = "CSV",
                    OutputDirectory = ".\\output\\items",
                    OutputProperties = new OutputSettings
                    {
                        CSV = new CsvSettings { Header = true, Delimiter = ",", Separator = ",", Decimal = "." },
                        XML = new XmlSettings { AppendHeader = true, RootNode = "Root" },
                        JSON = new JsonSettings()
                    },
                    CommandTimeout = 30
                };
                var demoPath = Path.Combine(ProfilesFolder, "default.json");
                var options = new JsonSerializerOptions { WriteIndented = true };
                string jsonString = JsonSerializer.Serialize(demo, JsonContext.Default.Profile);
                File.WriteAllText(demoPath, jsonString);
                profileFiles = new[] { demoPath };
            }

            foreach (var file in profileFiles)
            {
                try 
                {
                    var json = File.ReadAllText(file);
                    var profile = JsonSerializer.Deserialize(json, JsonContext.Default.Profile);
                    if (profile != null && !string.IsNullOrWhiteSpace(profile.Name))
                        Profiles[profile.Name] = profile;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error loading profile from {File}: {Message}", file, ex.Message);
                    AnsiConsole.MarkupLine($"[red]Error loading profile from {file}: {ex.Message}[/]");
                }
            }

            if (Profiles.Count == 0)
            {
                Log.Warning("No valid profiles were loaded");
                AnsiConsole.MarkupLine("[yellow]Warning: No valid profiles were loaded.[/]");
            }
        }

        static void RunConsole()
        {
            AnsiConsole.MarkupLine("[bold green]=== SQL Profile CLI ===[/]");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[yellow]Available profiles:[/]");
            
            
            var sortedProfiles = Profiles.Keys.OrderBy(k => k).ToList();
            
            if (sortedProfiles.Count == 0)
            {
                AnsiConsole.MarkupLine("[red]No profiles found. Please create a profile first.[/]");
                return;
            }
            
            
            for (int i = 0; i < sortedProfiles.Count; i++)
            {
                AnsiConsole.MarkupLine($" {i+1}. [aqua]{sortedProfiles[i]}[/]");
            }

            Console.Write("\nEnter profile number or name: ");
            var input = Console.ReadLine()?.Trim() ?? "";
            
            string profile;
            
            if (int.TryParse(input, out int profileIndex) && profileIndex >= 1 && profileIndex <= sortedProfiles.Count)
            {
                
                profile = sortedProfiles[profileIndex - 1];
            }
            else
            {
                
                profile = input;
            }
            
            if (!Profiles.TryGetValue(profile, out var profileData))
            {
                AnsiConsole.MarkupLine($"[red]Profile '{profile}' not found.[/]");
                return;
            }
            
            
            AnsiConsole.MarkupLine($"[green]Using profile:[/] [blue]{profile}[/]");

            Console.Write("Use default query from profile? (Y/N) [Y]: ");
            var useDefaultQueryInput = Console.ReadLine()?.Trim() ?? "";
            var useDefaultQuery = string.IsNullOrEmpty(useDefaultQueryInput) || useDefaultQueryInput.Equals("Y", StringComparison.OrdinalIgnoreCase);
            string query = useDefaultQuery ? profileData.Query : Prompt("Enter custom SQL query:");

            Console.Write("Use default format from profile? (Y/N) [Y]: ");
            var useDefaultFormatInput = Console.ReadLine()?.Trim() ?? "";
            var useDefaultFormat = string.IsNullOrEmpty(useDefaultFormatInput) || useDefaultFormatInput.Equals("Y", StringComparison.OrdinalIgnoreCase);
            string format = useDefaultFormat ? profileData.Format : Prompt("Enter format (JSON, XML, CSV, TAB, YAML):");

            Console.Write("Use default output path from profile? (Y/N) [Y]: ");
            var useDefaultPathInput = Console.ReadLine()?.Trim() ?? "";
            var useDefaultPath = string.IsNullOrEmpty(useDefaultPathInput) || useDefaultPathInput.Equals("Y", StringComparison.OrdinalIgnoreCase);
            string? customOutputPath = null;

            if (!useDefaultPath)
            {
                customOutputPath = Prompt("Enter output file path:");
            }

            try
            {
                ExecuteQuery(profile, query, format, customOutputPath);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Operation failed: {ex.Message}[/]");
                Log.Error(ex, "Operation failed in interactive mode");
            }
        }

        static string Prompt(string message)
        {
            Console.Write(message + " ");
            return Console.ReadLine()?.Trim() ?? string.Empty;
        }

        static void DisplayProfiles()
        {
            AnsiConsole.MarkupLine("[bold green]=== Available Profiles ===[/]");
            
            var sortedProfiles = Profiles.Keys.OrderBy(k => k).ToList();
            if (sortedProfiles.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No profiles found.[/]");
                return;
            }
            
            foreach (var profileName in sortedProfiles)
            {
                var profile = Profiles[profileName];
                AnsiConsole.MarkupLine($"[aqua]{profileName}[/]");
                AnsiConsole.MarkupLine($"  Connection: {MaskConnectionString(profile.ConnectionString)}");
                AnsiConsole.MarkupLine($"  Format: {profile.Format}");
                AnsiConsole.MarkupLine($"  Output Directory: {profile.OutputDirectory}");
                AnsiConsole.MarkupLine($"  Command Timeout: {profile.CommandTimeout ?? 30} seconds");
                AnsiConsole.WriteLine();
            }
        }

        static int ExecuteQueryWithStatus(string profile, string query, string format, FileInfo output)
        {
            try
            {
                if (!Profiles.ContainsKey(profile))
                {
                    AnsiConsole.MarkupLine($"[red]Profile '{profile}' not found. Use --list to see available profiles.[/]");
                    Log.Error("Profile '{Profile}' not found", profile);
                    return (int)ErrorCodes.ProfileNotFound;
                }
                
                string customOutputPath = output?.FullName ?? string.Empty;
                
                if (string.IsNullOrWhiteSpace(format))
                {
                    format = Profiles[profile].Format;
                    if (string.IsNullOrWhiteSpace(format))
                    {
                        AnsiConsole.MarkupLine("[red]No format specified in command or profile.[/]");
                        Log.Error("No format specified for profile '{Profile}'", profile);
                        return (int)ErrorCodes.ConfigurationError;
                    }
                }
                
                if (!new[] { "JSON", "XML", "CSV", "TAB", "YAML", "AUTO" }.Contains(format.ToUpper()))
                {
                    AnsiConsole.MarkupLine($"[red]Invalid format: {format}. Must be one of: AUTO, JSON, XML, CSV, TAB, YAML[/]");
                    Log.Error("Invalid format '{Format}' specified", format);
                    return (int)ErrorCodes.FormatError;
                }
                
                if (string.IsNullOrWhiteSpace(query))
                {
                    query = Profiles[profile].Query;
                    if (string.IsNullOrWhiteSpace(query))
                    {
                        AnsiConsole.MarkupLine("[red]No query specified in command or profile.[/]");
                        Log.Error("No query specified for profile '{Profile}'", profile);
                        return (int)ErrorCodes.ConfigurationError;
                    }
                }
                
                
                if (!string.IsNullOrWhiteSpace(customOutputPath))
                {
                    try
                    {
                        var directory = Path.GetDirectoryName(customOutputPath);
                        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                        {
                            Directory.CreateDirectory(directory);
                        }
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.MarkupLine($"[red]Error creating output directory: {ex.Message}[/]");
                        Log.Error(ex, "Error creating output directory for path '{Path}'", customOutputPath);
                        return (int)ErrorCodes.OutputFileError;
                    }
                }
                
                ExecuteQuery(profile, query, format, customOutputPath);
                return (int)ErrorCodes.Success; 
            }
            catch (SqlException ex)
            {
                Log.Error(ex, "SQL Error executing query: {Message}", ex.Message);
                AnsiConsole.MarkupLine($"[red]SQL Error: {ex.Message}[/]");
                AnsiConsole.MarkupLine($"[red]Error Number: {ex.Number}[/]");
                
                if (ex.Errors != null)
                {
                    foreach (SqlError error in ex.Errors)
                    {
                        Log.Error("SQL Error {Number}: {Message} at Line {Line}", 
                            error.Number, error.Message, error.LineNumber);
                    }
                }
                
                return (int)ErrorCodes.QueryExecutionError;
            }
            catch (InvalidOperationException ex)
            {
                Log.Error(ex, "Connection configuration error: {Message}", ex.Message);
                AnsiConsole.MarkupLine($"[red]Connection configuration error: {ex.Message}[/]");
                return (int)ErrorCodes.ConnectionError;
            }
            catch (IOException ex)
            {
                Log.Error(ex, "File I/O error: {Message}", ex.Message);
                AnsiConsole.MarkupLine($"[red]File I/O error: {ex.Message}[/]");
                return (int)ErrorCodes.OutputFileError;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unhandled error executing query: {Message}", ex.Message);
                AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
                
                if (ex.InnerException != null)
                {
                    Log.Error(ex.InnerException, "Inner exception: {Message}", ex.InnerException.Message);
                    AnsiConsole.MarkupLine($"[red]Details: {ex.InnerException.Message}[/]");
                }
                
                return (int)ErrorCodes.GeneralError;
            }
        }

        private static readonly JsonSerializerOptions CachedJsonOptions = new JsonSerializerOptions { WriteIndented = true }; 

        static void ExecuteQuery(string profile, string query, string format, string? customOutputPath = null)
        {
            if (!Profiles.ContainsKey(profile))
            {
                Log.Error("Profile '{Profile}' not found", profile);
                AnsiConsole.MarkupLine($"[red]Profile '{profile}' not found.[/]");
                return;
            }

            var profileData = Profiles[profile];
            var connString = profileData.ConnectionString;
            query ??= profileData.Query;
            format ??= profileData.Format;
            var outputProps = profileData.OutputProperties;

            if (string.IsNullOrWhiteSpace(connString))
            {
                Log.Error("Empty connection string in profile '{Profile}'", profile);
                AnsiConsole.MarkupLine("[red]Connection string is empty in profile.[/]");
                return;
            }

            if (string.IsNullOrWhiteSpace(query))
            {
                Log.Error("Empty query in profile '{Profile}'", profile);
                AnsiConsole.MarkupLine("[red]Query is empty. Please specify a query.[/]");
                return;
            }

            using var connection = new SqlConnection(connString);
            try
            {
                AnsiConsole.Status()
                    .Start("Connecting to database...", ctx => 
                    {
                        try
                        {
                            connection.Open();
                            ctx.Status("Connection established");
                        }
                        catch (SqlException ex)
                        {
                            ctx.Status("Connection failed");
                            Log.Error(ex, "SQL Connection error: {Message}", ex.Message);
                            
                            
                            if (ex.InnerException != null)
                                Log.Error(ex.InnerException, "Inner exception: {Message}", ex.InnerException.Message);
                            
                            
                            Log.Error("Connection failed to: {Connection}", 
                                MaskConnectionString(connString));
                            
                            throw;
                        }
                        catch (Exception ex)
                        {
                            ctx.Status("Connection failed");
                            Log.Error(ex, "Connection error details: {Message}", ex.Message);
                            if (ex.InnerException != null)
                                Log.Error(ex.InnerException, "Inner exception: {Message}", ex.InnerException.Message);
                            throw;
                        }
                    });
            }
            catch (Exception)
            {
                
                AnsiConsole.MarkupLine("[red]Failed to connect to database.[/]");
                throw;
            }

            List<Dictionary<string, object?>> rows = new();
            
            try
            {
                using var command = new SqlCommand(query, connection);
                
                command.CommandTimeout = profileData.CommandTimeout ?? 30; 
                
                AnsiConsole.Status()
                    .Start("Executing query...", ctx => 
                    {
                        try
                        {
                            using var reader = command.ExecuteReader();
                            ctx.Status("Processing results...");
                            
                            while (reader.Read())
                            {
                                var row = new Dictionary<string, object?>();
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    try
                                    {
                                        row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                                    }
                                    catch (Exception ex)
                                    {
                                        Log.Warning(ex, "Error reading column {Column}: {Message}", 
                                            reader.GetName(i), ex.Message);
                                        row[reader.GetName(i)] = null;
                                    }
                                }
                                rows.Add(row);
                            }
                            
                            ctx.Status($"Retrieved {rows.Count} rows");
                        }
                        catch (SqlException ex)
                        {
                            ctx.Status("Query execution failed");
                            Log.Error(ex, "SQL Error executing query: {Message}", ex.Message);
                            
                            
                            var truncatedQuery = query.Length > 500 ? query.Substring(0, 500) + "..." : query;
                            Log.Error("Failed query: {Query}", truncatedQuery);
                            throw;
                        }
                    });
            }
            catch (Exception)
            {
                
                AnsiConsole.MarkupLine("[red]Failed to execute query.[/]");
                throw;
            }

            var output = new StringBuilder();
            string fileExtension = format.ToLower();  
            
            try
            {
                switch (format.ToUpper())
                {
                    case "AUTO":
                        fileExtension = FormatWithAutoDetection(rows, output, outputProps);
                        break;
                    case "JSON":
                        FormatAsJson(rows, output, outputProps?.JSON);
                        break;
                    case "XML":
                        FormatAsXml(rows, output, outputProps?.XML);
                        break;
                    case "YAML":
                        FormatAsYaml(rows, output, outputProps?.YAML);
                        break;
                    case "CSV":
                    case "TAB":
                        FormatAsDelimited(rows, output, format, outputProps?.CSV);
                        break;
                    default:
                        Log.Error("Unsupported format: {Format}", format);
                        AnsiConsole.MarkupLine("[red]Unsupported format.[/]");
                        return;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error formatting output as {Format}: {Message}", format, ex.Message);
                AnsiConsole.MarkupLine($"[red]Error formatting output: {ex.Message}[/]");
                throw;
            }

            string outputPath;
            try
            {
                if (!string.IsNullOrWhiteSpace(customOutputPath))
                {
                    
                    outputPath = customOutputPath;
                    
                    var dirName = Path.GetDirectoryName(outputPath);
                    if (!string.IsNullOrEmpty(dirName))
                    {
                        Directory.CreateDirectory(dirName);
                    }
                }
                else
                {
                    
                    var fileName = $"output_{DateTime.Now:yyyyMMdd_HHmmss}.{fileExtension.ToLower()}";
                    var outputDir = string.IsNullOrWhiteSpace(profileData.OutputDirectory)
                        ? Directory.GetCurrentDirectory()
                        : Path.GetFullPath(profileData.OutputDirectory);

                    Directory.CreateDirectory(outputDir);
                    outputPath = Path.Combine(outputDir, fileName);
                }

                
                File.WriteAllText(outputPath, output.ToString());
                AnsiConsole.MarkupLine($"[green]Output written to:[/] [blue]{outputPath}[/]");
                Log.Information("Output successfully written to: {Path}", outputPath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error writing output to file: {Message}", ex.Message);
                AnsiConsole.MarkupLine($"[red]Error writing output to file: {ex.Message}[/]");
                throw;
            }
        }
        
        private static void FormatAsJson(List<Dictionary<string, object?>> rows, StringBuilder output, JsonSettings? settings)
        {
            if (rows.Count > 0 && rows[0].Count == 1)
            {
                var columnName = rows[0].Keys.First();
                var firstValue = rows[0][columnName]?.ToString() ?? "";
                
                
                if (firstValue.TrimStart().StartsWith("[") || firstValue.TrimStart().StartsWith("{"))
                {
                    try
                    {
                        
                        var fullJson = string.Concat(rows.Select(r => r[columnName]?.ToString() ?? ""));
                        
                        
                        var parsedJson = JsonDocument.Parse(fullJson);
                        output.AppendLine(JsonSerializer.Serialize(parsedJson.RootElement, 
                            new JsonSerializerOptions { WriteIndented = true }));
                    }
                    catch (JsonException ex)
                    {
                        Log.Warning(ex, "Failed to parse JSON from column data: {Message}", ex.Message);
                        
                        
                        output.AppendLine(JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
                    }
                }
                else
                {
                    
                    output.AppendLine(JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
                }
            }
            else
            {
                
                output.AppendLine(JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
            }
        }

        private static void FormatAsXml(List<Dictionary<string, object?>> rows, StringBuilder output, XmlSettings? settings)
        {
            
            if (settings?.AppendHeader == true)
                output.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
                
            string rootNode = settings?.RootNode ?? "Root";
            string rowNode = settings?.RowNode ?? "Row";  
            
            output.AppendLine($"<{rootNode}>");
            
            foreach (var row in rows)
            {
                output.AppendLine($"  <{rowNode}>");
                foreach (var kv in row)
                {
                    try
                    {
                        string value = System.Security.SecurityElement.Escape(kv.Value?.ToString() ?? string.Empty);
                        output.AppendLine($"    <{kv.Key}>{value}</{kv.Key}>");
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Error formatting XML value for {Key}: {Message}", kv.Key, ex.Message);
                        output.AppendLine($"    <{kv.Key}></{kv.Key}>");
                    }
                }
                output.AppendLine($"  </{rowNode}>");
            }
            
            output.AppendLine($"</{rootNode}>");
        }

        private static void FormatAsYaml(List<Dictionary<string, object?>> rows, StringBuilder output, YamlSettings? settings)
        {
            try
            {
                var serializerBuilder = new SerializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance);
                    
                
                if (settings != null)
                {
                    if (settings.IndentationLevel > 0)
                        serializerBuilder.WithIndentedSequences();
                        
                    if (settings.EmitDefaults)
                        serializerBuilder.ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull);
                }
                
                var serializer = serializerBuilder.Build();
                output.AppendLine(serializer.Serialize(rows));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error serializing to YAML: {Message}", ex.Message);
                throw;
            }
        }

        private static void FormatAsDelimited(List<Dictionary<string, object?>> rows, StringBuilder output, string format, CsvSettings? settings)
        {
            var sep = format.Equals("CSV", StringComparison.OrdinalIgnoreCase) ? settings?.Separator ?? "," : "\t";
            var includeHeader = settings?.Header ?? true;
            
            try
            {
                if (rows.Count > 0 && includeHeader)
                {
                    output.AppendLine(string.Join(sep, rows[0].Keys));
                }
                
                foreach (var row in rows)
                {
                    
                    var escapedValues = row.Values
                        .Select(v => FormatCsvValue(v?.ToString() ?? string.Empty, sep))
                        .ToList();
                    
                    output.AppendLine(string.Join(sep, escapedValues));
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error formatting as delimited text: {Message}", ex.Message);
                throw;
            }
        }

        private static string FormatCsvValue(string value, string separator)
        {
            bool needsQuoting = value.Contains(separator) || value.Contains("\"") || 
                                value.Contains("\r") || value.Contains("\n");
            
            if (needsQuoting)
            {
                
                value = value.Replace("\"", "\"\"");
                
                return $"\"{value}\"";
            }
            
            return value;
        }

        static string MaskConnectionString(string connectionString)
        {
            
            if (string.IsNullOrEmpty(connectionString))
                return string.Empty;
                
            return connectionString
                .Replace("Password=", "Password=*****")
                .Replace("pwd=", "pwd=*****")
                .Replace("User ID=", "User ID=*****")
                .Replace("uid=", "uid=*****");
        }

        static void DisplayHelp()
        {
            AnsiConsole.MarkupLine("[bold green]=== MinimalSqlExport Help ===[/]");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[yellow]Command Line Parameters:[/]");
            AnsiConsole.MarkupLine("  --profile, -p  : Specify which profile to use (default: 'default')");
            AnsiConsole.MarkupLine("  --query, -q    : SQL query to execute (overrides profile's query)");
            AnsiConsole.MarkupLine("  --format, -f   : Output format: JSON, XML, CSV, TAB, YAML (overrides profile's format)");
            AnsiConsole.MarkupLine("  --output, -o   : Output file path (overrides profile's output directory)");
            AnsiConsole.MarkupLine("  --list, -l     : List available profiles");
            
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[yellow]Available Profiles:[/]");
            
            var sortedProfiles = Profiles.Keys.OrderBy(k => k).ToList();
            for (int i = 0; i < sortedProfiles.Count; i++)
            {
                var profileName = sortedProfiles[i];
                var profileData = Profiles[profileName];
                AnsiConsole.MarkupLine($"  {i+1}. [aqua]{profileName}[/]");
                AnsiConsole.MarkupLine($"     ConnectionString: {MaskConnectionString(profileData.ConnectionString)}");
                AnsiConsole.MarkupLine($"     Query: {(profileData.Query.Length > 50 ? profileData.Query.Substring(0, 47) + "..." : profileData.Query)}");
                AnsiConsole.MarkupLine($"     Format: {profileData.Format}");
                AnsiConsole.MarkupLine($"     Output Directory: {profileData.OutputDirectory}");
                AnsiConsole.MarkupLine($"     Command Timeout: {profileData.CommandTimeout ?? 30} seconds");
                if (profileData.OutputProperties?.XML != null)
                {
                    AnsiConsole.MarkupLine($"     XML Settings: AppendHeader={profileData.OutputProperties.XML.AppendHeader}, RootNode=\"{profileData.OutputProperties.XML.RootNode}\"");
                }
                AnsiConsole.WriteLine();
            }
            
            AnsiConsole.MarkupLine("[yellow]Examples:[/]");
            AnsiConsole.MarkupLine("  MinimalSqlExport -l");
            AnsiConsole.MarkupLine("  MinimalSqlExport -p default -f JSON");
            AnsiConsole.MarkupLine("  MinimalSqlExport -p default -q \"SELECT * FROM customers\" -o \"C:\\exports\\result.csv\"");
        }

        [JsonSerializable(typeof(Profile))]
        [JsonSourceGenerationOptions(WriteIndented = true)]
        public partial class JsonContext : JsonSerializerContext
        {
        }

        private static string FormatWithAutoDetection(List<Dictionary<string, object?>> rows, StringBuilder output, OutputSettings? outputProps)
        {
            string detectedFormat = "CSV"; 
            
            
            if (rows.Count == 0)
            {
                Log.Warning("No rows returned from query");
                return detectedFormat;
            }

            
            
            if (rows.Count > 0 && rows[0].Count == 1)
            {
                var columnName = rows[0].Keys.First();
                var firstValue = rows[0][columnName]?.ToString() ?? "";
                
                
                if (firstValue.TrimStart().StartsWith("<"))
                {
                    try
                    {
                        
                        var fullXml = string.Concat(rows.Select(r => r[columnName]?.ToString() ?? ""));
                        
                        
                        var doc = new XmlDocument();
                        doc.LoadXml(fullXml);
                        
                        
                        using var stringWriter = new StringWriter();
                        using var xmlTextWriter = new XmlTextWriter(stringWriter)
                        {
                            Formatting = Formatting.Indented,
                            Indentation = 2
                        };
                        doc.WriteTo(xmlTextWriter);
                        
                        output.AppendLine(stringWriter.ToString());
                        Log.Information("Detected and formatted SQL Server FOR XML result");
                        detectedFormat = "XML";
                        return detectedFormat;
                    }
                    catch (XmlException ex)
                    {
                        Log.Warning(ex, "Data looks like XML but failed to parse: {Message}", ex.Message);
                        
                    }
                }
                
                
                else if (firstValue.TrimStart().StartsWith("[") || firstValue.TrimStart().StartsWith("{"))
                {
                    try
                    {
                        
                        var fullJson = string.Concat(rows.Select(r => r[columnName]?.ToString() ?? ""));
                        
                        
                        var parsedJson = JsonDocument.Parse(fullJson);
                        
                        var jsonOptions = new JsonSerializerOptions 
                        { 
                            WriteIndented = outputProps?.JSON?.WriteIndented ?? true 
                        };
                        
                        output.AppendLine(JsonSerializer.Serialize(parsedJson.RootElement, jsonOptions));
                        Log.Information("Detected and formatted SQL Server FOR JSON result");
                        detectedFormat = "JSON";
                        return detectedFormat;
                    }
                    catch (JsonException ex)
                    {
                        Log.Warning(ex, "Data looks like JSON but failed to parse: {Message}", ex.Message);
                        
                    }
                }
            }
            
            
            Log.Information("No specific format detected, defaulting to CSV format");
            FormatAsDelimited(rows, output, "CSV", outputProps?.CSV);
            return detectedFormat;
        }
        public class Profile
        {
            public string Name { get; set; } = string.Empty;
            public string ConnectionString { get; set; } = string.Empty;
            public string Query { get; set; } = string.Empty;
            public string Format { get; set; } = string.Empty;
            public string OutputDirectory { get; set; } = string.Empty;
            public OutputSettings? OutputProperties { get; set; }
            public int? CommandTimeout { get; set; } = 30; 
        }

        public class OutputSettings
        {
            public CsvSettings? CSV { get; set; }
            public XmlSettings? XML { get; set; }
            public JsonSettings? JSON { get; set; }
            public YamlSettings? YAML { get; set; } 
        }

        
        public class YamlSettings
        {
            public bool IncludeHeader { get; set; } = true;
            public int IndentationLevel { get; set; } = 2;
            public bool EmitDefaults { get; set; } = false;
        }

        public class CsvSettings
        {
            public bool Header { get; set; } = true;
            public string Delimiter { get; set; } = ",";
            public string Separator { get; set; } = ",";
            public string Decimal { get; set; } = ".";
        }

        public class XmlSettings
        {
            public bool AppendHeader { get; set; } = true;
            public string RootNode { get; set; } = "Root";
            public string RowNode { get; set; } = "Row";  
        }
        public class JsonSettings
        {
            public bool WriteIndented { get; set; } = true; 
        }
    }
}