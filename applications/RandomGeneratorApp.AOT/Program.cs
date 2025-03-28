﻿using PolyType;
using PolyType.Examples.PrettyPrinter;
using PolyType.Examples.RandomGenerator;

foreach (MeasurementData data in RandomGenerator.GenerateValues<MeasurementData>())
{
    Console.WriteLine(PrettyPrinter.Print(data));
    await Task.Delay(1000);
}

[GenerateShape]
public partial class MeasurementData
{
    public required string Id { get; init; }
    public required float[] Data { get; init; }
    public required Dictionary<int, int> Metadata { get; init; }
}
