using System;
using System.Collections.Generic;

namespace EFInterceptorDemo
{
    public static class EnumerableExtensions
    {
        public static void Each<T>(this IEnumerable<T> items, Action<T> action)
        {
            if (items != null)
                foreach (var i in items)
                    action(i);
        }
    }
}
