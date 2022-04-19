namespace RedisQ.Core;

public static class AsyncEnumerable
{
    public static async Task<IReadOnlyList<T>> Collect<T>(this IAsyncEnumerable<T> enumerable, int? limit = null)
    {
        var items = new List<T>();
        await foreach (var item in enumerable)
        {
            if (limit.HasValue && limit.Value <= items.Count) break;
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
