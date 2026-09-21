using System;
using UnityEngine;

namespace ButchersGames.Core
{
    /// <summary>
    /// Fixed-size round-robin pool. All items are created once in the constructor, Get() never allocates.
    /// When every item is busy the oldest one is reused.
    /// </summary>
    public sealed class RoundRobinPool<T> where T : class
    {
        private readonly T[] items;
        private readonly Func<T, bool> isBusy;
        private int next;

        public RoundRobinPool(int size, Func<T> factory, Func<T, bool> isBusy)
        {
            this.isBusy = isBusy;
            items = new T[Mathf.Max(1, size)];
            for (int i = 0; i < items.Length; i++)
                items[i] = factory();
        }

        public T Get()
        {
            for (int i = 0; i < items.Length; i++)
            {
                int index = (next + i) % items.Length;
                if (isBusy(items[index])) continue;

                next = (index + 1) % items.Length;
                return items[index];
            }

            T oldest = items[next];
            next = (next + 1) % items.Length;
            return oldest;
        }
    }
}
