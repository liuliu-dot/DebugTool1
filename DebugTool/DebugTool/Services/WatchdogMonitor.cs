using System;
using System.Diagnostics;
using System.Threading;

namespace DebugTool.Services
{
    /// <summary>
    /// 看门狗监控器，用于监控通信健康状态
    /// </summary>
    public class WatchdogMonitor : IDisposable
    {
        private readonly int _timeoutSeconds;
        private readonly Action<string> _onTimeout;
        private Timer _watchdogTimer;
        private DateTime _lastFeedTime;
        private readonly object _feedLock = new object();
        private bool _isEnabled = false;
        private bool _disposed = false;
        private int _timeoutCount = 0;

        public bool IsEnabled => _isEnabled;

        public WatchdogMonitor(int timeoutSeconds, Action<string> onTimeout)
        {
            if (timeoutSeconds <= 0) throw new ArgumentException("超时时间必须大于0");
            _timeoutSeconds = timeoutSeconds;
            _onTimeout = onTimeout ?? throw new ArgumentNullException(nameof(onTimeout));
            _watchdogTimer = new Timer(CheckTimeout, null, Timeout.Infinite, Timeout.Infinite);
        }

        public void Start()
        {
            if (_disposed) return;
            lock (_feedLock)
            {
                if (_isEnabled) return;
                _isEnabled = true;
                _lastFeedTime = DateTime.Now;
                _timeoutCount = 0;
                // 启动定时器（每1秒检查一次）
                _watchdogTimer.Change(1000, 1000);
                Debug.WriteLine($"[看门狗] 已启动，超时时间: {_timeoutSeconds}秒");
            }
        }

        public void Stop()
        {
            lock (_feedLock)
            {
                if (!_isEnabled) return;
                _isEnabled = false;
                _watchdogTimer.Change(Timeout.Infinite, Timeout.Infinite);
                Debug.WriteLine($"[看门狗] 已停止");
            }
        }

        /// <summary>
        /// 喂狗：告诉看门狗“我还活着”
        /// </summary>
        public void Feed()
        {
            if (!_isEnabled || _disposed) return;
            lock (_feedLock)
            {
                _lastFeedTime = DateTime.Now;
                _timeoutCount = 0;
            }
        }

        private void CheckTimeout(object state)
        {
            if (!_isEnabled || _disposed) return;

            double secondsSinceLastFeed;
            lock (_feedLock)
            {
                secondsSinceLastFeed = (DateTime.Now - _lastFeedTime).TotalSeconds;
            }

            if (secondsSinceLastFeed > _timeoutSeconds)
            {
                // 触发超时回调
                try
                {
                    _timeoutCount++;
                    // 为了防止频繁弹窗，可以加个锁或者限制频率，这里简化处理
                    // 如果已经超时很久了，每隔 5 秒报一次，或者只报一次
                    // 这里我们只在刚超时的时候报一次，直到下次喂狗
                    // (简化逻辑：直接回调，交给上层处理)
                    string msg = $"通信超时: 已 {secondsSinceLastFeed:F1}秒 未收到数据";
                    _onTimeout?.Invoke(msg);
                }
                catch { }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            Stop();
            _watchdogTimer?.Dispose();
            _disposed = true;
        }
    }
}