# QGIS Workflow: Tile Generation Guide

## Overview

This guide provides step-by-step instructions for generating terrain tiles from satellite imagery and DEM data using QGIS. The workflow is designed specifically for Unity's Tile Streamer system, which requires Web Mercator (EPSG:3857) tiles in XYZ format.

> **📷 Image Status**: This guide references screenshots to illustrate key steps. Image files should be placed in the `images/` subdirectory. The guide is written to be fully usable without images, but screenshots will significantly improve clarity. See `images/README.md` for required image list.

**Target Output:**
- Base tiles: zoom levels 14-18 covering 42×48km area
- High-resolution ROI tiles: zoom levels 19-20 for 6-7 building areas (1-3km each)
- Format: PNG tiles, 512×512 pixels
- Coordinate system: Web Mercator (EPSG:3857) with XYZ tile numbering

---

## 1. Data Download Preparation

### 1.1 Required Data Types

1. **Satellite Imagery** (Primary texture source)
   - Format: PNG or TIF
   - Coverage: 42×48km minimum area
   - Resolution: High enough for z20 tiles (~0.3m/pixel at ground level)
   - Sources: 天地图 (Tianditu), 奥维地图 (Aowei Map)

2. **DEM Elevation Data** (Optional, for terrain height)
   - Format: TIF (GeoTIFF)
   - Coverage: Same area as satellite imagery
   - Resolution: 10-30m DEM is sufficient for most applications

### 1.2 Download Sources

#### 天地图 (Tianditu)
```
Website: https://www.tianditu.gov.cn/
Steps:
1. Register for a free account
2. Navigate to "数据服务" → "影像数据"
3. Select your target area (42×48km rectangle)
4. Download as TIF format with highest available resolution
5. For DEM: Use "地形数据" → "DEM数据"
```

#### 奥维地图 (Aowei Map)
```
Website: https://www.auwei.com/
Steps:
1. Install Aowei Map desktop application
2. Use rectangle selection tool to mark 42×48km area
3. Export as high-resolution PNG/TIF
4. Ensure coordinates are embedded in the file
```

### 1.3 Recommended Zoom Levels

| Zoom Level | Ground Resolution | Coverage per Tile | Use Case |
|------------|-------------------|-------------------|----------|
| z14        | ~67m/pixel        | 42×48km area      | Base overview |
| z15        | ~33m/pixel        | 21×24km area      | Regional view |
| z16        | ~17m/pixel        | 10×12km area      | City level |
| z17        | ~8m/pixel         | 5×6km area        | District level |
| z18        | ~4m/pixel         | 2.5×3km area      | Neighborhood |
| z19        | ~2m/pixel         | 1.25×1.5km area   | Building clusters |
| z20        | ~1m/pixel         | 600×750m area     | Individual buildings |

### 1.4 File Naming Conventions

```
Raw data files:
- satellite_imagery.tif      (or .png)
- dem_elevation.tif

Generated tiles:
tiles_base/
├── 14/000/000.png
├── 15/000/000.png
└── ...

tiles_roi_building1/
├── 19/000/000.png
├── 20/000/000.png
└── ...
```

---

## 2. QGIS Setup & Configuration

### 2.1 Install QGIS

```
Download: https://qgis.org/en/site/forusers/download.html
Version: QGIS LTR (Long Term Release) recommended
Minimum version: QGIS 3.20+
```

### 2.2 Verify Processing Toolbox

1. Open QGIS
2. Go to **View** → **Panels** → **Processing Toolbox** (check it)
3. Processing Toolbox should appear on the right side
4. If missing: Go to **Processing** → **Options** → **Providers** → **Activate** all providers

![Processing Toolbox Location](images/qgis_processing_toolbox.png)
*Processing Toolbox should be visible in the right panel*

### 2.3 Set Project Coordinate System

