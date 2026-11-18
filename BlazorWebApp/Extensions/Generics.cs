namespace BlazorWebApp.Extensions
{
    public static class Generics
    {
        private static async Task<IEnumerable<TResult>> ToIEnumerable<TSource, TResult>(this IAsyncEnumerable<TSource> source, Func<TSource, TResult> mapper)
        {
            var result = new List<TResult>();
            await foreach (var item in source)
            {
                result.Add(mapper(item));
            }
            return result;
        }
    }
}
