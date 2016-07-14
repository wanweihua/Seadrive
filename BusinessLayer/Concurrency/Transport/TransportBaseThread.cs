using System;
using System.Diagnostics;
using System.Threading;

namespace BusinessLayer.Concurrency.Transport
{
    public enum ThreadOpCode
    {
        CreateStart = 0,
        CreateSuspend
    };

    public enum ThreadStatus
    {
        Started = 0,
        Suspended,
        Terminated
    };

    public enum TerminationResult
    {
        Failed = 0,
        GracefullyTerminated,
        ForcefullyTerminate,
        NotOnRunning,
    };

    public class TransportBaseThread
    {
        private Thread _mThreadHandle;
        private ThreadPriority _mThreadPriority;
        private Thread _mParentThreadHandle;
        private ThreadStatus _mStatus;
        private readonly Action _mThreadFunc;
        private readonly Action<object> _mThreadParameterizedFunc;
        private readonly object _mParameter;
        private readonly Object _mThreadLock = new Object();
        private ulong _mExitCode;

        public TransportBaseThread(ThreadPriority priority = ThreadPriority.Normal)
        {
            _mThreadHandle = null;
            _mThreadPriority = priority;
            _mParentThreadHandle = null;
            _mStatus = ThreadStatus.Terminated;
            _mExitCode = 0;
            _mThreadFunc = null;
            _mThreadParameterizedFunc = null;
            _mParameter = null;
        }

        public TransportBaseThread(Action threadFunc, ThreadPriority priority = ThreadPriority.Normal)
        {
            _mThreadHandle = null;
            _mThreadPriority = priority;
            _mParentThreadHandle = null;
            _mStatus = ThreadStatus.Terminated;
            _mExitCode = 0;
            _mThreadFunc = threadFunc;
            _mThreadParameterizedFunc = null;
            _mParameter = null;

            _mParentThreadHandle = Thread.CurrentThread;
            _mThreadHandle = new Thread(TransportBaseThread.EntryPoint) { Priority = _mThreadPriority };
            _mThreadHandle.Start(this);
            _mStatus = ThreadStatus.Started;


        }

        public TransportBaseThread(Action<object> threadParameterizedFunc, object parameter, ThreadPriority priority = ThreadPriority.Normal)
        {
            _mThreadHandle = null;
            _mThreadPriority = priority;
            _mParentThreadHandle = null;
            _mStatus = ThreadStatus.Terminated;
            _mExitCode = 0;
            _mThreadFunc = null;
            _mThreadParameterizedFunc = threadParameterizedFunc;
            _mParameter = parameter;

            _mParentThreadHandle = Thread.CurrentThread;
            _mThreadHandle = new Thread(TransportBaseThread.EntryPoint) { Priority = _mThreadPriority };
            _mThreadHandle.Start(this);
            _mStatus = ThreadStatus.Started;


        }

        public TransportBaseThread(TransportBaseThread b)
        {
            _mThreadFunc = b._mThreadFunc;
            _mThreadParameterizedFunc = b._mThreadParameterizedFunc;
            _mParameter = b._mParameter;
            if (_mThreadFunc != null || _mParentThreadHandle != null)
            {
                _mParentThreadHandle = b._mParentThreadHandle;
                _mThreadHandle = b._mThreadHandle;
                _mThreadPriority = b._mThreadPriority;
                _mStatus = b._mStatus;
                _mExitCode = b._mExitCode;

                b._mParentThreadHandle = null;
                b._mThreadHandle = null;
                b._mStatus = ThreadStatus.Terminated;
                b._mExitCode = 0;
            }
            else
            {
                _mThreadHandle = null;
                _mThreadPriority = b._mThreadPriority;
                _mParentThreadHandle = null;
                _mExitCode = 0;

                _mStatus = ThreadStatus.Terminated;
            }
        }

        ~TransportBaseThread()
        {
            ResetThread();
        }

        public bool Start(ThreadOpCode opCode = ThreadOpCode.CreateStart, int stackSize = 0)
        {
            lock (_mThreadLock)
            {
                _mParentThreadHandle = Thread.CurrentThread;
                if (_mStatus == ThreadStatus.Terminated && _mThreadHandle == null)
                {
                    _mThreadHandle = new Thread(TransportBaseThread.EntryPoint, stackSize);
                    if (_mThreadHandle != null)
                    {
                        _mThreadHandle.Priority = _mThreadPriority;
                        if (opCode == ThreadOpCode.CreateStart)
                        {
                            _mThreadHandle.Start(this);
                            _mStatus = ThreadStatus.Started;
                        }
                        else
                            _mStatus = ThreadStatus.Suspended;
                        return true;
                    }

                }
                return false;
            }
        }

