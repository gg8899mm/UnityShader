using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TileStreaming
{
    /// <summary>
    /// Main TileStreamer manager that handles tile loading, caching, and material application.
    /// Supports configurable LOD levels (r14-r20) and background loading via UnityWebRequest/AsyncGPUReadback.
    /// </summary>
    public class TileStreamer : MonoBehaviour
    {
        [SerializeField]
        private int _minLod = 14;

        [SerializeField]
        private int _maxLod = 20;

        [SerializeField]
        private long _cacheMemoryMB = 512;

        [SerializeField]
        private int _maxConcurrentLoads = 4;

        [SerializeField]
        private bool _useAsyncGpuReadback = false;

        private LRUTileCache _cache;
        private ITileProvider _tileProvider;
        private IMaterialHook _materialHook;
        private Queue<TileLoadRequest> _loadQueue;
        private HashSet<TileCoord> _loadingTiles;
        private int _activeLoadCount;

        private class TileLoadRequest
        {
            public TileCoord Coord;
            public System.Action<TileData> OnComplete;
            public System.Action<string> OnError;
        }

        public TileStreamer()
        {
            _loadQueue = new Queue<TileLoadRequest>();
            _loadingTiles = new HashSet<TileCoord>();
            _activeLoadCount = 0;
        }

        private void Awake()
        {
            _cache = new LRUTileCache(_cacheMemoryMB);
            _loadQueue = new Queue<TileLoadRequest>();
            _loadingTiles = new HashSet<TileCoord>();
        }

        /// <summary>
        /// Initialize the tile streamer with a tile provider and material hook.
        /// </summary>
        public void Initialize(ITileProvider tileProvider, IMaterialHook materialHook = null)
        {
            _tileProvider = tileProvider;
            _materialHook = materialHook;

            if (_tileProvider != null)
            {
                var (minLod, maxLod) = _tileProvider.GetSupportedLodRange();
                _minLod = Mathf.Max(_minLod, minLod);
                _maxLod = Mathf.Min(_maxLod, maxLod);
            }

            Debug.Log($"TileStreamer initialized with LOD range: {_minLod}-{_maxLod}");
        }

        /// <summary>
        /// Request to load a tile. Queues the request for background loading.
        /// </summary>
        public void RequestTile(TileCoord coord, System.Action<TileData> onComplete = null, System.Action<string> onError = null)
        {
            if (_tileProvider == null)
            {
                Debug.LogError("TileStreamer not initialized with a tile provider.");
                onError?.Invoke("No tile provider");
                return;
            }

            if (coord.Z < _minLod || coord.Z > _maxLod)
            {
                onError?.Invoke($"LOD {coord.Z} outside supported range [{_minLod}, {_maxLod}]");
                return;
            }

            if (_cache.TryGetTile(coord, out var cachedTile))
            {
                onComplete?.Invoke(cachedTile);
                return;
            }

            if (_loadingTiles.Contains(coord))
            {
                return;
            }

            var request = new TileLoadRequest
            {
                Coord = coord,
                OnComplete = onComplete,
                OnError = onError
            };

            _loadQueue.Enqueue(request);
        }

        /// <summary>
        /// Get a cached tile directly.
        /// </summary>
        public TileData GetCachedTile(TileCoord coord)
        {
            _cache.TryGetTile(coord, out var tileData);
            return tileData;
        }

        /// <summary>
        /// Clear all cached tiles and pending requests.
        /// </summary>
        public void ClearCache()
        {
            _cache.Clear();
            _loadQueue.Clear();
            _loadingTiles.Clear();
        }

        /// <summary>
        /// Set the memory budget for the LRU cache.
        /// </summary>
        public void SetCacheMemoryMB(long memoryMB)
        {
            _cacheMemoryMB = memoryMB;
            if (_cache != null)
            {
                _cache = new LRUTileCache(memoryMB);
            }
        }

        /// <summary>
        /// Get cache statistics.
        /// </summary>
        public (int tileCount, long memoryUsedMB, long memoryBudgetMB) GetCacheStats()
        {
            return (
                _cache.GetCacheCount(),
                _cache.GetCurrentMemoryUsage() / (1024 * 1024),
                _cache.GetMaxMemoryBytes() / (1024 * 1024)
            );
        }

        private void Update()
        {
            ProcessLoadQueue();
        }

        private void ProcessLoadQueue()
        {
            while (_activeLoadCount < _maxConcurrentLoads && _loadQueue.Count > 0)
            {
                var request = _loadQueue.Dequeue();
                _loadingTiles.Add(request.Coord);
                _activeLoadCount++;
                StartCoroutine(LoadTileCoroutine(request));
            }
        }

        private System.Collections.IEnumerator LoadTileCoroutine(TileLoadRequest request)
        {
            yield return StartCoroutine(_tileProvider.LoadTileAsync(
                request.Coord,
                (tileData) =>
                {
                    if (tileData != null)
                    {
                        _cache.CacheTile(tileData);
                        request.OnComplete?.Invoke(tileData);
                    }
                    else
                    {
                        request.OnError?.Invoke("Failed to load tile");
                    }

                    _loadingTiles.Remove(request.Coord);
                    _activeLoadCount--;
                },
                (error) =>
                {
                    request.OnError?.Invoke(error);
                    _loadingTiles.Remove(request.Coord);
                    _activeLoadCount--;
                }
            ));
        }

        private void OnDestroy()
        {
            _cache?.Clear();
        }
    }
}
