using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.Utility
{
    public static class MemUtil
    {
        [ThreadStatic]
        public static byte[] _megaBuffer;
        public static byte[] MegaBuffer
        {
            get
            {
                if (_megaBuffer == null)
                {
                    _megaBuffer = new byte[1048576 * 8];
                }
                return _megaBuffer;
            }
        }
    }
}
