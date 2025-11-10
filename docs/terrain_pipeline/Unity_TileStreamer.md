# Unity Tile Streamer - Setup and Usage Guide

## Overview

The Tile Streamer is a streaming system for loading terrain and imagery tiles at different LOD (Level of Detail) levels from StreamingAssets. It features:

- **Configurable LOD Levels**: Support for zoom levels r14–r20
- **LRU Cache**: Least Recently Used cache with adjustable memory budget
- **Background Loading**: Asynchronous tile loading using UnityWebRequest
- **Extensible Architecture**: Interfaces for custom tile providers and material hooks
- **KTX2 Shader Support**: Optimized shader for KTX2 texture rendering
- **Memory Management**: Automatic eviction of least recently used tiles when memory budget is exceeded

## Directory Structure

```
Unity/
├── Scripts/
│   └── TileStreaming/
│       ├── ITileProvider.cs              # Tile provider and material hook interfaces
│       ├── LRUTileCache.cs               # LRU cache implementation
│       ├── TileStreamer.cs               # Main streamer manager
│       ├── StreamingAssetsTileProvider.cs # Example provider for StreamingAssets
│       ├── StandardMaterialHook.cs       # Material hook implementations
│       ├── TileStreamerExample.cs        # Example MonoBehaviour usage
│       └── TileShader.shader             # KTX2-capable shader
```

## Quick Start

### 1. Scene Setup

1. Create a new scene or open an existing one
2. Create a Quad or other geometry that will receive the tiles
3. Assign a material using the "Terrain/KTX2Tile" shader (or standard shader)

### 2. Add TileStreamerExample to Scene

1. Create an empty GameObject in your scene
2. Add the `TileStreamerExample` script component
3. Configure the inspector properties:

```
Min LOD:                14
Max LOD:                20
Cache Memory MB:        512
Max Concurrent Loads:   4
Use KTX2 Hook:          true
Terrain Material:       (Assign your material here)
```

### 3. Prepare StreamingAssets

Create the following directory structure in your project:

```
Assets/StreamingAssets/
└── Tiles/
    ├── 14/
    │   └── x/
    │       └── y.ktx2
    ├── 15/
    │   └── x/
    │       └── y.ktx2
    └── ...
```

Tiles follow the standard Web Mercator tile naming convention:
- `Z`: Zoom level (LOD, 14-20)
- `X`: Tile column
- `Y`: Tile row

### 4. Run and Test

1. Press Play in the Unity editor
2. Press **Space** to view cache statistics (console output)
3. Press **C** to clear the cache
4. Check the console for tile loading messages

## API Reference

### TileStreamer Component

The main component managing tile streaming operations.

#### Properties

```csharp
[SerializeField]
private int _minLod = 14;              // Minimum LOD (zoom level)

[SerializeField]
private int _maxLod = 20;              // Maximum LOD (zoom level)

[SerializeField]
private long _cacheMemoryMB = 512;     // Memory budget for LRU cache

[SerializeField]
private int _maxConcurrentLoads = 4;   // Maximum concurrent tile loads
```

#### Methods

```csharp
// Initialize with tile provider and optional material hook
public void Initialize(ITileProvider tileProvider, IMaterialHook materialHook = null);

// Request a tile for loading
public void RequestTile(
    TileCoord coord,
    System.Action<TileData> onComplete = null,
    System.Action<string> onError = null
);

// Get a cached tile
public TileData GetCachedTile(TileCoord coord);

// Clear all cached tiles
public void ClearCache();

// Set cache memory budget
public void SetCacheMemoryMB(long memoryMB);

// Get cache statistics
public (int tileCount, long memoryUsedMB, long memoryBudgetMB) GetCacheStats();
```

### TileCoord Structure

Represents a tile coordinate in Web Mercator grid.

```csharp
public struct TileCoord
{
    public int X;      // Tile column
    public int Y;      // Tile row
    public int Z;      // Zoom level (LOD)
}
```

### TileData Class

Contains loaded tile information.

```csharp
public class TileData
{
    public TileCoord Coord;           // Tile coordinate
    public Texture2D Texture;         // Loaded texture
    public long SizeInBytes;          // Memory size
    public System.DateTime LoadTime;  // Load timestamp
}
```

### ITileProvider Interface

Implement custom tile providers by extending this interface.

```csharp
public interface ITileProvider
{
    // Asynchronous tile loading coroutine
    System.Collections.IEnumerator LoadTileAsync(
        TileCoord coord,
        System.Action<TileData> onComplete,
        System.Action<string> onError
    );

    // Get supported LOD range
    (int minLod, int maxLod) GetSupportedLodRange();

    // Get tile size in pixels
    int GetTileSize();
}
```

### IMaterialHook Interface

Implement custom material application logic.

```csharp
public interface IMaterialHook
{
    // Apply tile texture to material
    void ApplyTile(Material material, TileData tileData, string propertyName = "_MainTex");

    // Clear tile from material
    void ClearTile(Material material, string propertyName = "_MainTex");
}
```

### StreamingAssetsTileProvider

Loads tiles from StreamingAssets using UnityWebRequest.

```csharp
// Create provider
var provider = new StreamingAssetsTileProvider(
    basePath: "Tiles",           // Base path in StreamingAssets
    minLod: 14,                  // Minimum LOD
    maxLod: 20,                  // Maximum LOD
    tileSize: 256,               // Tile pixel size
    fileExtension: "ktx2"        // File format (ktx2, png, jpg, etc.)
);
```

