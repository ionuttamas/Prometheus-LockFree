using System;
using System.Collections.Generic;
using System.Linq;

namespace Prometheus.Common
{
    public static class CollectionExtensions {
        public static Dictionary<TK, TV> Merge<TK, TV>(this Dictionary<TK, TV> instance, Dictionary<TK, TV> value)
        {
            foreach (var entry in value)
            {
                instance[entry.Key] = entry.Value;
            }

            return instance;
        }

        public static T MinItem<T>(this IEnumerable<T> value, Func<T, object> comparer) {
            T minElement = value.OrderBy(comparer).FirstOrDefault();

            return minElement;
        }

        public static T MaxItem<T>(this IEnumerable<T> value, Func<T, object> comparer) {
            T minElement = value.OrderBy(comparer).FirstOrDefault();

            return minElement;
        }

        public static bool IsNullOrEmpty<T>(this IEnumerable<T> value) {
            if (value == null || !value.Any())
                return true;

            return false;
        }

        public static bool IsDefault<T>(this T value) where T : struct {
            bool isDefault = value.Equals(default(T));

            return isDefault;
        }

        public static void EnqueueRange<T>(this Queue<T> queue, IEnumerable<T> values) {
            foreach (var value in values) {
                queue.Enqueue(value);
            }
        }

        public static string Join<T>(this IEnumerable<T> collection, string separator) {
            List<T> list = collection as List<T> ?? collection.ToList();

            if (list.IsNullOrEmpty())
                return string.Empty;

            string result = string.Join(separator, list);

            return result;
        }

        public static IEnumerable<T> Distinct<T>(this IEnumerable<T> collection, Func<T, object> selector) {
            IEnumerable<T> result = collection.Distinct(new FuncComparer<T>(selector));

            return result;
        }

        public static List<TOut> ToList<TIn, TOut>(this IEnumerable<TIn> collection, Func<TIn, TOut> factory) {
            List<TOut> result = collection.Select(factory).ToList();

            return result;
        }

        public static bool Contains<T>(this IEnumerable<T> collection, T value, Func<T, object> comparer) {
            return collection.Contains(value, new FuncComparer<T>(comparer));
        }

        public static bool ContainsInvariant(this IEnumerable<string> collection, string value) {
            if (collection.IsNullOrEmpty())
                return false;

            string lowercaseValue = value.ToLower();
            bool result = collection.Any(x => x.EqualsInvariant(lowercaseValue));

            return result;
        }

        public static IEnumerable<T> Except<T>(this IEnumerable<T> collection, IEnumerable<T> comparand, Func<T, object> comparer) {
            return collection.Except(comparand, new FuncComparer<T>(comparer));
        }

        private class FuncComparer<T> : IEqualityComparer<T> {
            private readonly Func<T, object> _comparer;

            public FuncComparer(Func<T, object> comparer) {
                _comparer = comparer;
            }

            public bool Equals(T x, T y) {
                return _comparer(x).Equals(_comparer(y));
            }

            public int GetHashCode(T obj) {
                return _comparer(obj).GetHashCode();
            }
        }
    }
}