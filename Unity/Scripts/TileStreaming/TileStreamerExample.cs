using UnityEngine;

namespace TileStreaming
{
    /// <summary>
    /// Example MonoBehaviour demonstrating TileStreamer usage.
    /// This shows how to set up and use the tile streaming system in a scene.
    /// </summary>
    public class TileStreamerExample : MonoBehaviour
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
        private Material _terrainMaterial;

        [SerializeField]
        private bool _useKTX2Hook = false;

        private TileStreamer _tileStreamer;
        private ITileProvider _tileProvider;
        private IMaterialHook _materialHook;

        private void Start()
        {
            InitializeTileStreamer();
            LoadExampleTiles();
        }

        private void InitializeTileStreamer()
        {
            _tileStreamer = gameObject.AddComponent<TileStreamer>();

            _tileProvider = new StreamingAssetsTileProvider(
                basePath: "Tiles",
                minLod: _minLod,
                maxLod: _maxLod,
                tileSize: 256,
                fileExtension: "ktx2"
            );

            _materialHook = _useKTX2Hook
                ? (IMaterialHook)new KTX2MaterialHook()
                : new StandardMaterialHook();

            _tileStreamer.Initialize(_tileProvider, _materialHook);
        }

        private void LoadExampleTiles()
        {
            int lod = 15;
            int tilesPerDimension = 1 << lod;

            int centerX = tilesPerDimension / 2;
            int centerY = tilesPerDimension / 2;

            int roiRadius = 2;

            for (int x = centerX - roiRadius; x <= centerX + roiRadius; x++)
            {
                for (int y = centerY - roiRadius; y <= centerY + roiRadius; y++)
                {
                    var coord = new TileCoord(x, y, lod);
                    _tileStreamer.RequestTile(
                        coord,
                        onComplete: (tileData) =>
                        {
                            Debug.Log($"Tile loaded: {tileData.Coord}");
                            if (_terrainMaterial != null)
                            {
                                _materialHook.ApplyTile(_terrainMaterial, tileData);
                            }
                        },
                        onError: (error) =>
                        {
                            Debug.LogWarning($"Tile failed to load: {error}");
                        }
                    );
                }
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                var stats = _tileStreamer.GetCacheStats();
                Debug.Log($"Cache: {stats.tileCount} tiles, {stats.memoryUsedMB}MB / {stats.memoryBudgetMB}MB");
            }

            if (Input.GetKeyDown(KeyCode.C))
            {
                _tileStreamer.ClearCache();
                Debug.Log("Cache cleared");
            }
        }

        private void OnDestroy()
        {
            if (_tileStreamer != null)
            {
                _tileStreamer.ClearCache();
            }
        }
    }
}
