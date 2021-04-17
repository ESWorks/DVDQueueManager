using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace DVDOrders.Services
{
    public class AdvancedTimer : IDisposable
    {
        private bool _disposed;
        private int _interval, _resolution;
        private UInt32 _timerId;

        // Hold the timer callback to prevent garbage collection.
        private readonly AdvancedTimerCallback _callback;

        public AdvancedTimer()
        {
            _callback = TimerCallbackMethod;
            Resolution = 5;
            Interval = 10;
        }

        ~AdvancedTimer()
        {
            Dispose(false);
        }

        public int Interval
        {
            get => _interval;
            set
            {
                CheckDisposed();

                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value));

                _interval = value;
                if (Resolution > Interval)
                    Resolution = value;
            }
        }

        // Note minimum resolution is 0, meaning highest possible resolution.
        public int Resolution
        {
            get => _resolution;
            set
            {
                CheckDisposed();

                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value));

                _resolution = value;
            }
        }

        public bool IsRunning => _timerId != 0;

        public void Start()
        {
            CheckDisposed();

            if (IsRunning)
                throw new InvalidOperationException("Timer is already running");

            // Event type = 0, one off event
            // Event type = 1, periodic event
            UInt32 userCtx = 0;
            _timerId = NativeMethods.TimeSetEvent((uint)Interval, (uint)Resolution, _callback, ref userCtx, 1);
            if (_timerId == 0)
            {
                int error = Marshal.GetLastWin32Error();
                throw new Win32Exception(error);
            }
        }

        public void Stop()
        {
            CheckDisposed();

            if (!IsRunning)
                throw new InvalidOperationException("Timer has not been started");

            StopInternal();
        }

        private void StopInternal()
        {
            NativeMethods.TimeKillEvent(_timerId);
            _timerId = 0;
        }

        public event EventHandler Elapsed;

        public void Dispose()
        {
            Dispose(true);
        }

        private void TimerCallbackMethod(uint id, uint msg, ref uint userCtx, uint rsv1, uint rsv2)
        {
            var handler = Elapsed;
            handler?.Invoke(this, EventArgs.Empty);
        }

        private void CheckDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException("AdvancedTimer");
        }

        private void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            _disposed = true;
            if (IsRunning)
            {
                StopInternal();
            }

            if (!disposing) return;
            Elapsed = null;
            GC.SuppressFinalize(this);
        }
    }

    internal delegate void AdvancedTimerCallback(UInt32 id, UInt32 msg, ref UInt32 userCtx, UInt32 rsv1, UInt32 rsv2);

    internal static class NativeMethods
    {
        [DllImport("winmm.dll", SetLastError = true, EntryPoint = "timeSetEvent")]
        internal static extern UInt32 TimeSetEvent(UInt32 msDelay, UInt32 msResolution, AdvancedTimerCallback callback, ref UInt32 userCtx, UInt32 eventType);

        [DllImport("winmm.dll", SetLastError = true, EntryPoint = "timeKillEvent")]
        internal static extern void TimeKillEvent(UInt32 uTimerId);
    }
}
