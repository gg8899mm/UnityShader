# PNG转KTX2转换指南

## 概述

KTX2是现代GPU优化的纹理格式，相比PNG能显著降低文件大小（通常1/3-1/2），加快加载速度。本指南教您在Windows上使用Khronos的KTX-Software工具进行批量转换。

### 为什么要转换为KTX2？

| 对比项 | PNG | KTX2 |
|-------|-----|------|
| **文件大小** | 100% | 30-50% |
| **加载速度** | 中等 | 快速 |
| **GPU兼容** | 需解压 | 直接使用 |
| **跨平台** | 有限 | 广泛 |
| **MipMap** | 需手动 | 自动生成 |

---

## 1. 工具安装

### 1.1 下载KTX-Software

1. **访问GitHub仓库**
   ```
   https://github.com/KhronosGroup/KTX-Software/releases
   ```

2. **下载最新Windows版本**
   - 查找最新Release（例如v4.3.0+）
   - 下载文件名如：`ktx-software-4.x.x-Windows-x86_64.zip`

3. **解压到本地**
   ```
   建议位置：C:\Tools\KTX-Software
   
   解压后目录结构：
   C:\Tools\KTX-Software\
   ├── bin/
   │   ├── toktx.exe          ← 重要！PNG→KTX2转换工具
   │   ├── ktxinfo.exe
   │   └── ...
   ├── lib/
   └── ...
   ```

### 1.2 配置系统PATH环境变量

#### 步骤A：打开环境变量设置

1. **按Windows键**，搜索"环境变量"
2. **点击**"编辑系统环境变量"
3. **窗口弹出**，点击底部"环境变量"按钮

#### 步骤B：添加KTX-Software到PATH

1. **在"系统变量"区域找到**"Path"
2. **选中Path，点击"编辑"**
3. **点击"新建"**
4. **输入KTX-Software的bin目录路径**
   ```
   C:\Tools\KTX-Software\bin
   ```
5. **依次点击**：确定 → 确定 → 确定

#### 步骤C：验证安装

1. **打开PowerShell**
   - 按Windows键 + X
   - 选择"Windows PowerShell (管理员)"

2. **输入验证命令**
   ```powershell
   toktx --version
   ```

3. **应看到输出**
   ```
   toktx version 4.x.x
   ```

   如果显示"toktx：无法识别的命令"，则PATH配置未生效，**重启电脑后重试**。

---

## 2. 批量转换脚本

### 2.1 完整PowerShell脚本

创建文件 `convert_png_to_ktx2.ps1`：

