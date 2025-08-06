using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PolyType.Examples.JsonSerializer;

/// <summary>
/// Defines a JSON marshalling wrapper for a regular .NET function.
/// </summary>
/// <param name="parameters">A JSON object containing named function parameters.</param>
/// <param name="cancellationToken">The cancellation token governing cancellation.</param>
/// <returns>A task containing the result of the function invocation.</returns>
public delegate ValueTask<JsonElement> JsonFunc(IReadOnlyDictionary<string, JsonElement> parameters, CancellationToken cancellationToken = default);