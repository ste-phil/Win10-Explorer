using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Explorer.Entities;

namespace Explorer.Helper
{
    public class MultiDictionary<TKey, TValue>
    {
        private Dictionary<TKey, List<TValue>> data = new Dictionary<TKey, List<TValue>>();

        public List<TValue> this[TKey key]
        {
            get { return data[key]; }
            set { data[key] = value; }
        }

        public void Add(TKey key, TValue value)
        {
            if (data.TryGetValue(key, out List<TValue> list))
                list.Add(value);
            else
                data.Add(key, new List<TValue>() { value });
        }

        public void AddFirst(TKey key, TValue value)
        {
            if (data.TryGetValue(key, out List<TValue> list))
                list.Insert(0, value);
            else
                data.Add(key, new List<TValue>() { value });
        }

        public void Clear()
        {
            data.Clear();
        }
    }
}
