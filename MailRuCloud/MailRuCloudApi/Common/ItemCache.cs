using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

/*
 * ВНИМАНИЕ! Файл выключен из компиляции!
 */

namespace YaR.Clouds.Common
{
    public class ItemCache<TKey, TValue>
    {
        private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(ItemCache<TKey, TValue>));

        private static readonly TimeSpan _minCleanUpInterval = new TimeSpan(0, 0, 20 /* секунды */ );
        private static readonly TimeSpan _maxCleanUpInterval = new TimeSpan(0, 10 /* минуты */, 0);

        // По умолчанию очистка кеша от устаревших записей производится каждые 5 минут
        private TimeSpan _cleanUpPeriod = TimeSpan.FromMinutes(5);

        private readonly TimeSpan _expirePeriod;

        private readonly bool _noCache;

        private readonly Timer _cleanTimer;
        private readonly ConcurrentDictionary<TKey, TimedItem<TValue>> _items = new();
        //private readonly object _locker = new object();

        public ItemCache(TimeSpan expirePeriod)
        {
            _expirePeriod = expirePeriod;
            _noCache = Math.Abs(_expirePeriod.TotalMilliseconds) < 0.001;

            long cleanPeriod = (long)CleanUpPeriod.TotalMilliseconds;

            // if there is no cache, we don't need clean up timer
            _cleanTimer = _noCache ? null : new Timer(_ => RemoveExpired(), null, cleanPeriod, cleanPeriod);
        }

        public TimeSpan CleanUpPeriod
        {
            get => _cleanUpPeriod;
            set
            {
                // Очистку кеша от устаревших записей не следует проводить часто чтобы не нагружать машину,
                // и не следует проводить редко, редко, чтобы не натыкаться постоянно на устаревшие записи.
                _cleanUpPeriod = value < _minCleanUpInterval
                                 ? _minCleanUpInterval
                                 : value > _maxCleanUpInterval
                                   ? _maxCleanUpInterval
                                   : value;

                if (!_noCache)
                {
                    long cleanPreiod = (long)_cleanUpPeriod.TotalMilliseconds;
                    _cleanTimer.Change(cleanPreiod, cleanPreiod);
                }
            }
        }

        public int RemoveExpired()
        {
            if (_items.IsEmpty) return 0;

            DateTime threshold = DateTime.Now - _expirePeriod;
            int removedCount = 0;
            foreach (var item in _items)
                if (item.Value.Created <= threshold)
                    if (_items.TryRemove(item.Key, out _))
                        removedCount++;

            if (removedCount > 0)
                Logger.Debug($"Items cache clean: removed {removedCount} expired items");

            return removedCount;
        }

        public TValue Get(TKey key)
        {
            if (_noCache)
                return default;

            if (!_items.TryGetValue(key, out var item))
                return default;

            if (IsExpired(item))
                _items.TryRemove(key, out _);
            else
            {
                Logger.Debug($"Cache hit: {key}");
                return item.Item;
            }
            return default;
        }

        public void Add(TKey key, TValue value)
        {
            if (_noCache) return;

            var item = new TimedItem<TValue>
            {
                Created = DateTime.Now,
                Item = value
            };

            _items.AddOrUpdate(key, item, (_, _) => item);
        }

        public void Add(IEnumerable<KeyValuePair<TKey, TValue>> items)
        {
            foreach (var item in items)
                Add(item.Key, item.Value);
        }

        public TValue Invalidate(TKey key)
        {
            _items.TryRemove(key, out var item);
            return item != null ? item.Item : default;
        }

        public void Invalidate()
        {
            _items.Clear();
        }

        public void Invalidate(params TKey[] keys)
        {
            Invalidate(keys.AsEnumerable());
        }

        internal void Invalidate(IEnumerable<TKey> keys)
        {
            foreach (var key in keys)
                _items.TryRemove(key, out _);
        }

        public void Forget(TKey whoKey, TKey whomKey)
        {
            Invalidate(whomKey);

            var who = Get(whoKey) as ICanForget;
            who?.Forget(whomKey);
        }

        private bool IsExpired(TimedItem<TValue> item)
        {
            DateTime threshold = DateTime.Now - _expirePeriod;
            return item.Created <= threshold;
        }

        private class TimedItem<T>
        {
            public DateTime Created { get; set; }
            public T Item { get; set; }
        }
    }

    public interface ICanForget
    {
        void Forget(object whomKey);
    }
}
