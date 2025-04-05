using System.Collections.Generic;
using System;

namespace BlackBytesBox.Routed.GitBackend.Extensions.EnumerableExtensions
{
    /// <summary>
    /// Provides extension methods for IEnumerable&lt;T&gt;.
    /// </summary>
    public static class EnumerableExtensions
    {
        /// <summary>
        /// Returns elements from a sequence until (and including) the first element that satisfies a specified condition.
        /// </summary>
        /// <typeparam name="T">The type of the elements of the sequence.</typeparam>
        /// <param name="source">The sequence to return elements from.</param>
        /// <param name="predicate">A function to test each element for a condition.</param>
        /// <returns>
        /// An IEnumerable&lt;T&gt; that contains the elements from the input sequence up to and including the first element that satisfies the condition.
        /// If no element satisfies the condition, the entire sequence is returned.
        /// </returns>
        /// <example>
        /// <code>
        /// List&lt;string&gt; ss = new List&lt;string&gt; { "gitrepos", "MyProject.git", "info", "refs" };
        /// var result = ss.TakeUntilIncluding(s => s.EndsWith(".git", StringComparison.OrdinalIgnoreCase));
        /// // result contains: "gitrepos", "MyProject.git"
        /// </code>
        /// </example>
        public static IEnumerable<T> TakeUntilIncluding<T>(this IEnumerable<T> source, Func<T, bool> predicate)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));

            foreach (var item in source)
            {
                yield return item;
                if (predicate(item))
                    yield break;
            }
        }
    }
}
