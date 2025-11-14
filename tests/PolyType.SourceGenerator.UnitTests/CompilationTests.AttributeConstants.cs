using Microsoft.CodeAnalysis;
using Xunit;

namespace PolyType.SourceGenerator.UnitTests;

public static partial class CompilationTests
{
    public static class AttributeConstants
    {
        [Fact]
        public static void AttributeWithBooleanConstants_NoErrors()
        {
            Compilation compilation = CompilationHelpers.CreateCompilation("""
                using PolyType;
                using System;

                [AttributeUsage(AttributeTargets.Class)]
                public class TestAttribute : Attribute
                {
                    public TestAttribute(bool value) => Value = value;
                    public bool Value { get; }
                }

                [GenerateShape]
                [Test(true)]
                public partial class MyClass1 { }

                [GenerateShape]
                [Test(false)]
                public partial class MyClass2 { }
                """);

            PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public static void AttributeWithByteConstants_NoErrors()
        {
            Compilation compilation = CompilationHelpers.CreateCompilation("""
                using PolyType;
                using System;

                [AttributeUsage(AttributeTargets.Class)]
                public class TestAttribute : Attribute
                {
                    public TestAttribute(byte value) => Value = value;
                    public byte Value { get; }
                }

                [GenerateShape]
                [Test(0)]
                public partial class MyClass1 { }

                [GenerateShape]
                [Test(255)]
                public partial class MyClass2 { }

                [GenerateShape]
                [Test(byte.MinValue)]
                public partial class MyClass3 { }

                [GenerateShape]
                [Test(byte.MaxValue)]
                public partial class MyClass4 { }
                """);

            PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public static void AttributeWithSByteConstants_NoErrors()
        {
            Compilation compilation = CompilationHelpers.CreateCompilation("""
                using PolyType;
                using System;

                [AttributeUsage(AttributeTargets.Class)]
                public class TestAttribute : Attribute
                {
                    public TestAttribute(sbyte value) => Value = value;
                    public sbyte Value { get; }
                }

                [GenerateShape]
                [Test(-128)]
                public partial class MyClass1 { }

                [GenerateShape]
                [Test(127)]
                public partial class MyClass2 { }

                [GenerateShape]
                [Test(sbyte.MinValue)]
                public partial class MyClass3 { }

                [GenerateShape]
                [Test(sbyte.MaxValue)]
                public partial class MyClass4 { }
                """);

            PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public static void AttributeWithShortConstants_NoErrors()
        {
            Compilation compilation = CompilationHelpers.CreateCompilation("""
                using PolyType;
                using System;

                [AttributeUsage(AttributeTargets.Class)]
                public class TestAttribute : Attribute
                {
                    public TestAttribute(short value) => Value = value;
                    public short Value { get; }
                }

                [GenerateShape]
                [Test(-32768)]
                public partial class MyClass1 { }

                [GenerateShape]
                [Test(32767)]
                public partial class MyClass2 { }

                [GenerateShape]
                [Test(short.MinValue)]
                public partial class MyClass3 { }

                [GenerateShape]
                [Test(short.MaxValue)]
                public partial class MyClass4 { }
                """);

            PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public static void AttributeWithUShortConstants_NoErrors()
        {
            Compilation compilation = CompilationHelpers.CreateCompilation("""
                using PolyType;
                using System;

                [AttributeUsage(AttributeTargets.Class)]
                public class TestAttribute : Attribute
                {
                    public TestAttribute(ushort value) => Value = value;
                    public ushort Value { get; }
                }

                [GenerateShape]
                [Test(0)]
                public partial class MyClass1 { }

                [GenerateShape]
                [Test(65535)]
                public partial class MyClass2 { }

                [GenerateShape]
                [Test(ushort.MinValue)]
                public partial class MyClass3 { }

                [GenerateShape]
                [Test(ushort.MaxValue)]
                public partial class MyClass4 { }
                """);

            PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public static void AttributeWithIntConstants_NoErrors()
        {
            Compilation compilation = CompilationHelpers.CreateCompilation("""
                using PolyType;
                using System;

                [AttributeUsage(AttributeTargets.Class)]
                public class TestAttribute : Attribute
                {
                    public TestAttribute(int value) => Value = value;
                    public int Value { get; }
                }

                [GenerateShape]
                [Test(-2147483648)]
                public partial class MyClass1 { }

                [GenerateShape]
                [Test(2147483647)]
                public partial class MyClass2 { }

                [GenerateShape]
                [Test(int.MinValue)]
                public partial class MyClass3 { }

                [GenerateShape]
                [Test(int.MaxValue)]
                public partial class MyClass4 { }

                [GenerateShape]
                [Test(0)]
                public partial class MyClass5 { }

                [GenerateShape]
                [Test(42)]
                public partial class MyClass6 { }
                """);

            PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public static void AttributeWithUIntConstants_NoErrors()
        {
            Compilation compilation = CompilationHelpers.CreateCompilation("""
                using PolyType;
                using System;

                [AttributeUsage(AttributeTargets.Class)]
                public class TestAttribute : Attribute
                {
                    public TestAttribute(uint value) => Value = value;
                    public uint Value { get; }
                }

                [GenerateShape]
                [Test(0u)]
                public partial class MyClass1 { }

                [GenerateShape]
                [Test(4294967295u)]
                public partial class MyClass2 { }

                [GenerateShape]
                [Test(uint.MinValue)]
                public partial class MyClass3 { }

                [GenerateShape]
                [Test(uint.MaxValue)]
                public partial class MyClass4 { }
                """);

            PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public static void AttributeWithLongConstants_NoErrors()
        {
            Compilation compilation = CompilationHelpers.CreateCompilation("""
                using PolyType;
                using System;

                [AttributeUsage(AttributeTargets.Class)]
                public class TestAttribute : Attribute
                {
                    public TestAttribute(long value) => Value = value;
                    public long Value { get; }
                }

                [GenerateShape]
                [Test(-9223372036854775808L)]
                public partial class MyClass1 { }

                [GenerateShape]
                [Test(9223372036854775807L)]
                public partial class MyClass2 { }

                [GenerateShape]
                [Test(long.MinValue)]
                public partial class MyClass3 { }

                [GenerateShape]
                [Test(long.MaxValue)]
                public partial class MyClass4 { }
                """);

            PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public static void AttributeWithULongConstants_NoErrors()
        {
            Compilation compilation = CompilationHelpers.CreateCompilation("""
                using PolyType;
                using System;

                [AttributeUsage(AttributeTargets.Class)]
                public class TestAttribute : Attribute
                {
                    public TestAttribute(ulong value) => Value = value;
                    public ulong Value { get; }
                }

                [GenerateShape]
                [Test(0UL)]
                public partial class MyClass1 { }

                [GenerateShape]
                [Test(18446744073709551615UL)]
                public partial class MyClass2 { }

                [GenerateShape]
                [Test(ulong.MinValue)]
                public partial class MyClass3 { }

                [GenerateShape]
                [Test(ulong.MaxValue)]
                public partial class MyClass4 { }
                """);

            PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public static void AttributeWithCharConstants_NoErrors()
        {
            Compilation compilation = CompilationHelpers.CreateCompilation("""
                using PolyType;
                using System;

                [AttributeUsage(AttributeTargets.Class)]
                public class TestAttribute : Attribute
                {
                    public TestAttribute(char value) => Value = value;
                    public char Value { get; }
                }

                [GenerateShape]
                [Test('A')]
                public partial class MyClass1 { }

                [GenerateShape]
                [Test('x')]
                public partial class MyClass2 { }

                [GenerateShape]
                [Test('\0')]
                public partial class MyClass3 { }

                [GenerateShape]
                [Test('\n')]
                public partial class MyClass4 { }

                [GenerateShape]
                [Test('\t')]
                public partial class MyClass5 { }

                [GenerateShape]
                [Test('\'')]
                public partial class MyClass6 { }

                [GenerateShape]
                [Test('\\')]
                public partial class MyClass7 { }
                """);

            PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public static void AttributeWithFloatConstants_NoErrors()
        {
            Compilation compilation = CompilationHelpers.CreateCompilation("""
                using PolyType;
                using System;

                [AttributeUsage(AttributeTargets.Class)]
                public class TestAttribute : Attribute
                {
                    public TestAttribute(float value) => Value = value;
                    public float Value { get; }
                }

                [GenerateShape]
                [Test(0.0f)]
                public partial class MyClass1 { }

                [GenerateShape]
                [Test(3.14f)]
                public partial class MyClass2 { }

                [GenerateShape]
                [Test(-3.14f)]
                public partial class MyClass3 { }

                [GenerateShape]
                [Test(1.23456e10f)]
                public partial class MyClass4 { }

                [GenerateShape]
                [Test(-9.87654e-5f)]
                public partial class MyClass5 { }
                """);

            PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public static void AttributeWithDoubleConstants_NoErrors()
        {
            Compilation compilation = CompilationHelpers.CreateCompilation("""
                using PolyType;
                using System;

                [AttributeUsage(AttributeTargets.Class)]
                public class TestAttribute : Attribute
                {
                    public TestAttribute(double value) => Value = value;
                    public double Value { get; }
                }

                [GenerateShape]
                [Test(0.0d)]
                public partial class MyClass1 { }

                [GenerateShape]
                [Test(3.14d)]
                public partial class MyClass2 { }

                [GenerateShape]
                [Test(-3.14d)]
                public partial class MyClass3 { }

                [GenerateShape]
                [Test(1.23456789012345e100d)]
                public partial class MyClass4 { }

                [GenerateShape]
                [Test(-9.87654321098765e-50d)]
                public partial class MyClass5 { }
                """);

            PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public static void AttributeWithStringConstants_NoErrors()
        {
            Compilation compilation = CompilationHelpers.CreateCompilation("""
                using PolyType;
                using System;

                [AttributeUsage(AttributeTargets.Class)]
                public class TestAttribute : Attribute
                {
                    public TestAttribute(string value) => Value = value;
                    public string Value { get; }
                }

                [GenerateShape]
                [Test("hello")]
                public partial class MyClass1 { }

                [GenerateShape]
                [Test("")]
                public partial class MyClass2 { }

                [GenerateShape]
                [Test("\"quotes\"")]
                public partial class MyClass3 { }

                [GenerateShape]
                [Test("line1\nline2")]
                public partial class MyClass4 { }

                [GenerateShape]
                [Test("tab\there")]
                public partial class MyClass5 { }

                [GenerateShape]
                [Test("?????\r\n??")]
                public partial class MyClass6 { }

                [GenerateShape]
                [Test("backslash\\here")]
                public partial class MyClass7 { }
                """);

            PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public static void AttributeWithEnumConstants_NoErrors()
        {
            Compilation compilation = CompilationHelpers.CreateCompilation("""
                using PolyType;
                using System;

                public enum MyEnum { A = 1, B = 2, C = 4, D = 8 }

                [AttributeUsage(AttributeTargets.Class)]
                public class TestAttribute : Attribute
                {
                    public TestAttribute(MyEnum value) => Value = value;
                    public MyEnum Value { get; }
                }

                [GenerateShape]
                [Test(MyEnum.A)]
                public partial class MyClass1 { }

                [GenerateShape]
                [Test(MyEnum.B)]
                public partial class MyClass2 { }

                [GenerateShape]
                [Test(MyEnum.A | MyEnum.C)]
                public partial class MyClass3 { }

                [GenerateShape]
                [Test((MyEnum)0)]
                public partial class MyClass4 { }
                """);

            PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public static void AttributeWithFlagsEnumConstants_NoErrors()
        {
            Compilation compilation = CompilationHelpers.CreateCompilation("""
                using PolyType;
                using System;

                [Flags]
                public enum FileAccess { None = 0, Read = 1, Write = 2, Execute = 4, All = 7 }

                [AttributeUsage(AttributeTargets.Class)]
                public class TestAttribute : Attribute
                {
                    public TestAttribute(FileAccess value) => Value = value;
                    public FileAccess Value { get; }
                }

                [GenerateShape]
                [Test(FileAccess.None)]
                public partial class MyClass1 { }

                [GenerateShape]
                [Test(FileAccess.Read)]
                public partial class MyClass2 { }

                [GenerateShape]
                [Test(FileAccess.Read | FileAccess.Write)]
                public partial class MyClass3 { }

                [GenerateShape]
                [Test(FileAccess.Read | FileAccess.Write | FileAccess.Execute)]
                public partial class MyClass4 { }

                [GenerateShape]
                [Test(FileAccess.All)]
                public partial class MyClass5 { }

                [GenerateShape]
                [Test((FileAccess)15)]
                public partial class MyClass6 { }
                """);

            PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public static void AttributeWithEnumNegativeConstants_NoErrors()
        {
            Compilation compilation = CompilationHelpers.CreateCompilation("""
                using PolyType;
                using System;

                public enum SignedEnum { NegativeTwo = -2, NegativeOne = -1, Zero = 0, One = 1, Two = 2 }

                [AttributeUsage(AttributeTargets.Class)]
                public class TestAttribute : Attribute
                {
                    public TestAttribute(SignedEnum value) => Value = value;
                    public SignedEnum Value { get; }
                }

                [GenerateShape]
                [Test(SignedEnum.NegativeTwo)]
                public partial class MyClass1 { }

                [GenerateShape]
                [Test(SignedEnum.NegativeOne)]
                public partial class MyClass2 { }

                [GenerateShape]
                [Test(SignedEnum.Zero)]
                public partial class MyClass3 { }

                [GenerateShape]
                [Test((SignedEnum)(-100))]
                public partial class MyClass4 { }

                [GenerateShape]
                [Test((SignedEnum)(-5))]
                public partial class MyClass5 { }
                """);

            PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public static void AttributeWithByteEnumConstants_NoErrors()
        {
            Compilation compilation = CompilationHelpers.CreateCompilation("""
                using PolyType;
                using System;

                public enum ByteEnum : byte { Min = 0, Low = 10, Mid = 127, High = 200, Max = 255 }

                [AttributeUsage(AttributeTargets.Class)]
                public class TestAttribute : Attribute
                {
                    public TestAttribute(ByteEnum value) => Value = value;
                    public ByteEnum Value { get; }
                }

                [GenerateShape]
                [Test(ByteEnum.Min)]
                public partial class MyClass1 { }

                [GenerateShape]
                [Test(ByteEnum.Max)]
                public partial class MyClass2 { }

                [GenerateShape]
                [Test((ByteEnum)50)]
                public partial class MyClass3 { }

                [GenerateShape]
                [Test((ByteEnum)128)]
                public partial class MyClass4 { }
                """);

            PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public static void AttributeWithSByteEnumConstants_NoErrors()
        {
            Compilation compilation = CompilationHelpers.CreateCompilation("""
                using PolyType;
                using System;

                public enum SByteEnum : sbyte { Min = -128, Negative = -50, Zero = 0, Positive = 50, Max = 127 }

                [AttributeUsage(AttributeTargets.Class)]
                public class TestAttribute : Attribute
                {
                    public TestAttribute(SByteEnum value) => Value = value;
                    public SByteEnum Value { get; }
                }

                [GenerateShape]
                [Test(SByteEnum.Min)]
                public partial class MyClass1 { }

                [GenerateShape]
                [Test(SByteEnum.Negative)]
                public partial class MyClass2 { }

                [GenerateShape]
                [Test(SByteEnum.Max)]
                public partial class MyClass3 { }

                [GenerateShape]
                [Test((SByteEnum)(-100))]
                public partial class MyClass4 { }

                [GenerateShape]
                [Test((SByteEnum)(-1))]
                public partial class MyClass5 { }
                """);

            PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public static void AttributeWithShortEnumConstants_NoErrors()
        {
            Compilation compilation = CompilationHelpers.CreateCompilation("""
                using PolyType;
                using System;

                public enum ShortEnum : short { Min = -32768, Negative = -1000, Zero = 0, Positive = 1000, Max = 32767 }

                [AttributeUsage(AttributeTargets.Class)]
                public class TestAttribute : Attribute
                {
                    public TestAttribute(ShortEnum value) => Value = value;
                    public ShortEnum Value { get; }
                }

                [GenerateShape]
                [Test(ShortEnum.Min)]
                public partial class MyClass1 { }

                [GenerateShape]
                [Test(ShortEnum.Negative)]
                public partial class MyClass2 { }

                [GenerateShape]
                [Test(ShortEnum.Max)]
                public partial class MyClass3 { }

                [GenerateShape]
                [Test((ShortEnum)(-15000))]
                public partial class MyClass4 { }
                """);

            PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public static void AttributeWithUShortEnumConstants_NoErrors()
        {
            Compilation compilation = CompilationHelpers.CreateCompilation("""
                using PolyType;
                using System;

                public enum UShortEnum : ushort { Min = 0, Low = 1000, Mid = 32767, High = 50000, Max = 65535 }

                [AttributeUsage(AttributeTargets.Class)]
                public class TestAttribute : Attribute
                {
                    public TestAttribute(UShortEnum value) => Value = value;
                    public UShortEnum Value { get; }
                }

                [GenerateShape]
                [Test(UShortEnum.Min)]
                public partial class MyClass1 { }

                [GenerateShape]
                [Test(UShortEnum.Max)]
                public partial class MyClass2 { }

                [GenerateShape]
                [Test((UShortEnum)40000)]
                public partial class MyClass3 { }
                """);

            PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public static void AttributeWithLongEnumConstants_NoErrors()
        {
            Compilation compilation = CompilationHelpers.CreateCompilation("""
                using PolyType;
                using System;

                public enum LongEnum : long { Min = long.MinValue, Negative = -1000000000000L, Zero = 0, Positive = 1000000000000L, Max = long.MaxValue }

                [AttributeUsage(AttributeTargets.Class)]
                public class TestAttribute : Attribute
                {
                    public TestAttribute(LongEnum value) => Value = value;
                    public LongEnum Value { get; }
                }

                [GenerateShape]
                [Test(LongEnum.Min)]
                public partial class MyClass1 { }

                [GenerateShape]
                [Test(LongEnum.Negative)]
                public partial class MyClass2 { }

                [GenerateShape]
                [Test(LongEnum.Max)]
                public partial class MyClass3 { }

                [GenerateShape]
                [Test((LongEnum)(-5000000000L))]
                public partial class MyClass4 { }
                """);

            PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public static void AttributeWithULongEnumConstants_NoErrors()
        {
            Compilation compilation = CompilationHelpers.CreateCompilation("""
                using PolyType;
                using System;

                public enum ULongEnum : ulong { Min = 0, Low = 1000UL, Mid = 9223372036854775807UL, High = 15000000000000000000UL, Max = ulong.MaxValue }

                [AttributeUsage(AttributeTargets.Class)]
                public class TestAttribute : Attribute
                {
                    public TestAttribute(ULongEnum value) => Value = value;
                    public ULongEnum Value { get; }
                }

                [GenerateShape]
                [Test(ULongEnum.Min)]
                public partial class MyClass1 { }

                [GenerateShape]
                [Test(ULongEnum.Max)]
                public partial class MyClass2 { }

                [GenerateShape]
                [Test((ULongEnum)10000000000000000000UL)]
                public partial class MyClass3 { }
                """);

            PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public static void AttributeWithUIntEnumConstants_NoErrors()
        {
            Compilation compilation = CompilationHelpers.CreateCompilation("""
                using PolyType;
                using System;

                public enum UIntEnum : uint { Min = 0u, Low = 1000u, Mid = 2147483647u, High = 3000000000u, Max = uint.MaxValue }

                [AttributeUsage(AttributeTargets.Class)]
                public class TestAttribute : Attribute
                {
                    public TestAttribute(UIntEnum value) => Value = value;
                    public UIntEnum Value { get; }
                }

                [GenerateShape]
                [Test(UIntEnum.Min)]
                public partial class MyClass1 { }

                [GenerateShape]
                [Test(UIntEnum.Max)]
                public partial class MyClass2 { }

                [GenerateShape]
                [Test((UIntEnum)3500000000u)]
                public partial class MyClass3 { }
                """);

            PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public static void AttributeWithComplexFlagsEnumCombinations_NoErrors()
        {
            Compilation compilation = CompilationHelpers.CreateCompilation("""
                using PolyType;
                using System;

                [Flags]
                public enum Permissions { None = 0, Read = 1, Write = 2, Execute = 4, Delete = 8, ReadWrite = Read | Write, All = Read | Write | Execute | Delete }

                [AttributeUsage(AttributeTargets.Class)]
                public class TestAttribute : Attribute
                {
                    public TestAttribute(Permissions value) => Value = value;
                    public Permissions Value { get; }
                }

                [GenerateShape]
                [Test(Permissions.None)]
                public partial class MyClass1 { }

                [GenerateShape]
                [Test(Permissions.ReadWrite)]
                public partial class MyClass2 { }

                [GenerateShape]
                [Test(Permissions.All)]
                public partial class MyClass3 { }

                [GenerateShape]
                [Test(Permissions.Read | Permissions.Execute)]
                public partial class MyClass4 { }

                [GenerateShape]
                [Test(Permissions.Write | Permissions.Delete)]
                public partial class MyClass5 { }

                [GenerateShape]
                [Test((Permissions)16)]
                public partial class MyClass6 { }

                [GenerateShape]
                [Test((Permissions)31)]
                public partial class MyClass7 { }
                """);

            PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public static void AttributeWithEmptyEnumConstants_NoErrors()
        {
            Compilation compilation = CompilationHelpers.CreateCompilation("""
                using PolyType;
                using System;

                public enum EmptyEnum { }

                [AttributeUsage(AttributeTargets.Class)]
                public class TestAttribute : Attribute
                {
                    public TestAttribute(EmptyEnum value) => Value = value;
                    public EmptyEnum Value { get; }
                }

                [GenerateShape]
                [Test((EmptyEnum)0)]
                public partial class MyClass1 { }

                [GenerateShape]
                [Test((EmptyEnum)1)]
                public partial class MyClass2 { }

                [GenerateShape]
                [Test((EmptyEnum)(-1))]
                public partial class MyClass3 { }
                """);

            PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public static void AttributeWithEnumArrayConstants_NoErrors()
        {
            Compilation compilation = CompilationHelpers.CreateCompilation("""
                using PolyType;
                using System;

                public enum Status { Pending = 0, Active = 1, Completed = 2, Failed = -1 }

                [AttributeUsage(AttributeTargets.Class)]
                public class TestAttribute : Attribute
                {
                    public TestAttribute(Status[] values) => Values = values;
                    public Status[] Values { get; }
                }

                [GenerateShape]
                [Test(new Status[] { Status.Pending, Status.Active })]
                public partial class MyClass1 { }

                [GenerateShape]
                [Test(new Status[] { Status.Failed, Status.Completed })]
                public partial class MyClass2 { }

                [GenerateShape]
                [Test(new[] { Status.Active })]
                public partial class MyClass3 { }

                [GenerateShape]
                [Test(new Status[] { (Status)10, (Status)(-5) })]
                public partial class MyClass4 { }
                """);

            PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public static void AttributeWithEnumNamedArguments_NoErrors()
        {
            Compilation compilation = CompilationHelpers.CreateCompilation("""
                using PolyType;
                using System;

                [Flags]
                public enum Options { None = 0, OptionA = 1, OptionB = 2, OptionC = 4 }

                [AttributeUsage(AttributeTargets.Class)]
                public class TestAttribute : Attribute
                {
                    public Options Primary { get; set; }
                    public Options Secondary { get; set; }
                }

                [GenerateShape]
                [Test(Primary = Options.OptionA)]
                public partial class MyClass1 { }

                [GenerateShape]
                [Test(Primary = Options.OptionA | Options.OptionB, Secondary = Options.OptionC)]
                public partial class MyClass2 { }

                [GenerateShape]
                [Test(Primary = (Options)8, Secondary = (Options)(-1))]
                public partial class MyClass3 { }
                """);

            PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public static void AttributeWithTypeConstants_NoErrors()
        {
            Compilation compilation = CompilationHelpers.CreateCompilation("""
                using PolyType;
                using System;

                [AttributeUsage(AttributeTargets.Class)]
                public class TestAttribute : Attribute
                {
                    public TestAttribute(Type value) => Value = value;
                    public Type Value { get; }
                }

                [GenerateShape]
                [Test(typeof(int))]
                public partial class MyClass1 { }

                [GenerateShape]
                [Test(typeof(string))]
                public partial class MyClass2 { }

                [GenerateShape]
                [Test(typeof(System.Collections.Generic.List<int>))]
                public partial class MyClass3 { }

                [GenerateShape]
                [Test(typeof(int[]))]
                public partial class MyClass4 { }

                [GenerateShape]
                [Test(typeof(int?))]
                public partial class MyClass5 { }
                """);

            PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public static void AttributeWithArrayConstants_NoErrors()
        {
            Compilation compilation = CompilationHelpers.CreateCompilation("""
                using PolyType;
                using System;

                [AttributeUsage(AttributeTargets.Class)]
                public class TestAttribute : Attribute
                {
                    public TestAttribute(int[] values) => Values = values;
                    public int[] Values { get; }
                }

                [GenerateShape]
                [Test(new int[] { 1, 2, 3 })]
                public partial class MyClass1 { }

                [GenerateShape]
                [Test(new int[] { })]
                public partial class MyClass2 { }

                [GenerateShape]
                [Test(new[] { 42 })]
                public partial class MyClass3 { }
                """);

            PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public static void AttributeWithStringArrayConstants_NoErrors()
        {
            Compilation compilation = CompilationHelpers.CreateCompilation("""
                using PolyType;
                using System;

                [AttributeUsage(AttributeTargets.Class)]
                public class TestAttribute : Attribute
                {
                    public TestAttribute(string[] values) => Values = values;
                    public string[] Values { get; }
                }

                [GenerateShape]
                [Test(new string[] { "a", "b", "c" })]
                public partial class MyClass1 { }

                [GenerateShape]
                [Test(new string[] { })]
                public partial class MyClass2 { }

                [GenerateShape]
                [Test(new[] { "single" })]
                public partial class MyClass3 { }
                """);

            PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public static void AttributeWithTypeArrayConstants_NoErrors()
        {
            Compilation compilation = CompilationHelpers.CreateCompilation("""
                using PolyType;
                using System;

                [AttributeUsage(AttributeTargets.Class)]
                public class TestAttribute : Attribute
                {
                    public TestAttribute(Type[] types) => Types = types;
                    public Type[] Types { get; }
                }

                [GenerateShape]
                [Test(new Type[] { typeof(int), typeof(string) })]
                public partial class MyClass1 { }

                [GenerateShape]
                [Test(new Type[] { })]
                public partial class MyClass2 { }

                [GenerateShape]
                [Test(new[] { typeof(double) })]
                public partial class MyClass3 { }
                """);

            PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public static void AttributeWithNullConstants_NoErrors()
        {
            Compilation compilation = CompilationHelpers.CreateCompilation("""
                using PolyType;
                using System;

                [AttributeUsage(AttributeTargets.Class)]
                public class TestAttribute : Attribute
                {
                    public TestAttribute(string? value) => Value = value;
                    public string? Value { get; }
                }

                [GenerateShape]
                [Test(null)]
                public partial class MyClass1 { }

                [GenerateShape]
                [Test("not null")]
                public partial class MyClass2 { }
                """);

            PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public static void AttributeWithDefaultValues_NoErrors()
        {
            Compilation compilation = CompilationHelpers.CreateCompilation("""
                using PolyType;
                using System;

                [AttributeUsage(AttributeTargets.Class)]
                public class TestAttribute : Attribute
                {
                    public TestAttribute(int value = 42) => Value = value;
                    public int Value { get; }
                    public string Name { get; set; } = "default";
                }

                [GenerateShape]
                [Test]
                public partial class MyClass1 { }

                [GenerateShape]
                [Test(100)]
                public partial class MyClass2 { }

                [GenerateShape]
                [Test(Name = "custom")]
                public partial class MyClass3 { }

                [GenerateShape]
                [Test(200, Name = "both")]
                public partial class MyClass4 { }
                """);

            PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public static void AttributeWithMultipleParameters_NoErrors()
        {
            Compilation compilation = CompilationHelpers.CreateCompilation("""
                using PolyType;
                using System;

                [AttributeUsage(AttributeTargets.Class)]
                public class TestAttribute : Attribute
                {
                    public TestAttribute(string name, int value, bool flag) 
                    {
                        Name = name;
                        Value = value;
                        Flag = flag;
                    }
                    public string Name { get; }
                    public int Value { get; }
                    public bool Flag { get; }
                }

                [GenerateShape]
                [Test("test", 42, true)]
                public partial class MyClass1 { }

                [GenerateShape]
                [Test("", 0, false)]
                public partial class MyClass2 { }
                """);

            PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public static void AttributeWithNamedArguments_NoErrors()
        {
            Compilation compilation = CompilationHelpers.CreateCompilation("""
                using PolyType;
                using System;

                [AttributeUsage(AttributeTargets.Class)]
                public class TestAttribute : Attribute
                {
                    public string? Name { get; set; }
                    public int Value { get; set; }
                    public Type? Type { get; set; }
                    public string[]? Tags { get; set; }
                }

                [GenerateShape]
                [Test(Name = "test1", Value = 42)]
                public partial class MyClass1 { }

                [GenerateShape]
                [Test(Type = typeof(int), Tags = new[] { "a", "b" })]
                public partial class MyClass2 { }

                [GenerateShape]
                [Test(Name = "test3", Value = 100, Type = typeof(string), Tags = new[] { "x" })]
                public partial class MyClass3 { }
                """);

            PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public static void AttributeWithComplexConstants_NoErrors()
        {
            Compilation compilation = CompilationHelpers.CreateCompilation("""
                using PolyType;
                using System;

                public enum Category { None, Primary, Secondary }

                [AttributeUsage(AttributeTargets.Class)]
                public class TestAttribute : Attribute
                {
                    public TestAttribute(
                        string name,
                        int count,
                        Type type,
                        Category category,
                        string[] tags)
                    {
                        Name = name;
                        Count = count;
                        Type = type;
                        Category = category;
                        Tags = tags;
                    }
                    
                    public string Name { get; }
                    public int Count { get; }
                    public Type Type { get; }
                    public Category Category { get; }
                    public string[] Tags { get; }
                    public bool IsActive { get; set; }
                    public double Score { get; set; }
                }

                [GenerateShape]
                [Test("complex", 42, typeof(int), Category.Primary, new[] { "tag1", "tag2" }, IsActive = true, Score = 3.14)]
                public partial class MyClass1 { }

                [GenerateShape]
                [Test("simple", 0, typeof(object), Category.None, new string[] { }, IsActive = false)]
                public partial class MyClass2 { }
                """);

            PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public static void AttributeWithGenericTypeConstants_NoErrors()
        {
            Compilation compilation = CompilationHelpers.CreateCompilation("""
                using PolyType;
                using System;
                using System.Collections.Generic;

                [AttributeUsage(AttributeTargets.Class)]
                public class TestAttribute : Attribute
                {
                    public TestAttribute(Type type) => Type = type;
                    public Type Type { get; }
                }

                [GenerateShape]
                [Test(typeof(List<int>))]
                public partial class MyClass1 { }

                [GenerateShape]
                [Test(typeof(Dictionary<string, int>))]
                public partial class MyClass2 { }

                [GenerateShape]
                [Test(typeof(List<List<string>>))]
                public partial class MyClass3 { }

                [GenerateShape]
                [Test(typeof(ValueTuple<int, string>))]
                public partial class MyClass4 { }
                """);

            PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public static void AttributeWithNestedTypeConstants_NoErrors()
        {
            Compilation compilation = CompilationHelpers.CreateCompilation("""
                using PolyType;
                using System;

                public class Container
                {
                    public class Nested
                    {
                        public class DeepNested { }
                    }
                }

                [AttributeUsage(AttributeTargets.Class)]
                public class TestAttribute : Attribute
                {
                    public TestAttribute(Type type) => Type = type;
                    public Type Type { get; }
                }

                [GenerateShape]
                [Test(typeof(Container))]
                public partial class MyClass1 { }

                [GenerateShape]
                [Test(typeof(Container.Nested))]
                public partial class MyClass2 { }

                [GenerateShape]
                [Test(typeof(Container.Nested.DeepNested))]
                public partial class MyClass3 { }
                """);

            PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public static void AttributeWithObjectConstants_NoErrors()
        {
            Compilation compilation = CompilationHelpers.CreateCompilation("""
                using PolyType;
                using System;

                [AttributeUsage(AttributeTargets.Class)]
                public class TestAttribute : Attribute
                {
                    public TestAttribute(object value) => Value = value;
                    public object Value { get; }
                }

                [GenerateShape]
                [Test(42)]
                public partial class MyClass1 { }

                [GenerateShape]
                [Test("string")]
                public partial class MyClass2 { }

                [GenerateShape]
                [Test(typeof(int))]
                public partial class MyClass3 { }
                """);

            PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public static void AttributeWithPrivateTypeConstants_NoErrors()
        {
            Compilation compilation = CompilationHelpers.CreateCompilation("""
                using PolyType;
                using System;

                [AttributeUsage(AttributeTargets.Class)]
                public class TestAttribute : Attribute
                {
                    public TestAttribute(Type type) => Type = type;
                    public Type Type { get; }
                }

                [GenerateShape]
                [Test(typeof(NestedPrivate))]
                public partial class MyClass2 
                {
                    private class NestedPrivate { }
                }
                """);

            PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public static void MultipleAttributesWithDifferentConstants_NoErrors()
        {
            Compilation compilation = CompilationHelpers.CreateCompilation("""
                using PolyType;
                using System;

                [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
                public class TestAttribute : Attribute
                {
                    public TestAttribute(string name, int value)
                    {
                        Name = name;
                        Value = value;
                    }
                    public string Name { get; }
                    public int Value { get; }
                }

                [GenerateShape]
                [Test("first", 1)]
                [Test("second", 2)]
                [Test("third", 3)]
                public partial class MyClass { }
                """);

            PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public static void AttributeWithNullTypeConstant_NoErrors()
        {
            Compilation compilation = CompilationHelpers.CreateCompilation("""
                using PolyType;
                using System;

                [AttributeUsage(AttributeTargets.Class)]
                public class TestAttribute : Attribute
                {
                    public TestAttribute(Type? type) => Type = type;
                    public Type? Type { get; }
                }

                [GenerateShape]
                [Test(null)]
                public partial class MyClass1 { }

                [GenerateShape]
                [Test(typeof(int))]
                public partial class MyClass2 { }
                """);

            PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public static void AttributeWithNullTypeInNamedArgument_NoErrors()
        {
            Compilation compilation = CompilationHelpers.CreateCompilation("""
                using PolyType;
                using System;

                [AttributeUsage(AttributeTargets.Class)]
                public class TestAttribute : Attribute
                {
                    public Type? Type { get; set; }
                    public string? Name { get; set; }
                }

                [GenerateShape]
                [Test(Type = null)]
                public partial class MyClass1 { }

                [GenerateShape]
                [Test(Type = null, Name = "test")]
                public partial class MyClass2 { }

                [GenerateShape]
                [Test(Type = typeof(string), Name = null)]
                public partial class MyClass3 { }
                """);

            PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public static void AttributeWithNullTypeArray_NoErrors()
        {
            Compilation compilation = CompilationHelpers.CreateCompilation("""
                using PolyType;
                using System;

                [AttributeUsage(AttributeTargets.Class)]
                public class TestAttribute : Attribute
                {
                    public TestAttribute(Type?[] types) => Types = types;
                    public Type?[] Types { get; }
                }

                [GenerateShape]
                [Test(new Type?[] { typeof(int), null, typeof(string) })]
                public partial class MyClass1 { }

                [GenerateShape]
                [Test(new Type?[] { null, null })]
                public partial class MyClass2 { }
                """);

            PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
            Assert.Empty(result.Diagnostics);
        }
    }
}
