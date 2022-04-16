namespace RedisQ.Core;

public static class AsyncEnumerable
{
    public static async Task<IReadOnlyList<T>> Collect<T>(this IAsyncEnumerable<T> enumerable)
    {
        var items = new List<T>();
        await foreach (var item in enumerable)
        {
            items.Add(item);
        }
        return items;
    }

    public static IAsyncEnumerable<T> FromCollection<T>(IReadOnlyCollection<T> collection)
    {
        async IAsyncEnumerable<T> Enumerate()
        {
            foreach (var value in collection)
            {
                yield return await ValueTask.FromResult(value).ConfigureAwait(false);
            }
        }

        return Enumerate();
    }
}
