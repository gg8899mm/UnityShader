# Unity Tile Streamer集成指南

## 概述

本指南详细介绍如何在Unity 2023.1中使用Tile Streamer系统加载和显示由QGIS生成的矿业数字孪生地形瓦片。

**核心功能**：
- ✅ 支持z14-z20缩放级别的瓦片
- ✅ 多ROI优先级加载（建筑群1-7优先级最高）
- ✅ LRU缓存管理，自动显存优化
- ✅ 后台并发加载，不卡顿主线程
- ✅ PNG和KTX2双格式支持
- ✅ 完整的性能统计和调试信息

---

## 1. 项目准备

### 1.1 Unity版本要求

```
最低版本：Unity 2019.4 LTS
推荐版本：Unity 2023.1.0f1
渲染管线：Built-in Render Pipeline（不支持URP/HDRP）
```

### 1.2 验证StreamingAssets结构

在Unity项目中确保已有以下结构（复制自QGIS工作流）：

```
Assets/StreamingAssets/
├── tiles_base/              (z14-z18)
│   ├── 14/x/y.png
│   ├── 15/x/y.png
│   └── ...
├── tiles_roi_building1/     (z19-z20)
│   ├── 19/x/y.png
│   └── 20/x/y.png
├── tiles_roi_building2/
│   └── ...
└── tiles_roi_building7/
    └── ...
```

**如未完成数据准备**：
1. 请先阅读 **QGIS_工作流.md** 生成瓦片
2. 使用 **PNG转KTX2.md** 转换为KTX2格式（可选）

### 1.3 安装KTX2运行时库（可选）

若使用KTX2格式，需安装支持库：

#### 选项A：使用KtxUnity包（推荐）

1. **打开Package Manager**
   - Window → TextureIO → (找KtxUnity)

2. **或通过Git URL安装**
   ```
   Window → Package Manager
   点击左上角"+"按钮 → "Add package from git URL"
   
   输入：https://github.com/atteneder/KtxUnity.git
   ```

3. **等待安装完成**

#### 选项B：Unity 2021.2+ 原生支持

- Unity 2021.2+已内置基本KTX2支持
- 无需额外安装

### 1.4 导入TileStreaming脚本

1. **在Unity项目中**
   - Assets → Scripts → 创建文件夹"TileStreaming"

2. **复制以下文件到该文件夹**
   ```
   ITileProvider.cs
   LRUTileCache.cs
   TileStreamer.cs
   StreamingAssetsTileProvider.cs
   StandardMaterialHook.cs
   TileStreamerExample.cs
   TileShader.shader
   ```

3. **验证编译**
   - Unity应无编译错误
   - 控制台无红色错误信息

---

## 2. 场景设置

### 2.1 创建基础场景

1. **创建新Scene**
   - File → New Scene
   - 选择"Empty"
   - 保存为"TileStreamerScene.unity"

2. **创建显示瓦片的Quad**
   - 右键 → 3D Object → Quad
   - 命名为"TerrainQuad"
   - 设置Position (0, 0, 0)
   - 设置Scale (100, 1, 100)

3. **创建相机**
   - 右键 → 3D Object → Camera
   - 命名为"MainCamera"
   - Position: (50, 50, 50)
   - Rotation: (45, 45, 0)

### 2.2 创建材质

1. **创建Material**
   - Assets → Create → Material
   - 命名为"TerrainMaterial"

2. **配置Shader**
   - 选择Material
   - Inspector → Shader
   - 选择"Terrain/KTX2Tile"（或"Standard"）

### 2.3 设置Quad材质

1. **选择TerrainQuad**
2. **Mesh Renderer** → Material
3. **拖拽TerrainMaterial到Material插槽**

---

## 3. 脚本配置详解

### 3.1 TileStreamer组件配置

1. **在Scene中创建空GameObject**
   - 右键 → Create Empty
   - 命名为"TileStreamerManager"

2. **添加TileStreamer脚本**
   - Add Component → TileStreamer

3. **配置参数**

