using UnityEngine;

namespace TileStreaming
{
    /// <summary>
    /// Standard material hook implementation for applying tiles to materials.
    /// Works with any shader that supports texture properties.
    /// </summary>
    public class StandardMaterialHook : IMaterialHook
    {
        private Texture2D _defaultTexture;

        public StandardMaterialHook()
        {
            _defaultTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _defaultTexture.SetPixel(0, 0, Color.gray);
            _defaultTexture.Apply();
        }

        public void ApplyTile(Material material, TileData tileData, string propertyName = "_MainTex")
        {
            if (material == null)
            {
                Debug.LogWarning("Material is null");
                return;
            }

            if (tileData?.Texture == null)
            {
                material.SetTexture(propertyName, _defaultTexture);
                return;
            }

            material.SetTexture(propertyName, tileData.Texture);
        }

        public void ClearTile(Material material, string propertyName = "_MainTex")
        {
            if (material != null && _defaultTexture != null)
            {
                material.SetTexture(propertyName, _defaultTexture);
            }
        }
    }

    /// <summary>
    /// KTX2-capable material hook for advanced shaders that support KTX2 textures.
    /// </summary>
    public class KTX2MaterialHook : IMaterialHook
    {
        private const string KTX2_SHADER_NAME = "Terrain/KTX2Tile";
        private Texture2D _defaultTexture;

        public KTX2MaterialHook()
        {
            _defaultTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _defaultTexture.SetPixel(0, 0, new Color(0.5f, 0.5f, 0.5f, 1f));
            _defaultTexture.Apply();
        }

        public void ApplyTile(Material material, TileData tileData, string propertyName = "_MainTex")
        {
            if (material == null)
            {
                Debug.LogWarning("Material is null");
                return;
            }

            if (tileData?.Texture == null)
            {
                material.SetTexture(propertyName, _defaultTexture);
                return;
            }

            material.SetTexture(propertyName, tileData.Texture);

            material.SetFloat("_TileX", tileData.Coord.X);
            material.SetFloat("_TileY", tileData.Coord.Y);
            material.SetFloat("_TileZ", tileData.Coord.Z);
        }

        public void ClearTile(Material material, string propertyName = "_MainTex")
        {
            if (material != null && _defaultTexture != null)
            {
                material.SetTexture(propertyName, _defaultTexture);
                material.SetFloat("_TileX", -1);
                material.SetFloat("_TileY", -1);
                material.SetFloat("_TileZ", -1);
            }
        }
    }
}
