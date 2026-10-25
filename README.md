# BatchPlotVB（Bplot）

AutoCAD 批量打印插件（VB.NET / WinForms / ObjectARX 托管 API）。

## 功能

- **五种输出模式**：直接批量打印、打印到文件（PDF/DWF/PNG 等）、图纸拆分归档（每图框一个 DWG）、批量生成布局（含视口）、打印已有布局
- **三种图框识别**：图块（含动态块）、闭合多段线（4 顶点及首尾重合 5 顶点）、四条直线组成的矩形
- **打印顺序**：选择顺序 / 左右上下 / 上下左右，支持逆序；图上序号标注与预览卡片联动
- **输出命名模板**：`<DWGNAME>` `<INDEX>` `<CTAB>` `<PAPERNAME>` `<DATE>` `<BLOCKATTR:块:属性>` `<LAYERTEXT:图层>` 等变量，自动净化非法字符；重名支持覆盖/自动顺延/跳过
- **块-纸张映射**：按图框块名指定打印机与纸张
- **多文档批量**：侧库（side database）模式不切换文档逐文件打印，输出落到各文件源目录；布局生成模式自动打开文档处理
- 打印戳记（同步开关，当批生效）、份数/逐份打印、比例（自动取整/布满/固定）、自动旋转/反向、居中/偏移

## 环境要求

- **AutoCAD 2015 ~ 2024**（编译目标 ObjectARX 2024；2014 及以下不支持——依赖 `Editor.Command`、`DocumentCollection.Open` 等 2015+ API）
- .NET Framework 4.8

## 编译

- VS 2022+ 或 MSBuild，直接构建 `BatchPlotVB.vbproj`
- 需要引用 ObjectARX 2024 的托管 DLL（`AcMgd` / `AcDbMgd` / `AcCoreMgd`），默认路径 `C:\Autodesk\ObjectARX_for_AutoCAD_2024_Win_64bit_dlm\inc\`，位置不同请自行修改 vbproj 中的 `HintPath`
- 用旧版 ARX（如 2015~2023）的 DLL 编译也可，产物在高版本 AutoCAD 上通用

## 部署

1. 将 `BatchPlotVB.dll` 放到固定目录（如 `C:\BPLOT`）
2. 二选一：
   - **自动加载**：运行一次随附的 `changepath.exe`（写入注册表 demand-load 与受信任路径，需管理员），之后 CAD 启动自动加载
   - **手动加载**：CAD 中执行 `NETLOAD` 选择 DLL
3. 日志位于 `%APPDATA%\BatchPlotVB\Logs\`

## 命令

| 命令 | 说明 |
|---|---|
| `BPlot` / `BatchPlot` | 打开批量打印主窗口（单例） |
| `BWK` | 为选中对象创建包围框（写入当前空间） |
| `BTEST` | 逻辑自检（图框检测/排序/命名/重名/视口数学/拆分归档），报告写入 `%TEMP%\BatchPlotVB_BTEST.txt` |
| `BMPLOT` | 多文档打印链路自检（生成测试图纸并真实输出 PDF 验证路径） |
| `BSTAMP` | 打印戳记效果自检（开/关各出一张 PDF 对比） |
| `BUI` | 窗体显示自检（各窗体截图到 `%TEMP%\BatchPlotVB_BUI\`，用于检查 DPI 缩放下的布局） |

自检命令会在当前图形创建测试图元，请勿在正式图纸中运行。

## 多文档输出位置

选择"源文件路径\PDF / DWF"时，每个文件的输出落到**各自源目录**下的对应子文件夹；勾选"使用DWG名"再向下套一层以 DWG 名命名的子目录。
