using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAWSimO.Toolbox
{
    /// <summary>
    /// class which holds all the extension methods for <see cref="List{T}"/>
    /// </summary>
    public static class ListExtensions
    {
        /// <summary>
        /// Get every n-th element of a <paramref name="list"/>
        /// </summary>
        /// <typeparam name="T">Type of list element</typeparam>
        /// <param name="list">List from which every n-th element will be taken</param>
        /// <param name="step">step size of type <see cref="int"/></param>
        /// <param name="offset">start index</param>
        /// <returns>Enumerable collection</returns>
        public static IEnumerable<T> GetNth<T>(this List<T> list, int step, int offset = 0)
        {
            for (int i = offset; i < list.Count; i += step)
                yield return list[i];
        }

        /// <summary>
        /// Finds element in a sorted list which is closest to <paramref name="value"/> using modified binary search, custom Comparer can be supplied as optional argument. WARNING: Dynamic cast and operator - can be used, If exception is thrown on custom types then override operator - 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="a"></param>
        /// <param name="value">algorithm will search for a value in list which is closest to this value</param>
        /// <param name="comparer">Comparer which is used to compare elements of list</param>
        /// <returns></returns>
        public static T BinaryFindClosest<T>(this List<T> a, T value, Comparer<T> comparer = null) where T: IComparable<T>
        {
            if (comparer == null)
                comparer = Comparer<T>.Default;

            if (comparer.Compare(value, a[0]) < 0)
            {
                return a[0];
            }
            if (comparer.Compare(value, a[a.Count - 1]) > 0)
            {
                return a[a.Count - 1];
            }

            int lo = 0;
            int hi = a.Count - 1;

            while (lo <= hi)
            {
                int mid = (hi - lo) / 2 + lo;  //(hi+lo)/2 could lead to overflow

                if (comparer.Compare(value, a[mid]) < 0)
                {
                    hi = mid - 1;
                }
                else if (comparer.Compare(value, a[mid]) > 0)
                {
                    lo = mid + 1;
                }
                else
                {
                    return a[mid];
                }
            }
            // lo == hi + 1
            try
            {
                dynamic returnValue = ((dynamic)a[lo] - (dynamic)value) < ((dynamic)value - (dynamic)a[hi]) ? (dynamic)a[lo] : (dynamic)a[hi];
                return returnValue;
            }catch(Exception e)
            {
                throw e;
            }
            
        }
    }
}
