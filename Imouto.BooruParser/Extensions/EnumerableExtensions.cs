namespace Imouto.BooruParser.Extensions;

public static class EnumerableExtensions
{
    public static IEnumerable<TResult> SelectPairs<TSource, TResult>(
        this IEnumerable<TSource> source,
        Func<TSource, TSource, TResult> selector)
    {
        using var iterator = source.GetEnumerator();
        
        var i = 0;
        var item1 = default(TSource)!;
        var item2 = default(TSource)!;
        
        while (iterator.MoveNext())
        {
            item1 = i == 0 ? iterator.Current : item1;
            item2 = i == 1 ? iterator.Current : item2;
            i++;

            if (i == 2)
            {
                yield return selector(item1, item2);
                i = 0;
            }
        }
    }
}