```powershell
#============================================================
# PNG转KTX2批量转换脚本
# 用法：
#   .\convert_png_to_ktx2.ps1 -SourceDir "C:\path\to\tiles" -DeleteSource $false
#
# 参数：
#   -SourceDir: 包含PNG文件的源目录（递归处理所有子目录）
#   -DeleteSource: 转换后是否删除原PNG文件 (默认: $false)
#============================================================

param(
    [string]$SourceDir = (Get-Location),
    [bool]$DeleteSource = $false
)

# 验证源目录存在
if (-not (Test-Path $SourceDir)) {
    Write-Error "源目录不存在: $SourceDir"
    exit 1
}

# 验证toktx工具可用
$toktxPath = (Get-Command toktx -ErrorAction SilentlyContinue).Path
if (-not $toktxPath) {
    Write-Error "未找到toktx命令。请确保KTX-Software已安装并添加到PATH环境变量。"
    exit 1
}

Write-Host "========================================" -ForegroundColor Green
Write-Host "PNG → KTX2 批量转换工具" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "源目录: $SourceDir"
Write-Host "toktx位置: $toktxPath"
Write-Host "删除源文件: $DeleteSource"
Write-Host ""

# 统计变量
$totalFiles = 0
$successCount = 0
$failureCount = 0
$startTime = Get-Date

# 递归查找所有PNG文件
$pngFiles = Get-ChildItem -Path $SourceDir -Filter "*.png" -Recurse

if ($pngFiles.Count -eq 0) {
    Write-Host "未找到PNG文件。" -ForegroundColor Yellow
    exit 0
}

Write-Host "找到 $($pngFiles.Count) 个PNG文件" -ForegroundColor Cyan
Write-Host ""

# 处理每个PNG文件
foreach ($pngFile in $pngFiles) {
    $totalFiles++
    
    # 计算输出路径（替换扩展名）
    $outputPath = $pngFile.FullName -replace '\.png$', '.ktx2'
    $relativePath = $pngFile.FullName.Substring($SourceDir.Length + 1)
    
    Write-Host "[$totalFiles/$($pngFiles.Count)] 转换: $relativePath"
    
    # 构建toktx命令
    # 参数说明：
    #   --t2              : 输出KTX2格式
    #   --bcmp            : 使用ETC1S压缩（高压缩率，跨平台兼容）
    #   --genmipmap       : 自动生成MipMap级别
    #   --assign_oetf srgb: 使用sRGB色彩空间（适合卫星影像）
    #   --quality uastc 128: UASTC质量等级（0-255，128为默认）
    
    $arguments = @(
        "--t2",
        "--bcmp",
        "--genmipmap",
        "--assign_oetf", "srgb",
        $outputPath,
        $pngFile.FullName
    )
    
    try {
        # 执行toktx转换
        & toktx $arguments 2>&1 | Out-Null
        
        if ($LASTEXITCODE -eq 0) {
            # 获取原文件和输出文件大小
            $pngSize = "{0:N0}" -f $pngFile.Length
            $ktx2File = Get-Item $outputPath -ErrorAction SilentlyContinue
            if ($ktx2File) {
                $ktx2Size = "{0:N0}" -f $ktx2File.Length
                $ratio = [math]::Round(($ktx2File.Length / $pngFile.Length) * 100, 1)
                Write-Host "  ✓ 成功 | PNG: $pngSize B → KTX2: $ktx2Size B ($ratio%)" -ForegroundColor Green
                $successCount++
                
                # 如果启用删除选项
                if ($DeleteSource) {
                    Remove-Item $pngFile.FullName -Force
                    Write-Host "  ✓ 已删除原PNG文件" -ForegroundColor Gray
                }
            } else {
                Write-Host "  ✗ 失败: 输出文件未生成" -ForegroundColor Red
                $failureCount++
            }
        } else {
            Write-Host "  ✗ 失败: toktx返回错误代码 $LASTEXITCODE" -ForegroundColor Red
            $failureCount++
        }
    }
    catch {
        Write-Host "  ✗ 异常: $_" -ForegroundColor Red
        $failureCount++
    }
}

# 统计摘要
$endTime = Get-Date
$duration = ($endTime - $startTime).TotalSeconds

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "转换完成摘要" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host "总文件数: $totalFiles"
Write-Host "成功: $successCount" -ForegroundColor Green
Write-Host "失败: $failureCount" -ForegroundColor $(if ($failureCount -gt 0) { "Red" } else { "Green" })
Write-Host "耗时: $([math]::Round($duration, 1)) 秒"
Write-Host ""

if ($failureCount -eq 0) {
    Write-Host "✓ 所有文件转换成功！" -ForegroundColor Green
} else {
    Write-Host "⚠ 部分文件转换失败，请检查日志。" -ForegroundColor Yellow
}
```

### 2.2 脚本保存位置

```
建议位置：C:\YourProject\scripts\convert_png_to_ktx2.ps1

完整目录结构：
C:\YourProject\
├── scripts/
│   └── convert_png_to_ktx2.ps1    ← 保存脚本在这里
├── tiles_base/
├── tiles_roi_building1/
└── ...
```

---

## 3. 使用示例

### 3.1 基本用法

1. **打开PowerShell**
   - 按Windows键 + X
   - 选择"Windows PowerShell (管理员)"

2. **导航到脚本目录**
   ```powershell
   cd "C:\YourProject\scripts"
   ```

3. **启用脚本执行权限**（首次需要）
   ```powershell
   Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
   ```
   系统提示时输入：`Y` 确认

4. **运行转换脚本**

   **方案A：转换当前目录及所有子目录的PNG**
   ```powershell
   .\convert_png_to_ktx2.ps1 -SourceDir "C:\YourProject\tiles_base" -DeleteSource $false
   ```

   **方案B：转换后删除源PNG文件**（节省磁盘空间）
   ```powershell
   .\convert_png_to_ktx2.ps1 -SourceDir "C:\YourProject\tiles_base" -DeleteSource $true
   ```

   **方案C：同时转换多个目录**
   ```powershell
   .\convert_png_to_ktx2.ps1 -SourceDir "C:\YourProject\tiles_base"
   .\convert_png_to_ktx2.ps1 -SourceDir "C:\YourProject\tiles_roi_building1"
   .\convert_png_to_ktx2.ps1 -SourceDir "C:\YourProject\tiles_roi_building2"
   # ... 以此类推
   ```

### 3.2 转换进度监控

脚本运行中，PowerShell会实时输出进度：

