IEnumerable<TOutput> Process(IEnumerable<TInput> input, ICSharpSandboxContext context)
{
    var n = context.GetArgument("count", int.Parse);
    var g = context.GetArgument("gain", int.Parse);
    var f = context.GetArgument("cycles", int.Parse);

    foreach (var row in input) 
    {
        yield return new TOutput { x = row.x, fx = MyCalculator.Calculate(g, n, f, row.x) };
    }
}

public static class MyCalculator
{
    public static double Calculate(int g, int n, int f, long? value)
    {
        return value.HasValue ? g * Math.Sin((double)value / n * 2 * Math.PI * f) : 0.0;
    }
}
