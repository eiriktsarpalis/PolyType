using PolyType;
using PolyType.Examples.JsonSerializer;
using PolyType.Examples.XmlSerializer;

DateOnly today = DateOnly.FromDateTime(DateTime.Now);
Todos? value = new(
    [ new (Id: 0, "Wash the dishes.", today, Status.Done),
      new (Id: 1, "Dry the dishes.", today, Status.Done),
      new (Id: 2, "Turn the dishes over.", today, Status.InProgress),
      new (Id: 3, "Walk the kangaroo.", today.AddDays(1), Status.NotStarted),
      new (Id: 4, "Call Grandma.", today.AddDays(1), Status.NotStarted)]);

string json = JsonSerializerTS.Serialize(value, options: new() { Indented = true });
Console.WriteLine($"JSON encoding:\n{json}");
value = JsonSerializerTS.Deserialize<Todos>(json);

[GenerateShape]
public partial record Todos(Todo[] Items);

public record Todo(int Id, string? Title, DateOnly? DueBy, Status Status);

public enum Status { NotStarted, InProgress, Done }