```
┌─ TileStreamer Component ────────────┐
│                                    │
│ Min LOD:               14           │ ← 最小缩放级别
│ Max LOD:               20           │ ← 最大缩放级别
│ Cache Memory MB:       512          │ ← 显存预算（MB）
│ Max Concurrent Loads:  4            │ ← 并发加载数
│ Use Async GPU Readback: false       │ ← 保持false
│                                    │
└────────────────────────────────────┘
```

**参数说明**：

| 参数 | 推荐值 | 说明 |
|------|------|------|
| **Min LOD** | 14 | 最低细节全覆盖 |
| **Max LOD** | 20 | 最高细节（z20） |
| **Cache Memory MB** | 256-512 | 根据显存调整 |
| **Max Concurrent Loads** | 4-8 | 平衡速度和IO |

### 3.2 TileStreamerExample脚本配置

1. **添加TileStreamerExample脚本**
   - Add Component → TileStreamerExample

2. **配置参数**

```csharp
[SerializeField] private int nearZoom = 20;      // 近景缩放级别
[SerializeField] private int midZoom = 19;       // 中景缩放级别
[SerializeField] private int farZoom = 18;       // 远景缩放级别

[SerializeField] private int nearRadius = 2;     // 近景加载半径（瓦片数）
[SerializeField] private int midRadius = 4;      // 中景加载半径
[SerializeField] private int farRadius = 8;      // 远景加载半径

[SerializeField] private Material terrainMaterial;  // 目标材质
[SerializeField] private bool useKTX2 = false;      // 是否使用KTX2格式
```

3. **在Inspector中配置**
   - Terrain Material → 拖拽"TerrainMaterial"到字段
   - useKTX2 → 若使用KTX2格式勾选(☑)

---

## 4. 多ROI优先级加载机制

### 4.1 加载优先级顺序

系统优先级查询顺序（从高到低）：

```
级别  缩放  搜索路径
─────────────────────────────────────
P1   z20   tiles_roi_building{1-7}/20/x/y
           ↓ (不存在则)
P2   z19   tiles_roi_building{1-7}/19/x/y
           ↓ (不存在则)
P3   z19   tiles_base/19/x/y
           ↓ (不存在则)
P4   z18   tiles_base/18/x/y
     ...
Pn   z14   tiles_base/14/x/y
```

### 4.2 伪代码实现

```csharp
// 核心加载逻辑
public TileData LoadTileWithFallback(TileCoord coord)
{
    // 阶段1：z19-z20优先查询ROI
    if (coord.Z >= 19)
    {
        for (int building = 1; building <= 7; building++)
        {
            string path = $"tiles_roi_building{building}/{coord.Z}/{coord.X}/{coord.Y}.png";
            if (FileExists(path))
                return LoadFromPath(path);
        }
    }
    
    // 阶段2：回退到基础瓦片
    string basePath = $"tiles_base/{coord.Z}/{coord.X}/{coord.Y}.png";
    if (FileExists(basePath))
        return LoadFromPath(basePath);
    
    // 阶段3：递归向下降级
    if (coord.Z > 14)
        return LoadTileWithFallback(new TileCoord(
            coord.X / 2,
            coord.Y / 2,
            coord.Z - 1
        ));
    
    return null;  // 无法加载
}
```

### 4.3 实现custom provider（可选）

创建`MultiROITileProvider.cs`以支持优先级加载：

```csharp
using System.Collections;
using TileStreaming;

public class MultiROITileProvider : ITileProvider
{
    private ITileProvider[] _roiProviders;  // 7个ROI provider
    private ITileProvider _baseProvider;     // 基础provider

    public MultiROITileProvider(
        ITileProvider[] roiProviders,
        ITileProvider baseProvider)
    {
        _roiProviders = roiProviders;
        _baseProvider = baseProvider;
    }

    public IEnumerator LoadTileAsync(
        TileCoord coord,
        System.Action<TileData> onComplete,
        System.Action<string> onError)
    {
        // 尝试ROI providers (z19-z20)
        if (coord.Z >= 19)
        {
            foreach (var roiProvider in _roiProviders)
            {
                bool loaded = false;
                yield return roiProvider.LoadTileAsync(
                    coord,
                    (tileData) =>
                    {
                        if (tileData != null)
                        {
                            onComplete?.Invoke(tileData);
                            loaded = true;
                        }
                    },
                    (error) => { }
                );

                if (loaded)
                    yield break;
            }
        }

        // 回退到基础provider
        yield return _baseProvider.LoadTileAsync(
            coord,
            onComplete,
            onError);
    }

    public (int minLod, int maxLod) GetSupportedLodRange() => (14, 20);
    public int GetTileSize() => 512;
}
```

