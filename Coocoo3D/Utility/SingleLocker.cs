using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Coocoo3D.Utility
{
    public struct SingleLocker
    {
        int LockRefCount;
        /// <summary>如果返回真，则需要调用FreeLocker()来释放锁</summary>
        public bool GetLocker()
        {
            int testValue = Interlocked.Increment(ref LockRefCount);
            if (testValue == 1)
            {
                return true;
            }
            else
            {
                Interlocked.Decrement(ref LockRefCount);
                return false;
            }
        }
        /// <summary>只有当之前调用GetLocker()返回true时才需要FreeLocker()</summary>
        public void FreeLocker()
        {
            Interlocked.Decrement(ref LockRefCount);
        }
    }
}
