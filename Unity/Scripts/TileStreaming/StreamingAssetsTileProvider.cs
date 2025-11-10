using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace TileStreaming
{
    /// <summary>
    /// Tile provider that loads tiles from StreamingAssets using UnityWebRequest.
    /// Supports both local files and remote URLs.
    /// </summary>
    public class StreamingAssetsTileProvider : ITileProvider
    {
        private readonly int _minLod;
        private readonly int _maxLod;
        private readonly int _tileSize;
        private readonly string _basePath;
        private readonly string _fileExtension;

        public StreamingAssetsTileProvider(
            string basePath = "Tiles",
            int minLod = 14,
            int maxLod = 20,
            int tileSize = 256,
            string fileExtension = "ktx2")
        {
            _basePath = basePath;
            _minLod = minLod;
            _maxLod = maxLod;
            _tileSize = tileSize;
            _fileExtension = fileExtension;
        }

        public (int minLod, int maxLod) GetSupportedLodRange()
        {
            return (_minLod, _maxLod);
        }

        public int GetTileSize()
        {
            return _tileSize;
        }

        public IEnumerator LoadTileAsync(TileCoord coord, System.Action<TileData> onComplete, System.Action<string> onError)
        {
            string tileUrl = GetTilePath(coord);

            using (UnityWebRequest request = UnityWebRequest.Get(tileUrl))
            {
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    byte[] tileData = request.downloadHandler.data;
                    TileData tile = CreateTileFromData(coord, tileData);

                    if (tile != null)
                    {
                        onComplete?.Invoke(tile);
                    }
                    else
                    {
                        onError?.Invoke($"Failed to create tile from data for {coord}");
                    }
                }
                else
                {
                    onError?.Invoke($"Failed to load tile {coord}: {request.error}");
                }
            }
        }

        private string GetTilePath(TileCoord coord)
        {
            string path = Path.Combine(_basePath, coord.Z.ToString(), coord.X.ToString(), $"{coord.Y}.{_fileExtension}");
            string streamingAssetsPath = Path.Combine(Application.streamingAssetsPath, path);

            if (Application.platform == RuntimePlatform.Android)
            {
                return Path.Combine("jar:file://" + Application.persistentDataPath, path).Replace("\\", "/");
            }
            else if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                return Path.Combine(Application.streamingAssetsPath, path).Replace("\\", "/");
            }
            else
            {
                return "file:///" + streamingAssetsPath.Replace("\\", "/");
            }
        }

        private TileData CreateTileFromData(TileCoord coord, byte[] data)
        {
            if (data == null || data.Length == 0)
                return null;

            Texture2D texture = new Texture2D(_tileSize, _tileSize, TextureFormat.RGBA32, false);

            try
            {
                if (_fileExtension.ToLower() == "ktx2")
                {
                    return CreateFromKTX2(coord, data, texture);
                }
                else
                {
                    return CreateFromPNG(coord, data, texture);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error creating tile texture: {ex.Message}");
                Object.Destroy(texture);
                return null;
            }
        }

        private TileData CreateFromPNG(TileCoord coord, byte[] data, Texture2D texture)
        {
            if (!texture.LoadImage(data))
            {
                return null;
            }

            texture.Apply(false);
            return new TileData(coord, texture, data.Length);
        }

        private TileData CreateFromKTX2(TileCoord coord, byte[] data, Texture2D texture)
        {
            Debug.LogWarning("KTX2 decoding requires Unity 2021.2+ or a KTX2 decoder plugin. Returning placeholder texture.");
            texture.SetPixels32(new Color32[_tileSize * _tileSize]);
            texture.Apply(false);
            return new TileData(coord, texture, data.Length);
        }
    }
}
