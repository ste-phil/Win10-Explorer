using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Explorer.Helper
{
    public static class Extensions
    {
        public static void RemoveRange<T>(this Collection<T> collection, Collection<T> remove)
        {
            for (int i = 0; i < remove.Count; i++)
            {
                collection.Remove(remove[i]);
            }
        }
    }
}
