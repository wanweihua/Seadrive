using System;
using System.Threading;

namespace BusinessLayer.Concurrency.Transport
{
    public sealed class DisposableBaseEvent : BaseLock, IDisposable
    {
        private EventWaitHandle _mEvent;
        private readonly bool _mIsInitialRaised;
        private readonly EventResetMode _mEventResetMode;
        private readonly String _mName;

        public DisposableBaseEvent(String eventName = null)
            : base()
        {
            _mEventResetMode = EventResetMode.AutoReset;
            _mIsInitialRaised = true;
            _mName = eventName;
            _mEvent = _mName == null ? new EventWaitHandle(_mIsInitialRaised, _mEventResetMode) : new EventWaitHandle(_mIsInitialRaised, _mEventResetMode, _mName);
        }

        public DisposableBaseEvent(bool isInitialRaised, EventResetMode eventResetMode, String eventName = null)
            : base()
        {
            _mEventResetMode = eventResetMode;
            _mIsInitialRaised = isInitialRaised;
            _mName = eventName;
            _mEvent = _mName == null ? new EventWaitHandle(_mIsInitialRaised, _mEventResetMode) : new EventWaitHandle(_mIsInitialRaised, _mEventResetMode, _mName);
        }

        public DisposableBaseEvent(DisposableBaseEvent b)
            : base(b)
        {
            _mIsInitialRaised = b._mIsInitialRaised;
            _mName = b._mName;
            _mEventResetMode = b._mEventResetMode;
            _mEvent = _mName == null ? new EventWaitHandle(_mIsInitialRaised, _mEventResetMode) : new EventWaitHandle(_mIsInitialRaised, _mEventResetMode, _mName);
        }

        public override bool Lock()
        {
            return _mEvent.WaitOne();
        }

        public override bool TryLock()
        {
            return _mEvent.WaitOne(0);
        }

        public override bool TryLockFor(int dwMilliSecond)
        {
            return _mEvent.WaitOne(dwMilliSecond);
        }

        public override void Unlock()
        {
            _mEvent.Set();
        }

        public bool ResetEvent()
        {
            return _mEvent.Reset();
        }

        public bool SetEvent()
        {
            return _mEvent.Set();
        }

        public EventResetMode GetEventResetMode()
        {
            return _mEventResetMode;
        }

        public bool WaitForEvent(int dwMilliSecond = Timeout.Infinite)
        {
            return _mEvent.WaitOne(dwMilliSecond);
        }

        public EventWaitHandle GetEventHandle()
        {
            return _mEvent;
        }

        private bool IsDisposed { get; set; }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool isDisposing)
        {
            try
            {
                if (!IsDisposed)
                {
                    if (isDisposing)
                    {
                        if (_mEvent != null)
                        {
                            _mEvent.Dispose();
                            _mEvent = null;
                        }
                    }
                }
            }
            finally
            {
                IsDisposed = true;
            }
        }

        ~DisposableBaseEvent() { Dispose(false); }

    }
}
