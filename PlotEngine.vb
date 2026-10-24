Imports System.IO
Imports System.Linq
Imports System.Text
Imports Autodesk.AutoCAD.ApplicationServices
Imports Autodesk.AutoCAD.DatabaseServices
Imports Autodesk.AutoCAD.Geometry
Imports Autodesk.AutoCAD.PlottingServices

Public Class BatchPlotEngine
    Private _doc As Document
    Private _db As Database
    Private _logger As Logger
    Private _frameDetector As FrameDetector
    Private _configManager As ConfigManager
    Private _originalDoc As Document  ' 保存启动时的原始文档引用

    ' 纸张名称映射字典 - 本地化名称 -> 规范名称
    Private _paperNameMap As New Dictionary(Of String, String)()

    Public Sub New()
        _logger = Logger.GetInstance()
        _configManager = New ConfigManager()
        UpdateCurrentDocument()
        ' 保存原始文档引用，用于检测文档切换
        _originalDoc = _doc
        If _db IsNot Nothing Then
            _frameDetector = New FrameDetector(_db)
        End If
    End Sub

    ' 更新当前文档引用
    Public Sub UpdateCurrentDocument()
        Try
            _doc = Application.DocumentManager.MdiActiveDocument
            If _doc IsNot Nothing Then
                _db = _doc.Database
                If _frameDetector Is Nothing Then
                    _frameDetector = New FrameDetector(_db)
                End If
            End If
        Catch ex As Exception
            _logger.Error("更新当前文档失败", ex)
        End Try
    End Sub

    ''' <summary>
    ''' 验证当前文档是否有效（未被关闭或切换）
    ''' </summary>
    Public Function ValidateCurrentDocument() As Boolean
        Try
            ' 检查原始文档是否仍然有效
            If _originalDoc Is Nothing Then
                Return False
            End If

            ' 检查文档是否仍在文档集合中（只比较引用，不访问属性）
            Dim found = False
            For Each doc As Document In Application.DocumentManager
                If doc Is _originalDoc Then
                    found = True
                    Exit For
                End If
            Next

            If Not found Then
                _logger.Warning("原始文档已被关闭")
                Return False
            End If

            ' 检查当前活动文档是否与原始文档一致
            Dim currentDoc As Document = Application.DocumentManager.MdiActiveDocument
            If currentDoc IsNot _originalDoc Then
                _logger.Warning("当前活动文档已切换")
                Return False
            End If

            Return True
        Catch ex As Exception
            _logger.Error("验证文档时出错", ex)
            Return False
        End Try
    End Function

    ''' <summary>
    ''' 获取当前文档，如果无效则返回 Nothing
    ''' </summary>
    Public Function GetCurrentDocument() As Document
        If Not ValidateCurrentDocument() Then
            Return Nothing
        End If
        Return _originalDoc
    End Function

    ''' <summary>
    ''' 安全地更新文档引用，避免访问已关闭文档的属性
    ''' </summary>
    Public Sub SafeUpdateDocument()
        Try
            ' 先获取新的活动文档，不访问旧文档的任何属性
            Dim newDoc As Document = Application.DocumentManager.MdiActiveDocument
            If newDoc IsNot Nothing Then
                _doc = newDoc
                _db = newDoc.Database
                _originalDoc = newDoc
                ' 重新创建帧检测器
                _frameDetector = New FrameDetector(_db)
                _logger.Info("文档引用已安全更新")
            Else
                _logger.Warning("没有活动的AutoCAD文档")
            End If
        Catch ex As Exception
            _logger.Error("安全更新文档失败", ex)
        End Try
    End Sub

#Region "Enums and Data Classes"

    ' 打印设置类
    Public Class PlotSettings
        Public Property PlotterName As String
        Public Property PaperSize As String
        Public Property PlotStyle As String
        Public Property OutputPath As String
        Public Property IsLandscape As Boolean
        Public Property PlotArea As PlotAreaType
        Public Property Copies As Integer = 1
        Public Property PlotScale As PlotScaleType = PlotScaleType.FitToPaper
        Public Property FixedScale As Double = 100
        Public Property AutoRotate As Boolean = True
        Public Property Reverse As Boolean = False
        Public Property AutoCenter As Boolean = True
        Public Property OffsetX As Double = 0
        Public Property OffsetY As Double = 0
        Public Property NameTemplate As String = "<DWGNAME>"
        Public Property UseDWGName As Boolean = False
        Public Property DuplicateHandling As DuplicateFileHandling = DuplicateFileHandling.AutoRename
        Public Property PlotStamp As Boolean = False
        Public Property PlotStampFile As String = ""
        Public Property AutoMatchBlock As Boolean = False
        Public Property Collate As Boolean = True
        Public Property BlockNamePattern As String = ""
        Public Property TraverseLayouts As Boolean = False
        Public Property DirectPrint As Boolean = False  ' True=直接打印到物理打印机, False=打印到文件
    End Class

    Public Enum PlotAreaType
        Window
        Layout
        Extents
    End Enum

    Public Enum PlotScaleType
        AutoScale
        FitToPaper
        FixedScale
    End Enum

    Public Enum PlotOrderType
        SelectOrder
        LeftRight
        TopBottom
    End Enum

    ' 重名文件处理方式
    Public Enum DuplicateFileHandling
        Overwrite
        AutoRename
        Skip
    End Enum

    ' 图框信息类
    Public Class FrameInfo
        Public Property ObjectId As ObjectId
        Public Property RelatedObjectIds As New List(Of ObjectId)()
        Public Property BlockName As String
        Public Property LayoutName As String
        Public Property Bounds As Extents3d
        Public Property FrameNumber As String
        Public Property IsSelected As Boolean = True
        Public Property OrderIndex As Integer = 0
    End Class

#End Region

#Region "Constants"

    ' 标准比例名称列表
    Private Shared ReadOnly _standardScaleNames As String() = {"50:1", "20:1", "10:1", "5:1", "4:1", "2:1", "1:1", "1:2", "1:4", "1:5", "1:8", "1:10", "1:16", "1:20", "1:30", "1:40", "1:50", "1:100", "1:200", "1:500", "1:1000"}
    ' 标准比例值列表（与名称对应，毫米/图面单位）
    Private Shared ReadOnly _standardScaleRatios As Double() = {50.0, 20.0, 10.0, 5.0, 4.0, 2.0, 1.0, 0.5, 0.25, 0.2, 0.125, 0.1, 0.0625, 0.05, 0.033333, 0.025, 0.02, 0.01, 0.005, 0.002, 0.001}

    ' ISO A系列纸张尺寸（毫米）
    Private Const A0_WIDTH As Double = 841
    Private Const A0_HEIGHT As Double = 1189
    Private Const A1_WIDTH As Double = 594
    Private Const A1_HEIGHT As Double = 841
    Private Const A2_WIDTH As Double = 420
    Private Const A2_HEIGHT As Double = 594
    Private Const A3_WIDTH As Double = 297
    Private Const A3_HEIGHT As Double = 420
    Private Const A4_WIDTH As Double = 210
    Private Const A4_HEIGHT As Double = 297

    ' 英寸到毫米转换
    Private Const MM_PER_INCH As Double = 25.4

#End Region

#Region "Frame Detection (Delegated to FrameDetector)"

    Public Function DetectFrames(blockName As String, layerName As String) As List(Of FrameInfo)
        Return DetectFrames(blockName, layerName, Nothing)
    End Function

    Public Function DetectFrames(blockName As String, layerName As String, settings As PlotSettings) As List(Of FrameInfo)
        If _frameDetector Is Nothing Then Return New List(Of FrameInfo)()
        Return _frameDetector.DetectFramesByBlock(blockName, layerName, settings)
    End Function

    Public Function DetectFramesByPolyline(layerName As String) As List(Of FrameInfo)
        Return DetectFramesByPolyline(layerName, Nothing)
    End Function

    Public Function DetectFramesByPolyline(layerName As String, settings As PlotSettings) As List(Of FrameInfo)
        If _frameDetector Is Nothing Then Return New List(Of FrameInfo)()
        Return _frameDetector.DetectFramesByPolyline(layerName, settings)
    End Function

    Public Function DetectFramesByLine(layerName As String) As List(Of FrameInfo)
        Return DetectFramesByLine(layerName, Nothing)
    End Function

    Public Function DetectFramesByLine(layerName As String, settings As PlotSettings) As List(Of FrameInfo)
        If _frameDetector Is Nothing Then Return New List(Of FrameInfo)()
        Return _frameDetector.DetectFramesByLine(layerName, settings)
    End Function

#End Region

#Region "Scale and Paper Utilities"

    ' 计算最接近的标准比例
    Public Function CalculateBestScale(paperWidth As Double, paperHeight As Double, frameWidth As Double, frameHeight As Double, isLandscape As Boolean) As Integer
        Try
            Dim effectivePaperWidth As Double = If(isLandscape, Math.Max(paperWidth, paperHeight), Math.Min(paperWidth, paperHeight))
            Dim effectivePaperHeight As Double = If(isLandscape, Math.Min(paperWidth, paperHeight), Math.Max(paperWidth, paperHeight))
            Dim effectiveFrameWidth As Double = If(isLandscape, Math.Max(frameWidth, frameHeight), Math.Min(frameWidth, frameHeight))
            Dim effectiveFrameHeight As Double = If(isLandscape, Math.Min(frameWidth, frameHeight), Math.Max(frameWidth, frameHeight))

            Dim marginFactor = 0.95
            Dim scaleX As Double = (effectivePaperWidth * marginFactor) / effectiveFrameWidth
            Dim scaleY As Double = (effectivePaperHeight * marginFactor) / effectiveFrameHeight
            Dim requiredScale As Double = Math.Min(scaleX, scaleY)

            Dim bestIndex As Integer = -1
            Dim minDifference As Double = Double.MaxValue

            For i = 0 To _standardScaleRatios.Length - 1
                Dim difference As Double = Math.Abs(_standardScaleRatios(i) - requiredScale)
                If difference < minDifference Then
                    minDifference = difference
                    bestIndex = i
                End If
            Next

            Return bestIndex
        Catch ex As Exception
            _logger.Error("计算最佳比例失败", ex)
            Return -1
        End Try
    End Function

    ' 从纸张名称解析纸张尺寸（毫米）
    Public Function ParsePaperSize(paperName As String) As Tuple(Of Double, Double)
        Try
            Dim width As Double = A4_WIDTH
            Dim height As Double = A4_HEIGHT

            If String.IsNullOrEmpty(paperName) Then
                Return Tuple.Create(width, height)
            End If

            Dim upperName As String = paperName.ToUpperInvariant()

            If upperName.Contains("A0") Then
                width = A0_WIDTH : height = A0_HEIGHT
            ElseIf upperName.Contains("A1") Then
                width = A1_WIDTH : height = A1_HEIGHT
            ElseIf upperName.Contains("A2") Then
                width = A2_WIDTH : height = A2_HEIGHT
            ElseIf upperName.Contains("A3") Then
                width = A3_WIDTH : height = A3_HEIGHT
            ElseIf upperName.Contains("A4") Then
                width = A4_WIDTH : height = A4_HEIGHT
            ElseIf upperName.Contains("ANSI A") OrElse upperName.Contains("A (8.5") Then
                width = 8.5 * MM_PER_INCH : height = 11 * MM_PER_INCH
            ElseIf upperName.Contains("ANSI B") OrElse upperName.Contains("B (11") Then
                width = 11 * MM_PER_INCH : height = 17 * MM_PER_INCH
            ElseIf upperName.Contains("ANSI C") OrElse upperName.Contains("C (17") Then
                width = 17 * MM_PER_INCH : height = 22 * MM_PER_INCH
            ElseIf upperName.Contains("ANSI D") OrElse upperName.Contains("D (22") Then
                width = 22 * MM_PER_INCH : height = 34 * MM_PER_INCH
            ElseIf upperName.Contains("ANSI E") OrElse upperName.Contains("E (34") Then
                width = 34 * MM_PER_INCH : height = 44 * MM_PER_INCH
            End If

            Return Tuple.Create(width, height)
        Catch ex As Exception
            _logger.Error("解析纸张尺寸失败", ex)
            Return Tuple.Create(A4_WIDTH, A4_HEIGHT)
        End Try
    End Function

#End Region

#Region "Plotter and Paper Lists"

    ' 获取系统默认打印机名称
    Public Function GetSystemDefaultPlotter() As String
        Try
            Using tr As Transaction = _db.TransactionManager.StartTransaction()
                Dim psv As PlotSettingsValidator = PlotSettingsValidator.Current
                Dim deviceList As Specialized.StringCollection = psv.GetPlotDeviceList()
                If deviceList.Count > 0 Then
                    Dim layoutMgr As LayoutManager = LayoutManager.Current
                    Dim layoutId As ObjectId = layoutMgr.GetLayoutId(layoutMgr.CurrentLayout)
                    If Not layoutId.IsNull Then
                        Dim layout = CType(tr.GetObject(layoutId, OpenMode.ForRead), Layout)
                        Dim plotSettings As New Autodesk.AutoCAD.DatabaseServices.PlotSettings(layout.ModelType)
                        plotSettings.CopyFrom(layout)
                        Return plotSettings.PlotConfigurationName
                    End If
                End If
                tr.Commit()
            End Using
        Catch ex As Exception
            _logger.Error("获取默认打印机失败", ex)
        End Try
        Return String.Empty
    End Function

    ' 获取默认纸张名称
    Public Function GetDefaultPaper(plotterName As String) As String
        Try
            If String.IsNullOrEmpty(plotterName) Then Return String.Empty
            Using tr As Transaction = _db.TransactionManager.StartTransaction()
                Dim psv As PlotSettingsValidator = PlotSettingsValidator.Current
                Dim ps As New Autodesk.AutoCAD.DatabaseServices.PlotSettings(True)
                psv.SetPlotConfigurationName(ps, plotterName, Nothing)
                Dim mediaList As Specialized.StringCollection = psv.GetCanonicalMediaNameList(ps)
                If mediaList.Count > 0 Then
                    Return psv.GetLocaleMediaName(ps, 0)
                End If
                tr.Commit()
            End Using
        Catch ex As Exception
            _logger.Error("获取默认纸张失败", ex)
        End Try
        Return String.Empty
    End Function

    ' 获取打印机列表
    Public Function GetPlotterList() As List(Of String)
        Dim plotters As New List(Of String)()
        Try
            Dim psv As PlotSettingsValidator = PlotSettingsValidator.Current
            Dim deviceList As Specialized.StringCollection = psv.GetPlotDeviceList()

            For Each device As String In deviceList
                plotters.Add(device)
            Next

            If plotters.Count = 0 Then
                plotters.Add("无")
            End If
        Catch ex As Exception
            _logger.Error("获取打印机列表失败", ex)
            plotters.Add("无")
        End Try
        Return plotters
    End Function

    ' 获取纸张列表
    Public Function GetPaperList(plotterName As String) As List(Of String)
        Dim papers As New List(Of String)()
        _paperNameMap.Clear()

        Try
            If String.IsNullOrEmpty(plotterName) OrElse plotterName = "None" OrElse plotterName = "无" Then
                Return GetDefaultPaperList()
            End If

            Using tr As Transaction = _db.TransactionManager.StartTransaction()
                Dim psv As PlotSettingsValidator = PlotSettingsValidator.Current
                Dim ps As New Autodesk.AutoCAD.DatabaseServices.PlotSettings(True)
                psv.SetPlotConfigurationName(ps, plotterName, Nothing)

                Dim mediaList As Specialized.StringCollection = psv.GetCanonicalMediaNameList(ps)

                For i = 0 To mediaList.Count - 1
                    Dim canonicalName As String = mediaList(i)
                    Dim localeName As String = psv.GetLocaleMediaName(ps, i)
                    papers.Add(localeName)
                    _paperNameMap(localeName) = canonicalName
                Next

                tr.Commit()
            End Using

            If papers.Count = 0 Then
                Return GetDefaultPaperList()
            End If
        Catch ex As Exception
            _logger.Error("获取纸张列表失败", ex)
            Return GetDefaultPaperList()
        End Try
        Return papers
    End Function

    Private Function GetDefaultPaperList() As List(Of String)
        Return New List(Of String) From {
            "ANSI A (8.50 x 11.00 英寸)",
            "ANSI B (11.00 x 17.00 英寸)",
            "ANSI C (17.00 x 22.00 英寸)",
            "ANSI D (22.00 x 34.00 英寸)",
            "ANSI E (34.00 x 44.00 英寸)",
            "ISO A4 (210.00 x 297.00 毫米)",
            "ISO A3 (297.00 x 420.00 毫米)",
            "ISO A2 (420.00 x 594.00 毫米)",
            "ISO A1 (594.00 x 841.00 毫米)",
            "ISO A0 (841.00 x 1189.00 毫米)"
        }
    End Function

    ' 获取规范纸张名称
    Public Function GetCanonicalPaperName(localeName As String) As String
        If _paperNameMap.ContainsKey(localeName) Then
            Return _paperNameMap(localeName)
        End If
        Return localeName
    End Function

    ' 将 UI 纸张名解析为当前打印机可用的规范介质名
    ' 名称映射命中且在设备介质列表中则直接使用；否则按物理尺寸就近匹配（每边容差3毫米）
    ' 设备介质列表缓存：介质目录只由 PC3/PMP 决定，同一设备会话内不变，避免每个图框重复枚举
    Private ReadOnly _mediaListCache As New Dictionary(Of String, System.Collections.Specialized.StringCollection)(StringComparer.OrdinalIgnoreCase)

    Public Function ResolveMediaName(plotSettings As Autodesk.AutoCAD.DatabaseServices.PlotSettings, paperName As String) As String
        Dim canonical As String = GetCanonicalPaperName(paperName)

        ' 查询当前设备介质列表（要求设备已先挂到 plotSettings 上），优先取会话内缓存
        Dim deviceKey As String = If(plotSettings.PlotConfigurationName, String.Empty)
        Dim mediaNames As System.Collections.Specialized.StringCollection = Nothing
        If deviceKey.Length > 0 Then
            _mediaListCache.TryGetValue(deviceKey, mediaNames)
        End If
        If mediaNames Is Nothing Then
            Try
                mediaNames = PlotSettingsValidator.Current.GetCanonicalMediaNameList(plotSettings)
                If deviceKey.Length > 0 AndAlso mediaNames IsNot Nothing Then
                    _mediaListCache(deviceKey) = mediaNames
                End If
            Catch ex As Exception
                _logger.Warning($"获取介质列表失败，使用原纸张名: {ex.Message}")
                Return canonical
            End Try
        End If

        ' 名称有效（在介质列表中）则直接使用
        If mediaNames IsNot Nothing Then
            For i = 0 To mediaNames.Count - 1
                If String.Equals(mediaNames(i), canonical, StringComparison.OrdinalIgnoreCase) Then
                    Return canonical
                End If
            Next
        End If

        ' 名称无效：让 AutoCAD 按物理尺寸就近选纸，再读回尺寸校验误差（长短边合计容差6毫米）
        Try
            If mediaNames Is Nothing OrElse mediaNames.Count = 0 Then
                Return canonical
            End If

            Dim dims = ParsePaperSize(paperName)
            PlotSettingsValidator.Current.SetClosestMediaName(plotSettings, dims.Item1, dims.Item2, PlotPaperUnit.Millimeters, True)

            Dim bestName As String = plotSettings.CanonicalMediaName
            Dim ps As Point2d = plotSettings.PlotPaperSize
            Dim err As Double = Math.Abs(Math.Max(ps.X, ps.Y) - Math.Max(dims.Item1, dims.Item2)) + Math.Abs(Math.Min(ps.X, ps.Y) - Math.Min(dims.Item1, dims.Item2))

            If bestName IsNot Nothing AndAlso err <= 6.0 Then
                _logger.Info($"纸张 '{paperName}' 在当前打印机上不存在，按尺寸匹配为: {bestName}")
                Return bestName
            End If
            _logger.Warning($"纸张 '{paperName}' 无法按尺寸匹配（最接近 {bestName}，误差 {err:F1} 毫米），使用原名称")
        Catch ex As Exception
            _logger.Warning($"按尺寸匹配纸张失败: {ex.Message}")
        End Try

        ' 兜底：原样返回，交给 AutoCAD 处理
        Return canonical
    End Function

    ' 获取打印样式列表
    ' 按图纸打印样式模式过滤：颜色相关(CTB)图纸列 .ctb，命名打印样式(STB)图纸列 .stb（带扩展名）
    Public Function GetPlotStyleList() As List(Of String)
        Dim styles As New List(Of String)()
        Try
            Dim foundPath As String = AutoCADPathResolver.GetPlotStylesPath()
            Dim pattern As String = If(_db.PlotStyleMode, "*.stb", "*.ctb")

            If Not String.IsNullOrEmpty(foundPath) AndAlso Directory.Exists(foundPath) Then
                For Each file As String In Directory.GetFiles(foundPath, pattern)
                    If _db.PlotStyleMode Then
                        ' 命名样式需带扩展名传给 SetCurrentStyleSheet
                        styles.Add(Path.GetFileName(file))
                    Else
                        styles.Add(Path.GetFileNameWithoutExtension(file))
                    End If
                Next
            End If

            ' 兜底列表仅适用于颜色相关(CTB)模式；STB 模式找不到样式时返回空，由设置点警告
            If styles.Count = 0 AndAlso Not _db.PlotStyleMode Then
                styles.AddRange(New String() {"acad.ctb", "grayscale.ctb", "monochrome.ctb", "Screening 100%.ctb", "Screening 75%.ctb", "Screening 50%.ctb", "Screening 25%.ctb"})
            End If
        Catch ex As Exception
            _logger.Error("获取打印样式列表失败", ex)
        End Try
        Return styles
    End Function

    ' 获取布局列表
    Public Function GetLayoutList() As List(Of String)
        Dim layouts As New List(Of String)()
        Try
            Using tr As Transaction = _db.TransactionManager.StartTransaction()
                Dim layoutDict = CType(tr.GetObject(_db.LayoutDictionaryId, OpenMode.ForRead), DBDictionary)
                For Each entry As DBDictionaryEntry In layoutDict
                    If entry.Key <> "Model" Then
                        layouts.Add(entry.Key)
                    End If
                Next
                tr.Commit()
            End Using
        Catch ex As Exception
            _logger.Error("获取布局列表失败", ex)
        End Try
        Return layouts
    End Function

#End Region

#Region "Plot Operations"

    ' 设置系统变量并回读校验：设置未生效/还原未成功时给出明确警告。
    ' BACKGROUNDPLOT 未生效会退化为异步打印，打印后的输出文件落盘校验可能误报失败
    Private Function SetSystemVariableChecked(name As String, value As Object) As Boolean
        Application.SetSystemVariable(name, value)
        Dim current As Object = Nothing
        Try
            current = Application.GetSystemVariable(name)
        Catch
        End Try
        If current Is Nothing OrElse CStr(current) <> CStr(value) Then
            _logger.Warning($"系统变量 {name} 设置后回读不一致（期望 {value}，实际 {If(current, "未知")}），可能未生效")
            Return False
        End If
        Return True
    End Function

    ' 执行批量打印 - 统一使用-PLOT命令方式
    Public Sub ExecuteBatchPlot(frames As List(Of FrameInfo), settings As PlotSettings, progressCallback As Action(Of Integer, String))
        ' 所有打印方式统一使用-PLOT命令
        ' DirectPrint=True: 直接打印到物理打印机
        ' DirectPrint=False: 打印到文件(PDF/DWF等)
        PlotToDeviceWithCopies(frames, settings, progressCallback)
    End Sub


    ' 执行批量打印任务，支持直接打印和打印到文件
    ' 使用PlotEngine后台打印方式，每个图框作为独立打印任务
    Private Sub PlotToDeviceWithCopies(frames As List(Of FrameInfo), settings As PlotSettings, progressCallback As Action(Of Integer, String))
        ' 保存原始系统变量值
        Dim oldBackgroundPlot As Object = Nothing

        Try
            If frames Is Nothing OrElse frames.Count = 0 Then
                Throw New Exception("没有要打印的图框")
            End If

            ' 禁用后台打印，避免 eBackgroundPlotInProgress 错误
            Try
                oldBackgroundPlot = Application.GetSystemVariable("BACKGROUNDPLOT")
                If SetSystemVariableChecked("BACKGROUNDPLOT", 0) Then
                    _logger.Info("已禁用后台打印 (BACKGROUNDPLOT = 0)")
                End If
            Catch ex As Exception
                _logger.Warning($"设置 BACKGROUNDPLOT 失败: {ex.Message}")
            End Try

            Dim selectedFrames = frames.Where(Function(f) f.IsSelected).ToList()
            Dim totalFrames As Integer = selectedFrames.Count
            Dim totalTasks As Integer = totalFrames * settings.Copies
            Dim currentTask = 0

            ' 准备输出路径和文件名（仅用于打印到文件模式）
            Dim outputPath As String = Nothing
            Dim outputFileName As String = Nothing

            If Not settings.DirectPrint Then
                ' 打印到文件模式才需要准备输出路径
                outputPath = settings.OutputPath
                If String.IsNullOrEmpty(outputPath) Then
                    outputPath = Path.GetDirectoryName(_doc.Name)
                End If
                If Not Directory.Exists(outputPath) Then
                    Directory.CreateDirectory(outputPath)
                End If

                ' 根据设置生成文件名
                If settings.UseDWGName OrElse settings.NameTemplate = "<DWGNAME>" Then
                    ' 使用DWG文件名
                    outputFileName = Path.GetFileNameWithoutExtension(_doc.Name)
                ElseIf Not String.IsNullOrEmpty(settings.NameTemplate) Then
                    ' 使用名称样板（在循环中会替换 <CTAB> 和 <INDEX>）
                    outputFileName = settings.NameTemplate
                Else
                    outputFileName = "BatchPlot_Output"
                End If
            End If

            Dim printMode As String = If(settings.DirectPrint, "直接打印", "打印到文件")
            progressCallback?.Invoke(0, $"开始{printMode} {totalFrames} 个图框，份数: {settings.Copies}，逐份打印: {settings.Collate}...")

            ' 根据设置启用或禁用打印戳记
            Try
                Using lock As DocumentLock = _doc.LockDocument()
                    Dim plotStampScript As String
                    If settings.PlotStamp Then
                        plotStampScript = "_-PLOTSTAMP" & vbCr & "On" & vbCr & vbCr
                        _logger.Info("启用打印戳记功能")
                    Else
                        plotStampScript = "_-PLOTSTAMP" & vbCr & "Off" & vbCr & vbCr
                        _logger.Info("禁用打印戳记功能")
                    End If
                    _doc.SendStringToExecute(plotStampScript, True, False, False)
                    Threading.Thread.Sleep(500)
                End Using
            Catch ex As Exception
                _logger.Warning($"设置打印戳记状态失败: {ex.Message}")
            End Try

            If settings.Collate Then
                ' 逐份打印：外层循环份数，内层循环图框
                For copy = 1 To settings.Copies
                    For i = 0 To totalFrames - 1
                        currentTask += 1
                        Dim frame As FrameInfo = selectedFrames(i)
                        progressCallback?.Invoke(CInt(currentTask / totalTasks * 100),
                            $"逐份打印 第{copy}/{settings.Copies}份 - 图框 {i + 1}/{totalFrames}...")

                        ' 构建输出文件路径（如果是打印到文件模式）
                        Dim outputFilePath As String = Nothing
                        If Not settings.DirectPrint Then
                            ' 应用名称样板生成文件名
                            Dim fileName As String = FormatFileName(outputFileName, frame.FrameNumber, currentTask, frame, settings.PaperSize)
                            ' 添加份数标识（如果份数大于1）
                            If settings.Copies > 1 Then
                                fileName = $"{fileName}_Copy{copy}"
                            End If
                            Dim tempFilePath As String = BuildOutputPath(outputPath, fileName, settings.PlotterName)
                            ' 处理重名文件（跳过模式命中返回空串，必须跳过，不得以直接打印模式执行）
                            outputFilePath = HandleDuplicateFile(tempFilePath, settings.DuplicateHandling)
                            If String.IsNullOrEmpty(outputFilePath) Then
                                _logger.Info($"图框 {frame.FrameNumber}: 输出文件已存在，跳过")
                                Continue For
                            End If
                        End If

                        ' 为单个图框执行独立打印任务
                        PrintSingleFrame(frame, settings, i + 1, totalFrames, copy, settings.Copies, outputFilePath)

                        _logger.Info($"图框 {frame.FrameNumber} 第{copy}份: 独立打印任务完成")
                    Next
                Next
            Else
                ' 非逐份打印：外层循环图框，内层循环份数
                For i = 0 To totalFrames - 1
                    Dim frame As FrameInfo = selectedFrames(i)

                    For copy = 1 To settings.Copies
                        currentTask += 1
                        progressCallback?.Invoke(CInt(currentTask / totalTasks * 100),
                            $"图框 {i + 1}/{totalFrames} - 第{copy}/{settings.Copies}份...")

                        ' 构建输出文件路径（如果是打印到文件模式）
                        Dim outputFilePath As String = Nothing
                        If Not settings.DirectPrint Then
                            ' 应用名称样板生成文件名
                            Dim fileName As String = FormatFileName(outputFileName, frame.FrameNumber, currentTask, frame, settings.PaperSize)
                            ' 添加份数标识（如果份数大于1）
                            If settings.Copies > 1 Then
                                fileName = $"{fileName}_Copy{copy}"
                            End If
                            Dim tempFilePath As String = BuildOutputPath(outputPath, fileName, settings.PlotterName)
                            ' 处理重名文件（跳过模式命中返回空串，必须跳过，不得以直接打印模式执行）
                            outputFilePath = HandleDuplicateFile(tempFilePath, settings.DuplicateHandling)
                            If String.IsNullOrEmpty(outputFilePath) Then
                                _logger.Info($"图框 {frame.FrameNumber}: 输出文件已存在，跳过")
                                Continue For
                            End If
                        End If

                        ' 为单个图框执行独立打印任务
                        PrintSingleFrame(frame, settings, i + 1, totalFrames, copy, settings.Copies, outputFilePath)

                        _logger.Info($"图框 {frame.FrameNumber} 第{copy}份: 独立打印任务完成")
                    Next
                Next
            End If

            progressCallback?.Invoke(100, $"打印完成，共打印 {totalFrames} 个图框，{settings.Copies} 份")
        Catch ex As Exception
            _logger.Error("打印任务失败", ex)
            Throw New Exception($"打印任务失败: {ex.Message}")
        Finally
            ' 恢复后台打印设置
            Try
                If oldBackgroundPlot IsNot Nothing Then
                    If SetSystemVariableChecked("BACKGROUNDPLOT", oldBackgroundPlot) Then
                        _logger.Info($"已恢复后台打印设置 (BACKGROUNDPLOT = {oldBackgroundPlot})")
                    End If
                End If
            Catch ex As Exception
                _logger.Warning($"恢复 BACKGROUNDPLOT 失败: {ex.Message}")
            End Try
        End Try
    End Sub

    ' 为单个图框执行独立打印任务 - 使用PlotEngine后台打印
    ' 支持直接打印到物理打印机或打印到文件，无对话框
    Private Sub PrintSingleFrame(frame As FrameInfo, settings As PlotSettings, frameIndex As Integer, totalFrames As Integer,
                                  Optional currentCopy As Integer = 1, Optional totalCopies As Integer = 1,
                                  Optional outputFilePath As String = Nothing)

        Try
            Dim printMode As String = If(String.IsNullOrEmpty(outputFilePath), "直接打印", $"打印到文件: {outputFilePath}")
            _logger.Info($"PrintSingleFrame开始: 图框{frame.FrameNumber} 第{currentCopy}/{totalCopies}份, {printMode}")

            ' 检查是否有打印正在进行
            If PlotFactory.ProcessPlotState <> ProcessPlotState.NotPlotting Then
                _logger.Warning("检测到打印引擎正在使用中，等待...")
                ' 等待最多10秒
                Dim waitCount = 0
                Do While PlotFactory.ProcessPlotState <> ProcessPlotState.NotPlotting AndAlso waitCount < 100
                    Threading.Thread.Sleep(100)
                    waitCount += 1
                Loop

                If PlotFactory.ProcessPlotState <> ProcessPlotState.NotPlotting Then
                    Throw New Exception("打印引擎忙，无法开始新的打印任务")
                End If
            End If

            ' 使用PlotEngine进行后台打印
            Using lock As DocumentLock = _doc.LockDocument()
                Using tr As Transaction = _db.TransactionManager.StartTransaction()
                    Dim layoutMgr As LayoutManager = LayoutManager.Current
                    ' 使用图框所在的布局
                    Dim layoutId As ObjectId = layoutMgr.GetLayoutId(frame.LayoutName)
                    Dim layout = CType(tr.GetObject(layoutId, OpenMode.ForRead), Layout)
                    Dim layoutName As String = layout.LayoutName

                    ' 切换到图框所在的布局，否则PlotInfoValidator会报eInvalidPlotInfo
                    If layoutMgr.CurrentLayout <> layoutName Then
                        layoutMgr.CurrentLayout = layoutName
                    End If

                    ' 创建PlotSettings并配置
                    Dim ps As New Autodesk.AutoCAD.DatabaseServices.PlotSettings(layout.ModelType)
                    ps.CopyFrom(layout)
                    ConfigurePlotSettings(ps, frame, settings)

                    ' 创建PlotInfo
                    Dim pi As New PlotInfo()
                    pi.Layout = layoutId
                    pi.OverrideSettings = ps

                    ' 验证打印信息
                    Using piv As New PlotInfoValidator()
                        piv.MediaMatchingPolicy = MatchingPolicy.MatchEnabled
                        Try
                            piv.Validate(pi)
                        Catch ex As Exception
                            _logger.Warning($"PlotInfo验证警告: {ex.Message}")
                        End Try
                    End Using

                    ' 执行打印（异常时自动逐级清理引擎状态）
                    RunPlotEngineSafely(pi, layoutName, outputFilePath)

                    ps.Dispose()
                    pi.Dispose()

                    tr.Commit()
                End Using
            End Using

            ' 打印到文件时校验输出文件已真实生成（BACKGROUNDPLOT=0 保证同步落盘）
            If Not String.IsNullOrEmpty(outputFilePath) AndAlso Not File.Exists(outputFilePath) Then
                Throw New Exception($"打印任务已执行但未找到输出文件: {outputFilePath}")
            End If

            _logger.Info($"图框 {frame.FrameNumber} 第{currentCopy}/{totalCopies}份: 打印完成")

        Catch ex As Exception
            _logger.Error($"打印图框 {frame.FrameNumber} 第{currentCopy}/{totalCopies}份失败", ex)
            Throw
        End Try
    End Sub

    ' 安全执行单页打印任务
    ' 异常时按状态逐级收尾（EndGenerateGraphics/EndPage/EndDocument/EndPlot），
    ' 防止打印引擎残留非 NotPlotting 状态导致后续任务全部卡在"引擎忙"
    Private Sub RunPlotEngineSafely(pi As PlotInfo, layoutName As String, outputFilePath As String)
        Using pe As PlotEngine = PlotFactory.CreatePublishEngine()
            Dim plotStarted As Boolean = False
            Dim documentStarted As Boolean = False
            Dim pageStarted As Boolean = False
            Dim graphicsStarted As Boolean = False

            Try
                pe.BeginPlot(Nothing, Nothing)
                plotStarted = True

                If Not String.IsNullOrEmpty(outputFilePath) Then
                    ' 打印到文件
                    pe.BeginDocument(pi, layoutName, Nothing, 1, True, outputFilePath)
                Else
                    ' 直接打印到设备
                    pe.BeginDocument(pi, layoutName, Nothing, 1, False, Nothing)
                End If
                documentStarted = True

                Dim pageInfo As New PlotPageInfo()
                pe.BeginPage(pageInfo, pi, True, Nothing)
                pageStarted = True
                pe.BeginGenerateGraphics(Nothing)
                graphicsStarted = True
                pe.EndGenerateGraphics(Nothing)
                graphicsStarted = False
                pe.EndPage(Nothing)
                pageStarted = False
                pe.EndDocument(Nothing)
                documentStarted = False
                pe.EndPlot(Nothing)
                plotStarted = False
            Finally
                If graphicsStarted Then
                    Try
                        pe.EndGenerateGraphics(Nothing)
                    Catch
                    End Try
                End If
                If pageStarted Then
                    Try
                        pe.EndPage(Nothing)
                    Catch
                    End Try
                End If
                If documentStarted Then
                    Try
                        pe.EndDocument(Nothing)
                    Catch
                    End Try
                End If
                If plotStarted Then
                    Try
                        pe.EndPlot(Nothing)
                    Catch
                    End Try
                End If
            End Try
        End Using
    End Sub

#End Region

#Region "Helper Methods"

    ' 构建输出文件路径 - 根据打印机名自动确定格式
    Private Function BuildOutputPath(outputPath As String, fileName As String, plotterName As String) As String
        ' 根据打印机名确定输出格式
        Dim extension As String = ".plt"  ' 默认为 PLT 格式

        If Not String.IsNullOrEmpty(plotterName) Then
            Dim lowerName As String = plotterName.ToLower()

            If lowerName.Contains("pdf") Then
                extension = ".pdf"
            ElseIf lowerName.Contains("dwfx") Then
                extension = ".dwfx"
            ElseIf lowerName.Contains("dwf") Then
                extension = ".dwf"
            ElseIf lowerName.Contains("png") Then
                extension = ".png"
            ElseIf lowerName.Contains("jpg") OrElse lowerName.Contains("jpeg") Then
                extension = ".jpg"
            ElseIf lowerName.Contains("tiff") OrElse lowerName.Contains("tif") Then
                extension = ".tif"
            ElseIf lowerName.Contains("bmp") Then
                extension = ".bmp"
            End If
        End If

        Return Path.Combine(outputPath, fileName & extension)
    End Function

#End Region

#Region "Preview"

    ' 预览打印
    Public Sub PreviewPlot(frame As FrameInfo, settings As PlotSettings)
        Try
            Using lock As DocumentLock = _doc.LockDocument()
                Using tr As Transaction = _db.TransactionManager.StartTransaction()
                    Dim layoutMgr As LayoutManager = LayoutManager.Current
                    ' 使用图框所在的布局，而不是当前布局
                    Dim layoutId As ObjectId = layoutMgr.GetLayoutId(frame.LayoutName)
                    Dim layout = CType(tr.GetObject(layoutId, OpenMode.ForWrite), Layout)
                    Dim layoutName As String = layout.LayoutName

                    ' 切换到图框所在的布局，否则PlotInfoValidator会报eInvalidPlotInfo
                    If layoutMgr.CurrentLayout <> layoutName Then
                        layoutMgr.CurrentLayout = layoutName
                    End If

                    ' 创建PlotSettings并配置
                    Dim ps As New Autodesk.AutoCAD.DatabaseServices.PlotSettings(layout.ModelType)
                    ps.CopyFrom(layout)
                    ConfigurePlotSettings(ps, frame, settings)

                    ' 注意：不要将预览的页面设置应用到共享布局
                    ' 这会导致多个预览之间的冲突

                    ' 创建PlotInfo
                    Dim pi As New PlotInfo()
                    pi.Layout = layoutId
                    pi.OverrideSettings = ps

                    ' 验证打印信息
                    Using piv As New PlotInfoValidator()
                        piv.MediaMatchingPolicy = MatchingPolicy.MatchEnabled
                        Try
                            piv.Validate(pi)
                        Catch ex As Exception
                            _logger.Warning($"PlotInfo验证警告: {ex.Message}")
                        End Try
                    End Using

                    ' 执行预览 - 在事务内执行
                    Using pe As PlotEngine = PlotFactory.CreatePreviewEngine(CInt(PreviewEngineFlags.Plot))
                        pe.BeginPlot(Nothing, Nothing)
                        pe.BeginDocument(pi, layoutName, Nothing, 1, False, Nothing)
                        Dim pageInfo As New PlotPageInfo()
                        pe.BeginPage(pageInfo, pi, True, Nothing)
                        pe.BeginGenerateGraphics(Nothing)
                        pe.EndGenerateGraphics(Nothing)
                        pe.EndPage(Nothing)
                        pe.EndDocument(Nothing)
                        pe.EndPlot(Nothing)
                    End Using

                    ps.Dispose()
                    pi.Dispose()

                    tr.Commit()
                End Using
            End Using
        Catch ex As Exception
            _logger.Error($"预览图框 {frame.FrameNumber} 失败", ex)
            Throw
        End Try
    End Sub

    Private Sub ConfigurePlotSettings(plotSettings As Autodesk.AutoCAD.DatabaseServices.PlotSettings, frame As FrameInfo, settings As PlotSettings)
        Dim psv As PlotSettingsValidator = PlotSettingsValidator.Current

        ' 先设置打印类型和窗口区域
        Try
            Dim windowArea As New Extents2d(frame.Bounds.MinPoint.X, frame.Bounds.MinPoint.Y, frame.Bounds.MaxPoint.X, frame.Bounds.MaxPoint.Y)
            psv.SetPlotWindowArea(plotSettings, windowArea)
            psv.SetPlotType(plotSettings, Autodesk.AutoCAD.DatabaseServices.PlotType.Window)
        Catch ex As Exception
            _logger.Warning("设置打印窗口失败: " & ex.Message)
        End Try

        ' 设置打印机和纸张（必须在打印比例之前设置，但会重置比例、打印类型和旋转）
        Try
            ' 先只挂设备，便于查询该设备的真实介质列表
            psv.SetPlotConfigurationName(plotSettings, settings.PlotterName, Nothing)

            ' 名称映射未命中时按物理尺寸就近匹配介质
            Dim canonicalPaperName As String = ResolveMediaName(plotSettings, settings.PaperSize)
            psv.SetPlotConfigurationName(plotSettings, settings.PlotterName, canonicalPaperName)

            ' 刷新设置缓存，防止沿用旧设备的介质快照
            psv.RefreshLists(plotSettings)
        Catch ex As Exception
            _logger.Warning("设置打印机失败: " & ex.Message)
        End Try

        ' 栅格设备（PNG/JPG）纸张单位必须用像素，毫米单位会抛 eInvalidInput
        Try
            Dim cfgName As String = If(plotSettings.PlotConfigurationName, String.Empty).ToUpperInvariant()
            If cfgName.Contains("PNG") OrElse cfgName.Contains("JPG") OrElse cfgName.Contains("JPEG") Then
                psv.SetPlotPaperUnits(plotSettings, PlotPaperUnit.Pixels)
            Else
                psv.SetPlotPaperUnits(plotSettings, PlotPaperUnit.Millimeters)
            End If
        Catch ex As Exception
            _logger.Warning("设置纸张单位失败: " & ex.Message)
        End Try

        ' 重新设置打印类型为窗口（SetPlotConfigurationName可能会重置它）
        Try
            psv.SetPlotType(plotSettings, Autodesk.AutoCAD.DatabaseServices.PlotType.Window)
            Dim windowArea As New Extents2d(frame.Bounds.MinPoint.X, frame.Bounds.MinPoint.Y, frame.Bounds.MaxPoint.X, frame.Bounds.MaxPoint.Y)
            psv.SetPlotWindowArea(plotSettings, windowArea)
        Catch ex As Exception
            _logger.Warning("重新设置打印窗口失败: " & ex.Message)
        End Try

        ' 设置打印比例（必须在SetPlotConfigurationName之后，因为它会重置比例）
        Try
            SetPlotScale(plotSettings, frame, settings)
        Catch ex As Exception
            _logger.Error("设置打印比例失败: " & ex.Message)
        End Try

        ' 设置旋转（必须在SetPlotConfigurationName之后，因为它可能会重置旋转）
        Try
            SetPlotRotation(plotSettings, frame, settings)
        Catch ex As Exception
            _logger.Error("设置旋转失败: " & ex.Message)
        End Try

        ' 设置居中/偏移（仅在图纸空间布局时设置）
        Try
            If Not plotSettings.ModelType Then
                ' 图纸空间布局
                If settings.AutoCenter Then
                    ' CopyFrom(layout) 可能带入旧原点，先清零再居中
                    psv.SetPlotCentered(plotSettings, False)
                    psv.SetPlotOrigin(plotSettings, New Point2d(0, 0))
                    psv.SetPlotCentered(plotSettings, True)
                Else
                    psv.SetPlotCentered(plotSettings, False)
                    psv.SetPlotOrigin(plotSettings, New Point2d(settings.OffsetX, settings.OffsetY))
                End If
            Else
                ' 模型空间打印，使用打印窗口的居中方式
                If settings.AutoCenter Then
                    psv.SetPlotCentered(plotSettings, False)
                    psv.SetPlotOrigin(plotSettings, New Point2d(0, 0))
                    psv.SetPlotCentered(plotSettings, True)
                Else
                    psv.SetPlotCentered(plotSettings, False)
                End If
            End If
        Catch ex As Exception
            _logger.Error("设置居中/偏移失败: " & ex.Message)
        End Try

        ' 设置打印样式
        If Not String.IsNullOrEmpty(settings.PlotStyle) AndAlso settings.PlotStyle <> "无" Then
            Try
                ' 样式模式校验：命名打印样式(STB)图纸只能用 .stb 样式，颜色相关(CTB)图纸用 .ctb
                Dim styleName As String = settings.PlotStyle
                If _db.PlotStyleMode AndAlso Not styleName.EndsWith(".stb", StringComparison.OrdinalIgnoreCase) Then
                    _logger.Warning($"打印样式 '{styleName}' 与图纸模式不兼容（命名打印样式图纸需 .stb），跳过样式设置")
                Else
                    psv.SetCurrentStyleSheet(plotSettings, styleName)
                    ' 必须显式启用打印样式应用，否则布局原值为关闭时用户样式不生效
                    plotSettings.PlotPlotStyles = True
                End If
            Catch ex As Exception
                _logger.Warning("设置打印样式失败: " & ex.Message)
            End Try
        End If
    End Sub

    Private Sub SetPlotScale(plotSettings As Autodesk.AutoCAD.DatabaseServices.PlotSettings, frame As FrameInfo, settings As PlotSettings)
        Dim psv As PlotSettingsValidator = PlotSettingsValidator.Current

        Select Case settings.PlotScale
            Case PlotScaleType.FitToPaper
                psv.SetUseStandardScale(plotSettings, True)
                psv.SetStdScaleType(plotSettings, StdScaleType.ScaleToFit)
            Case PlotScaleType.AutoScale
                ' 从 PlotSettings 对象获取实际纸张尺寸
                Dim pw As Double
                Dim ph As Double
                Try
                    If IsPixelPaperUnits(plotSettings) Then
                        ' 栅格设备（Pixels）的 PlotPaperSize 是像素而非毫米，直接用会算错比例
                        Dim fallback = ParsePaperSize(settings.PaperSize)
                        pw = fallback.Item1
                        ph = fallback.Item2
                    Else
                        Dim physicalSize As Point2d = plotSettings.PlotPaperSize
                        pw = physicalSize.X
                        ph = physicalSize.Y
                    End If
                Catch
                    Dim fallback = ParsePaperSize(settings.PaperSize)
                    pw = fallback.Item1
                    ph = fallback.Item2
                End Try
                Dim frameWidth As Double = Math.Abs(frame.Bounds.MaxPoint.X - frame.Bounds.MinPoint.X)
                Dim frameHeight As Double = Math.Abs(frame.Bounds.MaxPoint.Y - frame.Bounds.MinPoint.Y)
                Dim isLandscape As Boolean = If(settings.AutoRotate, frameWidth > frameHeight, settings.IsLandscape)
                Dim bestScaleIndex = CalculateBestScale(pw, ph, frameWidth, frameHeight, isLandscape)

                If bestScaleIndex >= 0 Then
                    If IsPixelPaperUnits(plotSettings) Then
                        ' 栅格设备（Pixels）不用标准比例：毫米比例按 DPI 换算成像素分子
                        psv.SetUseStandardScale(plotSettings, False)
                        Dim pixelScale = New CustomScale(ToPlotPaperUnitScale(plotSettings, _standardScaleRatios(bestScaleIndex)), 1.0)
                        psv.SetCustomPrintScale(plotSettings, pixelScale)
                    Else
                        ' 优先使用真实存在的标准比例枚举（StdScale1To50 等）；
                        ' 无枚举的比例（5:1/20:1/50:1/1:200/1:500/1:1000）直接用自定义比例，不再借道错误枚举
                        Dim stdScaleTypeName As String = "StdScale" & _standardScaleNames(bestScaleIndex).Replace(":", "To")
                        Dim stdScaleEnum As StdScaleType
                        If [Enum].TryParse(Of StdScaleType)(stdScaleTypeName, True, stdScaleEnum) Then
                            psv.SetUseStandardScale(plotSettings, True)
                            psv.SetStdScaleType(plotSettings, stdScaleEnum)
                        Else
                            psv.SetUseStandardScale(plotSettings, False)
                            Dim scale = New CustomScale(ToPlotPaperUnitScale(plotSettings, _standardScaleRatios(bestScaleIndex)), 1.0)
                            psv.SetCustomPrintScale(plotSettings, scale)
                        End If
                    End If
                Else
                    psv.SetUseStandardScale(plotSettings, True)
                    psv.SetStdScaleType(plotSettings, StdScaleType.ScaleToFit)
                End If
            Case PlotScaleType.FixedScale
                psv.SetUseStandardScale(plotSettings, False)
                ' CustomScale(分子, 分母)，用户输入100表示1:100；分子按当前纸张单位换算（Pixels 需乘 DPI/25.4）
                Dim scale = New CustomScale(ToPlotPaperUnitScale(plotSettings, 1.0), settings.FixedScale)
                psv.SetCustomPrintScale(plotSettings, scale)
        End Select
    End Sub

    ' 把「每图面单位对应的毫米」换算成当前 PlotPaperUnits 下 CustomScale 的分子。
    ' Pixels 必须按 DPI 换成像素/图面单位（毫米×DPI/25.4），Inches 除以 25.4，不能把毫米数直接当分子
    Private Function ToPlotPaperUnitScale(plotSettings As Autodesk.AutoCAD.DatabaseServices.PlotSettings, millimetersPerDrawingUnit As Double) As Double
        Try
            If plotSettings.PlotPaperUnits = PlotPaperUnit.Inches Then
                Return millimetersPerDrawingUnit / 25.4
            End If
            If plotSettings.PlotPaperUnits = PlotPaperUnit.Pixels Then
                Dim dpi As Double = GetRasterDpi(plotSettings.PlotConfigurationName)
                Return millimetersPerDrawingUnit * dpi / 25.4
            End If
        Catch
        End Try
        Return millimetersPerDrawingUnit
    End Function

    ' 从 PC3 文件解析栅格设备 DPI（像素/英寸），用于 Pixels 单位的比例换算。
    ' 依次尝试 effective/addr/phys_resolution_x；解析不到按 100（AutoCAD 自带 PublishToWeb 默认即 100）
    Private Shared Function GetRasterDpi(deviceName As String) As Double
        Try
            If String.IsNullOrEmpty(deviceName) Then Return 100.0
            Dim plottersDir As String = AutoCADPathResolver.GetPlottersPath()
            If String.IsNullOrEmpty(plottersDir) Then Return 100.0
            Dim pc3Path As String = IO.Path.Combine(plottersDir, IO.Path.GetFileName(deviceName))
            If Not IO.File.Exists(pc3Path) Then Return 100.0

            Dim raw As String = IO.File.ReadAllText(pc3Path)
            For Each key As String In New String() {"effective_resolution_x", "addr_resolution_x", "phys_resolution_x"}
                Dim m As Text.RegularExpressions.Match = Text.RegularExpressions.Regex.Match(raw, key & "\D{0,5}([\d.]+)")
                If m.Success Then
                    Dim value As Double
                    If Double.TryParse(m.Groups(1).Value, value) AndAlso value > 0 Then
                        Return value
                    End If
                End If
            Next
        Catch
        End Try
        Return 100.0
    End Function

    ' 当前设置是否为像素纸张单位（栅格设备）
    Private Shared Function IsPixelPaperUnits(plotSettings As Autodesk.AutoCAD.DatabaseServices.PlotSettings) As Boolean
        Try
            Return plotSettings.PlotPaperUnits = PlotPaperUnit.Pixels
        Catch
            Return False
        End Try
    End Function

    ' 首次打印会写 plot.log 造成首张卡顿；等价 acedSetEnv("AutomaticPlotLog","0")。
    ' AutomaticPlotLog 是环境变量而非系统变量，.NET API 未暴露，按导出修饰名探测 accore.dll/acad.exe，
    ' 探测失败则静默跳过，只影响首张写日志这一小段开销
    Private Shared ReadOnly s_automaticPlotLogDisabled As Boolean = TryDisableAutomaticPlotLog()

    Private Shared Function TryDisableAutomaticPlotLog() As Boolean
        Try
            For Each moduleName As String In New String() {"accore.dll", "acad.exe"}
                Dim moduleHandle As IntPtr = NativeMethods.GetModuleHandle(moduleName)
                If moduleHandle = IntPtr.Zero Then Continue For

                ' 两个候选覆盖不同版本的 C++ 修饰差异
                For Each entry As String In New String() {"?acedSetEnv@@YAHPEB_W0@Z", "?acedSetEnv@@YAHPEB_WPEB_W@Z"}
                    Dim address As IntPtr = NativeMethods.GetProcAddress(moduleHandle, entry)
                    If address = IntPtr.Zero Then Continue For

                    Dim setEnv = CType(Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer(address, GetType(NativeMethods.SetEnvDelegate)), NativeMethods.SetEnvDelegate)
                    setEnv("AutomaticPlotLog", "0")
                    Return True
                Next
            Next
        Catch
        End Try
        Return False
    End Function

    Private Class NativeMethods

        Delegate Function SetEnvDelegate(
            <Runtime.InteropServices.MarshalAs(Runtime.InteropServices.UnmanagedType.LPWStr)> name As String,
            <Runtime.InteropServices.MarshalAs(Runtime.InteropServices.UnmanagedType.LPWStr)> value As String) As Integer

        <Runtime.InteropServices.DllImport("kernel32.dll", CharSet:=Runtime.InteropServices.CharSet.Ansi, SetLastError:=True)>
        Public Shared Function GetProcAddress(hModule As IntPtr, procName As String) As IntPtr
        End Function

        <Runtime.InteropServices.DllImport("kernel32.dll", CharSet:=Runtime.InteropServices.CharSet.Unicode, SetLastError:=True)>
        Public Shared Function GetModuleHandle(name As String) As IntPtr
        End Function

    End Class

    Private Sub SetPlotRotation(plotSettings As Autodesk.AutoCAD.DatabaseServices.PlotSettings, frame As FrameInfo, settings As PlotSettings)
        Dim psv As PlotSettingsValidator = PlotSettingsValidator.Current

        Dim rotation As PlotRotation = PlotRotation.Degrees000

        If settings.AutoRotate Then
            ' 从 PlotSettings 对象获取实际纸张尺寸
            Dim paperWidth As Double
            Dim paperHeight As Double
            Try
                Dim physicalSize As Point2d = plotSettings.PlotPaperSize
                paperWidth = physicalSize.X
                paperHeight = physicalSize.Y
            Catch ex As Exception
                Dim paperSize = ParsePaperSize(settings.PaperSize)
                paperWidth = paperSize.Item1
                paperHeight = paperSize.Item2
            End Try

            Dim isPaperLandscape As Boolean = paperWidth > paperHeight
            Dim frameWidth As Double = Math.Abs(frame.Bounds.MaxPoint.X - frame.Bounds.MinPoint.X)
            Dim frameHeight As Double = Math.Abs(frame.Bounds.MaxPoint.Y - frame.Bounds.MinPoint.Y)
            Dim isFrameLandscape As Boolean = frameWidth > frameHeight

            ' 图框方向与纸张方向不一致时旋转90度
            If isFrameLandscape <> isPaperLandscape Then
                rotation = PlotRotation.Degrees090
            End If
        End If
        ' 关闭自动旋转时，使用纸张默认方向（0度），不做任何旋转

        ' 如果设置了反向，则在当前旋转基础上再旋转180度
        If settings.Reverse Then
            Select Case rotation
                Case PlotRotation.Degrees000
                    rotation = PlotRotation.Degrees180
                Case PlotRotation.Degrees090
                    rotation = PlotRotation.Degrees270
                Case PlotRotation.Degrees180
                    rotation = PlotRotation.Degrees000
                Case PlotRotation.Degrees270
                    rotation = PlotRotation.Degrees090
            End Select
        End If

        psv.SetPlotRotation(plotSettings, rotation)
    End Sub

#End Region

#Region "Archive and Layout Operations"

    ' 图纸拆分归档
    Public Sub SplitAndArchive(frames As List(Of FrameInfo), outputPath As String, nameTemplate As String, useDWGName As Boolean, progressCallback As Action(Of Integer, String), Optional duplicateHandling As DuplicateFileHandling = DuplicateFileHandling.AutoRename)
        ' FILEDIA 保存/恢复必须覆盖异常路径：异常后不恢复会禁用 CAD 全部文件对话框直到重启
        Dim oldFileDia As Object = Nothing
        Try
            Dim count = 0
            Dim total As Integer = frames.Count

            ' 保存当前 FILEDIA 设置并禁用文件对话框
            Try
                oldFileDia = Application.GetSystemVariable("FILEDIA")
                SetSystemVariableChecked("FILEDIA", 0)
            Catch
                ' 忽略设置失败
            End Try

            For Each frame As FrameInfo In frames
                If Not frame.IsSelected Then Continue For

                count += 1
                progressCallback?.Invoke(CInt((count / total) * 100), $"正在拆分: {frame.FrameNumber}")

                Dim fileName As String = If(useDWGName, Path.GetFileNameWithoutExtension(_doc.Name), FormatFileName(nameTemplate, frame.FrameNumber, count, frame))
                Dim outputFile As String = Path.Combine(outputPath, $"{fileName}.dwg")
                outputFile = HandleDuplicateFile(outputFile, duplicateHandling)

                If String.IsNullOrEmpty(outputFile) Then Continue For

                Try
                    ' 整库克隆后在克隆库中删除图框外对象（保留穿框图元与完整图纸结构）
                    CreateSplitFile(frame, outputFile)
                    _logger.Info($"图纸拆分成功: {outputFile}")
                Catch ex As Exception
                    _logger.Error($"拆分图框 {frame.FrameNumber} 失败", ex)
                End Try
            Next

            progressCallback?.Invoke(100, "拆分完成")
        Catch ex As Exception
            _logger.Error("图纸拆分归档失败", ex)
            Throw New Exception("图纸拆分归档失败: " & ex.Message)
        Finally
            ' 恢复 FILEDIA 设置（含异常路径）
            Try
                If oldFileDia IsNot Nothing Then
                    SetSystemVariableChecked("FILEDIA", oldFileDia)
                End If
            Catch
                ' 忽略恢复失败
            End Try
        End Try
    End Sub

    ''' <summary>
    ''' 整库克隆后删除图框外对象并另存（保留穿框图元与完整图纸结构）
    ''' </summary>
    Private Sub CreateSplitFile(frame As FrameInfo, outputFile As String)
        ' 锁定文档
        Using docLock As DocumentLock = _doc.LockDocument()
            ' 克隆整个数据库（空集合 = 整库克隆），不改动当前文档
            Dim emptyIds As New ObjectIdCollection()
            Using sideDb As Database = _db.Wblock(emptyIds, Point3d.Origin)

                ' 确定要拆分的空间（按图框所在布局，找不到时回退模型空间）
                Dim spaceId As ObjectId = GetSplitSpaceId(sideDb, frame)

                Dim toErase As New List(Of ObjectId)()
                Dim keptCount As Integer = 0

                ' 开始事务筛选图框内的对象
                Using tr As Transaction = sideDb.TransactionManager.StartTransaction()
                    ' 获取拆分空间
                    Dim bt = CType(tr.GetObject(sideDb.BlockTableId, OpenMode.ForRead), BlockTable)
                    Dim space = CType(tr.GetObject(spaceId, OpenMode.ForRead), BlockTableRecord)

                    ' 获取图框边界
                    Dim frameMin As Point3d = frame.Bounds.MinPoint
                    Dim frameMax As Point3d = frame.Bounds.MaxPoint

                    ' 遍历拆分空间中的所有实体
                    For Each objId As ObjectId In space
                        If objId.IsErased Then Continue For

                        Try
                            Dim entity = TryCast(tr.GetObject(objId, OpenMode.ForRead), Entity)
                            If entity Is Nothing Then
                                toErase.Add(objId)
                                Continue For
                            End If

                            Dim extents As Extents3d
                            Try
                                extents = entity.GeometricExtents
                            Catch
                                ' 某些实体可能没有几何范围，不保留
                                toErase.Add(objId)
                                Continue For
                            End Try

                            ' 检查实体是否与图框边界相交
                            If IsEntityInFrame(extents, frameMin, frameMax) Then
                                keptCount += 1
                            Else
                                toErase.Add(objId)
                            End If
                        Catch
                            toErase.Add(objId)
                        End Try
                    Next

                    ' 统一删除图框外的对象
                    If keptCount > 0 Then
                        For Each objId As ObjectId In toErase
                            Dim eraseEntity = TryCast(tr.GetObject(objId, OpenMode.ForWrite), Entity)
                            If eraseEntity IsNot Nothing Then eraseEntity.Erase()
                        Next
                    End If

                    tr.Commit()
                End Using

                If keptCount = 0 Then
                    _logger.Warning($"图框 {frame.FrameNumber} 内没有找到对象，跳过保存")
                    Return
                End If

                ' 确保输出目录存在
                Dim outputDir As String = Path.GetDirectoryName(outputFile)
                If Not String.IsNullOrEmpty(outputDir) AndAlso Not Directory.Exists(outputDir) Then
                    Directory.CreateDirectory(outputDir)
                End If

                ' 保存拆分结果（使用源文件版本）
                sideDb.SaveAs(outputFile, _db.OriginalFileVersion)
            End Using
        End Using
    End Sub

    ''' <summary>
    ''' 获取拆分目标空间的 ObjectId（按图框所在布局名，失败回退模型空间）
    ''' </summary>
    Private Function GetSplitSpaceId(sideDb As Database, frame As FrameInfo) As ObjectId
        Using tr = sideDb.TransactionManager.StartTransaction()
            Dim bt = CType(tr.GetObject(sideDb.BlockTableId, OpenMode.ForRead), BlockTable)

            If frame IsNot Nothing AndAlso Not String.IsNullOrEmpty(frame.LayoutName) Then
                For Each btId As ObjectId In bt
                    Dim btr = TryCast(tr.GetObject(btId, OpenMode.ForRead), BlockTableRecord)
                    If btr IsNot Nothing AndAlso btr.IsLayout AndAlso
                       String.Equals(btr.Name, frame.LayoutName, StringComparison.OrdinalIgnoreCase) Then
                        tr.Commit()
                        Return btId
                    End If
                Next
            End If

            tr.Commit()
            Return bt(BlockTableRecord.ModelSpace)
        End Using
    End Function

    ''' <summary>
    ''' 检查实体是否在图框范围内
    ''' </summary>
    Private Function IsEntityInFrame(extents As Extents3d, frameMin As Point3d, frameMax As Point3d) As Boolean
        ' 检查实体的范围是否与图框范围相交
        Return Not (extents.MaxPoint.X < frameMin.X OrElse
                    extents.MinPoint.X > frameMax.X OrElse
                    extents.MaxPoint.Y < frameMin.Y OrElse
                    extents.MinPoint.Y > frameMax.Y)
    End Function

    ' 批量生成布局
    Public Sub BatchCreateLayouts(frames As List(Of FrameInfo), settings As PlotSettings, progressCallback As Action(Of Integer, String), Optional modelSpaceScale As Boolean = False, Optional duplicateHandling As DuplicateFileHandling = DuplicateFileHandling.AutoRename)
        ' 系统变量保存/恢复必须覆盖异常路径：异常后不恢复会永久改变图纸线型显示
        Dim originalLtScale As Object = Nothing
        Dim originalPsLtScale As Object = Nothing
        Try
            Dim count = 0
            Dim total As Integer = frames.Count

            originalLtScale = Application.GetSystemVariable("LTSCALE")
            originalPsLtScale = Application.GetSystemVariable("PSLTSCALE")

            ' 锁定文档以避免 eLockViolation 错误
            Using docLock As DocumentLock = _doc.LockDocument()
                For Each frame As FrameInfo In frames
                    If Not frame.IsSelected Then Continue For

                    count += 1
                    progressCallback?.Invoke(CInt((count / total) * 100), $"正在创建布局: {frame.FrameNumber}")

                    ' 为每个布局单独创建事务
                    Dim createdLayoutId As ObjectId = ObjectId.Null
                    Using tr As Transaction = _db.TransactionManager.StartTransaction()
                        Dim layoutDict = CType(tr.GetObject(_db.LayoutDictionaryId, OpenMode.ForWrite), DBDictionary)

                        Dim layoutName As String = HandleDuplicateLayoutName(layoutDict, frame.FrameNumber, duplicateHandling)
                        If String.IsNullOrEmpty(layoutName) Then Continue For

                        ' 使用 LayoutManager 创建布局
                        createdLayoutId = LayoutManager.Current.CreateLayout(layoutName)

                        ' 提交当前事务以确保布局完全创建
                        tr.Commit()
                    End Using

                    ' 激活布局以确保它完全初始化
                    If Not createdLayoutId.IsNull Then
                        ' 获取布局名称并激活
                        Using tr As Transaction = _db.TransactionManager.StartTransaction()
                            Dim layout = CType(tr.GetObject(createdLayoutId, OpenMode.ForRead), Layout)
                            LayoutManager.Current.CurrentLayout = layout.LayoutName
                            tr.Commit()
                        End Using
                        ' 给 AutoCAD 一点时间来初始化布局
                        Threading.Thread.Sleep(100)
                    End If

                    ' 在新的事务中配置布局
                    Using tr As Transaction = _db.TransactionManager.StartTransaction()
                        If Not createdLayoutId.IsNull Then
                            Dim layout = CType(tr.GetObject(createdLayoutId, OpenMode.ForWrite), Layout)
                            
                            ' 先应用打印设置（确保纸张尺寸正确）
                            ApplyPlotSettingsToLayout(layout, settings, tr, frame)
                            
                            ' 再删除布局中的默认视口
                            DeleteDefaultViewports(tr, layout)
                            
                            ' 最后创建视口（此时纸张尺寸已正确）
                            If modelSpaceScale Then
                                CreateViewportForLayout(tr, layout, frame, settings)
                            End If
                        End If

                        tr.Commit()
                    End Using
                Next
            End Using

            progressCallback?.Invoke(100, "布局创建完成")
        Catch ex As Exception
            _logger.Error("批量生成布局失败", ex)
            Throw New Exception("批量生成布局失败: " & ex.Message)
        Finally
            ' 恢复系统变量（含异常路径）
            Try
                If originalLtScale IsNot Nothing Then
                    Application.SetSystemVariable("LTSCALE", originalLtScale)
                End If
                If originalPsLtScale IsNot Nothing Then
                    Application.SetSystemVariable("PSLTSCALE", originalPsLtScale)
                End If
            Catch
                ' 忽略恢复失败
            End Try
        End Try
    End Sub

    ''' <summary>
    ''' 将主界面的打印设置应用到布局
    ''' 使用 API 直接设置布局的 PlotSettings
    ''' 注意：必须先设置打印机，再设置窗口区域，最后设置打印类型
    ''' </summary>
    Private Sub ApplyPlotSettingsToLayout(layout As Layout, settings As PlotSettings, tr As Transaction, frame As FrameInfo)
        Try
            Dim layoutName As String = layout.LayoutName
            _logger.Info($"开始应用打印设置到布局: {layoutName}")

            ' 确保布局对象以写模式打开
            If Not layout.IsWriteEnabled Then
                layout.UpgradeOpen()
                _logger.Info($"布局 {layoutName} 已升级为写模式")
            End If

            ' 获取 PlotSettingsValidator
            Dim validator As PlotSettingsValidator = PlotSettingsValidator.Current

            ' 第1步：设置打印设备和图纸尺寸（必须先设置）
            If Not String.IsNullOrEmpty(settings.PlotterName) AndAlso Not String.IsNullOrEmpty(settings.PaperSize) Then
                Try
                    Dim canonicalPaperName As String = GetCanonicalPaperName(settings.PaperSize)
                    validator.SetPlotConfigurationName(layout, settings.PlotterName, canonicalPaperName)
                    _logger.Info($"设置打印设备: {settings.PlotterName}, 图纸尺寸: {canonicalPaperName}")
                Catch ex As Exception
                    _logger.Warning($"设置打印设备失败: {ex.Message}")
                End Try
            End If

            ' 第2步：设置打印比例
            Try
                Select Case settings.PlotScale
                    Case PlotScaleType.FitToPaper
                        validator.SetUseStandardScale(layout, True)
                        validator.SetStdScaleType(layout, StdScaleType.ScaleToFit)
                        _logger.Info($"设置打印比例: 布满图纸")
                    Case PlotScaleType.FixedScale
                        validator.SetUseStandardScale(layout, False)
                        ' 分子按当前纸张单位换算（栅格设备 Pixels 需乘 DPI/25.4）
                        validator.SetCustomPrintScale(layout, New CustomScale(ToPlotPaperUnitScale(layout, 1.0), settings.FixedScale))
                        _logger.Info($"设置打印比例: 1:{settings.FixedScale}")
                    Case Else
                        validator.SetUseStandardScale(layout, True)
                        validator.SetStdScaleType(layout, StdScaleType.ScaleToFit)
                        _logger.Info($"设置打印比例: 布满图纸(默认)")
                End Select
            Catch ex As Exception
                _logger.Warning($"设置打印比例失败: {ex.Message}")
            End Try

            ' 第3步：重置打印原点为(0,0)，确保图纸空间原点在纸张左下角
            Try
                validator.SetPlotOrigin(layout, New Point2d(0, 0))
                _logger.Info($"重置打印原点为(0,0)")
            Catch ex As Exception
                _logger.Warning($"重置打印原点失败: {ex.Message}")
            End Try

            ' 第4步：设置打印偏移（仅在非布满图纸时设置）
            ' 注意：当使用布满图纸时，设置偏移会导致 eInvalidInput 错误
            Try
                If settings.PlotScale <> PlotScaleType.FitToPaper Then
                    If settings.AutoCenter Then
                        validator.SetPlotCentered(layout, True)
                        _logger.Info($"设置打印偏移: 居中")
                    Else
                        validator.SetPlotCentered(layout, False)
                        validator.SetPlotOrigin(layout, New Point2d(settings.OffsetX, settings.OffsetY))
                        _logger.Info($"设置打印偏移: X={settings.OffsetX}, Y={settings.OffsetY}")
                    End If
                Else
                    _logger.Info($"使用布满图纸，跳过偏移设置")
                End If
            Catch ex As Exception
                _logger.Warning($"设置打印偏移失败: {ex.Message}")
            End Try

            ' 第4步：设置打印方向
            Try
                Dim rotation As PlotRotation
                If settings.IsLandscape Then
                    rotation = PlotRotation.Degrees090
                Else
                    rotation = PlotRotation.Degrees000
                End If

                ' 如果设置了反向，在当前旋转基础上再旋转180度
                If settings.Reverse Then
                    Select Case rotation
                        Case PlotRotation.Degrees000
                            rotation = PlotRotation.Degrees180
                        Case PlotRotation.Degrees090
                            rotation = PlotRotation.Degrees270
                        Case PlotRotation.Degrees180
                            rotation = PlotRotation.Degrees000
                        Case PlotRotation.Degrees270
                            rotation = PlotRotation.Degrees090
                    End Select
                End If

                validator.SetPlotRotation(layout, rotation)
                _logger.Info($"设置打印方向: {rotation}{If(settings.Reverse, " (反向)", "")}")
            Catch ex As Exception
                _logger.Warning($"设置打印方向失败: {ex.Message}")
            End Try

            ' 第5步：设置打印区域
            ' 使用 Layout 模式打印整个布局（包括视口），这样纸张左下角始终在 (0,0)
            Try
                validator.SetPlotType(layout, Autodesk.AutoCAD.DatabaseServices.PlotType.Layout)
                _logger.Info($"设置打印类型为 Layout（打印整个布局，纸张原点在0,0）")
            Catch ex As Exception
                _logger.Warning($"设置打印区域失败: {ex.Message}")
            End Try

            ' 不自动缩放到纸张，避免重置视口位置
            validator.SetZoomToPaperOnUpdate(layout, False)

            _logger.Info($"布局 {layoutName} 打印设置已应用")
        Catch ex As Exception
            _logger.Error($"应用打印设置到布局 {layout.LayoutName} 失败: {ex.Message}", ex)
        End Try
    End Sub

    Private Sub CreateViewportForLayout(tr As Transaction, layout As Layout, frame As FrameInfo, settings As PlotSettings)
        Dim layoutBtr = CType(tr.GetObject(layout.BlockTableRecordId, OpenMode.ForWrite), BlockTableRecord)

        ' 从 layout.PlotPaperSize 获取实际的纸张尺寸（应用打印设置后）
        Dim paperWidth As Double = A4_WIDTH
        Dim paperHeight As Double = A4_HEIGHT
        
        Try
            ' 使用 layout.PlotPaperSize 获取实际纸张尺寸
            Dim vpPaperSize As Point2d = layout.PlotPaperSize
            paperWidth = vpPaperSize.X
            paperHeight = vpPaperSize.Y
        Catch ex As Exception
            Try
                If Not String.IsNullOrEmpty(settings.PaperSize) Then
                    Dim match As RegularExpressions.Match = RegularExpressions.Regex.Match(settings.PaperSize, "(\d+\.?\d*)\s*x\s*(\d+\.?\d*)")
                    If match.Success Then
                        paperWidth = Double.Parse(match.Groups(1).Value)
                        paperHeight = Double.Parse(match.Groups(2).Value)
                    Else
                        Dim paperSizeKey As String = settings.PaperSize.ToUpper().Replace("ISO_", "").Replace(" ", "")
                        Select Case paperSizeKey
                            Case "A0" : paperWidth = 841.0 : paperHeight = 1189.0
                            Case "A1" : paperWidth = 594.0 : paperHeight = 841.0
                            Case "A2" : paperWidth = 420.0 : paperHeight = 594.0
                            Case "A3" : paperWidth = 297.0 : paperHeight = 420.0
                            Case "A4" : paperWidth = 210.0 : paperHeight = 297.0
                            Case "A5" : paperWidth = 148.0 : paperHeight = 210.0
                            Case "B4" : paperWidth = 250.0 : paperHeight = 353.0
                            Case "B5" : paperWidth = 176.0 : paperHeight = 250.0
                        End Select
                    End If
                End If
            Catch ex2 As Exception
            End Try
        End Try
        
        ' 从 layout 获取纸张方向
        Dim isLandscape As Boolean = False
        Try
            Dim rotation As Integer = CInt(layout.PlotRotation)
            isLandscape = (rotation = 1 OrElse rotation = 3)
        Catch ex As Exception
            isLandscape = settings.IsLandscape
        End Try
        
        ' 自动旋转：比较图框宽高比和纸张宽高比
        Dim needTwist As Boolean = False
        If settings.AutoRotate Then
            Dim frameW As Double = frame.Bounds.MaxPoint.X - frame.Bounds.MinPoint.X
            Dim frameH As Double = frame.Bounds.MaxPoint.Y - frame.Bounds.MinPoint.Y
            Dim frameIsLandscape As Boolean = frameW > frameH
            Dim rawPaperSize As Point2d = layout.PlotPaperSize
            Dim paperIsLandscape As Boolean = rawPaperSize.X > rawPaperSize.Y
            If frameIsLandscape <> paperIsLandscape Then
                needTwist = True
            End If
        End If
        
        ' 如果横向，交换纸张尺寸
        If isLandscape Then
            Dim temp As Double = paperWidth
            paperWidth = paperHeight
            paperHeight = temp
        End If

        Dim frameWidth As Double = frame.Bounds.MaxPoint.X - frame.Bounds.MinPoint.X
        Dim frameHeight As Double = frame.Bounds.MaxPoint.Y - frame.Bounds.MinPoint.Y

        ' 计算视口比例
        Dim scaleFactor As Double
        If settings.PlotScale = PlotScaleType.FitToPaper Then
            Dim scaleX As Double = paperWidth / frameWidth
            Dim scaleY As Double = paperHeight / frameHeight
            scaleFactor = Math.Min(scaleX, scaleY) * 0.9 ' 留一些边距
        ElseIf settings.PlotScale = PlotScaleType.FixedScale Then
            scaleFactor = settings.FixedScale
        Else
            ' AutoScale - 自动取整到最接近的标准比例
            Dim scaleX As Double = paperWidth / frameWidth
            Dim scaleY As Double = paperHeight / frameHeight
            Dim rawScale As Double = Math.Min(scaleX, scaleY)
            Dim bestIndex As Integer = CalculateBestScale(paperWidth, paperHeight, frameWidth, frameHeight, isLandscape)
            If bestIndex >= 0 Then
                scaleFactor = _standardScaleRatios(bestIndex)
            Else
                scaleFactor = rawScale * 0.9
            End If
        End If

        ' 计算图框中心点
        Dim frameCenterX As Double = (frame.Bounds.MinPoint.X + frame.Bounds.MaxPoint.X) / 2
        Dim frameCenterY As Double = (frame.Bounds.MinPoint.Y + frame.Bounds.MaxPoint.Y) / 2

        _logger.Info($"图框尺寸: {frameWidth:F2} x {frameHeight:F2}, 纸张尺寸: {paperWidth:F2} x {paperHeight:F2}")

        ' 获取图形单位（毫米、厘米、英寸等）
        Dim units = CInt(Application.GetSystemVariable("INSUNITS"))
        Dim unitFactor = 1.0
        Select Case units
            Case 0, 1 ' 无单位或英寸
                unitFactor = 25.4 ' 转换为毫米
            Case 4 ' 毫米
                unitFactor = 1.0
            Case 5 ' 厘米
                unitFactor = 10.0
            Case 6 ' 米
                unitFactor = 1000.0
        End Select

        Dim viewportUnitFactor = 1.0
        
        ' 根据主窗口设置计算视口比例
        ' needScale = 模型空间单位 / 图纸空间单位（即打印比例的倒数）
        ' 例如 1:100 时 needScale = 0.01，表示图纸空间1单位 = 模型空间100单位
        Dim needScale As Double
        Select Case settings.PlotScale
            Case PlotScaleType.FitToPaper
                needScale = 0
            Case PlotScaleType.AutoScale
                ' AutoScale 使用取整后的标准比例
                needScale = 1.0 / scaleFactor
            Case PlotScaleType.FixedScale
                needScale = 1.0 / settings.FixedScale
            Case Else
                needScale = 0
        End Select
        Dim paperUnitFactor As Double = 1.0
        Try
            If layout.PlotPaperUnits = PlotPaperUnit.Inches Then
                paperUnitFactor = 25.4
            End If
        Catch
        End Try
        
        Dim marginMin As Point2d = layout.PlotPaperMargins.MinPoint
        Dim marginMax As Point2d = layout.PlotPaperMargins.MaxPoint
        
        ' 如果横向，页边距的 XY 也需要互换
        ' 纵向时：左边距=marginMin.X, 下边距=marginMin.Y
        ' 横向时（旋转90/270度）：原来的上下边距变成左右边距
        Dim marginLeft As Double = marginMin.X
        Dim marginBottom As Double = marginMin.Y
        Dim marginRight As Double = marginMax.X
        Dim marginTop As Double = marginMax.Y
        
        If isLandscape Then
            marginLeft = marginMin.Y
            marginBottom = marginMin.X
            marginRight = marginMax.Y
            marginTop = marginMax.X
        End If
        
        ' 计算可打印区域（纸张尺寸减去页边距）
        ' paperWidth 和 paperHeight 是毫米值（已考虑方向）
        Dim printableWidth As Double = paperWidth - marginLeft - marginRight
        Dim printableHeight As Double = paperHeight - marginBottom - marginTop
        
        Dim printableCenterX_mm As Double = marginLeft + printableWidth / 2
        Dim printableCenterY_mm As Double = marginBottom + printableHeight / 2
        
        ' 视口大小计算
        Dim viewportWidth As Double
        Dim viewportHeight As Double
        Dim viewHeight As Double ' 模型空间视图高度（控制缩放级别）
        
        ' 安全保护：确保单位转换因子不为零
        If unitFactor = 0 Then unitFactor = 1.0
        If paperUnitFactor = 0 Then paperUnitFactor = 1.0
        
        If needScale = 0 Then
            viewportWidth = Math.Max(printableWidth / paperUnitFactor, 1.0)
            viewportHeight = Math.Max(printableHeight / paperUnitFactor, 1.0)
            ' 同时考虑 XY 两个方向，取较大比例，确保图框不超出视口
            Dim ratioX As Double = frameWidth / (printableWidth / paperUnitFactor)
            Dim ratioY As Double = frameHeight / (printableHeight / paperUnitFactor)
            viewHeight = Math.Max(Math.Max(ratioX, ratioY), 1.0)
        Else
            viewportWidth = Math.Max(frameWidth * needScale / unitFactor * paperUnitFactor, 1.0)
            viewportHeight = Math.Max(frameHeight * needScale / unitFactor * paperUnitFactor, 1.0)
            ' 同时考虑 XY 两个方向
            Dim actualW As Double = frameWidth * needScale / unitFactor * paperUnitFactor
            Dim actualH As Double = frameHeight * needScale / unitFactor * paperUnitFactor
            Dim ratioX As Double = frameWidth / Math.Max(actualW, 0.001)
            Dim ratioY As Double = frameHeight / Math.Max(actualH, 0.001)
            viewHeight = Math.Max(Math.Max(ratioX, ratioY) * Math.Max(frameHeight, 1.0), 1.0)
        End If
        
        If needTwist Then
            ' 旋转时交换宽高考虑
            Dim ratioX As Double = frameHeight / (viewportWidth)
            Dim ratioY As Double = frameWidth / (viewportHeight)
            viewHeight = Math.Max(Math.Max(ratioX, ratioY) * Math.Max(frameHeight, 1.0), 1.0)
        End If
        
        Dim viewportCenterX As Double = (printableWidth / 2) / paperUnitFactor
        Dim viewportCenterY As Double = (printableHeight / 2) / paperUnitFactor
        
        If Not settings.AutoCenter Then
            viewportCenterX += settings.OffsetX / paperUnitFactor
            viewportCenterY += settings.OffsetY / paperUnitFactor
        End If

        ' 创建视口
        Try
            Dim viewport As New Viewport()
            
            ' 设置基本几何属性（添加前）
            viewport.CenterPoint = New Point3d(viewportCenterX, viewportCenterY, 0)
            viewport.Width = viewportWidth
            viewport.Height = viewportHeight
            viewport.ViewDirection = Vector3d.ZAxis
            
            ' 添加到数据库
            layoutBtr.AppendEntity(viewport)
            tr.AddNewlyCreatedDBObject(viewport, True)
            
            ' 获取写权限
            viewport.UpgradeOpen()
            
            ' 关闭视口显示，设置视图属性
            viewport.On = False
            
            ' 设置 ViewCenter（模型空间视图中心）- 应该是图框中心，不是视口中心
            viewport.ViewCenter = New Point2d(frameCenterX, frameCenterY)
            
            ' 自动旋转：旋转视口中的图形来匹配纸张方向
            If needTwist Then
                viewport.TwistAngle = Math.PI / 2
                Dim angle As Double = Math.PI / 2
                Dim newCenterX As Double = frameCenterX * Math.Cos(angle) - frameCenterY * Math.Sin(angle)
                Dim newCenterY As Double = frameCenterX * Math.Sin(angle) + frameCenterY * Math.Cos(angle)
                viewport.ViewCenter = New Point2d(newCenterX, newCenterY)
            End If
            
            ' 设置模型空间视图属性（ViewHeight 控制缩放级别）
            viewport.ViewHeight = viewHeight
            
            ' 启用视口显示
            viewport.On = True
            
            ' 再次设置 CenterPoint（确保位置正确）
            viewport.CenterPoint = New Point3d(viewportCenterX, viewportCenterY, 0)
            
            ' 更新显示
            viewport.UpdateDisplay()

            _logger.Info($"创建视口: Center=({viewport.CenterPoint.X:F2}, {viewport.CenterPoint.Y:F2}), ViewCenter=({viewport.ViewCenter.X:F2}, {viewport.ViewCenter.Y:F2}), 大小={viewportWidth:F2}x{viewportHeight:F2}")
        Catch ex As Exception
            _logger.Error($"创建视口失败: {ex.Message}", ex)
        End Try
    End Sub

    ''' <summary>
    ''' 删除布局中的默认视口
    ''' </summary>
    Private Sub DeleteDefaultViewports(tr As Transaction, layout As Layout)
        Try
            Dim layoutBtr = CType(tr.GetObject(layout.BlockTableRecordId, OpenMode.ForWrite), BlockTableRecord)
            Dim viewportsToDelete As New List(Of ObjectId)
            
            ' 遍历布局中的所有实体，查找视口
            For Each entityId As ObjectId In layoutBtr
                If Not entityId.IsNull Then
                    Dim entity As Entity = CType(tr.GetObject(entityId, OpenMode.ForRead), Entity)
                    If TypeOf entity Is Viewport Then
                        viewportsToDelete.Add(entityId)
                        _logger.Info($"找到默认视口，准备删除: {entityId.Handle}")
                    End If
                End If
            Next
            
            ' 删除找到的视口
            For Each viewportId As ObjectId In viewportsToDelete
                Dim viewport As Viewport = CType(tr.GetObject(viewportId, OpenMode.ForWrite), Viewport)
                viewport.Erase()
                _logger.Info($"已删除默认视口: {viewportId.Handle}")
            Next
            
            If viewportsToDelete.Count = 0 Then
                _logger.Info("布局中没有默认视口")
            End If
        Catch ex As Exception
            _logger.Warning($"删除默认视口失败: {ex.Message}")
        End Try
    End Sub

    ' 打印已有布局 - 复用PrintSingleFrame逻辑
    Public Sub PlotExistingLayouts(settings As PlotSettings, progressCallback As Action(Of Integer, String))
        ' 禁用后台打印，保证每张打印完成后输出已落盘（与批量打印路径一致）
        Dim oldBackgroundPlot As Object = Nothing
        Try
            Try
                oldBackgroundPlot = Application.GetSystemVariable("BACKGROUNDPLOT")
                If SetSystemVariableChecked("BACKGROUNDPLOT", 0) Then
                    _logger.Info("已禁用后台打印 (BACKGROUNDPLOT = 0)")
                End If
            Catch ex As Exception
                oldBackgroundPlot = Nothing
                _logger.Warning($"设置 BACKGROUNDPLOT 失败: {ex.Message}")
            End Try

            Dim layouts As List(Of String) = GetLayoutList()
            If layouts.Count = 0 Then
                Throw New Exception("没有找到图纸空间布局")
            End If

            Dim count = 0
            Dim total As Integer = layouts.Count * settings.Copies

            _logger.Info($"开始打印已有布局，共 {layouts.Count} 个布局，{settings.Copies} 份")

            For Each layoutName As String In layouts
                For copy = 1 To settings.Copies
                    count += 1
                    progressCallback?.Invoke(CInt((count / total) * 100), $"正在打印布局: {layoutName} (第{copy}/{settings.Copies}份)")

                    ' 复用PrintSingleFrame逻辑打印已有布局
                    PrintExistingLayoutSingle(layoutName, settings, copy, settings.Copies)

                    _logger.Info($"布局 {layoutName} 第{copy}份打印完成")
                Next
            Next

            progressCallback?.Invoke(100, "布局打印完成")
        Catch ex As Exception
            _logger.Error("打印已有布局失败", ex)
            Throw New Exception("打印已有布局失败: " & ex.Message)
        Finally
            ' 恢复后台打印设置
            Try
                If oldBackgroundPlot IsNot Nothing Then
                    If SetSystemVariableChecked("BACKGROUNDPLOT", oldBackgroundPlot) Then
                        _logger.Info($"已恢复后台打印设置 (BACKGROUNDPLOT = {oldBackgroundPlot})")
                    End If
                End If
            Catch
            End Try
        End Try
    End Sub

    ' 打印单个已有布局 - 复用PrintSingleFrame逻辑
    Private Sub PrintExistingLayoutSingle(layoutName As String, settings As PlotSettings, currentCopy As Integer, totalCopies As Integer)
        Try
            _logger.Info($"PrintExistingLayoutSingle开始: 布局={layoutName}, 第{currentCopy}/{totalCopies}份")

            ' 检查打印引擎状态
            If PlotFactory.ProcessPlotState <> ProcessPlotState.NotPlotting Then
                _logger.Warning("检测到打印引擎正在使用中，等待...")
                Dim waitCount = 0
                Do While PlotFactory.ProcessPlotState <> ProcessPlotState.NotPlotting AndAlso waitCount < 100
                    Threading.Thread.Sleep(100)
                    waitCount += 1
                Loop
                If PlotFactory.ProcessPlotState <> ProcessPlotState.NotPlotting Then
                    Throw New Exception("打印引擎忙，无法开始新的打印任务")
                End If
            End If

            Using lock As DocumentLock = _doc.LockDocument()
                Using tr As Transaction = _db.TransactionManager.StartTransaction()
                    Dim layoutMgr As LayoutManager = LayoutManager.Current
                    Dim layoutId As ObjectId = layoutMgr.GetLayoutId(layoutName)

                    If layoutId.IsNull Then
                        Throw New Exception($"找不到布局: {layoutName}")
                    End If

                    ' 设置为当前布局
                    layoutMgr.CurrentLayout = layoutName
                    Dim layout = CType(tr.GetObject(layoutId, OpenMode.ForRead), Layout)

                    _logger.Info($"使用布局 '{layoutName}' 的原有设置: 打印机={layout.PlotConfigurationName}, 纸张={layout.CanonicalMediaName}, 打印样式={layout.CurrentStyleSheet}")

                    ' 创建PlotSettings并从布局复制（保留布局原有设置）
                    Dim ps As New Autodesk.AutoCAD.DatabaseServices.PlotSettings(layout.ModelType)
                    ps.CopyFrom(layout)

                    ' 仅在用户明确指定时才覆盖布局设置
                    Dim psv As PlotSettingsValidator = PlotSettingsValidator.Current

                    ' 应用打印机设置（如果用户指定了且与布局不同）
                    If Not String.IsNullOrEmpty(settings.PlotterName) AndAlso settings.PlotterName <> layout.PlotConfigurationName Then
                        Try
                            psv.SetPlotConfigurationName(ps, settings.PlotterName, Nothing)
                            _logger.Info($"覆盖布局打印机: {layout.PlotConfigurationName} -> {settings.PlotterName}")
                        Catch ex As Exception
                            _logger.Warning($"设置打印机失败，使用布局原有设置: {ex.Message}")
                        End Try
                    End If

                    ' 应用纸张设置（如果用户指定了）
                    If Not String.IsNullOrEmpty(settings.PaperSize) Then
                        Try
                            Dim canonicalName As String = ResolveMediaName(ps, settings.PaperSize)
                            If canonicalName <> layout.CanonicalMediaName Then
                                psv.SetPlotConfigurationName(ps, ps.PlotConfigurationName, canonicalName)
                                psv.RefreshLists(ps)
                                _logger.Info($"覆盖布局纸张: {layout.CanonicalMediaName} -> {canonicalName}")
                            End If
                        Catch ex As Exception
                            _logger.Warning($"设置纸张失败，使用布局原有设置: {ex.Message}")
                        End Try
                    End If

                    ' 应用打印样式（如果用户指定了且与布局不同）
                    If Not String.IsNullOrEmpty(settings.PlotStyle) AndAlso settings.PlotStyle <> "无" AndAlso settings.PlotStyle <> layout.CurrentStyleSheet Then
                        Try
                            ' 命名打印样式(STB)图纸只能用 .stb 样式，不兼容时跳过并警告
                            Dim styleName As String = settings.PlotStyle
                            If _db.PlotStyleMode AndAlso Not styleName.EndsWith(".stb", StringComparison.OrdinalIgnoreCase) Then
                                _logger.Warning($"打印样式 '{styleName}' 与图纸模式不兼容（命名打印样式图纸需 .stb），保留布局原样式")
                            Else
                                psv.SetCurrentStyleSheet(ps, styleName)
                                ' 显式启用打印样式应用，防止布局原值为关闭导致样式不生效
                                ps.PlotPlotStyles = True
                                _logger.Info($"覆盖布局打印样式: {layout.CurrentStyleSheet} -> {styleName}")
                            End If
                        Catch ex As Exception
                            _logger.Warning($"设置打印样式失败，使用布局原有设置: {ex.Message}")
                        End Try
                    End If

                    ' 应用打印比例（仅当用户选择FitToPaper时）
                    If settings.PlotScale = PlotScaleType.FitToPaper Then
                        psv.SetUseStandardScale(ps, True)
                        psv.SetStdScaleType(ps, StdScaleType.ScaleToFit)
                        _logger.Info("应用FitToPaper打印比例")
                    End If

                    ' 创建PlotInfo
                    Dim pi As New PlotInfo()
                    pi.Layout = layoutId
                    pi.OverrideSettings = ps

                    ' 验证
                    Using piv As New PlotInfoValidator()
                        piv.MediaMatchingPolicy = MatchingPolicy.MatchEnabled
                        Try
                            piv.Validate(pi)
                        Catch ex As Exception
                            _logger.Warning($"PlotInfo验证警告: {ex.Message}")
                        End Try
                    End Using

                    ' 执行打印（异常时自动逐级清理引擎状态）
                    RunPlotEngineSafely(pi, layoutName, Nothing)

                    ps.Dispose()
                    pi.Dispose()
                    tr.Commit()
                End Using
            End Using

            _logger.Info($"布局 {layoutName} 第{currentCopy}/{totalCopies}份: 打印完成")

        Catch ex As Exception
            _logger.Error($"打印布局 {layoutName} 第{currentCopy}份失败", ex)
            Throw
        End Try
    End Sub

