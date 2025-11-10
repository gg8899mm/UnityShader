# TileStreaming System

A complete tile streaming system for Unity with LRU caching, background loading, and KTX2 shader support.

## Quick Reference

### File Descriptions

- **ITileProvider.cs** - Core interfaces and data structures
  - `ITileProvider` - Implement to create custom tile sources
  - `IMaterialHook` - Implement to customize tile application to materials
  - `TileCoord` - Web Mercator tile coordinates (X, Y, Z)
  - `TileData` - Loaded tile information

- **LRUTileCache.cs** - Memory-managed cache
  - Evicts least-recently-used tiles when budget exceeded
  - O(1) lookup, O(1) insertion
  - Tracks memory usage in bytes

- **TileStreamer.cs** - Main manager component
  - Orchestrates loading, caching, and application
  - Queue-based concurrent loading
  - Statistics tracking

- **StreamingAssetsTileProvider.cs** - Example provider
  - Loads from StreamingAssets
  - Platform-aware paths
  - Supports KTX2, PNG, JPG, and other formats

- **StandardMaterialHook.cs** - Material application
  - `StandardMaterialHook` - Basic texture application
  - `KTX2MaterialHook` - Tile metadata exposure for shaders

- **TileShader.shader** - PBR-style tile shader
  - Normal mapping
  - Metallic/smoothness
  - Tile coordinate exposure

- **TileStreamerConfig.cs** - Configuration management
  - ScriptableObject for editor configuration
  - Validation utilities
  - Parameter constraints

- **TileStreamerSetup.cs** - Setup utilities
  - Platform-aware recommendations
  - Material creation helpers
  - Directory verification

- **TileStreamerExample.cs** - Usage example
  - Scene setup demonstration
  - ROI-based tile loading
  - Input handling for debugging

## Basic Usage

```csharp
// Create and setup
var streamer = gameObject.AddComponent<TileStreamer>();
var provider = new StreamingAssetsTileProvider();
var hook = new KTX2MaterialHook();
streamer.Initialize(provider, hook);

// Request tiles
var coord = new TileCoord(32, 45, 15);
streamer.RequestTile(coord, 
    onComplete: (tile) => Debug.Log($"Loaded {tile.Coord}"),
    onError: (err) => Debug.LogError(err)
);

// Monitor
var (count, used, budget) = streamer.GetCacheStats();
Debug.Log($"Cache: {count} tiles, {used}MB / {budget}MB");
```

## StreamingAssets Structure

```
Assets/StreamingAssets/
└── Tiles/
    ├── 14/
    │   ├── 0/
    │   │   ├── 0.ktx2
    │   │   └── 1.ktx2
    │   └── 1/
    │       └── 0.ktx2
    ├── 15/
    │   └── ...
    └── ...
```

Tiles follow Web Mercator naming: `Z/X/Y.format`

## Configuration

Edit in inspector or create ScriptableObject:

```csharp
var config = TileStreamerConfig.CreateDefault();
config.MinLod = 14;
config.MaxLod = 20;
config.CacheMemoryMB = 512;
config.MaxConcurrentLoads = 4;
config.TileFileExtension = "ktx2";
```

## Performance Tips

- **Mobile**: 128-256 MB cache, 2 concurrent loads
- **Desktop**: 512+ MB cache, 4-8 concurrent loads
- **LOD r14-r16**: Global coverage, small file sizes
- **LOD r18-r20**: High detail, larger file sizes

## Custom Providers

Implement `ITileProvider`:

```csharp
public class MyProvider : ITileProvider
{
    public (int, int) GetSupportedLodRange() => (14, 20);
    public int GetTileSize() => 256;
    public IEnumerator LoadTileAsync(TileCoord coord, 
        Action<TileData> onComplete, 
        Action<string> onError)
    {
        // Custom loading logic
        yield return null;
    }
}
```

## Shader Integration

Apply tiles via material hook:

```csharp
var material = new Material(Shader.Find("Terrain/KTX2Tile"));
var tileData = new TileData(coord, texture, size);
hook.ApplyTile(material, tileData);
```

Shader properties:
- `_MainTex` - Tile texture
- `_NormalMap` - Normal map
- `_Metallic` - Metallic value [0-1]
- `_Smoothness` - Surface smoothness [0-1]
- `_Tint` - Color multiplier
- `_TileX`, `_TileY`, `_TileZ` - Tile coordinates (for debugging/LOD)

## Documentation

See `docs/terrain_pipeline/Unity_TileStreamer.md` for:
- Full API reference
- Setup instructions
- Examples and troubleshooting
- Advanced usage patterns

## License

Part of the UnityShader project.
