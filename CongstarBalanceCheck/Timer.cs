using System;
using System.Threading;

using MathNet.Numerics.Random;

namespace CongstarBalanceCheck
{
    public class Timer : IDisposable
    {
        private System.Threading.Timer _taskTimer;
        private readonly int _interval;
        private readonly bool _autoReset;
        private static readonly Random _Random = new MersenneTwister(true);

        public event ElapsedEventHandler Elapsed;

        public Timer(int interval, bool autoReset)
        {
            _interval = interval;
            _autoReset = autoReset;
        }

        private void InternalElapsed(object sender)
        {
            if (!_autoReset)
                Stop();

            var eargs = new EventArgs();
            Elapsed?.Invoke(sender, eargs);
        }

        public void Start()
        {
            var ts = new TimeSpan(0, 0, 0, 0, _interval + _Random.Next(1, 900000));

            if (_taskTimer == null)
                _taskTimer = new System.Threading.Timer(InternalElapsed, null, ts, ts);
            else
                _taskTimer.Change(ts, ts);
        }

        public void Stop()
        {
            _taskTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        }

        public void Dispose()
        {
            Stop();
            _taskTimer?.Dispose();

            _taskTimer = null;
            Elapsed = null;
        }
    }
}