using UnityEngine;

namespace TileStreaming
{
    /// <summary>
    /// Configuration holder for TileStreamer settings.
    /// Can be used as a ScriptableObject for editor configuration.
    /// </summary>
    public class TileStreamerConfig : ScriptableObject
    {
        [Header("LOD Configuration")]
        [Range(0, 28)]
        public int MinLod = 14;

        [Range(0, 28)]
        public int MaxLod = 20;

        [Header("Cache Configuration")]
        [Range(32, 4096)]
        public long CacheMemoryMB = 512;

        [Header("Loading Configuration")]
        [Range(1, 16)]
        public int MaxConcurrentLoads = 4;

        [Header("Provider Configuration")]
        public string TileBasePath = "Tiles";

        [Range(64, 512)]
        public int TileSizePixels = 256;

        public string TileFileExtension = "ktx2";

        [Header("Material Configuration")]
        public bool UseKTX2Hook = true;

        [Range(0.1f, 1.0f)]
        public float CacheEvictionThreshold = 0.9f;

        /// <summary>
        /// Create a default configuration.
        /// </summary>
        public static TileStreamerConfig CreateDefault()
        {
            var config = CreateInstance<TileStreamerConfig>();
            config.name = "TileStreamerConfig_Default";
            return config;
        }

        /// <summary>
        /// Validate configuration values.
        /// </summary>
        public bool Validate()
        {
            if (MinLod < 0)
            {
                Debug.LogError("MinLod must be >= 0");
                return false;
            }

            if (MaxLod > 28)
            {
                Debug.LogError("MaxLod must be <= 28");
                return false;
            }

            if (MinLod > MaxLod)
            {
                Debug.LogError("MinLod must be <= MaxLod");
                return false;
            }

            if (CacheMemoryMB < 32)
            {
                Debug.LogError("CacheMemoryMB must be >= 32");
                return false;
            }

            if (MaxConcurrentLoads < 1)
            {
                Debug.LogError("MaxConcurrentLoads must be >= 1");
                return false;
            }

            if (TileSizePixels < 64 || TileSizePixels > 512)
            {
                Debug.LogError("TileSizePixels must be between 64 and 512");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Get a string representation of the configuration.
        /// </summary>
        public override string ToString()
        {
            return $"TileStreamerConfig: LOD[{MinLod}-{MaxLod}] Cache[{CacheMemoryMB}MB] Loads[{MaxConcurrentLoads}] Tiles[{TileSizePixels}px {TileFileExtension}]";
        }
    }
}
