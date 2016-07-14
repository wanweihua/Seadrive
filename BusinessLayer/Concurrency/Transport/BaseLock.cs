using System.Diagnostics;

namespace BusinessLayer.Concurrency.Transport
{
    public abstract class BaseLock
    {
        protected BaseLock()
        {
        }

        protected BaseLock(BaseLock b)
        {
        }

        public abstract bool Lock();
        public abstract bool TryLock();
        public abstract bool TryLockFor(int dwMilliSecond);
        public abstract void Unlock();

        public class BaseLockObj
        {
            public BaseLockObj(BaseLock iLock)
            {
                Debug.Assert(iLock != null, "Lock is null!");
                _mLock = iLock;
                if (_mLock != null)
                    _mLock.Lock();
            }

            ~BaseLockObj()
            {
                if (_mLock != null)
                {
                    _mLock.Unlock();
                }
            }

            private BaseLockObj()
            {
                _mLock = null;
            }

            private readonly BaseLock _mLock;
        };
    }
}
