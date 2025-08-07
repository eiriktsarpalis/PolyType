# PolyType - .NET Generic Programming Library

PolyType is a practical generic programming library for .NET that facilitates rapid development of feature-complete, high-performance libraries like serializers, validators, parsers, and mappers. It includes a built-in source generator for Native AOT support.

**Always reference these instructions first and fallback to search or bash commands only when you encounter unexpected information that does not match the info here.**

## Working Effectively

### Prerequisites and Setup

**CRITICAL: Install .NET 9 SDK first** - this repository requires .NET 9.0.300+ as specified in global.json:
```bash
wget https://dotnet.microsoft.com/download/dotnet/scripts/v1/dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 9.0
./dotnet-install.sh --channel 9.0 --runtime dotnet  # Install runtime too
export PATH="/home/runner/.dotnet:$PATH"
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

**NEVER CANCEL builds or tests - build times are normal and expected:**

#### Bootstrap, Build, and Test (Complete Workflow)
```bash
export PATH="/home/runner/.dotnet:$PATH"
time make restore   # ~60 seconds - NEVER CANCEL, includes tool restore
time make build     # ~75 seconds - NEVER CANCEL, Release configuration  
time make test      # ~80 seconds - NEVER CANCEL, runs 49,323+ tests
time make pack      # ~80 seconds - NEVER CANCEL, includes build+test+pack
```

**CRITICAL TIMEOUT SETTINGS:**
- `make restore`: Use 600+ second timeout (10+ minutes)
- `make build`: Use 1800+ second timeout (30+ minutes)  
- `make test`: Use 2400+ second timeout (40+ minutes)
- `make pack`: Use 2400+ second timeout (40+ minutes)
- `make generate-docs`: Use 1800+ second timeout (30+ minutes)

#### Individual Commands
```bash
make clean          # Clean build artifacts
make restore        # Restore NuGet packages and tools (nbgv, docfx)
make build          # Build all projects in Release configuration
make test           # Run complete test suite (all target frameworks)
make pack           # Create NuGet packages
make generate-docs  # Generate API documentation with DocFX
make serve-docs     # Serve documentation locally on port 8080
```

#### Development Commands
```bash
dotnet build --configuration Debug    # Debug builds (faster)
dotnet test --configuration Debug     # Debug test runs
dotnet run --project applications/SerializationApp.Reflection  # Run examples
```

## Validation

### Manual Testing Requirements
**ALWAYS run complete validation scenarios after making changes:**

#### Test Example Applications
```bash
export PATH="/home/runner/.dotnet:$PATH"
export DOTNET_ROOT="/home/runner/.dotnet"

# Test serialization functionality
dotnet applications/SerializationApp.Reflection/bin/Release/net9.0/SerializationApp.Reflection.dll

# Test AOT applications (Native AOT compiled)  
dotnet applications/ValidationApp.AOT/bin/Release/net9.0/ValidationApp.AOT.dll
dotnet applications/ObjectMapper.AOT/bin/Release/net9.0/ObjectMapper.AOT.dll
dotnet applications/RandomGeneratorApp.AOT/bin/Release/net9.0/RandomGeneratorApp.AOT.dll
```

#### Expected Test Results
- **Total tests**: ~49,349 tests
- **Passing**: ~49,323 tests  
- **Skipped**: ~26 tests
- **Duration**: 65-80 seconds
- **Frameworks tested**: .NET 9.0, .NET 8.0, .NET Framework 4.7.2

#### Validation Scenarios
1. **JSON Serialization**: Verify JSON serialization/deserialization works correctly
2. **XML Serialization**: Test XML output generation
3. **CBOR Serialization**: Validate binary serialization
4. **Schema Generation**: Check JSON schema generation
5. **Native AOT**: Ensure AOT applications run without reflection
6. **Source Generator**: Verify code generation produces valid C#

### Pre-commit Validation
**Always run these before committing changes:**
```bash
make build    # NEVER CANCEL - validates compilation
make test     # NEVER CANCEL - ensures no regressions  
```

**No separate linting step required** - StyleCop analyzers run during build with strict warnings-as-errors.

### Quick Validation Commands
```bash
# Check .NET installation
dotnet --version  # Should show 9.0.300+
dotnet --list-runtimes  # Should include Microsoft.NETCore.App 9.0.x