```
========================================
PNG → KTX2 批量转换工具
========================================

源目录: C:\YourProject\tiles_base
toktx位置: C:\Tools\KTX-Software\bin\toktx.exe
删除源文件: False

找到 542 个PNG文件

[1/542] 转换: 14/1/0.png
  ✓ 成功 | PNG: 145230 B → KTX2: 52341 B (36.0%)
[2/542] 转换: 14/1/1.png
  ✓ 成功 | PNG: 167892 B → KTX2: 61234 B (36.5%)
  ✓ 已删除原PNG文件
[3/542] 转换: 15/2/3.png
  ✓ 成功 | PNG: 234567 B → KTX2: 78432 B (33.4%)
...
```

### 3.3 预期转换时间

| 文件数量 | 预期耗时 | 硬件要求 |
|--------|--------|--------|
| 100个 PNG | 1-2分钟 | 普通PC |
| 500个 PNG | 5-10分钟 | 普通PC |
| 1000个 PNG | 10-20分钟 | 普通PC |
| 2500个 PNG | 25-50分钟 | 多核CPU更快 |

---

## 4. 参数说明详解

### 4.1 toktx命令参数

```
toktx [选项] <输出文件> <输入文件>

重要参数说明：
```

| 参数 | 说明 | 推荐值 |
|------|-----|--------|
| `--t2` | 输出KTX2格式（必须） | 必须 |
| `--bcmp` | ETC1S压缩算法 | 推荐（兼容性好） |
| `--uastc` | UASTC压缩算法 | 高质量模式 |
| `--genmipmap` | 自动生成MipMap级别 | 推荐（提升远景采样） |
| `--assign_oetf srgb` | sRGB色彩空间 | 推荐（卫星影像） |
| `--assign_oetf linear` | Linear色彩空间 | 法线贴图用 |

### 4.2 压缩模式对比

#### 模式1：ETC1S压缩（推荐）

```powershell
toktx --t2 --bcmp --genmipmap --assign_oetf srgb output.ktx2 input.png
```

**优点**：
- 最小文件（通常原大小的30-40%）
- 跨平台兼容（Windows, macOS, Linux, Android, iOS）
- 加载速度快
- 显存占用小

**缺点**：
- 质量略有下降（对卫星影像基本不可见）

**适用场景**：
- 基础瓦片(z14-z18)
- ROI远景瓦片(z19)
- 对质量要求不极端的项目

#### 模式2：UASTC压缩（高质量）

```powershell
toktx --t2 --uastc --genmipmap --assign_oetf srgb output.ktx2 input.png
```

**优点**：
- 质量保留更好
- 适合要求极高的近景细节

**缺点**：
- 文件较大（通常原大小的50-70%）
- 不如ETC1S兼容

**适用场景**：
- 建筑群近景(z20)
- 高端工作站项目

### 4.3 色彩空间选择

```
--assign_oetf srgb    ← 用于：卫星影像、纹理贴图（大多数情况）
--assign_oetf linear  ← 用于：法线贴图、高程贴图
```

对于矿业数字孪生项目的卫星影像，**建议统一使用 srgb**。

---

## 5. 质量验证

### 5.1 检查转换完整性

1. **验证KTX2文件生成**
   ```powershell
   # 检查某个目录的KTX2文件数
   (Get-ChildItem -Path "tiles_base" -Recurse -Filter "*.ktx2").Count
   
   # 应等于原PNG文件数
   (Get-ChildItem -Path "tiles_base" -Recurse -Filter "*.png").Count
   ```

2. **检查文件大小**
   ```powershell
   # 计算原PNG总大小
   (Get-ChildItem -Path "tiles_base" -Recurse -Filter "*.png" | Measure-Object -Property Length -Sum).Sum / 1MB
   
   # 计算转换后KTX2总大小
   (Get-ChildItem -Path "tiles_base" -Recurse -Filter "*.ktx2" | Measure-Object -Property Length -Sum).Sum / 1MB
   ```

   **预期结果**：KTX2总大小约为PNG的30-50%

### 5.2 查看单个文件信息

使用`ktxinfo`工具查看KTX2文件元数据：

```powershell
ktxinfo "tiles_base/14/1/0.ktx2"
```

**预期输出**：
```
File: tiles_base/14/1/0.ktx2
Size: 52341 bytes

KTX v2 Information:
Index:                    0
VkFormat:                 VK_FORMAT_ETC2_R8G8B8_SRGB_BLOCK
Pixel width:              512
Pixel height:             512
Pixel depth:              0
Layer count:              1
Face count:              1
Mip level count:          10
Data Format Descriptor:   ...
```

### 5.3 转换失败排查

