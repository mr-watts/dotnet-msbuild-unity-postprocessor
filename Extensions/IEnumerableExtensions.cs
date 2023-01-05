using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MrWatts.Internal.Extensions
{
    internal static class IEnumerableExtensions
    {
        internal static async Task ForEachAsync<TSource>(this IEnumerable<TSource> source, Func<TSource, Task> selector)
        {
            foreach (TSource item in source)
            {
                await selector(item);
            }
        }

        internal static async Task<IEnumerable<TResult>> SelectAsync<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, Task<TResult>> selector)
        {
            ConcurrentBag<TResult> resultList = new ConcurrentBag<TResult>();

            foreach (TSource item in source)
            {
                resultList.Add(await selector(item));
            }

            return resultList;
        }
    }
}