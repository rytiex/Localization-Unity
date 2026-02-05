using System;
using System.Collections.Concurrent;

namespace PicoShot.Localization.Utilities
{
    /// <summary>
    /// A simple thread-safe object pool for reusing objects.
    /// </summary>
    public class ObjectPool<T> where T : class
    {
        private readonly Func<T> _createFunc;
        private readonly Action<T> _resetAction;
        private readonly ConcurrentBag<T> _objects = new();

        public ObjectPool(Func<T> createFunc, Action<T> resetAction = null)
        {
            _createFunc = createFunc ?? throw new ArgumentNullException(nameof(createFunc));
            _resetAction = resetAction;
        }

        /// <summary>
        /// Gets an object from the pool or creates a new one.
        /// </summary>
        public T Get()
        {
            return _objects.TryTake(out T item) ? item : _createFunc();
        }

        /// <summary>
        /// Returns an object to the pool for reuse.
        /// </summary>
        public void Return(T item)
        {
            if (item == null) return;
            
            _resetAction?.Invoke(item);
            _objects.Add(item);
        }

        /// <summary>
        /// Clears all objects from the pool.
        /// </summary>
        public void Clear()
        {
            _objects.Clear();
        }
    }

    /// <summary>
    /// Static pool for StringBuilder instances.
    /// </summary>
    public static class StringBuilderPool
    {
        private static readonly ObjectPool<System.Text.StringBuilder> Pool = new(
            () => new System.Text.StringBuilder(256),
            sb => sb.Clear()
        );

        public static System.Text.StringBuilder Get() => Pool.Get();
        public static void Return(System.Text.StringBuilder sb) => Pool.Return(sb);
    }
}
