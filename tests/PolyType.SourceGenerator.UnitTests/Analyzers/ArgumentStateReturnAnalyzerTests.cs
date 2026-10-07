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

    [Fact]
    public async Task DoesNotWarnForDynamicInvocation()
    {
        string source = /* lang=c#-test */ """
            using PolyType.Abstractions;

            class Usage
            {
                void Create<TState>(dynamic createObject, ref TState state)
                    where TState : IArgumentState
                    => createObject(ref state);
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task WarnsForConcreteArgumentStatePassedByParenthesizedReference()
    {
        string source = /* lang=c#-test */ """
            using PolyType.Abstractions;

            class State : IArgumentState
            {
                public int Count => 0;
                public bool AreRequiredArgumentsSet => true;
                public bool IsArgumentSet(int index) => true;
                public void Return() { }
            }

            class Usage
            {
                object Create(Constructor<State, object> createObject, ref State state)
                {
                    State local = state;
                    return createObject(ref ({|PT0033:local|}));
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task DoesNotWarnForAConstructorWithNonArgumentStateType()
    {
        string source = /* lang=c#-test */ """
            using PolyType.Abstractions;

            class Usage
            {
                object Create(Constructor<int[], object> createObject, ref int[] state)
                    => createObject(ref state);
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task WarnsForArgumentStateFieldPassedByReference()
    {
        string source = /* lang=c#-test */ """
            using PolyType.Abstractions;

            class State : IArgumentState
            {
                public int Count => 0;
                public bool AreRequiredArgumentsSet => true;
                public bool IsArgumentSet(int index) => true;
                public void Return() { }
            }

            class Usage
            {
                private State state = new();

                object Create(Constructor<State, object> createObject)
                    => createObject(ref {|PT0033:state|});
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task WarnsForArgumentStateInheritedThroughGenericConstraint()
    {
        string source = /* lang=c#-test */ """
            using PolyType.Abstractions;

            class Usage
            {
                object Create<TState, TBase>(Constructor<TState, object> createObject, ref TState state)
                    where TState : TBase
                    where TBase : IArgumentState
                    => createObject(ref {|PT0033:state|});
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(source);
    }

}
