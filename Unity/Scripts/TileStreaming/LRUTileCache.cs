using System.Collections.Generic;
using UnityEngine;

namespace TileStreaming
{
    /// <summary>
    /// LRU (Least Recently Used) cache for tiles with configurable memory budget.
    /// </summary>
    public class LRUTileCache
    {
        private readonly Dictionary<TileCoord, LinkedListNode<CacheEntry>> _cache;
        private readonly LinkedList<CacheEntry> _lruList;
        private long _currentMemoryUsage;
        private readonly long _maxMemoryBytes;

        private class CacheEntry
        {
            public TileCoord Coord;
            public TileData Data;
        }

        public LRUTileCache(long maxMemoryMB)
        {
            _maxMemoryBytes = maxMemoryMB * 1024 * 1024;
            _cache = new Dictionary<TileCoord, LinkedListNode<CacheEntry>>();
            _lruList = new LinkedList<CacheEntry>();
            _currentMemoryUsage = 0;
        }

        /// <summary>
        /// Try to get a cached tile. Updates LRU order on hit.
        /// </summary>
        public bool TryGetTile(TileCoord coord, out TileData tileData)
        {
            if (_cache.TryGetValue(coord, out var node))
            {
                _lruList.Remove(node);
                _lruList.AddFirst(node);
                tileData = node.Value.Data;
                return true;
            }

            tileData = null;
            return false;
        }

        /// <summary>
        /// Add or update a tile in the cache.
        /// </summary>
        public void CacheTile(TileData tileData)
        {
            if (tileData == null) return;

            if (_cache.TryGetValue(tileData.Coord, out var existingNode))
            {
                _lruList.Remove(existingNode);
                _currentMemoryUsage -= existingNode.Value.Data.SizeInBytes;
                if (existingNode.Value.Data.Texture != null)
                {
                    Object.Destroy(existingNode.Value.Data.Texture);
                }
            }

            var entry = new CacheEntry { Coord = tileData.Coord, Data = tileData };
            var node = _lruList.AddFirst(entry);
            _cache[tileData.Coord] = node;
            _currentMemoryUsage += tileData.SizeInBytes;

            EvictLRUTilesIfNeeded();
        }

        /// <summary>
        /// Remove a specific tile from the cache.
        /// </summary>
        public void RemoveTile(TileCoord coord)
        {
            if (_cache.TryGetValue(coord, out var node))
            {
                _lruList.Remove(node);
                _currentMemoryUsage -= node.Value.Data.SizeInBytes;
                if (node.Value.Data.Texture != null)
                {
                    Object.Destroy(node.Value.Data.Texture);
                }
                _cache.Remove(coord);
            }
        }

        /// <summary>
        /// Clear all tiles from the cache.
        /// </summary>
        public void Clear()
        {
            foreach (var node in _lruList)
            {
                if (node.Data.Texture != null)
                {
                    Object.Destroy(node.Data.Texture);
                }
            }

            _cache.Clear();
            _lruList.Clear();
            _currentMemoryUsage = 0;
        }

        /// <summary>
        /// Get current memory usage in bytes.
        /// </summary>
        public long GetCurrentMemoryUsage() => _currentMemoryUsage;

        /// <summary>
        /// Get max memory budget in bytes.
        /// </summary>
        public long GetMaxMemoryBytes() => _maxMemoryBytes;

        /// <summary>
        /// Get number of tiles currently cached.
        /// </summary>
        public int GetCacheCount() => _cache.Count;

        private void EvictLRUTilesIfNeeded()
        {
            while (_currentMemoryUsage > _maxMemoryBytes && _lruList.Count > 0)
            {
                var lruNode = _lruList.Last;
                _lruList.RemoveLast();
                _currentMemoryUsage -= lruNode.Value.Data.SizeInBytes;

                if (lruNode.Value.Data.Texture != null)
                {
                    Object.Destroy(lruNode.Value.Data.Texture);
                }

                _cache.Remove(lruNode.Value.Coord);
            }
        }
    }
}