---

## 5. 运行时加载逻辑

### 5.1 主要组件交互

```
┌─────────────────────────────────┐
│ TileStreamerExample (MonoBehaviour)
│ ├─ 计算相机周边的瓦片坐标
│ ├─ 按优先级请求加载
│ └─ 更新Display Material
│                                  │
│ ↓                               │
│                                  │
│ TileStreamer                     │
│ ├─ 维护加载队列                  │
│ ├─ 控制并发数量                  │
│ └─ 触发加载回调                  │
│                                  │
│ ↓                               │
│                                  │
│ StreamingAssetsTileProvider      │
│ ├─ 查询文件系统                  │
│ ├─ 使用UnityWebRequest加载       │
│ └─ 返回TileData                 │
│                                  │
│ ↓                               │
│                                  │
│ LRUTileCache                    │
│ ├─ 存储已加载瓦片                │
│ ├─ 管理显存预算                  │
│ └─ LRU驱逐算法                  │
│                                  │
│ ↓                               │
│                                  │
│ Material.SetTexture()            │
│ └─ 最终显示到屏幕                │
└─────────────────────────────────┘
```

### 5.2 Update循环中的处理

```csharp
private void Update()
{
    // 1. 获取相机位置
    Vector3 camPos = Camera.main.transform.position;
    
    // 2. 计算周边瓦片坐标（Web Mercator）
    int tileCenterX = GetTileX(camPos, nearZoom);
    int tileCenterY = GetTileY(camPos, nearZoom);
    
    // 3. 请求加载近中远三个LOD的瓦片
    LoadTilesAroundPosition(tileCenterX, tileCenterY);
    
    // 4. 更新显示统计信息
    DisplayCacheStats();
}

private void LoadTilesAroundPosition(int centerX, int centerY)
{
    // 近景(LOD20)：以相机为中心加载
    for (int x = centerX - nearRadius; x <= centerX + nearRadius; x++)
    {
        for (int y = centerY - nearRadius; y <= centerY + nearRadius; y++)
        {
            _tileStreamer.RequestTile(new TileCoord(x, y, nearZoom));
        }
    }
    
    // 中景(LOD19)：稍大范围
    int midCenterX = centerX / 2;
    int midCenterY = centerY / 2;
    for (int x = midCenterX - midRadius; x <= midCenterX + midRadius; x++)
    {
        for (int y = midCenterY - midRadius; y <= midCenterY + midRadius; y++)
        {
            _tileStreamer.RequestTile(new TileCoord(x, y, midZoom));
        }
    }
    
    // 远景(LOD18)：全覆盖
    int farCenterX = centerX / 4;
    int farCenterY = centerY / 4;
    for (int x = farCenterX - farRadius; x <= farCenterX + farRadius; x++)
    {
        for (int y = farCenterY - farRadius; y <= farCenterY + farRadius; y++)
        {
            _tileStreamer.RequestTile(new TileCoord(x, y, farZoom));
        }
    }
}
```

---

## 6. 性能参数调优

### 6.1 LOD距离调优

根据相机移动速度调整参数：

```
| 使用场景 | nearZoom | midZoom | farZoom | nearRadius | midRadius | farRadius |
|--------|----------|---------|---------|-----------|-----------|-----------|
| 静态或缓慢移动 | 20 | 19 | 18 | 2 | 3 | 5 |
| 正常飞行 | 20 | 19 | 17 | 2 | 4 | 8 |
| 高速飞行 | 19 | 18 | 16 | 2 | 4 | 8 |
| 受限硬件 | 18 | 17 | 16 | 1 | 2 | 4 |
```

### 6.2 并发加载调整

根据硬件配置和网络调整：

