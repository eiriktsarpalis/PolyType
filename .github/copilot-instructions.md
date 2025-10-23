# PolyType - .NET Generic Programming Library

PolyType is a practical generic programming library for .NET that facilitates rapid development of feature-complete, high-performance libraries like serializers, validators, parsers, and mappers. It includes a built-in source generator for Native AOT support.

**CRITICAL: Before and after any change you MUST ensure the solution builds and all tests pass. Always run (in order):**

```bash
dotnet restore
dotnet build
dotnet test
```

Do not commit or push if any step fails. Fix issues immediately.

**Always reference these instructions first and fallback to search or shell commands only when you encounter unexpected information that does not match the info here.**

## Working Effectively

### Prerequisites and Setup

**CRITICAL: Install .NET 10 SDK first** – the repository requires .NET 10 SDK as specified in `global.json`:

Linux/macOS (script-based install):
```bash
wget https://dotnet.microsoft.com/download/dotnet/scripts/v1/dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 10.0
./dotnet-install.sh --channel 9.0 --runtime dotnet  # Also install the .NET 9 runtime.
export PATH="/home/runner/.dotnet:$PATH"
```

Windows:
1. Prefer the official installer (https://dotnet.microsoft.com/download) and select .NET 10 SDK.
2. Or use the same script in a bash shell (Git Bash / WSL) as above.

Validate installation:
```bash
dotnet --version      # Should report 10.0.*
dotnet --list-runtimes
```

**Install mono for .NET Framework testing** (required for complete test suite):
```bash
sudo apt-get update && sudo apt-get install -y mono-devel
```

**Fix git versioning** (required in CI environments):
```bash
git fetch --unshallow  # Fixes version calculation issues
```

### Build and Test Workflow

```bash
dotnet restore
dotnet build
dotnet test
```

#### Packaging (NuGet)
```bash
dotnet pack --configuration Release --no-build
```

#### Documentation (DocFX)
DocFX is installed as a dotnet tool. To generate docs:
```bash
dotnet tool restore
docfx docs/docfx.json  # Generates site into artifacts/_site (default output configured)
```
To serve locally:
```bash
docfx docs/docfx.json --serve --port 8080
```

#### Development (Fast Iteration)
```bash
dotnet build
dotnet test --framework net10.0
```


## Repository Structure

### Key Projects
```
src/
├── PolyType/                    # Core abstractions and reflection provider
├── PolyType.SourceGenerator/    # Built-in source generator for Native AOT
├── PolyType.Roslyn/            # Roslyn utilities for source generation
├── PolyType.Examples/          # Example implementations (JSON, XML, CBOR, etc.)
├── PolyType.TestCases/         # Test case definitions
└── PolyType.TestCases.FSharp/  # F# test cases

tests/
├── PolyType.Tests/             # Main test suite (3 target frameworks)
├── PolyType.Benchmarks/        # Performance benchmarks  
├── PolyType.Roslyn.Tests/      # Roslyn utilities tests
└── PolyType.SourceGenerator.UnitTests/ # Source generator tests

applications/                   # Sample Native AOT applications
├── SerializationApp.AOT/       # AOT serialization demo
├── SerializationApp.Reflection/# Reflection-based demo
├── ValidationApp.AOT/          # AOT validation demo
├── ObjectMapper.AOT/           # AOT object mapping demo
├── ConfigurationBinder.AOT/    # AOT configuration binding
└── RandomGeneratorApp.AOT/     # AOT random value generation
```

### Important Files
* `PolyType.slnx`: Solution file (XML-based solution format)
* `global.json`: Specifies required .NET SDK version
* `Directory.Build.props`: Global MSBuild properties
* `Directory.Packages.props`: Central package version management
* `.editorconfig`: Code style settings and analyzer suppressions

### Build Artifacts
- `artifacts/`: Output directory for packages and documentation  
- `artifacts/_site/`: Generated documentation site (DocFX output)
- `artifacts/testResults/`: Test results and coverage reports

## Common Development Tasks

### Frequently Referenced Files
**Use these file paths instead of searching to save time:**
```
README.md                           # Project overview and getting started
Makefile                           # Build commands and targets
global.json                        # .NET SDK version requirements  
Directory.Build.props              # Global MSBuild settings
Directory.Packages.props           # Package version management
.editorconfig                      # Code style and analyzer settings
src/PolyType/Abstractions/         # Core interfaces (IShapeable, ITypeShape)
src/PolyType.Examples/JsonSerializer/  # High-performance JSON serializer
src/PolyType.Examples/README.md    # Examples documentation
tests/PolyType.Tests/               # Main test suite
applications/*/Program.cs           # Example application entry points
docs/                              # Documentation source files

## CI/CD Integration

The repository uses GitHub Actions with:
- Multi-OS testing (Ubuntu, Windows, macOS)
- Multi-configuration (Debug, Release)
- Code coverage reporting to Codecov  
- Automatic package publishing on main branch
- Documentation deployment