using BenchmarkDotNet.Attributes;
using PolyType.Abstractions;
using PolyType.ReflectionProvider;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace PolyType.Benchmarks;

[MemoryDiagnoser]
public partial class MethodInvokeBenchmarks
{
    private static readonly ReflectionTypeShapeProvider EmitProvider = ReflectionTypeShapeProvider.Create(new() { UseReflectionEmit = true });
    private static readonly ReflectionTypeShapeProvider NoEmitProvider = ReflectionTypeShapeProvider.Create(new() { UseReflectionEmit = false });

    private readonly TestClass _instance = new();
    private readonly int[] _testArray = [1, 2, 3, 4, 5, 6];

    // Baseline bespoke wrapper
    private readonly Func<int[], int> _baselineWrapper;

    // MethodInfo reflection wrapper
    private readonly Func<int[], int> _reflectionWrapper;

    // IMethodShape wrappers
    private readonly Func<int[], int> _methodShapeEmitWrapper;
    private readonly Func<int[], int> _methodShapeNoEmitWrapper;
    private readonly Func<int[], int> _methodShapeSourceGenWrapper;

    public MethodInvokeBenchmarks()
    {
        // Initialize baseline bespoke wrapper
        _baselineWrapper = CreateBaselineWrapper();

        // Initialize MethodInfo reflection wrapper
        _reflectionWrapper = CreateReflectionWrapper();

        // Initialize IMethodShape wrappers
        _methodShapeEmitWrapper = CreateMethodShapeWrapper(EmitProvider.GetShape<TestClass>());
        _methodShapeNoEmitWrapper = CreateMethodShapeWrapper(NoEmitProvider.GetShape<TestClass>());
        _methodShapeSourceGenWrapper = CreateMethodShapeWrapper(TypeShapeProvider.Resolve<TestClass>());
    }

    [Benchmark(Baseline = true)]
    public int Baseline() => _baselineWrapper(_testArray);

    [Benchmark]
    public int MethodInfoReflection() => _reflectionWrapper(_testArray);

    [Benchmark]
    public int MethodShapeReflectionEmit() => _methodShapeEmitWrapper(_testArray);

    [Benchmark]
    public int MethodShapeReflection() => _methodShapeNoEmitWrapper(_testArray);

    [Benchmark]
    public int MethodShapeSourceGen() => _methodShapeSourceGenWrapper(_testArray);

    private Func<int[], int> CreateBaselineWrapper()
    {
        return args => _instance.Sum6Integers(args[0], args[1], args[2], args[3], args[4], args[5]);
    }

    private Func<int[], int> CreateReflectionWrapper()
    {
        var methodInfo = typeof(TestClass).GetMethod(nameof(TestClass.Sum6Integers))!;
        
        return args =>
        {
            object[] objArgs = new object[6];
            for (int i = 0; i < args.Length; i++)
            {
                objArgs[i] = args[i];
            }
            return (int)methodInfo.Invoke(_instance, objArgs)!;
        };
    }

    private Func<int[], int> CreateMethodShapeWrapper(ITypeShape<TestClass> typeShape)
    {
        var methodShape = typeShape.Methods.First(m => m.Name == nameof(TestClass.Sum6Integers));
        return (Func<int[], int>)methodShape.Accept(new MethodInvokerBuilder(), _instance)!;
    }

    [GenerateShape, TypeShape(IncludeMethods = MethodShapeFlags.PublicInstance)]
    public partial class TestClass
    {
        public int Sum6Integers(int a, int b, int c, int d, int e, int f)
        {
            return a + b + c + d + e + f;
        }
    }

    private sealed class MethodInvokerBuilder : TypeShapeVisitor
    {
        public override object? VisitMethod<TDeclaringType, TArgumentState, TResult>(
            IMethodShape<TDeclaringType, TArgumentState, TResult> methodShape, object? state)
        {
            if (methodShape.Parameters.Count != 6)
                throw new InvalidOperationException("Expected method with 6 parameters");

            StrongBox<TDeclaringType?> instance = new((TDeclaringType?)state);
            var parameterSetters = new Setter<TArgumentState, int>[6];
            
            for (int i = 0; i < 6; i++)
            {
                var parameter = (IParameterShape<TArgumentState, int>)methodShape.Parameters[i];
                parameterSetters[i] = parameter.GetSetter();
            }

            var argumentStateCtor = methodShape.GetArgumentStateConstructor();
            var methodInvoker = methodShape.GetMethodInvoker();

            return new Func<int[], TResult>(args =>
            {
                var argumentState = argumentStateCtor();
                for (int i = 0; i < 6; i++)
                {
                    parameterSetters[i](ref argumentState, args[i]);
                }

                return methodInvoker(ref instance.Value, ref argumentState).Result;
            });
        }
    }
}