| 错误现象 | 可能原因 | 解决方案 |
|--------|--------|--------|
| `toktx: 无法识别的命令` | PATH未配置 | 重启电脑，重新配置PATH |
| `错误: 不支持的输入格式` | PNG文件损坏 | 检查源PNG是否完整；使用图像查看器验证 |
| `输出文件权限被拒` | 文件被占用 | 关闭图像查看器；确保无其他程序使用该文件 |
| `MipMap生成失败` | 文件过大 | 减小--genmipmap参数或检查磁盘空间 |

---

## 6. Unity中使用KTX2

### 6.1 配置StreamingAssetsTileProvider

修改Unity脚本以使用KTX2文件：

```csharp
// 在TileStreamerExample.cs中
var provider = new StreamingAssetsTileProvider(
    basePath: "tiles_base",           // 基础瓦片目录
    minLod: 14,
    maxLod: 18,
    tileSize: 512,
    fileExtension: "ktx2"             // ← 改为"ktx2"而非"png"
);

// 多ROI优先级加载
var roiProviders = new List<ITileProvider>();
for (int i = 1; i <= 7; i++)
{
    roiProviders.Add(new StreamingAssetsTileProvider(
        basePath: $"tiles_roi_building{i}",
        minLod: 19,
        maxLod: 20,
        tileSize: 512,
        fileExtension: "ktx2"
    ));
}
```

### 6.2 验证加载

1. **打开Unity Editor**
2. **在Profiler中检查**：
   - 纹理加载时间（应比PNG快30-50%）
   - 显存占用（应比PNG少）
3. **在Scene视图中视觉对比**：
   - 瓦片应清晰显示
   - 无明显质量损失（除非使用UASTC模式）

---

## 7. 完整工作流示例

### 场景：转换全部瓦片（基础 + 7个ROI）

```powershell
# 1. 打开PowerShell (管理员)

# 2. 启用脚本执行
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser

# 3. 转换基础瓦片 (z14-z18)
.\convert_png_to_ktx2.ps1 -SourceDir "C:\YourProject\tiles_base" -DeleteSource $false

# 4. 转换7个ROI瓦片
.\convert_png_to_ktx2.ps1 -SourceDir "C:\YourProject\tiles_roi_building1" -DeleteSource $false
.\convert_png_to_ktx2.ps1 -SourceDir "C:\YourProject\tiles_roi_building2" -DeleteSource $false
.\convert_png_to_ktx2.ps1 -SourceDir "C:\YourProject\tiles_roi_building3" -DeleteSource $false
.\convert_png_to_ktx2.ps1 -SourceDir "C:\YourProject\tiles_roi_building4" -DeleteSource $false
.\convert_png_to_ktx2.ps1 -SourceDir "C:\YourProject\tiles_roi_building5" -DeleteSource $false
.\convert_png_to_ktx2.ps1 -SourceDir "C:\YourProject\tiles_roi_building6" -DeleteSource $false
.\convert_png_to_ktx2.ps1 -SourceDir "C:\YourProject\tiles_roi_building7" -DeleteSource $false

# 5. 验证完成
Write-Host "全部转换完成！" -ForegroundColor Green
```

---

## 8. 常见问题解答

### Q1：能否同时保留PNG和KTX2文件？
**A**：可以。在脚本中设置 `-DeleteSource $false`（默认值）。输出时会在同一目录生成.ktx2文件，原.png文件保留。

### Q2：转换后的KTX2在Unity中看起来质量很差？
**A**：可能原因：
1. 使用了ETC1S压缩且图像对比度极高 → 改用UASTC模式
2. 色彩空间设置错误 → 检查 `--assign_oetf` 参数
3. 原PNG质量已损坏 → 验证源PNG文件完整性

### Q3：脚本运行很慢，如何加速？
**A**：
1. 使用CPU多核：toktx会自动利用多核（无需配置）
2. 使用SSD存储：HDD会成为瓶颈
3. 减少--genmipmap的深度（高级用法，不推荐）

### Q4：KTX2是否支持透明度（Alpha通道）？
**A**：支持。转换过程会自动保留PNG的Alpha通道。

### Q5：转换后的KTX2能否在其他引擎（UE、Godot）中使用？
**A**：可以。KTX2是标准格式，大多数现代引擎都支持。但需要该引擎支持KTX2运行时加载。

---

## 参考资源

- **KTX-Software GitHub**：https://github.com/KhronosGroup/KTX-Software
- **KTX2格式规范**：https://www.khronos.org/ktx/
- **Toktx工具文档**：https://github.khronos.org/KTX-Software/toktx.html
- **ETC压缩详解**：https://www.khronos.org/opengl/wiki/Texture_Compression