1. Go to **Project** → **Properties** → **CRS** tab
2. Filter for "EPSG:3857" or search "Web Mercator"
3. Select **WGS 84 / Pseudo-Mercator (EPSG:3857)**
4. Click **OK**
5. Verify coordinate system in bottom-right corner shows "EPSG:3857"

![CRS Settings](images/qgis_crs_settings.png)
*Project Properties → CRS tab showing EPSG:3857 selection*

### 2.4 Enable On-the-Fly Reprojection

1. Go to **Settings** → **Options** → **CRS** tab
2. Check **Enable 'on-the-fly' reprojection**
3. Set **Coordinate reference system** to EPSG:3857
4. Click **Apply** → **OK**

---

## 3. Data Loading & Reprojection

### 3.1 Load Satellite Imagery

1. Drag and drop your satellite image file into QGIS canvas
2. Or use **Layer** → **Add Layer** → **Add Raster Layer...**
3. Browse to your satellite image file
4. Click **Add** → **Close**

### 3.2 Load DEM Data (Optional)

1. Repeat the same process for DEM elevation data
2. The DEM will appear as a separate layer
3. You can toggle visibility using the Layers panel

### 3.3 Verify Current CRS

1. Right-click on the loaded layer → **Properties**
2. Go to **Information** tab
3. Check **Coordinate Reference System** section
4. Note the current CRS (likely EPSG:4326 or other local CRS)

### 3.4 Reproject to Web Mercator

1. Open **Processing Toolbox** → **GDAL** → **Raster projections** → **Warp (reproject)**
2. **Input layer**: Select your satellite image
3. **Source CRS**: Auto-detected (keep as is)
4. **Target CRS**: EPSG:3857 - WGS 84 / Pseudo-Mercator
5. **Resampling method**: Bilinear
6. **No data values**: Leave as default
7. **Output file size**: Leave as default
8. **Additional creation parameters**: 
   ```
   TILED=YES
   COMPRESS=DEFLATE
   BIGTIFF=IF_NEEDED
   ```
9. **Reprojected**: Save as `satellite_webmercator.tif`
10. Click **Run**

![Warp Reproject Settings](images/qgis_warp_settings.png)
*GDAL Warp (reproject) dialog with key settings highlighted*

### 3.5 Reproject DEM (If Applicable)

1. Repeat the same warp process for DEM data
2. Use the same settings except:
   - **Resampling method**: Cubic (better for elevation data)
3. Save as `dem_webmercator.tif`

---

## 4. Clipping to Target Area

### 4.1 Determine Target Extent

For a 42×48km area in Web Mercator:

```
Example coordinates (adjust based on your location):
Min X: -8288000   (longitude ~ -74.5°)
Max X: -8246000   (longitude ~ -74.0°)
Min Y: 4962000    (latitude ~ 40.5°)
Max Y: 5004000    (latitude ~ 41.0°)
```

### 4.2 Clip Raster by Extent

1. **Processing Toolbox** → **GDAL** → **Raster extraction** → **Clip raster by extent**
2. **Input layer**: `satellite_webmercator.tif`
3. **Clipping extent**: Click **...** → **Calculate from layer** → **Use layer extent**
4. **OR manually enter coordinates**:
   - Set the exact 42×48km bounds you need
5. **Additional creation parameters**:
   ```
   TILED=YES
   COMPRESS=DEFLATE
   BIGTIFF=IF_NEEDED
   ```
6. **Clipped**: Save as `satellite_clipped.tif`
7. Click **Run**

![Clip Raster Settings](images/qgis_clip_settings.png)
*Clip raster by extent dialog with extent coordinates*

### 4.3 Verify Clipped Area

1. Load the clipped file into QGIS
2. Check that it covers your intended 42×48km area
3. Use the **Measure Line** tool to verify dimensions

---

## 5. Generate Base Tiles (z14-z18)

### 5.1 Configure XYZ Tile Generation

