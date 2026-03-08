using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmithForge.Main.Collections
{
    // Потокобезопасный HashSet
    public class ConcurrentHashSet<T> where T : notnull
    {
        private readonly HashSet<T> _set = new();
        private readonly object _lock = new();

        public bool Add(T item)
        {
            lock (_lock)
            {
                return _set.Add(item);
            }
        }

        public bool Remove(T item)
        {
            lock (_lock)
            {
                return _set.Remove(item);
            }
        }

        public bool Contains(T item)
        {
            lock (_lock)
            {
                return _set.Contains(item);
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _set.Clear();
            }
        }

        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _set.Count;
                }
            }
        }

        public List<T> ToList()
        {
            lock (_lock)
            {
                return new List<T>(_set);
            }
        }

        public T[] ToArray()
        {
            lock (_lock)
            {
                return _set.ToArray();
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            lock (_lock)
            {
                // Возвращаем копию для безопасной итерации
                return new List<T>(_set).GetEnumerator();
            }
        }

        public bool IsEmpty
        {
            get
            {
                lock (_lock)
                {
                    return _set.Count == 0;
                }
            }
        }

        // Метод для атомарной операции
        public bool AddIfNotContains(T item)
        {
            lock (_lock)
            {
                if (!_set.Contains(item))
                {
                    _set.Add(item);
                    return true;
                }
                return false;
            }
        }
    }
}
