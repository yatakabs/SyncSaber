using System;
using System.Collections.Generic;

namespace SyncSaber.Extentions
{
    public static class HashSetExtention
    {
        public static void AddRange<T>(this HashSet<T> hashSet, IEnumerable<T> values)
        {
            if (values == null) {
                throw new ArgumentNullException();
            }
            foreach (var item in values) {
                hashSet.Add(item);
            }
        }
    }
}
