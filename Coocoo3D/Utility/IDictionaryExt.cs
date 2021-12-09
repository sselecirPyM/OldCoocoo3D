using Coocoo3D.FileFormat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.Utility
{
    public static class IDictionaryExt
    {
        public static T ConcurrenceGetOrCreate<T>(this IDictionary<string, T> iDictionary, string path) where T : new()
        {
            T val = default(T);
            lock (iDictionary)
            {
                if (!iDictionary.TryGetValue(path, out val))
                {
                    val = new T();
                    iDictionary.Add(path, val);
                }
            }
            return val;
        }
        public static T GetOrCreate<T>(this IDictionary<string, T> iDictionary, string path) where T : new()
        {
            T val = default(T);
            if (!iDictionary.TryGetValue(path, out val))
            {
                val = new T();
                iDictionary.Add(path, val);
            }
            return val;
        }
        public static T GetOrCreate<T1, T>(this IDictionary<T1, T> dict, T1 key, Func<T1, T> createFun) 
        {
            if (dict.TryGetValue(key, out T v))
            {
                return v;
            }
            v = createFun(key);


            dict[key] = v;


            return v;
        }
        public static T GetOrCreate<T1, T>(this IDictionary<T1, T> dict, T1 key, Func<T> createFun)
        {
            if (dict.TryGetValue(key, out T v))
            {
                return v;
            }
            v = createFun();
            dict[key] = v;
            return v;
        }
    }
}
