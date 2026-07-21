using System;
using System.Collections.Generic;
using System.Linq;

namespace ChurchSuffering.ECS.Utils
{
	public static class ArrayUtils
	{
		/// <summary>
		/// Returns a new IEnumerable<T> with the elements of the input sequence in random order.
		/// The input is not modified.
		/// </summary>
		public static IEnumerable<T> Shuffled<T>(IEnumerable<T> source, Random rng = null)
		{
			if (source == null) throw new ArgumentNullException(nameof(source));
			rng ??= new Random();
			// Fisher–Yates shuffle on a copied list
			var list = source.ToList();
			for (int i = list.Count - 1; i > 0; i--)
			{
				int j = rng.Next(i + 1);
				(list[i], list[j]) = (list[j], list[i]);
			}
			return list;
		}

		/// <summary>
		/// Returns a sequence of count elements randomly sampled from source, with replacement.
		/// If source is empty, returns an empty sequence regardless of count.
		/// </summary>
		public static IEnumerable<T> TakeRandomWithReplacement<T>(IList<T> source, int count, Random rng = null)
		{
			if (source == null) throw new ArgumentNullException(nameof(source));
			if (count <= 0 || source.Count == 0) yield break;
			rng ??= new Random();
			for (int i = 0; i < count; i++)
			{
				int idx = rng.Next(source.Count);
				yield return source[idx];
			}
		}

		/// <summary>
		/// Returns a sequence of up to 'count' elements randomly sampled from source, without replacement.
		/// If count is greater than the source count, all elements are returned in random order.
		/// </summary>
		public static IEnumerable<T> TakeRandomWithoutReplacement<T>(IList<T> source, int count, Random rng = null)
		{
			if (source == null) throw new ArgumentNullException(nameof(source));
			if (count <= 0 || source.Count == 0) yield break;
			rng ??= new Random();

			// Copy list to avoid modifying original
			var items = source.ToList();
			int returnCount = Math.Min(count, items.Count);

			for (int i = 0; i < returnCount; i++)
			{
				int idx = rng.Next(items.Count);
				yield return items[idx];
				items.RemoveAt(idx);
			}
		}
	}
}


