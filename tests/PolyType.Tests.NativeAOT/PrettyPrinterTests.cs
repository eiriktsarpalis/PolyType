using TUnit.Core;

namespace PolyType.Tests.NativeAOT;

/// <summary>
/// Tests for pretty printing in Native AOT.
/// </summary>
public class PrettyPrinterTests
{
    [Test]
    public void CanPrettyPrintSimpleData()
    {
        var data = TestDataFactory.CreateSimpleData();
        var prettyString = PolyType.Examples.PrettyPrinter.PrettyPrinter.Print(data);

        Assert.That(prettyString).IsNotNullOrWhiteSpace();
        Assert.That(prettyString).Contains("SimpleTestData");
    }

    [Test]
    public void CanPrettyPrintTodosData()
    {
        var todos = TestDataFactory.CreateSampleTodos();
        var prettyString = PolyType.Examples.PrettyPrinter.PrettyPrinter.Print(todos);

        Assert.That(prettyString).IsNotNullOrWhiteSpace();
        Assert.That(prettyString).Contains("TestTodos");
    }
}