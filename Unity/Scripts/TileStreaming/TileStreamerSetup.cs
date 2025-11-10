using UnityEngine;

namespace TileStreaming
{
    /// <summary>
    /// Helper script for setting up TileStreamer in the editor.
    /// Provides utility methods and initialization helpers.
    /// </summary>
    public class TileStreamerSetup
    {
        /// <summary>
        /// Create a configured TileStreamer GameObject in the scene.
        /// </summary>
        public static GameObject CreateTileStreamerGameObject(TileStreamerConfig config = null)
        {
            var go = new GameObject("TileStreamer");
            var streamer = go.AddComponent<TileStreamer>();

            if (config != null)
            {
                if (!config.Validate())
                {
                    Debug.LogWarning("Config validation failed, using defaults");
                    config = TileStreamerConfig.CreateDefault();
                }
            }
            else
            {
                config = TileStreamerConfig.CreateDefault();
            }

            return go;
        }

        /// <summary>
        /// Setup tile streamer with custom provider.
        /// </summary>
        public static void SetupStreamerWithProvider(
            TileStreamer streamer,
            string tileBasePath,
            int minLod,
            int maxLod,
            int tileSize,
            string fileExtension,
            IMaterialHook materialHook = null)
        {
            var provider = new StreamingAssetsTileProvider(
                basePath: tileBasePath,
                minLod: minLod,
                maxLod: maxLod,
                tileSize: tileSize,
                fileExtension: fileExtension
            );

            if (materialHook == null)
            {
                materialHook = new StandardMaterialHook();
            }

            streamer.Initialize(provider, materialHook);
        }

        /// <summary>
        /// Get suggested cache memory based on platform.
        /// </summary>
        public static long GetRecommendedCacheMemoryMB()
        {
            if (Application.platform == RuntimePlatform.Android || Application.platform == RuntimePlatform.IPhonePlayer)
            {
                return 256;
            }
            else if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                return 128;
            }
            else
            {
                return 512;
            }
        }

        /// <summary>
        /// Get suggested max concurrent loads based on platform.
        /// </summary>
        public static int GetRecommendedMaxConcurrentLoads()
        {
            if (Application.platform == RuntimePlatform.Android || Application.platform == RuntimePlatform.IPhonePlayer)
            {
                return 2;
            }
            else if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                return 2;
            }
            else
            {
                return 4;
            }
        }

        /// <summary>
        /// Create a material for tile rendering with the KTX2 shader.
        /// </summary>
        public static Material CreateKTX2TileMaterial()
        {
            var shader = Shader.Find("Terrain/KTX2Tile");
            if (shader == null)
            {
                Debug.LogError("Terrain/KTX2Tile shader not found!");
                return null;
            }

            var material = new Material(shader);
            material.name = "TileMaterial_KTX2";

            material.SetColor("_Tint", Color.white);
            material.SetFloat("_Metallic", 0.0f);
            material.SetFloat("_Smoothness", 0.5f);

            return material;
        }

        /// <summary>
        /// Verify StreamingAssets directory structure.
        /// </summary>
        public static bool VerifyStreamingAssetsStructure(string basePath = "Tiles")
        {
            string streamingPath = System.IO.Path.Combine(Application.streamingAssetsPath, basePath);

            if (!System.IO.Directory.Exists(streamingPath))
            {
                Debug.LogWarning($"Tiles directory not found at: {streamingPath}");
                return false;
            }

            var lodDirs = System.IO.Directory.GetDirectories(streamingPath);
            if (lodDirs.Length == 0)
            {
                Debug.LogWarning($"No LOD directories found in: {streamingPath}");
                return false;
            }

            Debug.Log($"Found {lodDirs.Length} LOD directories in StreamingAssets");
            return true;
        }
    }
}