# Verify tools are available
dotnet tool list  # Should show nbgv and docfx

# Quick build check (faster than full make build)
dotnet build --configuration Debug

# Run subset of tests (faster)
dotnet test tests/PolyType.Tests/ --framework net9.0 --configuration Debug
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
- `PolyType.slnx`: Solution file (XML-based solution format)
- `Makefile`: Primary build orchestration
- `global.json`: Specifies required .NET SDK version (9.0.300)
- `Directory.Build.props`: Global MSBuild properties
- `Directory.Packages.props`: Central package version management
- `.editorconfig`: Code style settings and analyzer suppressions

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
```

### Working with PolyType Libraries

#### Creating a New Serializer
1. Reference core abstractions: `IShapeable<T>` constraint
2. Implement visitor pattern with `ITypeShapeVisitor` 
3. Handle primitive, object, collection, and enum shapes
4. Add to `PolyType.Examples` for testing

#### Adding Source Generator Features  
1. Work in `src/PolyType.SourceGenerator/`
2. Use Roslyn incremental source generators
3. Test with `PolyType.SourceGenerator.UnitTests`
4. Validate AOT compatibility with sample applications

#### Testing Changes
```bash
# Run specific test project
dotnet test tests/PolyType.Tests/ --configuration Release

# Run benchmarks (use BenchmarkDotNet arguments)
dotnet run --project tests/PolyType.Benchmarks/ --configuration Release -- --list
dotnet run --project tests/PolyType.Benchmarks/ --configuration Release -- --filter '*JsonBenchmark*'

# Test specific framework
dotnet test tests/PolyType.Tests/ --framework net9.0
```

### Documentation
```bash
make generate-docs  # NEVER CANCEL - takes ~53 seconds
make serve-docs     # Local preview on http://localhost:8080
```

Generated documentation includes:
- API reference for all public types
- Code samples from `docs/CSharpSamples/`
- Conceptual documentation from markdown files

## Known Issues and Workarounds

### Build Issues
- **Shallow git clones**: Run `git fetch --unshallow` to fix version calculation
- **Missing mono**: Install with `sudo apt-get install -y mono-devel` for .NET Framework tests
- **Wrong .NET version**: Ensure .NET 9.0.300+ is installed and in PATH

### Test Issues  
- **Some tests may fail without mono**: This is expected for .NET Framework 4.7.2 targets
- **Long test runs**: 49k+ tests take 65-80 seconds - this is normal
- **CI package publishing**: Uses GitHub Packages and Feedz.io, requires authentication

### Expected Behavior
- Build warnings are treated as errors (`-warnAsError`)  
- Some analyzer rules are suppressed via `.editorconfig`
- Version numbers are calculated using Nerdbank.GitVersioning (nbgv tool)

## CI/CD Integration

The repository uses GitHub Actions with:
- Multi-OS testing (Ubuntu, Windows, macOS)
- Multi-configuration (Debug, Release)
- Code coverage reporting to Codecov  
- Automatic package publishing on main branch
- Documentation deployment

**Build matrix runs on 6 combinations** - expect 6x longer CI times than local builds.

## Performance Characteristics

PolyType serializers **outperform System.Text.Json**:
- **Serialization**: ~25% faster, zero allocations
- **Deserialization**: ~100% faster, ~57% fewer allocations  
- Supports more types and edge cases than built-in serializers

Benchmark with: `dotnet run --project tests/PolyType.Benchmarks/ -c Release`