1. **Processing Toolbox** → **GDAL** → **Raster conversion** → **Generate XYZ tiles (Directory)**
2. **Input layer**: `satellite_clipped.tif`
3. **Zoom levels**:
   - **Min zoom**: 14
   - **Max zoom**: 18
4. **Tile size**: 512 (pixels)
5. **Output format**: PNG
6. **CRITICAL SETTINGS**:
   - ✅ **Use XYZ tile numbering (OSM/SlippyMap)** - MUST BE CHECKED
   - ✅ **Continue on errors** (optional, for robustness)
7. **Output directory**: Create and select `tiles_base` folder
8. Click **Run**

![XYZ Tiles Generation](images/qgis_xyz_tiles_settings.png)
*Generate XYZ tiles dialog with critical XYZ numbering option*

### 5.2 Expected Output Structure

```
tiles_base/
├── 14/
│   ├── 5428/
│   │   ├── 3706.png
│   │   ├── 3707.png
│   │   └── ...
│   └── 5429/
│       └── ...
├── 15/
├── 16/
├── 17/
└── 18/
```

### 5.3 File Count and Size Estimates

| Zoom Level | Approx. Tiles | File Size per Tile | Total Size |
|------------|---------------|-------------------|------------|
| z14        | ~4 tiles      | 20-50 KB          | ~200 KB    |
| z15        | ~16 tiles     | 30-80 KB          | ~1 MB      |
| z16        | ~64 tiles     | 50-120 KB         | ~6 MB      |
| z17        | ~256 tiles    | 80-200 KB         | ~40 MB     |
| z18        | ~1024 tiles   | 120-300 KB        | ~200 MB    |
| **Total**  | **~1364 tiles** | -                 | **~250 MB** |

---

## 6. Generate ROI High-Resolution Tiles (z19-z20)

### 6.1 Identify ROI Areas

For each building ROI (1-3km area):

1. Use the **Select Features by Rectangle** tool
2. Draw rectangles around each building cluster
3. Note the coordinates for each ROI
4. Typical ROI size: 1.5×2.0km for good building detail

### 6.2 ROI Processing Workflow

Repeat the following for each ROI (building1 through building7):

#### Step A: Clip to ROI Extent

1. **Processing Toolbox** → **GDAL** → **Raster extraction** → **Clip raster by extent**
2. **Input layer**: `satellite_clipped.tif`
3. **Clipping extent**: Draw rectangle around ROI or enter coordinates manually
4. **Example ROI coordinates**:
   ```
   ROI Building 1:
   Min X: -8272000
   Max X: -8269000
   Min Y: 4980000
   Max Y: 4982000
   ```
5. **Clipped**: Save as `roi_building1.tif`
6. Click **Run**

#### Step B: Generate High-Resolution Tiles

1. **Processing Toolbox** → **GDAL** → **Raster conversion** → **Generate XYZ tiles (Directory)**
2. **Input layer**: `roi_building1.tif`
3. **Zoom levels**:
   - **Min zoom**: 19
   - **Max zoom**: 20
4. **Tile size**: 512 (pixels)
5. **Output format**: PNG
6. ✅ **Use XYZ tile numbering (OSM/SlippyMap)** - MUST BE CHECKED
7. **Output directory**: Create and select `tiles_roi_building1` folder
8. Click **Run`

#### Step C: Repeat for All ROIs

Create separate directories for each ROI:
```
tiles_roi_building1/
tiles_roi_building2/
tiles_roi_building3/
tiles_roi_building4/
tiles_roi_building5/
tiles_roi_building6/
tiles_roi_building7/
```

### 6.3 ROI Tile Estimates

| ROI Size | z19 Tiles | z20 Tiles | Total per ROI | File Size |
|----------|-----------|-----------|---------------|-----------|
| 1×1km    | ~16       | ~64       | ~80 tiles     | ~15-25 MB |
| 2×2km    | ~64       | ~256      | ~320 tiles    | ~60-100 MB |
| 3×3km    | ~144      | ~576      | ~720 tiles    | ~130-200 MB |

**Expected total for all ROIs**: 300-800 MB depending on ROI sizes

---

## 7. Verification & Quality Checks

### 7.1 Check Folder Structure

Verify your output matches this structure:

```
tiles_base/
├── 14/000/000.png
├── 15/000/000.png
├── 16/000/000.png
├── 17/000/000.png
└── 18/000/000.png

