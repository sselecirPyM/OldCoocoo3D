using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.Utility
{
    public static class IListExt
    {
        public static void MoveTo_CC<T>(this IList<T> source, IList<T> target)
        {
            lock (source)
            {
                for (int i = 0; i < source.Count; i++)
                {
                    target.Add(source[i]);
                }
                source.Clear();
            }
        }
    }
}
