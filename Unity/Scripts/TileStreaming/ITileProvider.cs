using System.Collections.Generic;
using UnityEngine;

namespace TileStreaming
{
    /// <summary>
    /// Tile coordinate in Web Mercator or standard tile grid.
    /// </summary>
    public struct TileCoord
    {
        public int X;
        public int Y;
        public int Z; // Zoom level (LOD)

        public TileCoord(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is TileCoord)) return false;
            var other = (TileCoord)obj;
            return X == other.X && Y == other.Y && Z == other.Z;
        }

        public override int GetHashCode()
        {
            return X.GetHashCode() ^ Y.GetHashCode() ^ Z.GetHashCode();
        }

        public override string ToString() => $"Tile({X},{Y},z{Z})";
    }

    /// <summary>
    /// Represents loaded tile data with texture and metadata.
    /// </summary>
    public class TileData
    {
        public TileCoord Coord;
        public Texture2D Texture;
        public long SizeInBytes;
        public System.DateTime LoadTime;

        public TileData(TileCoord coord, Texture2D texture, long sizeInBytes)
        {
            Coord = coord;
            Texture = texture;
            SizeInBytes = sizeInBytes;
            LoadTime = System.DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Tile provider interface for loading tiles from various sources.
    /// </summary>
    public interface ITileProvider
    {
        /// <summary>
        /// Load a tile asynchronously and return the tile data.
        /// </summary>
        System.Collections.IEnumerator LoadTileAsync(TileCoord coord, System.Action<TileData> onComplete, System.Action<string> onError);

        /// <summary>
        /// Get supported zoom levels (LOD range).
        /// </summary>
        (int minLod, int maxLod) GetSupportedLodRange();

        /// <summary>
        /// Get tile size in pixels.
        /// </summary>
        int GetTileSize();
    }

    /// <summary>
    /// Material hook interface for applying loaded tiles to materials.
    /// </summary>
    public interface IMaterialHook
    {
        /// <summary>
        /// Apply a loaded tile texture to the specified material.
        /// </summary>
        void ApplyTile(Material material, TileData tileData, string propertyName = "_MainTex");

        /// <summary>
        /// Clear a tile from the material.
        /// </summary>
        void ClearTile(Material material, string propertyName = "_MainTex");
    }
}