```csharp
// 磁盘IO充足的情况
_maxConcurrentLoads = 8;

// 标准配置
_maxConcurrentLoads = 4;

// 性能受限
_maxConcurrentLoads = 2;
```

### 6.3 显存预算配置

根据显卡VRAM调整：

```
| GPU VRAM | 推荐缓存预算 | 最大瓦片数 |
|---------|----------|---------|
| 2GB | 128 MB | 128 |
| 4GB | 256-512 MB | 256-512 |
| 6GB+ | 1024 MB+ | 1024+ |
```

计算公式：
```
最大瓦片数 ≈ (缓存预算 MB × 1024) / (单个瓦片大小 bytes)
```

例如：512MB缓存，KTX2瓦片~200KB
```
最大瓦片数 ≈ (512 × 1024) / 200 ≈ 2600瓦片
```

---

## 7. 调试与验证

### 7.1 在Editor中验证

1. **Play模式测试**
   - 按Play键启动场景
   - 使用鼠标或键盘移动相机
   - 观察瓦片是否逐级加载

2. **验证加载顺序**

   查看Console输出：
   ```
   TileStreamer initialized with LOD range: 14-20
   Tile loaded: Tile(100,200,z20)
   Tile loaded: Tile(50,100,z19)
   ...
   Cache: 128 tiles, 256MB / 512MB
   ```

3. **使用Profiler检查性能**

   - Window → Analysis → Profiler
   - 监控：GPU、Memory标签
   - 观察瓦片加载时的峰值

### 7.2 常见问题排查

#### 问题1：瓦片不显示

**排查步骤**：
```
1. 检查Material是否正确分配 → Material字段
2. 验证StreamingAssets路径 → 查看QGIS_工作流.md
3. 检查tile坐标是否有效 → Console输出
4. 确认PNG/KTX2格式匹配 → useKTX2参数
```

**调试脚本**：
```csharp
// 在TileStreamerExample.cs中添加
void Start()
{
    // 验证文件存在
    string testPath = System.IO.Path.Combine(
        Application.streamingAssetsPath,
        "tiles_base/14/1/0.png"
    );
    Debug.Log($"Test file exists: {System.IO.File.Exists(testPath)}");
}
```

#### 问题2：加载卡顿

**排查步骤**：
```
1. 减少Max Concurrent Loads
   当前值 → 当前值-1（最低2）

2. 增加Cache Memory MB
   当前值 → 当前值×1.5

3. 使用KTX2格式替代PNG
   转换参考 → PNG转KTX2.md

4. 检查磁盘IO
   使用Resource Monitor查看磁盘使用率
```

#### 问题3：显存溢出

**排查步骤**：
```
1. 减少Cache Memory MB
   当前值 → 当前值/2

2. 减少加载半径
   nearRadius: 2 → 1
   midRadius: 4 → 2
   farRadius: 8 → 4

3. 降低LOD级别
   nearZoom: 20 → 19
   farZoom: 18 → 17
```

### 7.3 使用Profiler监控

```csharp
// 在Update中添加性能监控
private void Update()
{
    // ... 加载逻辑 ...
    
    if (Input.GetKeyDown(KeyCode.Space))
    {
        var (count, usedMB, budgetMB) = _tileStreamer.GetCacheStats();
        Debug.Log($"Cache: {count} tiles, {usedMB}MB / {budgetMB}MB");
        
        // GPU内存监控
        long gpuMemory = Profiler.GetTotalReservedMemory();
        Debug.Log($"GPU Memory: {gpuMemory / 1024 / 1024}MB");
    }
}
```

---

## 8. 高级用法

### 8.1 ROI动态切换

```csharp
// 在运行时切换活跃的ROI provider
public void SwitchToROI(int buildingIndex)
{
    var roiProvider = new StreamingAssetsTileProvider(
        basePath: $"tiles_roi_building{buildingIndex}",
        minLod: 19,
        maxLod: 20,
        tileSize: 512,
        fileExtension: "png"
    );
    
    _tileStreamer.Initialize(roiProvider);
    _tileStreamer.ClearCache();
}
```

### 8.2 自定义tile provider

