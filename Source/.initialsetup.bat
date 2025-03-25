# Open a terminal in VS Code (Terminal > New Terminal)

# Create a new directory for your project and navigate to it
mkdir MinimalSqlExport
cd MinimalSqlExport

# Create a new .NET solution
dotnet new sln -n MinimalSqlExport

# Create a new console application project
dotnet new console -n MinimalSqlExport.Core

# Add the project to the solution
dotnet sln add MinimalSqlExport.Core/MinimalSqlExport.Core.csproj

# Add required NuGet packages
cd MinimalSqlExport.Core
dotnet add package Serilog
dotnet add package Microsoft.Data.SqlClient
dotnet add package System.CommandLine