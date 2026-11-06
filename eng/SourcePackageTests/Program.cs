using System.Diagnostics;
using System.IO.Compression;
using System.Xml.Linq;

if (args.Length != 3)
{
    throw new ArgumentException("Usage: SourcePackageTests <repository-root> <configuration> <dependency-source>");
}

string repository = Path.GetFullPath(args[0]);
string configuration = args[1];
string workspace = Path.Combine(repository, "artifacts", "source-package-tests", Guid.NewGuid().ToString("N"));
string feed = Path.Combine(workspace, "feed");
string consumer = Path.Combine(workspace, "Consumer");
Directory.CreateDirectory(feed);
Directory.CreateDirectory(consumer);

Console.WriteLine($"Testing source package in {workspace}");
await Run(repository, "pack", Path.Combine(repository, "src", "PolyType.Roslyn", "PolyType.Roslyn.csproj"),
    "-c", configuration, "-o", feed, "--nologo", "-v", "quiet");

string version;
using (ZipArchive archive = ZipFile.OpenRead(Directory.GetFiles(feed, "PolyType.Roslyn.*.nupkg").Single()))
using (Stream stream = archive.Entries.Single(e => e.FullName.EndsWith(".nuspec", StringComparison.Ordinal)).Open())
{
    version = XDocument.Load(stream).Descendants().Single(e => e.Name.LocalName == "version").Value;
}

File.WriteAllText(Path.Combine(workspace, "Directory.Build.props"), "<Project />");
File.WriteAllText(Path.Combine(workspace, "Directory.Build.targets"), "<Project />");
File.WriteAllText(Path.Combine(workspace, "Directory.Packages.props"), "<Project />");
File.WriteAllText(Path.Combine(workspace, ".editorconfig"), "root = true");
File.WriteAllText(Path.Combine(workspace, "NuGet.Config"), new XDocument(
    new XElement("configuration",
        new XElement("packageSources",
            new XElement("clear"),
            new XElement("add", new XAttribute("key", "local"), new XAttribute("value", feed)),
            new XElement("add", new XAttribute("key", "dependencies"), new XAttribute("value", args[2]))))).ToString());

string project = Path.Combine(consumer, "Consumer.csproj");
File.WriteAllText(project, $"""
    <Project Sdk="Microsoft.NET.Sdk">
      <PropertyGroup>
        <TargetFramework>netstandard2.0</TargetFramework>
        <LangVersion>14.0</LangVersion>
        <ImplicitUsings>disable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
        <RestoreConfigFile>$(MSBuildThisFileDirectory)..\NuGet.Config</RestoreConfigFile>
      </PropertyGroup>
      <ItemGroup>
        <PackageReference Include="PolyType.Roslyn" Version="{version}" PrivateAssets="all" />
        <PackageReference Include="PolySharp" Version="1.16.0" PrivateAssets="all" />
      </ItemGroup>
    </Project>
    """);
File.WriteAllText(Path.Combine(consumer, "Source.cs"), """
    using Microsoft.CodeAnalysis.CSharp;
    using PolyType.Roslyn;
    using System.Threading;

    internal static class Consumer
    {
        public static TypeDataModelGenerator CreateGenerator()
        {
            var compilation = CSharpCompilation.Create("consumer");
            return new TypeDataModelGenerator(compilation.Assembly, new KnownSymbols(compilation), CancellationToken.None);
        }
    }
    """);

await Run(workspace, "build", project, "-c", configuration, "--nologo", "-v", "quiet");
Console.WriteLine("Source-package compilation smoke test passed.");
Directory.Delete(workspace, recursive: true);

async Task Run(string directory, params string[] arguments)
{
    var startInfo = new ProcessStartInfo("dotnet")
    {
        WorkingDirectory = directory,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
    };
    foreach (string argument in arguments)
    {
        startInfo.ArgumentList.Add(argument);
    }

    if (directory == workspace)
    {
        startInfo.Environment["NUGET_PACKAGES"] = Path.Combine(workspace, "packages");
    }

    using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start dotnet.");
    Task<string> stdout = process.StandardOutput.ReadToEndAsync();
    Task<string> stderr = process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();
    string output = await stdout + await stderr;
    if (process.ExitCode != 0)
    {
        throw new InvalidOperationException(
            $"dotnet {string.Join(' ', arguments)} failed ({process.ExitCode}):{Environment.NewLine}{output}");
    }
}
