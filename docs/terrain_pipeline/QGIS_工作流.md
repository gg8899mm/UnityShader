# QGIS工作流指南：地形瓦片生成完整流程

## 概述

本指南为矿业数字孪生项目提供从原始地理数据到Unity可用瓦片的完整工作流。通过QGIS处理工具，您将生成：

- **基础瓦片层** (z14-z18)：覆盖42×48km矿区全景
- **高精度ROI瓦片** (z19-z20)：针对6-7个重点建筑群的精细细节
- **输出格式**：512×512 PNG文件，XYZ坐标系统，Web Mercator投影(EPSG:3857)

---

## 1. 数据下载与准备

### 1.1 所需数据类型

#### 卫星影像
- **格式**：PNG或TIF
- **覆盖范围**：至少42×48km
- **分辨率**：越高越好（z20瓦片需要≤0.5m/像素）
- **推荐来源**：
  - 天地图 (https://www.tianditu.gov.cn/)
  - 谷歌地球 (Google Earth Pro)
  - 高德地图

#### 高程数据（可选但推荐）
- **格式**：GeoTIFF (.tif)
- **分辨率**：10-30m DEM足以满足需求
- **用途**：Unity中的地形起伏
- **来源**：SRTM DEM数据、OpenTopography

### 1.2 下载步骤示例（天地图）

```
1. 访问 https://www.tianditu.gov.cn/
2. 注册免费账户
3. 选择菜单 → "数据服务" → "影像数据"
4. 在地图上框选您的矿区范围（42×48km）
5. 选择最高分辨率，导出为 GeoTIFF 格式
6. 记录下载的坐标范围和投影信息
```

### 1.3 关键参数参考表

| 缩放等级 | 地面分辨率 | 单瓦片覆盖 | 推荐用途 | z14-z18瓦片数(42km²) |
|---------|-----------|---------|--------|-------------------|
| z14 | ~67m/像素 | 42×48km | 矿区总体规划 | 1-2 |
| z15 | ~33m/像素 | 21×24km | 矿区分区 | 4-8 |
| z16 | ~17m/像素 | 10×12km | 生产区块 | 16-32 |
| z17 | ~8m/像素 | 5×6km | 工作面 | 64-128 |
| z18 | ~4m/像素 | 2.5×3km | 设备位置 | 256-512 |
| z19 | ~2m/像素 | 1.25×1.5km | 建筑群详情 | 1024-2048 |
| z20 | ~1m/像素 | 600×750m | 单体设备 | 4096-8192 |

### 1.4 文件命名规范

```
工作目录结构：
project_root/
├── raw_data/
│   ├── satellite.tif              # 原始卫星影像
│   └── dem.tif                    # 原始高程数据
├── processed/
│   ├── satellite_3857.tif         # 重投影后
│   ├── satellite_clip_3857.tif    # 裁剪后
│   └── roi_building1_3857.tif     # ROI裁剪
├── tiles_base/                    # 基础瓦片输出
└── tiles_roi_building{1-7}/       # ROI瓦片输出
```

---

## 2. QGIS环境配置

### 2.1 安装QGIS

```
下载地址：https://qgis.org/en/site/forusers/download.html
推荐版本：QGIS 3.28 LTR（长期支持版）或更新
最低要求：QGIS 3.20+
安装方式：
  Windows: 直接运行exe安装程序
  Mac: 通过DMG包安装
  Linux: apt install qgis
```

### 2.2 验证Processing工具箱

1. **打开QGIS**
2. **菜单路径**：`Processing` → `Toolbox`（或按快捷键Ctrl+Alt+T）
3. **应该看到的界面**：

```
┌─ Processing Toolbox ──────────────────┐
│                                       │
│ ○ Search...                          │
│                                       │
│ ▼ GDAL                               │ ← 需要此项
│   ▼ Raster conversion                │ ← 需要此项
│   ▼ Raster projections              │ ← 需要此项
│   ▼ Raster tools                    │ ← 需要此项
│                                       │
└───────────────────────────────────────┘
```

4. **若Processing工具箱不显示**，执行：
   - `Plugins` → `Manage and Install Plugins`
   - 搜索"Processing"，安装或启用
   - 重启QGIS

### 2.3 创建新项目并设置投影

1. **创建新项目**：`File` → `New Project`
2. **设置项目坐标系**：
   - 菜单：`Project` → `Properties`
   - 选择标签页：`CRS`
   - 搜索框输入：`EPSG:3857`
   - 选择：`WGS 84 / Pseudo-Mercator`
   - 点击`OK`

```
这一步很关键！Web Mercator (EPSG:3857) 是所有瓦片系统的标准投影。
不正确的投影会导致Unity中坐标错位。
```

3. **验证投影已设置**：
   - 窗口右下角应显示：`EPSG:3857`
   - 或 `WGS 84 / Pseudo-Mercator`

### 2.4 加载源数据

1. **打开卫星影像**：
   - `Layer` → `Add Layer` → `Add Raster Layer...`
   - 选择下载的`satellite.tif`
   - 点击`Open`

2. **打开高程数据**（可选）：
   - 重复上述步骤
   - 选择`dem.tif`

3. **检查数据加载**：
   - 左侧Layers面板应显示两个图层
   - QGIS会自动重投影到EPSG:3857进行显示

---

## 3. 重投影到Web Mercator

### 3.1 为什么需要重投影？

原始卫星影像通常使用**WGS84地理坐标系** (EPSG:4326)，单位为度。但瓦片系统需要**Web Mercator** (EPSG:3857)，单位为米。重投影步骤能将数据转换到正确的坐标系统。

### 3.2 详细步骤

1. **打开Processing工具箱**
   - 菜单：`Processing` → `Toolbox`

2. **搜索重投影工具**
   - 工具箱中输入搜索框
   - 输入：`Warp (reproject)`
   - 点击找到的工具

3. **配置参数**

```
┌─ Warp (Reproject) ─────────────────────┐
│                                        │
│ Input layer:    satellite.tif          │ ← 选择源影像
│ Target CRS:     EPSG:3857              │ ← 必须设置为Web Mercator
│ Resampling:     Bilinear               │ ← 使用双线性插值
│                                        │
│ Creation options (可选，但推荐)：      │
│   TILED=YES                            │ ← 分块存储，便于处理
│   COMPRESS=DEFLATE                     │ ← 压缩算法
│   BIGTIFF=IF_NEEDED                    │ ← 支持大文件
│                                        │
│ Output file:    satellite_3857.tif    │ ← 输出位置
│                                        │
│ [Run]                                  │
└────────────────────────────────────────┘
```

4. **执行工具**
   - 点击`Run`按钮
   - 控制台显示进度条
   - 完成后自动加载结果到地图

5. **验证结果**
   - 新图层`satellite_3857`应出现在Layers面板
   - 地图应显示投影后的影像
   - 坐标显示应为米数（例如：12000000.5, 3500000.3）而非度数

**预期耗时**：3-5分钟（取决于文件大小和PC配置）

---

## 4. 裁剪到矿区范围（42×48km）

### 4.1 准备裁剪范围

裁剪有两种方法：

#### 方法A：使用已知坐标范围（推荐）

如果您知道矿区的Web Mercator坐标范围，可以直接输入。例如：
```
西边界 (Xmin): 12000000.0
东边界 (Xmax): 12042000.0  (相差42km)
南边界 (Ymin): 3500000.0
北边界 (Ymax): 3548000.0   (相差48km)
```

#### 方法B：在QGIS中手动选择（用户友好）

1. 使用工具栏中的**矩形选择工具**
   - 或快捷键：`R`

2. 在地图上拖拽框选您的矿区
   - 可以微调边界

### 4.2 执行裁剪步骤

1. **打开Processing工具箱**
   - `Processing` → `Toolbox`

2. **搜索裁剪工具**
   - 搜索框输入：`Clip raster by extent`
   - 或按分类：`GDAL` → `Raster tools` → `Clip raster by extent`

3. **配置参数**

```
┌─ Clip Raster by Extent ──────────────┐
│                                      │
│ Input layer:    satellite_3857.tif   │ ← 选择上步的重投影结果
│                                      │
│ Clipping extent:                     │ ← 选择范围方式
│   ○ Use layer extent                 │
│   ○ Use canvas extent (推荐)         │ ← 使用当前地图范围
│   ○ Use extent from vector layer     │
│                                      │
│ 或直接输入坐标：                      │
│   Xmin, Xmax, Ymin, Ymax            │ ← 手工输入
│                                      │
│ Output file:  satellite_clip_3857.tif│ ← 输出文件名
│                                      │
│ [Run]                                │
└──────────────────────────────────────┘
```

4. **执行裁剪**
   - 点击`Run`
   - 等待完成
   - 新图层`satellite_clip_3857`加载到地图

**预期耗时**：1-2分钟

**验证**：
- 输出图像大小应约为 42×48km 范围
- 文件大小应显著小于原始数据（通常减小50-70%）

---

## 5. 生成基础瓦片（z14-z18）

### 5.1 理解XYZ瓦片系统

```
XYZ瓦片命名规范（最重要！）:
  /z/x/y.png
  
其中：
  z = 缩放级别（14-20）
  x = 东西方向的瓦片列号（从西往东递增）
  y = 南北方向的瓦片行号（从北往南递增）

注意：世界上有两种标准：
  1. XYZ格式（OSM/SlippyMap）：y从北往南递增 ← Unity需要此格式！
  2. TMS格式（Google）：y从南往北递增
  
如果选错格式，瓦片会上下翻转！
```

### 5.2 配置参数

1. **打开Processing工具箱**
   - `Processing` → `Toolbox`

2. **搜索瓦片生成工具**
   - 搜索：`Generate XYZ tiles (Directory)`
   - 路径：`GDAL` → `Raster tools` → `Generate XYZ tiles (Directory)`

3. **参数配置**（关键参数见下表）

```
┌─ Generate XYZ Tiles (Directory) ──────────┐
│                                           │
│ Input layer: satellite_clip_3857.tif      │
│ Min zoom level:          14                │
│ Max zoom level:          18                │
│ Tile width:              512               │ ← 重要！
│ Tile height:             512               │ ← 重要！
│ Resampling:              Bilinear          │
│ Output format:           PNG               │
│                                           │
│ ✓ Use XYZ tile numbering (OSM/SlippyMap)  │ ← 必须勾选！
│                                           │
│ Output directory: ./tiles_base/           │
│                                           │
│ [Run]                                     │
└───────────────────────────────────────────┘
```

### 5.3 关键参数说明

| 参数 | 值 | 解释 |
|-----|-----|------|
| **Min zoom** | 14 | 最低细节级别，覆盖全矿区 |
| **Max zoom** | 18 | 基础层最高细节，预留z19-z20给ROI |
| **Tile size** | 512×512 | 标准瓦片大小，显存友好 |
| **Resampling** | Bilinear | 双线性插值，质量与速度平衡 |
| **Format** | PNG | 无损压缩，质量好 |
| **XYZ numbering** | ☑ 勾选 | **最关键！** |

### 5.4 执行与监控

1. 点击`Run`开始生成
2. 控制台输出进度：
   ```
   Processing: Generating XYZ tiles...
   [=============>        ] 65%
   Zoom level 14: 1 tiles
   Zoom level 15: 4 tiles
   ...
   ```

3. **预期耗时**：10-30分钟（取决于数据量和并发设置）

4. **预期输出文件数量**：
   ```
   z14: 1-2个文件
   z15: 4-8个文件
   z16: 16-32个文件
   z17: 64-128个文件
   z18: 256-512个文件
   ────────────
   总计: ~450-680个PNG文件
   磁盘占用: 150-500MB
   ```

### 5.5 输出验证

生成完成后，检查目录结构：

```
tiles_base/
├── 14/
│   └── x/
│       ├── y.png
│       └── ...
├── 15/
│   └── x/
│       └── y.png
├── ...
└── 18/
    └── x/
        └── y.png
```

**验证方法**：
1. 随机打开几个z18级别的PNG文件（工具：Windows图像查看器）
2. 确保影像内容清晰正确
3. 检查文件大小：单个PNG通常 100KB-500KB

---

## 6. 生成ROI高精度瓦片（z19-z20）—— 关键流程

本步骤针对6-7个建筑群分别生成高精度瓦片。**每个建筑群执行一遍以下步骤**。

### 6.1 步骤概览

```
对每个建筑群 (建筑群1 到 建筑群7)：
│
├─ 步骤A：裁剪单个ROI (1-3km范围)
│
├─ 步骤B：生成z19-z20高精度瓦片
│
└─ 存储到：tiles_roi_building{N}/
```

### 6.2 步骤A：裁剪建筑群ROI

#### A1. 定位建筑群位置

使用Google Maps或其他地图工具：
1. 找到建筑群位置
2. 记录**经纬度范围**（例如：112.5°-112.6°, 35.2°-35.3°）

或在QGIS中：
1. 缩放到建筑群位置
2. 使用鼠标悬停读取坐标
3. 拍截图或记录范围

#### A2. 在QGIS中手动选择

1. **激活矩形选择工具**
   - 快捷键：`R`

2. **在地图上框选建筑群**
   - 包含建筑群周围1-3km缓冲
   - 保持正方形或接近正方形

3. **打开Processing工具箱**
   - `Processing` → `Toolbox`

4. **搜索裁剪工具**
   - 搜索：`Clip raster by extent`

5. **配置裁剪**

```
┌─ Clip Raster by Extent ──────────────────┐
│                                          │
│ Input layer:   satellite_clip_3857.tif   │
│                                          │
│ Clipping extent:                         │
│   ○ Use canvas extent (推荐)             │ ← 使用当前屏幕范围
│                                          │
│ Output file:  roi_building1_3857.tif     │ ← 按名称区分
│               roi_building2_3857.tif     │
│               ...                        │
│               roi_building7_3857.tif     │
│                                          │
│ [Run]                                    │
└──────────────────────────────────────────┘
```

6. **执行**
   - 点击`Run`生成ROI裁剪

**预期结果**：
```
roi_building1_3857.tif  (~50-500MB，取决于范围和分辨率)
roi_building2_3857.tif
...
roi_building7_3857.tif
```

### 6.3 步骤B：生成z19-z20高精度瓦片

对每个ROI文件重复执行：

1. **打开Processing工具箱**
   - `Processing` → `Toolbox`

2. **搜索瓦片生成工具**
   - `Generate XYZ tiles (Directory)`

3. **参数配置**

```
┌─ Generate XYZ Tiles (Directory) ──────────┐
│                                           │
│ Input layer:  roi_building1_3857.tif      │ ← 逐个处理
│                                           │
│ Min zoom level:          19                │ ← 高精度起点
│ Max zoom level:          20                │ ← 最高细节
│ Tile width:              512               │
│ Tile height:             512               │
│ Resampling:              Bilinear          │
│ Output format:           PNG               │
│                                           │
│ ✓ Use XYZ tile numbering (OSM/SlippyMap)  │ ← 必须勾选！
│                                           │
│ Output directory: ./tiles_roi_building1/  │ ← 分别输出
│                                           │
│ [Run]                                     │
└───────────────────────────────────────────┘
```

4. **重复7次**

对`roi_building2_3857.tif`到`roi_building7_3857.tif`分别执行，更改：
- Input layer
- Output directory 为：`./tiles_roi_building2/`、`./tiles_roi_building3/` ...

**预期耗时**：每个ROI 5-15分钟

**预期输出**：

```
tiles_roi_building1/
├── 19/
│   └── x/y.png (若干文件)
└── 20/
    └── x/y.png (若干文件)

tiles_roi_building2/
├── 19/...
└── 20/...

...

tiles_roi_building7/
├── 19/...
└── 20/...
```

**预期文件数量**：
```
单个ROI的z19-z20:
  z19: 8-64个文件
  z20: 32-256个文件
  单个ROI总计: 50-300 文件
  单个ROI磁盘占用: 50-300MB

7个ROI总计: 350-2100 文件，磁盘占用: 350-2100MB
```

---

## 7. 质量验证与检查

### 7.1 瓦片生成完成性检查

```bash
# Windows PowerShell 检查命令：

# 统计文件数量
(Get-ChildItem -Path "tiles_base" -Recurse -Filter "*.png").Count
(Get-ChildItem -Path "tiles_roi_building*" -Recurse -Filter "*.png").Count

# 检查目录结构完整性
tree /F tiles_base
tree /F tiles_roi_building1
```

### 7.2 手动验证影像内容

1. **打开z20级别的单个PNG文件**
   - 路径例如：`tiles_roi_building1/20/x/y.png`
   - 使用Windows图像查看器或任意图像查看工具打开
   - **应该看到清晰的建筑群卫星影像**

2. **检查z14基础瓦片**
   - 路径例如：`tiles_base/14/0/0.png`
   - 应显示整个矿区的鸟瞰图

3. **渐进式缩放验证**
   - z14 → z15 → ... → z20
   - 影像应逐级变清晰，无异常或黑块

### 7.3 常见问题与排查

#### 问题1：生成的是TMS格式而非XYZ？

**症状**：瓦片在Unity中上下翻转

**识别方法**：
- TMS格式：y坐标从下往上递增
- XYZ格式：y坐标从上往下递增
- 在QGIS中预览，z20的北边是否显示矿区北边

**解决方法**：
```
方案A（推荐）：重新执行步骤5/6，确保勾选"Use XYZ tile numbering"

方案B（如果已生成TMS）：
  使用脚本翻转y坐标：
  
  for /r "tiles_base" %%f in (*.png) do (
    REM 提取z/x/y值
    REM 重新计算y' = 2^z - 1 - y
    REM 移动文件到新位置
  )
  
  或使用GDAL工具重新生成
```

#### 问题2：某些缩放级别完全黑色？

**原因**：该级别的瓦片范围超出了数据覆盖区域

**解决**：这是正常的。Unity中会自动降级加载。

#### 问题3：文件大小异常（太大或太小）？

**正常范围**：
```
PNG文件大小参考：
  - 复杂影像（城市建筑）: 300-800 KB
  - 中等影像（矿区裸露地）: 100-300 KB
  - 单调影像（水面或草地）: 20-100 KB

若大量文件 > 1MB：可能为原始分辨率过高，运行时性能会下降
若大量文件 < 10KB：可能为数据损坏或投影错误
```

---

## 8. 为Unity准备文件结构

### 8.1 完整的StreamingAssets目录结构

创建如下目录结构（在Unity项目中）：

```
Assets/StreamingAssets/
│
├── tiles_base/                  # 基础瓦片 (z14-z18)
│   ├── 14/
│   │   ├── 1/
│   │   │   ├── 0.png
│   │   │   ├── 1.png
│   │   │   └── ...
│   │   └── ...
│   ├── 15/
│   │   └── x/y.png
│   ├── 16/
│   │   └── x/y.png
│   ├── 17/
│   │   └── x/y.png
│   └── 18/
│       └── x/y.png
│
├── tiles_roi_building1/         # ROI瓦片 (z19-z20)
│   ├── 19/
│   │   └── x/y.png
│   └── 20/
│       └── x/y.png
│
├── tiles_roi_building2/
│   ├── 19/...
│   └── 20/...
│
├── tiles_roi_building3/
│   ├── 19/...
│   └── 20/...
│
├── tiles_roi_building4/
│   ├── 19/...
│   └── 20/...
│
├── tiles_roi_building5/
│   ├── 19/...
│   └── 20/...
│
├── tiles_roi_building6/
│   ├── 19/...
│   └── 20/...
│
└── tiles_roi_building7/
    ├── 19/...
    └── 20/...
```

### 8.2 文件复制步骤（Windows）

1. **打开PowerShell**
   - 右键 → "以管理员身份运行PowerShell"

2. **导航到项目目录**
   ```powershell
   cd "C:\Users\YourName\Documents\MyUnityProject"
   ```

3. **复制tiles_base目录**
   ```powershell
   Copy-Item -Path "C:\Your\Path\tiles_base" `
             -Destination "Assets\StreamingAssets\tiles_base" `
             -Recurse -Force
   ```

4. **复制所有ROI目录**
   ```powershell
   Copy-Item -Path "C:\Your\Path\tiles_roi_building1" `
             -Destination "Assets\StreamingAssets\tiles_roi_building1" `
             -Recurse -Force
   
   # 重复复制 building2 到 building7
   Copy-Item -Path "C:\Your\Path\tiles_roi_building2" `
             -Destination "Assets\StreamingAssets\tiles_roi_building2" `
             -Recurse -Force
   # ... 以此类推
   ```

5. **验证复制完成**
   ```powershell
   Get-ChildItem -Path "Assets\StreamingAssets" -Recurse | Measure-Object
   ```

   应输出总文件数接近预期值。

### 8.3 在Unity中验证

1. **打开Unity Editor**
2. **选择Project窗口 → Assets → StreamingAssets**
3. **应看到**：
   - `tiles_base/` 文件夹
   - `tiles_roi_building1/` 到 `tiles_roi_building7/` 文件夹
   - 每个文件夹内包含z级别子文件夹

4. **不需要做任何额外配置**
   - StreamingAssets中的文件在构建时自动包含

---

## 9. 补充说明与常见术语

### 9.1 时间估算

| 步骤 | 耗时 | 备注 |
|-----|-----|------|
| 1. 数据下载 | 10-30分钟 | 取决于网速 |
| 2. QGIS配置 | 5分钟 | 一次性 |
| 3. 重投影 | 3-5分钟 | 取决于数据大小 |
| 4. 裁剪主图像 | 1-2分钟 | 快速 |
| 5. 生成z14-z18 | 10-30分钟 | 主要耗时步骤 |
| 6. 生成7个ROI的z19-z20 | 40-120分钟 | 需要运行7次 |
| **总计** | **1-3小时** | 首次完整流程 |

### 9.2 术语解释

| 术语 | 英文 | 解释 |
|-----|-----|------|
| **重投影** | Reproject | 从一个坐标系转换到另一个坐标系 |
| **Web Mercator** | | Google Maps/OSM使用的投影方式 |
| **EPSG:4326** | WGS84 | 经纬度坐标系（度数） |
| **EPSG:3857** | Web Mercator | 米制平面坐标系，适合瓦片 |
| **XYZ瓦片** | XYZ tiles | OSM标准，y坐标从北往南递增 |
| **TMS瓦片** | TMS | 坐标系相反，y从南往北递增 |
| **LOD** | Level of Detail | 缩放级别，数字越大越详细 |
| **512×512** | Tile size | 单个瓦片的像素尺寸 |
| **DEM** | Digital Elevation Model | 数字高程模型，记录地面海拔 |
| **GeoTIFF** | | 带地理坐标信息的TIFF文件 |

### 9.3 参考资源

- **QGIS官方文档**：https://qgis.org/en/docs/index.html
- **Web Mercator详解**：https://wiki.openstreetmap.org/wiki/Slippy_map_tilenames
- **GDAL处理工具**：https://gdal.org/
- **坐标系参考**：https://epsg.io/3857
- **QGIS处理工具箱**：https://docs.qgis.org/latest/en/docs/user_manual/processing/index.html

---

## 10. 流程总结与检查清单

### 快速参考清单

```
□ 第1阶段：准备
  □ 下载卫星影像 (42×48km)
  □ 下载DEM数据（可选）
  □ 安装QGIS 3.20+
  □ 验证Processing工具箱

□ 第2阶段：预处理
  □ 创建QGIS项目，设置CRS为EPSG:3857
  □ 加载卫星影像
  □ 重投影到EPSG:3857
  □ 裁剪到42×48km范围

□ 第3阶段：生成基础瓦片
  □ 运行"Generate XYZ tiles"
  □ 参数：z14-z18, 512×512, PNG, 勾选XYZ
  □ 输出到 tiles_base/
  □ 验证文件数量和内容

□ 第4阶段：生成ROI高精度瓦片（7次）
  □ [建筑群1] 裁剪ROI → 生成z19-z20
  □ [建筑群2] 裁剪ROI → 生成z19-z20
  □ [建筑群3] 裁剪ROI → 生成z19-z20
  □ [建筑群4] 裁剪ROI → 生成z19-z20
  □ [建筑群5] 裁剪ROI → 生成z19-z20
  □ [建筑群6] 裁剪ROI → 生成z19-z20
  □ [建筑群7] 裁剪ROI → 生成z19-z20

□ 第5阶段：部署到Unity
  □ 复制 tiles_base/ 到 Assets/StreamingAssets/
  □ 复制 tiles_roi_building{1-7}/ 到 Assets/StreamingAssets/
  □ 在Unity中验证文件夹结构
  □ 准备就绪！
```

---

## 下一步

完成本流程后，继续阅读：
- **PNG转KTX2.md** - 如何将PNG转换为更小的KTX2格式（可选但推荐）
- **Unity_TileStreamer集成.md** - 在Unity中加载和显示这些瓦片
- **坐标对齐与场景设置.md** - 确保坐标系统正确