tiles_roi_building1/
├── 19/000/000.png
└── 20/000/000.png

tiles_roi_building2/
├── 19/000/000.png
└── 20/000/000.png
... (continuing for all 7 ROIs)
```

### 7.2 Spot-Check Tile Contents

1. Navigate to the deepest zoom level folders (z18 for base, z20 for ROIs)
2. Open several PNG files in an image viewer
3. Verify:
   - Image content is clear and not corrupted
   - Buildings and features are visible at high zoom levels
   - Tiles align properly when viewed side-by-side
   - No black or empty tiles (unless representing water/empty areas)

### 7.3 File Size Sanity Checks

```bash
# Check total sizes (run in terminal)
du -sh tiles_base/
du -sh tiles_roi_building*/
du -sh tiles_roi_*

# Expected ranges:
# tiles_base/: 200-300 MB
# Each ROI: 50-300 MB (depending on size)
# Total project: 500-1500 MB
```

### 7.4 Tile Count Verification

```bash
# Count tiles by zoom level
find tiles_base/ -name "*.png" | grep "/14/" | wc -l
find tiles_base/ -name "*.png" | grep "/15/" | wc -l
find tiles_base/ -name "*.png" | grep "/16/" | wc -l
find tiles_base/ -name "*.png" | grep "/17/" | wc -l
find tiles_base/ -name "*.png" | grep "/18/" | wc -l

# Expected approximate counts:
# z14: 4-16 tiles
# z15: 16-64 tiles
# z16: 64-256 tiles
# z17: 256-1024 tiles
# z18: 1024-4096 tiles
```

---

## 8. File Organization for Unity

### 8.1 Final Directory Structure

Organize your files for Unity StreamingAssets:

```
YourProject/
├── Assets/
│   └── StreamingAssets/
│       ├── tiles_base/
│       │   ├── 14/
│       │   ├── 15/
│       │   ├── 16/
│       │   ├── 17/
│       │   └── 18/
│       ├── tiles_roi_building1/
│       │   ├── 19/
│       │   └── 20/
│       ├── tiles_roi_building2/
│       │   ├── 19/
│       │   └── 20/
│       └── ... (continuing for all ROIs)
```

### 8.2 Copy Tiles to Unity

```bash
# Copy all tiles to Unity StreamingAssets
cp -r tiles_base/ /path/to/your/unity/project/Assets/StreamingAssets/
cp -r tiles_roi_building*/ /path/to/your/unity/project/Assets/StreamingAssets/

# Or use file manager for drag-and-drop:
# Select all tiles_roi_* folders and tiles_base folder
# Drag them into Assets/StreamingAssets in Unity Project window
```

### 8.3 Naming Rules and Conventions

- **Folder names**: Must match exactly what your Unity code expects
- **No spaces**: Use underscores instead of spaces
- **Lowercase**: Keep all folder and file names lowercase
- **Consistent pattern**: `tiles_roi_building1`, `tiles_roi_building2`, etc.
- **Tile files**: Auto-generated by QGIS, format `z/x/y.png`

---

## 9. Common Pitfalls & Troubleshooting

### 9.1 TMS vs XYZ Coordinate Systems

**Problem**: Tiles don't align properly in Unity
**Cause**: Using TMS tile numbering instead of XYZ

**Solution**: Always ensure "Use XYZ tile numbering (OSM/SlippyMap)" is checked when generating tiles.

```
XYZ (SlippyMap):  Origin at top-left
TMS:              Origin at bottom-left

