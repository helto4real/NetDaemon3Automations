namespace NetDaemonApps;

public static class RandomSelection
{
    public static string SelectRandom(IReadOnlyList<string> values, Random random)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(random);

        if (values.Count == 0)
        {
            throw new ArgumentException("At least one value is required.", nameof(values));
        }

        return values[random.Next(values.Count)];
    }
}