```csharp
public class RemoteTileProvider : ITileProvider
{
    private string _serverUrl;

    public RemoteTileProvider(string serverUrl)
    {
        _serverUrl = serverUrl;
    }

    public IEnumerator LoadTileAsync(
        TileCoord coord,
        System.Action<TileData> onComplete,
        System.Action<string> onError)
    {
        string url = $"{_serverUrl}/{coord.Z}/{coord.X}/{coord.Y}.png";

        using (UnityEngine.Networking.UnityWebRequest request = 
               UnityEngine.Networking.UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result == 
                UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                byte[] data = request.downloadHandler.data;
                Texture2D texture = new Texture2D(512, 512, 
                    TextureFormat.RGB24, false);
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

    public (int minLod, int maxLod) GetSupportedLodRange() => (14, 20);
    public int GetTileSize() => 512;
}
```

### 8.3 缓存预加载

```csharp
// 场景启动时预加载常用瓦片
IEnumerator PreloadCommonTiles()
{
    // 预加载基础瓦片z14-z16
    for (int z = 14; z <= 16; z++)
    {
        int max = 1 << z;  // 2^z
        for (int x = 0; x < max; x += 2)
        {
            for (int y = 0; y < max; y += 2)
            {
                _tileStreamer.RequestTile(new TileCoord(x, y, z));
            }
        }
        yield return new WaitForSeconds(0.1f);
    }
}
```

---

## 9. 完整集成示例

### 完整场景设置代码

```csharp
using UnityEngine;
using TileStreaming;

public class MineryTileStreamerSetup : MonoBehaviour
{
    [SerializeField] private int minLOD = 14;
    [SerializeField] private int maxLOD = 20;
    [SerializeField] private long cacheMemoryMB = 512;
    [SerializeField] private Material terrainMaterial;
    [SerializeField] private bool useKTX2 = false;

    private TileStreamer _tileStreamer;

    private void Start()
    {
        // 创建并初始化Tile Streamer
        _tileStreamer = GetComponent<TileStreamer>();
        
        // 创建文件provider
        var fileExtension = useKTX2 ? "ktx2" : "png";
        var tileProvider = new StreamingAssetsTileProvider(
            basePath: "tiles_base",
            minLod: minLOD,
            maxLod: maxLOD,
            tileSize: 512,
            fileExtension: fileExtension
        );

        // 创建材质hook
        var materialHook = new StandardMaterialHook();

        // 初始化
        _tileStreamer.Initialize(tileProvider, materialHook);
        
        Debug.Log("Tile Streamer initialized successfully!");
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            var stats = _tileStreamer.GetCacheStats();
            Debug.Log($"Cache Stats: {stats.tileCount} tiles, " +
                     $"{stats.memoryUsedMB}MB / {stats.memoryBudgetMB}MB");
        }

        if (Input.GetKeyDown(KeyCode.C))
        {
            _tileStreamer.ClearCache();
            Debug.Log("Cache cleared!");
        }
    }
}
```

---

## 10. 检查清单

```
□ 项目准备
  □ Unity 2023.1已安装
  □ Built-in Render Pipeline配置
  □ StreamingAssets文件夹已创建

□ 脚本导入
  □ TileStreaming脚本复制到项目
  □ 无编译错误
  □ KTX2库已安装（若使用KTX2）

□ 场景配置
  □ 创建Quad几何体
  □ 创建材质并分配Shader
  □ 创建TileStreamer GameObject
  □ 创建TileStreamerExample脚本

□ 运行测试
  □ Press Play启动
  □ 相机移动时看到瓦片加载
  □ Console无错误
  □ Profiler监控显存正常

□ 性能调优
  □ 根据硬件调整参数
  □ 验证帧率稳定（30+FPS）
  □ 显存占用在预算内
```

---

## 参考资源

- **Web Mercator坐标系**：https://wiki.openstreetmap.org/wiki/Slippy_map_tilenames
- **KTX2格式**：https://www.khronos.org/ktx/
- **Unity官方文档**：https://docs.unity3d.com/
- **本项目其他文档**：
  - QGIS_工作流.md
  - PNG转KTX2.md
  - 坐标对齐与场景设置.md
  - 性能优化.md