        public bool Resume()
        {
            lock (_mThreadLock)
            {
                if (_mStatus == ThreadStatus.Suspended && _mThreadHandle != null)
                {
                    _mThreadHandle.Resume();
                    _mStatus = ThreadStatus.Started;
                    return true;
                }
            }
            return false;
        }

        public bool Suspend()
        {

            if (_mStatus == ThreadStatus.Started && _mThreadHandle != null)
            {
                lock (_mThreadLock)
                {
                    _mStatus = ThreadStatus.Suspended;
                }
                _mThreadHandle.Suspend();
                return true;
            }
            return false;

        }

        public bool Terminate()
        {
            Debug.Assert(_mThreadHandle != Thread.CurrentThread, "Exception : Thread should not terminate self.");

            if (_mStatus != ThreadStatus.Terminated && _mThreadHandle != null)
            {
                lock (_mThreadLock)
                {
                    _mStatus = ThreadStatus.Terminated;
                    _mExitCode = 1;
                    _mThreadHandle.Abort();
                    _mThreadHandle = null;
                    _mParentThreadHandle = null;
                }
                ulong exitCode = _mExitCode;
                OnTerminated(exitCode);
                return true;
            }
            return true;
        }

        public bool WaitFor(int tMilliseconds = Timeout.Infinite)
        {
            if (_mStatus != ThreadStatus.Terminated && _mThreadHandle != null)
            {
                return _mThreadHandle.Join(tMilliseconds);
            }
            return false;
        }

        public void Join()
        {
            if (_mStatus != ThreadStatus.Terminated && _mThreadHandle != null)
            {
                _mThreadHandle.Join();
            }
        }

        public bool Joinable()
        {
            return (_mStatus != ThreadStatus.Terminated && _mThreadHandle != null);
        }

        public void Detach()
        {
            Debug.Assert(Joinable() == true);
            lock (_mThreadLock)
            {
                _mStatus = ThreadStatus.Terminated;
                _mThreadHandle = null;
                _mParentThreadHandle = null;
                _mExitCode = 0;
            }
        }

        public TerminationResult TerminateAfter(int tMilliseconds)
        {
            if (_mStatus != ThreadStatus.Terminated && _mThreadHandle != null)
            {
                bool status = _mThreadHandle.Join(tMilliseconds);
                if (status)
                {
                    return TerminationResult.GracefullyTerminated;
                }
                else
                {
                    return Terminate() ? TerminationResult.ForcefullyTerminate : TerminationResult.Failed;
                }
            }
            else
            {
                return TerminationResult.NotOnRunning;
            }
        }

        public Thread GetParentThreadHandle()
        {
            return _mParentThreadHandle;
        }

        public ThreadStatus GetStatus()
        {
            return _mStatus;
        }

        public ulong GetExitCode()
        {
            return _mExitCode;
        }

        public ThreadPriority GetPriority()
        {
            return _mThreadPriority;
        }

        public bool SetPriority(ThreadPriority priority)
        {
            _mThreadPriority = priority;
            _mThreadHandle.Priority = priority;
            return true;
        }

        protected Thread GetHandle()
        {
            return _mThreadHandle;
        }

        protected virtual void Execute()
        {
            if (_mThreadFunc != null)
                _mThreadFunc();
            else if (_mThreadParameterizedFunc != null)
                _mThreadParameterizedFunc(_mParameter);
        }

        protected virtual void OnTerminated(ulong exitCode, bool isInDeletion = false)
        {
        }

        private void SuccessTerminate()
        {
            lock (_mThreadLock)
            {
                _mStatus = ThreadStatus.Terminated;
                _mThreadHandle = null;
                _mParentThreadHandle = null;
                _mExitCode = 0;
            }

            OnTerminated(_mExitCode);
        }

        private int run()
        {
            Execute();
            SuccessTerminate();
            return 0;
        }

        private void ResetThread()
        {
            if (_mStatus != ThreadStatus.Terminated)
            {
                _mExitCode = 1;
                _mThreadHandle.Abort();
                OnTerminated(_mExitCode, true);
            }

            _mThreadHandle = null;
            _mParentThreadHandle = null;
            _mExitCode = 0;
            _mStatus = ThreadStatus.Terminated;
        }

        private static void EntryPoint(object pThis)
        {
            TransportBaseThread pt = (TransportBaseThread)pThis;
            pt.run();
        }


    }
}
