using UnityEngine;
using UnityEngine.Rendering;

namespace TileStreaming
{
    /// <summary>
    /// Example tile provider that uses AsyncGPUReadback for GPU-based tile processing.
    /// This is useful for GPU-accelerated texture decompression or processing.
    /// Note: Requires Unity 2018.3+
    /// </summary>
    public class AsyncGPUReadbackTileProvider : ITileProvider
    {
        private readonly int _minLod;
        private readonly int _maxLod;
        private readonly int _tileSize;
        private readonly ITileProvider _baseProvider;

        public AsyncGPUReadbackTileProvider(
            ITileProvider baseProvider,
            int minLod = 14,
            int maxLod = 20,
            int tileSize = 256)
        {
            _baseProvider = baseProvider;
            _minLod = minLod;
            _maxLod = maxLod;
            _tileSize = tileSize;
        }

        public (int minLod, int maxLod) GetSupportedLodRange()
        {
            return (_minLod, _maxLod);
        }

        public int GetTileSize()
        {
            return _tileSize;
        }

        public System.Collections.IEnumerator LoadTileAsync(
            TileCoord coord,
            System.Action<TileData> onComplete,
            System.Action<string> onError)
        {
            TileData baseTile = null;
            bool loadSuccess = false;

            yield return _baseProvider.LoadTileAsync(
                coord,
                (tile) =>
                {
                    baseTile = tile;
                    loadSuccess = true;
                },
                (error) =>
                {
                    Debug.LogError($"Base provider failed: {error}");
                    onError?.Invoke(error);
                }
            );

            if (!loadSuccess || baseTile?.Texture == null)
            {
                onError?.Invoke("Base provider returned null tile");
                yield break;
            }

            yield return ProcessTileWithAsyncGPUReadback(baseTile, onComplete, onError);
        }

        private System.Collections.IEnumerator ProcessTileWithAsyncGPUReadback(
            TileData baseTile,
            System.Action<TileData> onComplete,
            System.Action<string> onError)
        {
            Texture2D texture = baseTile.Texture;

            var request = AsyncGPUReadback.Request(texture);

            while (!request.done)
            {
                yield return null;
            }

            if (request.hasError)
            {
                Debug.LogError("AsyncGPUReadback error");
                onError?.Invoke("AsyncGPUReadback failed");
                yield break;
            }

            try
            {
                var data = request.GetData<byte>();
                if (data != null && data.Length > 0)
                {
                    var resultTile = new TileData(baseTile.Coord, texture, data.Length);
                    onComplete?.Invoke(resultTile);
                }
                else
                {
                    onError?.Invoke("No data from AsyncGPUReadback");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error processing AsyncGPUReadback: {ex.Message}");
                onError?.Invoke(ex.Message);
            }
        }
    }

    /// <summary>
    /// GPU-based tile processor using compute shaders.
    /// This is a placeholder for future GPU-accelerated processing.
    /// </summary>
    public class GPUTileProcessor
    {
        private ComputeShader _tileComputeShader;
        private int _processKernel;

        public GPUTileProcessor(ComputeShader tileComputeShader = null)
        {
            _tileComputeShader = tileComputeShader;
            if (_tileComputeShader != null)
            {
                _processKernel = _tileComputeShader.FindKernel("ProcessTile");
            }
        }

        /// <summary>
        /// Process a tile using GPU compute shader.
        /// </summary>
        public RenderTexture ProcessTileGPU(Texture2D inputTile, int tileSize)
        {
            if (_tileComputeShader == null)
            {
                Debug.LogWarning("No compute shader assigned for GPU tile processing");
                return null;
            }

            RenderTexture output = new RenderTexture(tileSize, tileSize, 0, RenderTextureFormat.ARGB32);
            output.enableRandomWrite = true;
            output.Create();

            _tileComputeShader.SetTexture(_processKernel, "InputTile", inputTile);
            _tileComputeShader.SetTexture(_processKernel, "OutputTile", output);
            _tileComputeShader.Dispatch(_processKernel, tileSize / 8, tileSize / 8, 1);

            return output;
        }
    }
}
