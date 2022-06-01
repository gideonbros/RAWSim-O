using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAWSimO.Toolbox
{   
    /// <summary>
    /// Class holding extension methods for <see cref="LinkedList{T}"/>
    /// </summary>
    public static class LinkedListExtensions
    {
        /// <summary>
        /// Cuts <paramref name="list"/> at <paramref name="index"/> and modifies it. <paramref name="list"/> remains with elements from First to index exclusively.
        /// Returns part of <paramref name="list"/> that was removed as a new <see cref="LinkedList{T}"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"><see cref="LinkedList{T}"/> which will be cut</param>
        /// <param name="index">0-based index at which cut will start</param>
        /// <returns>Part of <paramref name="list"/> that was removed as a new <see cref="LinkedList{T}"/></returns>
        public static LinkedList<T> CutOffAt<T>(this LinkedList<T> list, int index)
        {
            //parameter checking
            if (list.Count <= index) return new LinkedList<T>();
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index) + " was out of range of " + nameof(list));
            if (list == null) throw new ArgumentNullException("argument " + nameof(list) + " was null!");
            //algorithm O(n)
            int i = 0;
            var node = list.First;
            LinkedList<T> CutoffList = new LinkedList<T>();
            while (node != null)
            {
                var nextNode = node.Next;
                if (i >= index)
                {
                    CutoffList.AddLast(new LinkedListNode<T>(node.Value));
                    list.Remove(node);
                }
                i++;
                node = nextNode;
            }
            return CutoffList;
        }
        /// <summary>
        /// Gets value of the <see cref="LinkedList{T}"/> located at <paramref name="index"/> in O(n)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list">list to search</param>
        /// <param name="index">zero based index</param>
        /// <returns>value located at <paramref name="index"/></returns>
        public static T GetValueAtIndex<T>(this LinkedList<T> list, int index)
        {
            int counter = 0;
            if (index >= list.Count) throw new ArgumentOutOfRangeException(nameof(index) + " was out of range of " + nameof(list) + "!");
            if (index < 0 ) throw new ArgumentOutOfRangeException(nameof(index) + " was out of range of " + nameof(list) + "!");
            if (list == null) throw new ArgumentNullException(nameof(list) + " was null!");
            for (var node = list.First; node != null; node = node.Next, counter++)
            {
                if (counter == index)
                    return node.Value;
            }
            return list.Last.Value; //this is how we make the code analyzer a happy code analyzer
        }
        /// <summary>
        /// Gets the zero-based index of <paramref name="item"/> in a <paramref name="list"/>. Returns -1 if <paramref name="item"/> is not present in a <see cref="LinkedList{T}"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"><see cref="LinkedList{T}"/> on which sarch will be performed</param>
        /// <param name="item">value for which <paramref name="list"/> will be searched</param>
        /// <returns>zero-based index of <paramref name="item"/> if <paramref name="list"/> contains <paramref name="item"/>. -1 otherwise</returns>
        public static int IndexOf<T>(this LinkedList<T> list, T item)
        {
            var count = 0;
            for (var node = list.First; node != null; node = node.Next, count++)
            {
                if (item.Equals(node.Value))
                    return count;
            }
            return -1;
        }
    }
}