Unity Tile Streamer expects XYZ format!
```

### 9.2 Missing Tiles or Black Tiles

**Common causes and solutions**:

1. **Extent too small**: Increase clipping area slightly
2. **No data values**: Check original raster for nodata areas
3. **Memory issues**: Process smaller areas at a time
4. **File permissions**: Ensure output directory is writable

### 9.3 QGIS Performance Issues

**If QGIS becomes slow during tile generation**:

1. Close other applications
2. Use smaller clipping extents for ROI tiles
3. Reduce tile size from 512 to 256 (if acceptable)
4. Process one zoom level at a time for very large areas

### 9.4 Coordinate System Mismatches

**Symptoms**: Tiles appear in wrong location or distorted

**Checklist**:
- [ ] Project CRS set to EPSG:3857
- [ ] Input raster properly reprojected to EPSG:3857
- [ ] Clipping coordinates in Web Mercator units
- [ ] XYZ tile numbering enabled

### 9.5 Large File Sizes

**If tiles are too large**:

1. Reduce JPEG/PNG quality in export settings
2. Use smaller tile size (256 instead of 512)
3. Limit maximum zoom level (z19 instead of z20) for non-critical areas
4. Consider using KTX2 format with compression (requires additional tools)

---

## 10. Time Estimates

| Phase | Estimated Time | Notes |
|-------|----------------|-------|
| Data Download | 30-60 minutes | Depends on internet speed and source |
| QGIS Setup | 5-10 minutes | One-time setup |
| Reprojection & Clipping | 15-30 minutes | Per dataset |
| Base Tile Generation (z14-z18) | 30-90 minutes | Depends on area size |
| ROI Tile Generation (6-7 areas) | 60-180 minutes | Most time-consuming phase |
| Verification & Organization | 15-30 minutes | Quality assurance |
| **Total** | **2.5-6 hours** | For complete pipeline |

**Tips to reduce time**:
- Use SSD storage for faster I/O
- Close unnecessary applications
- Process multiple ROIs simultaneously if you have sufficient RAM
- Start with a smaller test area to verify workflow

---

## 11. Quick Reference Commands

### QGIS Processing Commands (for advanced users)

```bash
# Reproject to Web Mercator
gdalwarp -t_srs EPSG:3857 -r bilinear -co TILED=YES -co COMPRESS=DEFLATE input.tif output.tif

# Clip to extent
gdal_translate -projwin xmin ymin xmax ymax -co TILED=YES -co COMPRESS=DEFLATE input.tif clipped.tif

# Generate XYZ tiles
gdal2tiles.py -z 14-18 -w none -r average --xyz input.tif output_folder/
```

### File Management Commands

```bash
# Check tile counts
find . -name "*.png" | wc -l

# Check disk usage
du -sh *

# Verify folder structure
tree -L 3 tiles_base/
```

---

## 12. References and Further Reading

### QGIS Documentation
- [QGIS User Guide](https://docs.qgis.org/latest/en/docs/user_manual/)
- [GDAL Tools in QGIS](https://docs.qgis.org/latest/en/docs/user_manual/processing/gdal.html)

### Tile System References
- [OpenStreetMap Slippy Map Tilenames](https://wiki.openstreetmap.org/wiki/Slippy_map_tilenames)
- [Web Mercator Projection](https://en.wikipedia.org/wiki/Web_Mercator_projection)
- [Tile Grid Calculator](https://www.maptiler.com/google-maps-coordinates-tile-bounds-projection/)

### Unity Integration
- [Unity Tile Streamer Documentation](Unity_TileStreamer.md)
- [StreamingAssets in Unity](https://docs.unity3d.com/Manual/StreamingAssets.html)

---

## 13. Support

For issues specific to this workflow:
1. Check the troubleshooting section above
2. Verify QGIS version compatibility (3.20+ recommended)
3. Ensure sufficient disk space (2-3 GB minimum for full project)
4. Consult the QGIS community forums for GDAL tool issues

For Unity integration issues, refer to the Unity Tile Streamer documentation.