using PolyType.SourceGenerator.Analyzers;
using Xunit;

namespace PolyType.SourceGenerator.UnitTests.Analyzers;

using VerifyCS = CodeFixVerifier<ArgumentStateReturnAnalyzer, Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

public class ArgumentStateReturnAnalyzerTests
{
    [Fact]
    public async Task WarnsWhenArgumentStateIsNotReturned()
    {
        string source = /* lang=c#-test */ """
            using PolyType.Abstractions;

            class Usage
            {
                object Create<TState>(Constructor<TState, object> createObject, ref TState state)
                    where TState : IArgumentState
                    => createObject(ref {|PT0033:state|});
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task DoesNotWarnWhenArgumentStateIsReturnedInFinally()
    {
        string source = /* lang=c#-test */ """
            using PolyType.Abstractions;

            class Usage
            {
                object Create<TState>(Constructor<TState, object> createObject, ref TState state)
                    where TState : IArgumentState
                {
                    try
                    {
                        return createObject(ref state);
                    }
                    finally
                    {
                        state.Return();
                    }
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task DoesNotWarnWhenArgumentStateIsReturnedAfterSuccessfulConstruction()
    {
        string source = /* lang=c#-test */ """
            using PolyType.Abstractions;

            class Usage
            {
                object Create<TState>(Constructor<TState, object> createObject, ref TState state)
                    where TState : IArgumentState
                {
                    object result = createObject(ref state);
                    state.Return();
                    return result;
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task WarnsWhenArgumentStateIsUsedAfterReturn()
    {
        string source = /* lang=c#-test */ """
            using PolyType.Abstractions;

            class Usage
            {
                object Create<TState>(Constructor<TState, object> createObject, ref TState state)
                    where TState : IArgumentState
                {
                    object result = createObject(ref {|PT0033:state|});
                    state.Return();
                    Use(state);
                    return result;
                }

                void Use<TState>(TState state) { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task WarnsWhenArgumentStateIsUsedAfterFinallyReturnsIt()
    {
        string source = /* lang=c#-test */ """
            using PolyType.Abstractions;

            class Usage
            {
                void Create<TState>(Constructor<TState, object> createObject, ref TState state)
                    where TState : IArgumentState
                {
                    try
                    {
                        createObject(ref {|PT0033:state|});
                    }
                    finally
                    {
                        state.Return();
                    }

                    Use(state);
                }

                void Use<TState>(TState state) { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task WarnsWhenDifferentArgumentStateIsReturned()
    {
        string source = /* lang=c#-test */ """
            using PolyType.Abstractions;

            class Usage
            {
                object Create<TState>(Constructor<TState, object> createObject, ref TState state, ref TState other)
                    where TState : IArgumentState
                {
                    try
                    {
                        return createObject(ref {|PT0033:state|});
                    }
                    finally
                    {
                        other.Return();
                    }
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(source);
    }
}