#End Region

#Region "Utility Functions"

    ' 高亮显示图框
    Public Sub HighlightFrames(frames As List(Of FrameInfo))
        Try
            Using tr As Transaction = _db.TransactionManager.StartTransaction()
                For Each frame As FrameInfo In frames
                    SetFrameHighlight(tr, frame, True)
                Next
                tr.Commit()
            End Using
        Catch ex As Exception
            _logger.Warning("高亮显示图框失败: " & ex.Message)
        End Try
    End Sub

    ' 排序图框：先按布局分组，再在组内做带状空间排序
    Public Sub OrderFrames(frames As List(Of FrameInfo), orderType As PlotOrderType, reverse As Boolean)
        If orderType <> PlotOrderType.SelectOrder Then
            Dim groups = GroupFramesByLayout(frames)
            For Each group In groups
                SortFramesSpatially(group, orderType)
            Next
            frames.Clear()
            For Each group In groups
                frames.AddRange(group)
            Next
        End If

        If reverse Then
            frames.Reverse()
        End If

        For i = 0 To frames.Count - 1
            frames(i).OrderIndex = i + 1
        Next
    End Sub

    ' 按布局分组图框（保持出现顺序），组间按布局 TabOrder 排序
    Private Function GroupFramesByLayout(frames As List(Of FrameInfo)) As List(Of List(Of FrameInfo))
        Dim groups As New List(Of List(Of FrameInfo))()
        Dim indexByLayout As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)

        For Each frame In frames
            Dim key As String = If(frame.LayoutName, String.Empty)
            Dim groupIndex As Integer
            If Not indexByLayout.TryGetValue(key, groupIndex) Then
                groupIndex = groups.Count
                indexByLayout(key) = groupIndex
                groups.Add(New List(Of FrameInfo)())
            End If
            groups(groupIndex).Add(frame)
        Next

        Dim tabOrders = TryReadLayoutTabOrders()
        If tabOrders IsNot Nothing AndAlso tabOrders.Count > 0 Then
            groups.Sort(Function(left, right)
                            Dim leftKey = If(left(0).LayoutName, String.Empty)
                            Dim rightKey = If(right(0).LayoutName, String.Empty)
                            Dim leftOrder = Integer.MaxValue
                            Dim rightOrder = Integer.MaxValue
                            If tabOrders.ContainsKey(leftKey) Then leftOrder = tabOrders(leftKey)
                            If tabOrders.ContainsKey(rightKey) Then rightOrder = tabOrders(rightKey)
                            Return leftOrder.CompareTo(rightOrder)
                        End Function)
        End If

        Return groups
    End Function

    ' 读取所有布局的 TabOrder（键为布局名），失败返回 Nothing
    Private Function TryReadLayoutTabOrders() As Dictionary(Of String, Integer)
        Try
            Dim result As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
            Using tr = _db.TransactionManager.StartTransaction()
                Dim layoutDict = TryCast(tr.GetObject(_db.LayoutDictionaryId, OpenMode.ForRead), DBDictionary)
                If layoutDict Is Nothing Then Return Nothing
                For Each entry As DBDictionaryEntry In layoutDict
                    Dim layout = TryCast(tr.GetObject(entry.Value, OpenMode.ForRead), Layout)
                    If layout IsNot Nothing Then
                        result(layout.LayoutName) = layout.TabOrder
                    End If
                Next
                tr.Commit()
            End Using
            Return result
        Catch
            Return Nothing
        End Try
    End Function

    ' 组内带状空间排序：TopBottom=列优先(从左到右选列,同列从上到下)；LeftRight=行优先(从上到下选行,同行从左到右)
    ' 只收与锚点本身处于同一条带的图框，不做传递扩展，避免混合图幅时顺序跳动
    Private Sub SortFramesSpatially(frames As List(Of FrameInfo), orderType As PlotOrderType)
        If frames Is Nothing OrElse frames.Count < 2 Then Return

        Dim horizontalFirst As Boolean = (orderType = PlotOrderType.TopBottom)
        Dim items As New List(Of FrameSortItem)(frames.Count)
        For i = 0 To frames.Count - 1
            items.Add(New FrameSortItem(frames(i), i, frames(i).Bounds))
        Next

        Dim remaining As New List(Of FrameSortItem)(items)
        Dim ordered As New List(Of FrameSortItem)()

        While remaining.Count > 0
            ' 选择锚点
            Dim anchor As FrameSortItem
            If horizontalFirst Then
                anchor = remaining.OrderBy(Function(x) x.MinX).ThenByDescending(Function(x) x.MaxY).First()
            Else
                anchor = remaining.OrderByDescending(Function(x) x.MaxY).ThenBy(Function(x) x.MinX).First()
            End If
            remaining.Remove(anchor)

            ' 收集与锚点处于同一条带的图框
            Dim band As New List(Of FrameSortItem)()
            band.Add(anchor)
            For i = remaining.Count - 1 To 0 Step -1
                If SharesBandWithAnchor(remaining(i), anchor, horizontalFirst) Then
                    band.Add(remaining(i))
                    remaining.RemoveAt(i)
                End If
            Next

            ' 带内排序
            If horizontalFirst Then
                ordered.AddRange(band.OrderByDescending(Function(x) x.CenterY).ThenBy(Function(x) x.CenterX))
            Else
                ordered.AddRange(band.OrderBy(Function(x) x.CenterX).ThenByDescending(Function(x) x.CenterY))
            End If
        End While

        For i = 0 To ordered.Count - 1
            frames(i) = ordered(i).Frame
        Next
    End Sub

    ' 判断 item 与锚点是否处于同一条带（重叠量不小于较小跨度的 30%）
    Private Function SharesBandWithAnchor(item As FrameSortItem, anchor As FrameSortItem, horizontalFirst As Boolean) As Boolean
        If horizontalFirst Then
            Dim overlap = Math.Min(item.MaxX, anchor.MaxX) - Math.Max(item.MinX, anchor.MinX)
            Dim minSpan = Math.Min(item.MaxX - item.MinX, anchor.MaxX - anchor.MinX)
            Return minSpan > 0 AndAlso overlap >= minSpan * 0.3
        Else
            Dim overlap = Math.Min(item.MaxY, anchor.MaxY) - Math.Max(item.MinY, anchor.MinY)
            Dim minSpan = Math.Min(item.MaxY - item.MinY, anchor.MaxY - anchor.MinY)
            Return minSpan > 0 AndAlso overlap >= minSpan * 0.3
        End If
    End Function

    ' 带状排序使用的空间项
    Private Class FrameSortItem
        Public ReadOnly Frame As FrameInfo
        Public ReadOnly OriginalIndex As Integer
        Public ReadOnly MinX As Double
        Public ReadOnly MaxX As Double
        Public ReadOnly MinY As Double
        Public ReadOnly MaxY As Double
        Public ReadOnly CenterX As Double
        Public ReadOnly CenterY As Double

        Public Sub New(frame As FrameInfo, index As Integer, bounds As Extents3d)
            Me.Frame = frame
            Me.OriginalIndex = index
            MinX = bounds.MinPoint.X
            MaxX = bounds.MaxPoint.X
            MinY = bounds.MinPoint.Y
            MaxY = bounds.MaxPoint.Y
            CenterX = (MinX + MaxX) / 2.0
            CenterY = (MinY + MaxY) / 2.0
        End Sub
    End Class

    ' 处理重名文件
    Private Function HandleDuplicateFile(filePath As String, handling As DuplicateFileHandling) As String
        If Not File.Exists(filePath) Then
            Return filePath
        End If

        Select Case handling
            Case DuplicateFileHandling.Overwrite
                Return filePath
            Case DuplicateFileHandling.Skip
                Return String.Empty
            Case DuplicateFileHandling.AutoRename
                Dim directory As String = Path.GetDirectoryName(filePath)
                Dim fileNameWithoutExt As String = Path.GetFileNameWithoutExtension(filePath)
                Dim extension As String = Path.GetExtension(filePath)
                Dim counter = 1
                Dim newFilePath As String

                Do
                    newFilePath = Path.Combine(directory, $"{fileNameWithoutExt}({counter}){extension}")
                    counter += 1
                Loop While File.Exists(newFilePath)

                Return newFilePath
            Case Else
                Return filePath
        End Select
    End Function

    ' 处理重名布局
    Private Function HandleDuplicateLayoutName(layoutDict As DBDictionary, layoutName As String, handling As DuplicateFileHandling) As String
        If Not layoutDict.Contains(layoutName) Then
            Return layoutName
        End If

        Select Case handling
            Case DuplicateFileHandling.Overwrite
                Return layoutName
            Case DuplicateFileHandling.Skip
                Return String.Empty
            Case DuplicateFileHandling.AutoRename
                Dim counter = 1
                Dim newLayoutName As String

                Do
                    newLayoutName = $"{layoutName}({counter})"
                    counter += 1
                Loop While layoutDict.Contains(newLayoutName)

                Return newLayoutName
            Case Else
                Return layoutName
        End Select
    End Function

    ' 格式化文件名
    Public Function FormatFileName(template As String, frameNumber As String, index As Integer, Optional frame As FrameInfo = Nothing, Optional paperName As String = Nothing) As String
        ' 净化后的兜底名（模板替换结果为空时使用）
        Dim fallbackName As String = If(String.IsNullOrWhiteSpace(frameNumber), $"图纸_{index:D3}", frameNumber)
        Try
            Dim result As String = template
            result = result.Replace("<DWGNAME>", Path.GetFileNameWithoutExtension(_doc.Name))
            result = result.Replace("<DWGFOLDERNAME>", Path.GetFileName(Path.GetDirectoryName(_doc.Name)))
            result = result.Replace("<CTAB>", frameNumber)
            result = result.Replace("<PAPERNAME>", ResolvePaperName(frame, paperName))
            result = result.Replace("<INDEX>", index.ToString("D3"))
            result = result.Replace("<DATE>", DateTime.Now.ToString("yyyyMMdd"))
            result = result.Replace("<TIME>", DateTime.Now.ToString("HHmmss"))

            result = ProcessBlockAttributeTemplate(result, frame)
            result = ProcessLayerTextTemplate(result, frame)

            Return SanitizeFileName(result, fallbackName)
        Catch ex As Exception
            _logger.Error("格式化文件名失败", ex)
            Return SanitizeFileName(template, fallbackName)
        End Try
    End Function

    ' 净化输出文件名：图纸属性值（<BLOCKATTR>/<LAYERTEXT>）可能含 Windows 非法字符，
    ' 未净化会导致打印输出/拆分归档写文件失败或落到意外子目录
    Private Shared ReadOnly s_invalidFileNameChars As Char() = New Char() {"\"c, "/"c, ":"c, "*"c, "?"c, """"c, "<"c, ">"c, "|"c}

    Private Shared Function SanitizeFileName(name As String, fallbackName As String) As String
        If String.IsNullOrWhiteSpace(name) Then
            Return fallbackName
        End If

        Dim sb As New Text.StringBuilder(name.Length)
        For Each ch As Char In name
            If Array.IndexOf(s_invalidFileNameChars, ch) >= 0 OrElse Char.IsControl(ch) Then
                sb.Append("_"c)
            Else
                sb.Append(ch)
            End If
        Next

        Dim result As String = sb.ToString().Trim().TrimEnd("."c, " "c)
        If String.IsNullOrWhiteSpace(result) Then
            Return fallbackName
        End If

        ' 保留设备名（CON/PRN/AUX/NUL/COM1-9/LPT1-9）加前缀，避免写文件失败
        Dim stem As String = result.Split("."c)(0)
        If Text.RegularExpressions.Regex.IsMatch(stem, "^(CON|PRN|AUX|NUL|COM\d|LPT\d)$", Text.RegularExpressions.RegexOptions.IgnoreCase) Then
            result = "_" & result
        End If

        ' 限制文件名长度（NTFS 文件名上限 255，留路径余量）
        If result.Length > 200 Then
            result = result.Substring(0, 200).TrimEnd("."c, " "c)
        End If

        Return result
    End Function

    Private Function ProcessBlockAttributeTemplate(template As String, Optional frame As FrameInfo = Nothing) As String
        Dim result As String = template
        Dim pattern = "<BLOCKATTR:([^>]+):([^>]+)>"
        Dim matches As RegularExpressions.MatchCollection = RegularExpressions.Regex.Matches(template, pattern)

        For Each match As RegularExpressions.Match In matches
            Dim blockName As String = match.Groups(1).Value
            Dim attrName As String = match.Groups(2).Value
            Dim attrValue As String = GetBlockAttributeValue(blockName, attrName, frame)
            result = result.Replace(match.Value, attrValue)
        Next

        Return result
    End Function

    Private Function GetBlockAttributeValue(blockName As String, attrName As String, Optional frame As FrameInfo = Nothing) As String
        Try
            Using tr As Transaction = _db.TransactionManager.StartTransaction()
                Dim bt = CType(tr.GetObject(_db.BlockTableId, OpenMode.ForRead), BlockTable)
                Dim space = CType(tr.GetObject(GetFrameSpaceId(tr, bt, frame), OpenMode.ForRead), BlockTableRecord)

                Dim intersectBr As BlockReference = Nothing

                For Each objId As ObjectId In space
                    If objId.ObjectClass.DxfName <> "INSERT" Then Continue For

                    Dim br = TryCast(tr.GetObject(objId, OpenMode.ForRead), BlockReference)
                    If br Is Nothing Then Continue For

                    Dim btr = TryCast(tr.GetObject(br.BlockTableRecord, OpenMode.ForRead), BlockTableRecord)
                    If btr Is Nothing OrElse Not String.Equals(btr.Name, blockName, StringComparison.OrdinalIgnoreCase) Then Continue For

                    Dim extents As Extents3d
                    Try
                        extents = br.GeometricExtents
                    Catch
                        Continue For
                    End Try

                    Dim relation = GetFrameRelation(extents, frame)
                    If relation <= 1 Then
                        ' 完全位于图框内（或无图框约束）的块优先
                        Return ReadBlockAttributeValue(tr, br, attrName)
                    ElseIf relation = 2 AndAlso intersectBr Is Nothing Then
                        intersectBr = br
                    End If
                Next

                If intersectBr IsNot Nothing Then
                    Return ReadBlockAttributeValue(tr, intersectBr, attrName)
                End If

                tr.Commit()
            End Using
        Catch ex As Exception
            _logger.Warning($"获取图块属性失败: {blockName}.{attrName}")
        End Try

        Return String.Empty
    End Function

    ' 读取块引用上指定 Tag 的属性值
    Private Function ReadBlockAttributeValue(tr As Transaction, br As BlockReference, attrName As String) As String
        For Each attrId As ObjectId In br.AttributeCollection
            Dim attr = TryCast(tr.GetObject(attrId, OpenMode.ForRead), AttributeReference)
            If attr IsNot Nothing AndAlso String.Equals(attr.Tag, attrName, StringComparison.OrdinalIgnoreCase) Then
                Return attr.TextString
            End If
        Next
        Return String.Empty
    End Function

    Private Function ProcessLayerTextTemplate(template As String, Optional frame As FrameInfo = Nothing) As String
        Dim result As String = template
        Dim pattern = "<LAYERTEXT:([^>]+)>"
        Dim matches As RegularExpressions.MatchCollection = RegularExpressions.Regex.Matches(template, pattern)

        For Each match As RegularExpressions.Match In matches
            Dim layerName As String = match.Groups(1).Value
            Dim textValue As String = GetLayerTextValue(layerName, frame)
            result = result.Replace(match.Value, textValue)
        Next

        Return result
    End Function

    Private Function GetLayerTextValue(layerName As String, Optional frame As FrameInfo = Nothing) As String
        Try
            Using tr As Transaction = _db.TransactionManager.StartTransaction()
                Dim bt = CType(tr.GetObject(_db.BlockTableId, OpenMode.ForRead), BlockTable)
                Dim space = CType(tr.GetObject(GetFrameSpaceId(tr, bt, frame), OpenMode.ForRead), BlockTableRecord)

                Dim intersectText As Entity = Nothing

                For Each objId As ObjectId In space
                    Dim ent = TryCast(tr.GetObject(objId, OpenMode.ForRead), Entity)
                    If ent Is Nothing OrElse Not String.Equals(ent.Layer, layerName, StringComparison.OrdinalIgnoreCase) Then Continue For

                    Dim isText = TypeOf ent Is DBText OrElse TypeOf ent Is MText
                    If Not isText Then Continue For

                    Dim extents As Extents3d
                    Try
                        extents = ent.GeometricExtents
                    Catch
                        Continue For
                    End Try

                    Dim relation = GetFrameRelation(extents, frame)
                    If relation <= 1 Then
                        ' 完全位于图框内（或无图框约束）的文字优先
                        Return ReadEntityTextValue(ent)
                    ElseIf relation = 2 AndAlso intersectText Is Nothing Then
                        intersectText = ent
                    End If
                Next

                If intersectText IsNot Nothing Then
                    Return ReadEntityTextValue(intersectText)
                End If

                tr.Commit()
            End Using
        Catch ex As Exception
            _logger.Warning($"获取图层文字失败: {layerName}")
        End Try

        Return String.Empty
    End Function

    ' 读取文字实体（DBText / MText）的内容
    Private Function ReadEntityTextValue(ent As Entity) As String
        If TypeOf ent Is DBText Then
            Return CType(ent, DBText).TextString
        ElseIf TypeOf ent Is MText Then
            Return CType(ent, MText).Contents
        End If
        Return String.Empty
    End Function

    ' 解析实际纸张名：优先从打印设置纸张名中提取 A0-A5，否则按图框尺寸推断
    Private Function ResolvePaperName(frame As FrameInfo, paperName As String) As String
        If Not String.IsNullOrEmpty(paperName) Then
            Dim m = RegularExpressions.Regex.Match(paperName, "A[0-5]")
            If m.Success Then Return m.Value
            Return paperName
        End If

        If frame Is Nothing Then Return String.Empty

        Try
            Dim width = frame.Bounds.MaxPoint.X - frame.Bounds.MinPoint.X
            Dim height = frame.Bounds.MaxPoint.Y - frame.Bounds.MinPoint.Y
            Dim longSide = Math.Max(width, height)
            Dim shortSide = Math.Min(width, height)
            Const sizeTolerance = 15.0

            ' A0-A4 系列纸张的 {长边, 短边}（毫米）
            Dim paperSizes = New Double(,) {{1189, 841}, {841, 594}, {594, 420}, {420, 297}, {297, 210}}
            For i = 0 To 4
                If Math.Abs(longSide - paperSizes(i, 0)) <= sizeTolerance AndAlso
                   Math.Abs(shortSide - paperSizes(i, 1)) <= sizeTolerance Then
                    Return "A" & i.ToString()
                End If
            Next
        Catch
            ' 忽略尺寸推断失败
        End Try

        Return String.Empty
    End Function

    ' 获取取值扫描的目标空间（按图框所在布局名匹配，失败回退模型空间）
    Private Function GetFrameSpaceId(tr As Transaction, bt As BlockTable, frame As FrameInfo) As ObjectId
        If frame IsNot Nothing AndAlso Not String.IsNullOrEmpty(frame.LayoutName) Then
            For Each btId As ObjectId In bt
                Dim btr = TryCast(tr.GetObject(btId, OpenMode.ForRead), BlockTableRecord)
                If btr IsNot Nothing AndAlso btr.IsLayout AndAlso
                   String.Equals(btr.Name, frame.LayoutName, StringComparison.OrdinalIgnoreCase) Then
                    Return btId
                End If
            Next
        End If
        Return bt(BlockTableRecord.ModelSpace)
    End Function

    ' 实体范围与图框的关系：0=无图框约束 1=完全在图框内 2=与图框相交 3=在图框外
    Private Function GetFrameRelation(extents As Extents3d, frame As FrameInfo) As Integer
        If frame Is Nothing Then Return 0
        If FrameContains(extents, frame.Bounds) Then Return 1
        If IsEntityInFrame(extents, frame.Bounds.MinPoint, frame.Bounds.MaxPoint) Then Return 2
        Return 3
    End Function

    ' 判断实体范围是否完全位于图框内
    Private Function FrameContains(extents As Extents3d, frameBounds As Extents3d) As Boolean
        Return extents.MinPoint.X >= frameBounds.MinPoint.X AndAlso
               extents.MinPoint.Y >= frameBounds.MinPoint.Y AndAlso
               extents.MaxPoint.X <= frameBounds.MaxPoint.X AndAlso
               extents.MaxPoint.Y <= frameBounds.MaxPoint.Y
    End Function

#End Region

#Region "Interactive Frame Selection"

    ' 判断图框是否符合图块名/图层名过滤条件（批量版本，共享事务）
    Public Sub ApplyFilterToFrames(frames As List(Of FrameInfo), blockName As String, layerName As String)
        Try
            Using tr As Transaction = _db.TransactionManager.StartTransaction()
                For Each frame As FrameInfo In frames
                    If frame.ObjectId.IsNull Then
                        frame.IsSelected = False
                        Continue For
                    End If

                    Try
                        Dim ent = CType(tr.GetObject(frame.ObjectId, OpenMode.ForRead), Entity)

                        ' 检查图层名
                        Dim match As Boolean = True
                        If Not String.IsNullOrEmpty(layerName) AndAlso Not MatchNamePattern(ent.Layer, layerName) Then
                            match = False
                        End If

                        ' 图块形式还需要检查图块名
                        If match AndAlso frame.ObjectId.ObjectClass.DxfName = "INSERT" Then
                            Dim br = TryCast(ent, BlockReference)
                            If br IsNot Nothing Then
                                Dim btr = CType(tr.GetObject(br.BlockTableRecord, OpenMode.ForRead), BlockTableRecord)
                                If Not String.IsNullOrEmpty(blockName) AndAlso Not MatchNamePattern(btr.Name, blockName) Then
                                    match = False
                                End If
                            End If
                        End If

                        frame.IsSelected = match
                    Catch
                        frame.IsSelected = False
                    End Try
                Next
                tr.Commit()
            End Using
        Catch ex As Exception
            _logger.Warning("批量过滤图框失败: " & ex.Message)
        End Try
    End Sub

    ' 判断名称是否匹配逗号分隔的多个模式（委托给 FrameDetector）
    Private Function MatchNamePattern(name As String, pattern As String) As Boolean
        Return FrameDetector.MatchAnyPattern(name, pattern)
    End Function

    ' 设置单个图框的高亮/取消高亮（包括关联对象）
    Private Sub SetFrameHighlight(tr As Transaction, frame As FrameInfo, highlight As Boolean)
        If frame.ObjectId.IsNull Then Return
        Try
            Dim ent = CType(tr.GetObject(frame.ObjectId, OpenMode.ForRead), Entity)
            If highlight Then
                ent.Highlight()
            Else
                ent.Unhighlight()
            End If
            For Each relId As ObjectId In frame.RelatedObjectIds
                If relId.IsNull OrElse relId = frame.ObjectId Then Continue For
                Try
                    Dim relEnt = CType(tr.GetObject(relId, OpenMode.ForRead), Entity)
                    If highlight Then
                        relEnt.Highlight()
                    Else
                        relEnt.Unhighlight()
                    End If
                Catch
                End Try
            Next
        Catch
        End Try
    End Sub

    ' 高亮显示选中的图框，取消高亮未选中的图框
    Public Sub UpdateFrameHighlight(frames As List(Of FrameInfo))
        Try
            Using tr As Transaction = _db.TransactionManager.StartTransaction()
                For Each frame As FrameInfo In frames
                    SetFrameHighlight(tr, frame, frame.IsSelected)
                Next
                tr.Commit()
            End Using
            Application.UpdateScreen()
        Catch ex As Exception
            _logger.Warning("更新图框高亮状态失败: " & ex.Message)
        End Try
    End Sub

    ' 取消所有图框的高亮
    Public Sub UnhighlightAllFrames(frames As List(Of FrameInfo))
        Try
            Using tr As Transaction = _db.TransactionManager.StartTransaction()
                For Each frame As FrameInfo In frames
                    SetFrameHighlight(tr, frame, False)
                Next
                tr.Commit()
            End Using
            Application.UpdateScreen()
        Catch ex As Exception
            _logger.Warning("取消图框高亮失败: " & ex.Message)
        End Try
    End Sub

    ' 根据用户点选的实体查找匹配的图框（通过 ObjectId 匹配，包括关联对象）
    Public Function FindFrameByObjectId(frames As List(Of FrameInfo), objId As ObjectId) As FrameInfo
        For Each frame As FrameInfo In frames
            If frame.ObjectId = objId Then
                Return frame
            End If
            ' 检查关联对象（如直线图框的其他直线）
            For Each relId As ObjectId In frame.RelatedObjectIds
                If relId = objId Then
                    Return frame
                End If
            Next
        Next
        Return Nothing
    End Function

    ' 根据用户点选的实体查找匹配的图框
    ' isBlockMode=True: 图块形式，通过图块名匹配
    ' isBlockMode=False: 多段线/直线形式，通过图层名匹配
    ' 嵌套时取最外层（面积最大）
    Public Function FindFrameByPick(frames As List(Of FrameInfo), objId As ObjectId, pickPoint As Point3d, isBlockMode As Boolean) As FrameInfo
        Try
            Using tr As Transaction = _db.TransactionManager.StartTransaction()
                ' 先尝试 ObjectId 直接匹配
                Dim directMatch = FindFrameByObjectId(frames, objId)
                If directMatch IsNot Nothing Then Return directMatch

                If isBlockMode Then
                    ' 图块形式：获取点选实体的图块名
                    If objId.ObjectClass.DxfName <> "INSERT" Then Return Nothing
                    Dim br = TryCast(tr.GetObject(objId, OpenMode.ForRead), BlockReference)
                    If br Is Nothing Then Return Nothing
                    Dim btr = CType(tr.GetObject(br.BlockTableRecord, OpenMode.ForRead), BlockTableRecord)
                    Dim blockName As String = btr.Name

                    ' 查找同名图块中包含点击位置的所有图框，取最外层
                    Dim bestFrame As FrameInfo = Nothing
                    Dim largestArea As Double = 0

                    For Each frame As FrameInfo In frames
                        If frame.BlockName <> blockName Then Continue For
                        If pickPoint.X >= frame.Bounds.MinPoint.X AndAlso pickPoint.X <= frame.Bounds.MaxPoint.X AndAlso
                           pickPoint.Y >= frame.Bounds.MinPoint.Y AndAlso pickPoint.Y <= frame.Bounds.MaxPoint.Y Then
                            Dim area As Double = (frame.Bounds.MaxPoint.X - frame.Bounds.MinPoint.X) * (frame.Bounds.MaxPoint.Y - frame.Bounds.MinPoint.Y)
                            If area > largestArea Then
                                largestArea = area
                                bestFrame = frame
                            End If
                        End If
                    Next

                    Return bestFrame
                Else
                    ' 多段线/直线形式：获取点选实体的图层名
                    Dim ent = TryCast(tr.GetObject(objId, OpenMode.ForRead), Entity)
                    If ent Is Nothing Then Return Nothing
                    Dim layerName As String = ent.Layer

                    ' 查找该图层上包含点击位置的所有图框，取最外层
                    Dim bestFrame As FrameInfo = Nothing
                    Dim largestArea As Double = 0

                    For Each frame As FrameInfo In frames
                        Dim frameEnt = TryCast(tr.GetObject(frame.ObjectId, OpenMode.ForRead), Entity)
                        If frameEnt Is Nothing OrElse frameEnt.Layer <> layerName Then Continue For

                        If pickPoint.X >= frame.Bounds.MinPoint.X AndAlso pickPoint.X <= frame.Bounds.MaxPoint.X AndAlso
                           pickPoint.Y >= frame.Bounds.MinPoint.Y AndAlso pickPoint.Y <= frame.Bounds.MaxPoint.Y Then
                            Dim area As Double = (frame.Bounds.MaxPoint.X - frame.Bounds.MinPoint.X) * (frame.Bounds.MaxPoint.Y - frame.Bounds.MinPoint.Y)
                            If area > largestArea Then
                                largestArea = area
                                bestFrame = frame
                            End If
                        End If
                    Next

                    Return bestFrame
                End If
            End Using
        Catch
        End Try
        Return Nothing
    End Function

#End Region

#Region "Page Setup"

    Private Sub CreatePageSetupForPlotter(plotterName As String, paperSize As String)
        CreatePageSetupForPlotterWithName(plotterName, paperSize, "BatchPlot_PS")
    End Sub

    Private Sub CreatePageSetupForPlotterWithName(plotterName As String, paperSize As String, pageSetupName As String)
        Using tr As Transaction = _db.TransactionManager.StartTransaction()
            Dim psDict = CType(tr.GetObject(_db.PlotSettingsDictionaryId, OpenMode.ForWrite), DBDictionary)

            If psDict.Contains(pageSetupName) Then
                psDict.Remove(pageSetupName)
            End If

            Dim ps As New Autodesk.AutoCAD.DatabaseServices.PlotSettings(True)
            ps.PlotSettingsName = pageSetupName
            ps.AddToPlotSettingsDictionary(_db)
            tr.AddNewlyCreatedDBObject(ps, True)

            Dim psv As PlotSettingsValidator = PlotSettingsValidator.Current
            Try
                Dim canonicalPaperName As String = GetCanonicalPaperName(paperSize)
                psv.SetPlotConfigurationName(ps, plotterName, canonicalPaperName)
            Catch ex As Exception
                _logger.Warning($"设置页面设置打印机失败: {ex.Message}")
            End Try

            tr.Commit()
        End Using
    End Sub

#End Region

#Region "Plot Stamp"

    Private Function FindDefaultPlotStampFile() As String
        Try
            Dim supportPaths As String = String.Empty
            Try
                supportPaths = Application.GetSystemVariable("ACAD").ToString()
            Catch
            End Try

            Dim paths As String() = supportPaths.Split(New Char() {";"c}, StringSplitOptions.RemoveEmptyEntries)
            For Each searchPath As String In paths
                If Directory.Exists(searchPath) Then
                    Dim mmFile As String = Path.Combine(searchPath, "Mm.pss")
                    Dim inchesFile As String = Path.Combine(searchPath, "Inches.pss")
                    If File.Exists(mmFile) Then Return mmFile
                    If File.Exists(inchesFile) Then Return inchesFile
                End If
            Next

            Dim supportDir As String = AutoCADPathResolver.GetSupportPath()
            If Not String.IsNullOrEmpty(supportDir) Then
                Dim mmFile As String = Path.Combine(supportDir, "Mm.pss")
                Dim inchesFile As String = Path.Combine(supportDir, "Inches.pss")
                If File.Exists(mmFile) Then Return mmFile
                If File.Exists(inchesFile) Then Return inchesFile
            End If
        Catch ex As Exception
            _logger.Warning("查找默认打印戳记文件失败: " & ex.Message)
        End Try

        Return String.Empty
    End Function

#End Region

#Region "Config Management (Delegated)"

    Public Function LoadConfig(filePath As String) As Dictionary(Of String, String)
        Return _configManager.LoadPreset(Path.GetFileNameWithoutExtension(filePath))
    End Function

    Public Sub SaveConfig(config As Dictionary(Of String, String), filePath As String)
        _configManager.SavePreset(Path.GetFileNameWithoutExtension(filePath), config)
    End Sub

    Public Function GetPresetNames() As List(Of String)
        Return _configManager.GetPresetNames()
    End Function

#End Region

#Region "Legacy Commands"

    ' 获取图块列表
    Public Function GetBlockList() As List(Of String)
        Dim blocks As New List(Of String)()
        Try
            Using tr As Transaction = _db.TransactionManager.StartTransaction()
                Dim bt = CType(tr.GetObject(_db.BlockTableId, OpenMode.ForRead), BlockTable)
                For Each id As ObjectId In bt
                    Dim btr = CType(tr.GetObject(id, OpenMode.ForRead), BlockTableRecord)
                    If Not btr.IsAnonymous AndAlso Not btr.IsLayout Then
                        blocks.Add(btr.Name)
                    End If
                Next
                tr.Commit()
            End Using
        Catch ex As Exception
            _logger.Error("获取图块列表失败", ex)
        End Try
        Return blocks
    End Function

    ' 获取图层列表
    Public Function GetLayerList() As List(Of String)
        Dim layers As New List(Of String)()
        Try
            Using tr As Transaction = _db.TransactionManager.StartTransaction()
                Dim lt = CType(tr.GetObject(_db.LayerTableId, OpenMode.ForRead), LayerTable)
                For Each id As ObjectId In lt
                    Dim ltr = CType(tr.GetObject(id, OpenMode.ForRead), LayerTableRecord)
                    layers.Add(ltr.Name)
                Next
                tr.Commit()
            End Using
        Catch ex As Exception
            _logger.Error("获取图层列表失败", ex)
        End Try
        Return layers
    End Function

    ' 图框去重
    Public Function BoxUnique(frames As List(Of FrameInfo)) As List(Of FrameInfo)
        Dim uniqueFrames As New List(Of FrameInfo)()
        Try
            For Each frame As FrameInfo In frames
                Dim isDuplicate = False
                For Each existing As FrameInfo In uniqueFrames
                    If Math.Abs(frame.Bounds.MinPoint.X - existing.Bounds.MinPoint.X) < 0.001 AndAlso
                       Math.Abs(frame.Bounds.MinPoint.Y - existing.Bounds.MinPoint.Y) < 0.001 AndAlso
                       Math.Abs(frame.Bounds.MaxPoint.X - existing.Bounds.MaxPoint.X) < 0.001 AndAlso
                       Math.Abs(frame.Bounds.MaxPoint.Y - existing.Bounds.MaxPoint.Y) < 0.001 Then
                        isDuplicate = True
                        Exit For
                    End If
                Next
                If Not isDuplicate Then
                    uniqueFrames.Add(frame)
                End If
            Next
        Catch ex As Exception
            _logger.Error("图框去重失败", ex)
        End Try
        Return uniqueFrames
    End Function

    ' 多文档打印脚本
    Public Sub MultiDocPlotScript(fileList As List(Of String), settings As PlotSettings, progressCallback As Action(Of Integer, String))
        Try
            Dim plotStyle As String = If(String.IsNullOrEmpty(settings.PlotStyle) OrElse settings.PlotStyle = "无", ".", settings.PlotStyle)
            Dim count = 0
            Dim total As Integer = fileList.Count

            For Each filePath As String In fileList
                count += 1
                progressCallback?.Invoke(CInt((count / total) * 100), $"正在处理: {Path.GetFileName(filePath)}")

                Dim sb As New StringBuilder(1024)
                sb.Append($"_-OPEN ""{filePath}""" & vbLf)
                sb.Append("_-PLOT" & vbLf)
                sb.Append("Y" & vbLf)
                sb.Append("Model" & vbLf)
                sb.Append(settings.PlotterName & vbLf)
                sb.Append(settings.PaperSize & vbLf)
                sb.Append("M" & vbLf)
                sb.Append("L" & vbLf)
                sb.Append("N" & vbLf)
                sb.Append("E" & vbLf)
                sb.Append("F" & vbLf)
                sb.Append("C" & vbLf)
                sb.Append("Y" & vbLf)
                sb.Append(plotStyle & vbLf)
                sb.Append("Y" & vbLf)
                sb.Append("A" & vbLf)
                sb.Append("N" & vbLf)
                sb.Append("N" & vbLf)
                sb.Append("Y" & vbLf)
                sb.Append("_-CLOSE" & vbLf)
                sb.Append("Y" & vbLf)
                _doc.SendStringToExecute(sb.ToString(), True, False, False)

                Threading.Thread.Sleep(1000)
            Next

            progressCallback?.Invoke(100, "多文档打印完成")
        Catch ex As Exception
            _logger.Error("多文档打印脚本执行失败", ex)
            Throw New Exception("多文档打印脚本执行失败: " & ex.Message)
        End Try
    End Sub

    ' 打印模型布局
    Public Sub PlotModelLayouts(settings As PlotSettings, progressCallback As Action(Of Integer, String))
        Try
            Dim plotStyle As String = If(String.IsNullOrEmpty(settings.PlotStyle) OrElse settings.PlotStyle = "无", ".", settings.PlotStyle)

            Dim sb As New StringBuilder(512)
            sb.Append("_-PLOT" & vbLf)
            sb.Append("Y" & vbLf)
            sb.Append("Model" & vbLf)
            sb.Append(settings.PlotterName & vbLf)
            sb.Append(settings.PaperSize & vbLf)
            sb.Append("M" & vbLf)
            sb.Append("L" & vbLf)
            sb.Append("N" & vbLf)
            sb.Append("E" & vbLf)
            sb.Append("F" & vbLf)
            sb.Append("C" & vbLf)
            sb.Append("Y" & vbLf)
            sb.Append(plotStyle & vbLf)
            sb.Append("Y" & vbLf)
            sb.Append("A" & vbLf)
            sb.Append("N" & vbLf)
            sb.Append("N" & vbLf)
            sb.Append("Y" & vbLf)
            _doc.SendStringToExecute(sb.ToString(), True, False, False)
            progressCallback?.Invoke(100, "模型布局打印完成")
        Catch ex As Exception
            _logger.Error("打印模型布局失败", ex)
            Throw New Exception("打印模型布局失败: " & ex.Message)
        End Try
    End Sub

    ' 打开打印机配置
    Public Sub OpenPlotterConfig()
        Try
            _doc.SendStringToExecute("-PLOTTERMANAGER ", True, False, False)
        Catch ex As Exception
            _logger.Warning("打开打印机配置失败: " & ex.Message)
        End Try
    End Sub

    ' 打开打印样式编辑
    Public Sub OpenPlotStyleEdit()
        Try
            _doc.SendStringToExecute("-STYLESMANAGER ", True, False, False)
        Catch ex As Exception
            _logger.Warning("打开打印样式编辑失败: " & ex.Message)
        End Try
    End Sub

    ' 获取实际输出文件夹
    Public Function GetActualOutputFolder(basePath As String, useDwgName As Boolean) As String
        Try
            If useDwgName Then
                Dim dwgName As String = Path.GetFileNameWithoutExtension(_doc.Name)
                Return Path.Combine(basePath, dwgName)
            Else
                Return basePath
            End If
        Catch ex As Exception
            _logger.Warning("获取实际输出文件夹失败: " & ex.Message)
            Return basePath
        End Try
    End Function

    ' 图层操作 - 创建或切换图层，设置图层颜色
    Public Sub LayerOperation(layerName As String, colorIndex As Integer)
        Try
            Dim layerId As ObjectId = ObjectId.Null

            Using tr As Transaction = _db.TransactionManager.StartTransaction()
                Dim layerTable = CType(tr.GetObject(_db.LayerTableId, OpenMode.ForWrite), LayerTable)

                If Not layerTable.Has(layerName) Then
                    Dim newLayer As New LayerTableRecord()
                    newLayer.Name = layerName
                    newLayer.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, CShort(colorIndex))
                    layerTable.Add(newLayer)
                    tr.AddNewlyCreatedDBObject(newLayer, True)
                    layerId = newLayer.ObjectId
                Else
                    layerId = layerTable(layerName)
                    Dim layer = CType(tr.GetObject(layerId, OpenMode.ForWrite), LayerTableRecord)
                    layer.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, CShort(colorIndex))
                End If

                tr.Commit()
            End Using

            _db.Clayer = layerId
            _db.Cecolor = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByLayer, 256)
            _db.Celtype = _db.ByLayerLinetype

        Catch ex As Exception
            _logger.Error("图层操作失败", ex)
        End Try
    End Sub

    ' 清理图层
    Public Sub PurgeLayer(layerName As String)
        Try
            Using tr As Transaction = _db.TransactionManager.StartTransaction()
                Dim layerTable = CType(tr.GetObject(_db.LayerTableId, OpenMode.ForWrite), LayerTable)
                If layerTable.Has(layerName) Then
                    Dim layerId As ObjectId = layerTable(layerName)
                    Dim layer = CType(tr.GetObject(layerId, OpenMode.ForWrite), LayerTableRecord)
                    layer.Erase()
                End If
                tr.Commit()
            End Using
        Catch ex As Exception
            _logger.Error("清理图层失败", ex)
        End Try
    End Sub

    ' 扩展边界框
    Public Function ExpandBounding(bounds As Extents3d, distance As Double) As Extents3d
        Try
            Dim minPt As New Point3d(bounds.MinPoint.X - distance, bounds.MinPoint.Y - distance, bounds.MinPoint.Z)
            Dim maxPt As New Point3d(bounds.MaxPoint.X + distance, bounds.MaxPoint.Y + distance, bounds.MaxPoint.Z)
            Return New Extents3d(minPt, maxPt)
        Catch ex As Exception
            _logger.Warning("扩展边界框失败: " & ex.Message)
            Return bounds
        End Try
    End Function

    ' 获取图块属性
    Public Function GetBlockAttributes(blockRefId As ObjectId) As Dictionary(Of String, String)
        Dim attributes As New Dictionary(Of String, String)()
        Try
            Using tr As Transaction = _db.TransactionManager.StartTransaction()
                Dim br = CType(tr.GetObject(blockRefId, OpenMode.ForRead), BlockReference)
                If br IsNot Nothing Then
                    For Each attId As ObjectId In br.AttributeCollection
                        Dim att = CType(tr.GetObject(attId, OpenMode.ForRead), AttributeReference)
                        If att IsNot Nothing Then
                            attributes(att.Tag) = att.TextString
                        End If
                    Next
                End If
                tr.Commit()
            End Using
        Catch ex As Exception
            _logger.Warning("获取图块属性失败: " & ex.Message)
        End Try
        Return attributes
    End Function

    ' 获取文字编号
    Public Function GetTextNumber(text As String) As String
        Try
            Dim result = ""
            For Each c As Char In text
                If Char.IsDigit(c) Then
                    result &= c
                End If
            Next
            Return result
        Catch ex As Exception
            _logger.Warning("获取文字编号失败: " & ex.Message)
            Return String.Empty
        End Try
    End Function

    ' 自动编号命名
    Public Function GetNewAutoNumName(template As String, index As Integer, total As Integer) As String
        Try
            Dim result As String = template
            result = result.Replace("<INDEX>", index.ToString("D3"))
            result = result.Replace("<TOTAL>", total.ToString("D3"))
            result = result.Replace("<DATE>", DateTime.Now.ToString("yyyyMMdd"))
            result = result.Replace("<TIME>", DateTime.Now.ToString("HHmmss"))
            Return result
        Catch ex As Exception
            _logger.Warning("自动编号命名失败: " & ex.Message)
            Return template
        End Try
    End Function

    ' 批量渲染图框为图片文件（用于预览）
    ' 返回：图片文件路径列表，与 frames 一一对应，失败的项为 Nothing
    Public Function RenderFramesToImages(frames As List(Of FrameInfo), settings As PlotSettings, progressCallback As Action(Of Integer, String)) As List(Of String)
        Dim imagePaths As New List(Of String)()
        If frames Is Nothing OrElse frames.Count = 0 Then Return imagePaths

        Dim pngPlotter As String = "PublishToWeb PNG.pc3"
        Dim tempDir As String = Path.Combine(Path.GetTempPath(), "BatchPlotVB_Preview")
        If Not Directory.Exists(tempDir) Then
            Directory.CreateDirectory(tempDir)
        End If

        ' 清理旧预览文件
        Try
            For Each oldFile In Directory.GetFiles(tempDir, "*.png")
                Try : File.Delete(oldFile) : Catch : End Try
            Next
        Catch : End Try

        ' 保存原始系统变量
        Dim oldBackgroundPlot As Object = Nothing
        Try
            oldBackgroundPlot = Application.GetSystemVariable("BACKGROUNDPLOT")
            SetSystemVariableChecked("BACKGROUNDPLOT", 0)
        Catch : End Try

        Try
            ' 不再按 IsSelected 过滤：返回列表必须与传入 frames 一一对应，
            ' 否则存在未选中图框时预览图与图框错位（调用方按下标对位）
            Dim selectedFrames = frames.ToList()
            Dim total As Integer = selectedFrames.Count

            For i As Integer = 0 To selectedFrames.Count - 1
                Dim frame = selectedFrames(i)
                progressCallback?.Invoke(CInt((i + 1) / total * 100), $"正在生成预览 {i + 1}/{total}...")

                Dim imgPath As String = Path.Combine(tempDir, $"preview_{i:D4}.png")
                Dim success As Boolean = False

                Try
                    ' 等待打印引擎空闲
                    Dim waitCount = 0
                    Do While PlotFactory.ProcessPlotState <> ProcessPlotState.NotPlotting AndAlso waitCount < 50
                        Threading.Thread.Sleep(100)
                        waitCount += 1
                    Loop

                    Using lock As DocumentLock = _doc.LockDocument()
                        Using tr As Transaction = _db.TransactionManager.StartTransaction()
                            Dim layoutMgr As LayoutManager = LayoutManager.Current
                            Dim layoutId As ObjectId = layoutMgr.GetLayoutId(frame.LayoutName)
                            Dim layout = CType(tr.GetObject(layoutId, OpenMode.ForRead), Layout)
                            Dim layoutName As String = layout.LayoutName

                            If layoutMgr.CurrentLayout <> layoutName Then
                                layoutMgr.CurrentLayout = layoutName
                            End If

                            ' 复用完整的打印设置配置（包含纸张、比例、旋转、居中、打印样式等）
                            Dim ps As New Autodesk.AutoCAD.DatabaseServices.PlotSettings(layout.ModelType)
                            ps.CopyFrom(layout)
                            ConfigurePlotSettings(ps, frame, settings)

                            ' 最后将打印机替换为 PNG 输出（覆盖用户选择的打印机）
                            Dim psv As PlotSettingsValidator = PlotSettingsValidator.Current
                            Try
                                psv.SetPlotConfigurationName(ps, pngPlotter, Nothing)
                                psv.RefreshLists(ps)
                                ' 栅格设备纸张单位必须用像素
                                psv.SetPlotPaperUnits(ps, PlotPaperUnit.Pixels)
                            Catch ex As Exception
                                _logger.Warning($"设置PNG打印机失败: {ex.Message}")
                            End Try

                            ' 重新设置窗口区域（SetPlotConfigurationName 可能重置）
                            Try
                                psv.SetPlotType(ps, Autodesk.AutoCAD.DatabaseServices.PlotType.Window)
                                Dim windowArea As New Extents2d(frame.Bounds.MinPoint.X, frame.Bounds.MinPoint.Y, frame.Bounds.MaxPoint.X, frame.Bounds.MaxPoint.Y)
                                psv.SetPlotWindowArea(ps, windowArea)
                            Catch : End Try

                            ' 重新设置打印比例（SetPlotConfigurationName 会重置比例）
                            ' 注意：PNG 打印机的纸张尺寸与用户选择的纸张不同，AutoScale 需要基于用户纸张尺寸计算
                            Try
                                If settings.PlotScale = PlotScaleType.AutoScale Then
                                    ' 使用用户选择的纸张尺寸计算自动取整比例
                                    Dim paperSize = ParsePaperSize(settings.PaperSize)
                                    Dim pw As Double = paperSize.Item1
                                    Dim ph As Double = paperSize.Item2
                                    Dim frameWidth As Double = Math.Abs(frame.Bounds.MaxPoint.X - frame.Bounds.MinPoint.X)
                                    Dim frameHeight As Double = Math.Abs(frame.Bounds.MaxPoint.Y - frame.Bounds.MinPoint.Y)
                                    Dim isLandscape As Boolean = If(settings.AutoRotate, frameWidth > frameHeight, settings.IsLandscape)
                                    Dim bestScaleIndex = CalculateBestScale(pw, ph, frameWidth, frameHeight, isLandscape)

                                    If bestScaleIndex >= 0 Then
                                        If IsPixelPaperUnits(ps) Then
                                            ' 栅格设备（Pixels）不用标准比例：毫米比例按 DPI 换算成像素分子
                                            psv.SetUseStandardScale(ps, False)
                                            Dim pixelScale = New CustomScale(ToPlotPaperUnitScale(ps, _standardScaleRatios(bestScaleIndex)), 1.0)
                                            psv.SetCustomPrintScale(ps, pixelScale)
                                        Else
                                            ' 优先使用真实存在的标准比例枚举；无枚举的比例直接自定义比例
                                            Dim stdScaleTypeName As String = "StdScale" & _standardScaleNames(bestScaleIndex).Replace(":", "To")
                                            Dim stdScaleEnum As StdScaleType
                                            If [Enum].TryParse(Of StdScaleType)(stdScaleTypeName, True, stdScaleEnum) Then
                                                psv.SetUseStandardScale(ps, True)
                                                psv.SetStdScaleType(ps, stdScaleEnum)
                                            Else
                                                psv.SetUseStandardScale(ps, False)
                                                Dim scale = New CustomScale(ToPlotPaperUnitScale(ps, _standardScaleRatios(bestScaleIndex)), 1.0)
                                                psv.SetCustomPrintScale(ps, scale)
                                            End If
                                        End If
                                    Else
                                        psv.SetUseStandardScale(ps, True)
                                        psv.SetStdScaleType(ps, StdScaleType.ScaleToFit)
                                    End If
                                Else
                                    SetPlotScale(ps, frame, settings)
                                End If
                            Catch : End Try

                            ' 重新设置旋转（SetPlotConfigurationName 会重置旋转）
                            Try
                                SetPlotRotation(ps, frame, settings)
                            Catch : End Try

                            ' 重新设置居中/偏移
                            Try
                                If settings.AutoCenter Then
                                    ' 可能带入旧原点，先清零再居中
                                    psv.SetPlotCentered(ps, False)
                                    psv.SetPlotOrigin(ps, New Point2d(0, 0))
                                    psv.SetPlotCentered(ps, True)
                                Else
                                    psv.SetPlotCentered(ps, False)
                                    psv.SetPlotOrigin(ps, New Point2d(settings.OffsetX, settings.OffsetY))
                                End If
                            Catch : End Try

                            Dim pi As New PlotInfo()
                            pi.Layout = layoutId
                            pi.OverrideSettings = ps

                            Using piv As New PlotInfoValidator()
                                piv.MediaMatchingPolicy = MatchingPolicy.MatchEnabled
                                Try
                                    piv.Validate(pi)
                                Catch ex As Exception
                                    _logger.Warning($"预览PlotInfo验证警告: {ex.Message}")
                                End Try
                            End Using

                            ' 使用发布引擎打印到 PNG 文件（异常时自动逐级清理引擎状态）
                            RunPlotEngineSafely(pi, layoutName, imgPath)

                            ps.Dispose()
                            pi.Dispose()
                            tr.Commit()
                        End Using
                    End Using

                    ' 验证文件是否生成
                    If File.Exists(imgPath) Then
                        success = True
                    End If

                Catch ex As Exception
                    _logger.Warning($"渲染图框 {frame.FrameNumber} 为图片失败: {ex.Message}")
                End Try

                imagePaths.Add(If(success, imgPath, Nothing))
            Next

        Finally
            ' 恢复系统变量
            Try
                If oldBackgroundPlot IsNot Nothing Then
                    SetSystemVariableChecked("BACKGROUNDPLOT", CInt(oldBackgroundPlot))
                End If
            Catch : End Try
        End Try

        Return imagePaths
    End Function

    ' 清理预览临时文件
    Public Sub CleanupPreviewImages()
        Try
            Dim tempDir As String = Path.Combine(Path.GetTempPath(), "BatchPlotVB_Preview")
            If Directory.Exists(tempDir) Then
                For Each f In Directory.GetFiles(tempDir, "*.png")
                    Try : File.Delete(f) : Catch : End Try
                Next
            End If
        Catch : End Try
    End Sub

#End Region

End Class