### LRUTileCache

Manages tile caching with LRU eviction policy.

```csharp
var cache = new LRUTileCache(512);  // 512 MB budget

// Try to get cached tile
if (cache.TryGetTile(coord, out var tileData))
{
    // Use tileData
}

// Add tile to cache
cache.CacheTile(tileData);

// Remove specific tile
cache.RemoveTile(coord);

// Clear all
cache.Clear();

// Get stats
long memoryUsed = cache.GetCurrentMemoryUsage();
int tileCount = cache.GetCacheCount();
```

## Custom Tile Provider Example

Create a custom tile provider for remote servers:

```csharp
public class RemoteTileProvider : ITileProvider
{
    private string _baseUrl;

    public RemoteTileProvider(string baseUrl)
    {
        _baseUrl = baseUrl;
    }

    public (int minLod, int maxLod) GetSupportedLodRange()
    {
        return (14, 20);
    }

    public int GetTileSize()
    {
        return 256;
    }

    public IEnumerator LoadTileAsync(
        TileCoord coord,
        System.Action<TileData> onComplete,
        System.Action<string> onError)
    {
        string url = $"{_baseUrl}/{coord.Z}/{coord.X}/{coord.Y}.ktx2";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                byte[] data = request.downloadHandler.data;
                Texture2D texture = new Texture2D(256, 256, TextureFormat.RGBA32, false);
                texture.LoadImage(data);
                
                var tileData = new TileData(coord, texture, data.Length);
                onComplete?.Invoke(tileData);
            }
            else
            {
                onError?.Invoke(request.error);
            }
        }
    }
}
```

## Performance Considerations

### Memory Budget
- Set `_cacheMemoryMB` based on your target platform
- Mobile: 128-256 MB
- Desktop: 512-1024 MB
- High-end: 2048+ MB

### Concurrent Loads
- Increase `_maxConcurrentLoads` for faster tile streaming (default: 4)
- Higher values use more bandwidth but load tiles faster
- Recommended range: 2-8

### LOD Levels
- r14 (zoom 14): ~67 km tiles, global coverage
- r15 (zoom 15): ~33 km tiles
- r20 (zoom 20): ~1 km tiles, high detail

### File Formats
- **KTX2**: Recommended for GPU-compressed tiles, smallest file size
- **PNG/JPG**: Higher quality, larger files, better compatibility
- **DDS**: Fast loading, good compression

## Shader Usage

### Basic Setup

```csharp
var material = new Material(Shader.Find("Terrain/KTX2Tile"));

// Apply tile
var hook = new KTX2MaterialHook();
hook.ApplyTile(material, tileData);
```

### Shader Properties

```glsl
_MainTex       - Tile texture (KTX2 or PNG/JPG)
_NormalMap     - Normal map for lighting
_Metallic      - Metallic value [0-1]
_Smoothness    - Surface smoothness [0-1]
_Tint          - Color tint multiplier
_TileX         - Current tile X coordinate
_TileY         - Current tile Y coordinate
_TileZ         - Current tile Z (LOD) level
```

## Debugging

### Enable Console Logging

The system logs tile loading events to the Unity console:

```
TileStreamer initialized with LOD range: 14-20
Tile loaded: Tile(32,45,z15)
Cache: 12 tiles, 45MB / 512MB
```

### Common Issues

1. **Tiles not loading**
   - Check StreamingAssets directory structure
   - Verify file paths match tile coordinate naming
   - Check browser console for CORS errors (WebGL)

2. **Out of memory**
   - Reduce `_cacheMemoryMB`
   - Reduce `_maxConcurrentLoads`
   - Use smaller tile sizes

3. **Slow loading**
   - Increase `_maxConcurrentLoads`
   - Use compressed formats (KTX2, DDS)
   - Consider LOD prioritization based on camera distance

## Advanced Usage

### LOD Prioritization

Implement camera-aware tile loading:

```csharp
public void LoadTilesNearCamera(Camera camera, int lodLevel)
{
    Vector3 camPos = camera.transform.position;
    int tileX = GetTileX(camPos, lodLevel);
    int tileY = GetTileY(camPos, lodLevel);

    for (int x = tileX - 2; x <= tileX + 2; x++)
    {
        for (int y = tileY - 2; y <= tileY + 2; y++)
        {
            _tileStreamer.RequestTile(new TileCoord(x, y, lodLevel));
        }
    }
}
```

### Tile Update Streaming

Continuously update visible tiles:

```csharp
private void Update()
{
    LoadTilesNearCamera(Camera.main, 16);
    
    var (count, usedMB, budgetMB) = _tileStreamer.GetCacheStats();
    if (usedMB > budgetMB * 0.9f)
    {
        _tileStreamer.SetCacheMemoryMB(budgetMB + 256);
    }
}
```

## References

- Web Mercator Tile System: https://wiki.openstreetmap.org/wiki/Slippy_map_tilenames
- KTX2 Format: https://www.khronos.org/ktx/
- Unity AsyncGPUReadback: https://docs.unity3d.com/ScriptReference/Rendering.AsyncGPUReadback.html

## Support

For issues or contributions, please refer to the project repository.
