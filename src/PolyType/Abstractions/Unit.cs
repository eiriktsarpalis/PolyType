namespace PolyType.Abstractions;

/// <summary>Defines a type inhabited by a single value.</summary>
/// <remarks>
/// Used as a substitute for <see langword="void"/> in cases
/// where it needs to be used as a type argument. See also
/// https://en.wikipedia.org/wiki/Unit_type for more information.
/// </remarks>
public readonly record struct Unit
{
    /// <summary>Gets the Unit instance.</summary>
    public static Unit Value => default;

    /// <inheritdoc/>
    public override string ToString() => "()";

    /// <summary>
    /// Wraps a <see cref="Task"/> into a <see cref="ValueTask{Unit}"/>.
    /// </summary>
    /// <param name="task">The task to wrap.</param>
    /// <returns>A <see cref="ValueTask{Unit}"/> representing the asynchronous operation.</returns>
    public static ValueTask<Unit> FromTaskAsync(Task task)
    {
        return task.Status is TaskStatus.RanToCompletion ? default : FromTaskCore(task);
        static async ValueTask<Unit> FromTaskCore(Task task)
        {
            await task.ConfigureAwait(false);
            return default;
        }
    }

    /// <summary>
    /// Wraps a <see cref="ValueTask"/> into a <see cref="ValueTask{Unit}"/>.
    /// </summary>
    /// <param name="task">The task to wrap.</param>
    /// <returns>A <see cref="ValueTask{Unit}"/> representing the asynchronous operation.</returns>
    public static ValueTask<Unit> FromValueTaskAsync(ValueTask task)
    {
        return task.IsCompletedSuccessfully ? default : FromValueTaskCore(task);
        static async ValueTask<Unit> FromValueTaskCore(ValueTask task)
        {
            await task.ConfigureAwait(false);
            return default;
        }
    }
}
