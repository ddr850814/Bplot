Imports System.IO
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

    ' 纸张名称映射字典 - 本地化名称 -> 规范名称
    Private _paperNameMap As New Dictionary(Of String, String)()

    Public Sub New()
        _logger = Logger.GetInstance()
        _configManager = New ConfigManager()
        UpdateCurrentDocument()
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

#Region "Enums and Data Classes"

    ' 打印设置类
    Public Class PlotSettings
        Public Property PlotterName As String
        Public Property PaperSize As String
        Public Property PlotStyle As String
        Public Property OutputPath As String
        Public Property IsLandscape As Boolean
        Public Property PlotArea As PlotAreaType
        Public Property OutputType As OutputFileType
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

    Public Enum OutputFileType
        PDF
        DWF
        DWFx
        PNG
        JPG
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
    ' 标准比例值列表（与名称对应）
    Private Shared ReadOnly _standardScaleRatios As Double() = {50.0, 20.0, 10.0, 5.0, 4.0, 2.0, 1.0, 0.5, 0.25, 0.2, 0.125, 0.1, 0.0625, 0.05, 0.033333, 0.025, 0.02, 0.01, 0.005, 0.002, 0.001}
    ' 标准比例类型列表（与名称对应，对应AutoCAD StdScaleType枚举值）
    Private Shared ReadOnly _standardScaleTypes As Integer() = {23, 22, 21, 20, 19, 18, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 14, 15, 16}

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

    ' 获取打印样式列表
    Public Function GetPlotStyleList() As List(Of String)
        Dim styles As New List(Of String)()
        Try
            Dim foundPath As String = AutoCADPathResolver.GetPlotStylesPath()

            If Not String.IsNullOrEmpty(foundPath) AndAlso Directory.Exists(foundPath) Then
                For Each file As String In Directory.GetFiles(foundPath, "*.ctb")
                    styles.Add(Path.GetFileNameWithoutExtension(file))
                Next
            End If

            If styles.Count = 0 Then
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
                Application.SetSystemVariable("BACKGROUNDPLOT", 0)
                _logger.Info("已禁用后台打印 (BACKGROUNDPLOT = 0)")
            Catch ex As Exception
                _logger.Warning($"设置 BACKGROUNDPLOT 失败: {ex.Message}")
            End Try

            Dim selectedFrames = frames.Where(Function(f) f.IsSelected).ToList()
            Dim totalFrames As Integer = selectedFrames.Count
            Dim totalTasks As Integer = totalFrames * settings.Copies
            Dim currentTask As Integer = 0

            ' 准备输出路径（用于打印到文件模式）
            Dim outputPath As String = settings.OutputPath
            If String.IsNullOrEmpty(outputPath) Then
                outputPath = Path.GetDirectoryName(_doc.Name)
            End If
            If Not Directory.Exists(outputPath) Then
                Directory.CreateDirectory(outputPath)
            End If

            ' 根据设置生成文件名
            Dim outputFileName As String
            If settings.UseDWGName OrElse settings.NameTemplate = "<DWGNAME>" Then
                ' 使用DWG文件名
                outputFileName = Path.GetFileNameWithoutExtension(_doc.Name)
            ElseIf Not String.IsNullOrEmpty(settings.NameTemplate) Then
                ' 使用名称样板（在循环中会替换 <CTAB> 和 <INDEX>）
                outputFileName = settings.NameTemplate
            Else
                outputFileName = "BatchPlot_Output"
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
                    System.Threading.Thread.Sleep(500)
                End Using
            Catch ex As Exception
                _logger.Warning($"设置打印戳记状态失败: {ex.Message}")
            End Try

            If settings.Collate Then
                ' 逐份打印：外层循环份数，内层循环图框
                For copy As Integer = 1 To settings.Copies
                    For i As Integer = 0 To totalFrames - 1
                        currentTask += 1
                        Dim frame As FrameInfo = selectedFrames(i)
                        progressCallback?.Invoke(CInt(currentTask / totalTasks * 100),
                            $"逐份打印 第{copy}/{settings.Copies}份 - 图框 {i + 1}/{totalFrames}...")

                        ' 构建输出文件路径（如果是打印到文件模式）
                        Dim outputFilePath As String = Nothing
                        If Not settings.DirectPrint Then
                            ' 应用名称样板生成文件名
                            Dim fileName As String = FormatFileName(outputFileName, frame.FrameNumber, currentTask)
                            ' 添加份数标识（如果份数大于1）
                            If settings.Copies > 1 Then
                                fileName = $"{fileName}_Copy{copy}"
                            End If
                            Dim tempFilePath As String = BuildOutputPath(outputPath, fileName, settings.OutputType)
                            ' 处理重名文件
                            outputFilePath = HandleDuplicateFile(tempFilePath, settings.DuplicateHandling)
                        End If

                        ' 为单个图框执行独立打印任务
                        PrintSingleFrame(frame, settings, i + 1, totalFrames, copy, settings.Copies, outputFilePath)

                        _logger.Info($"图框 {frame.FrameNumber} 第{copy}份: 独立打印任务完成")
                    Next
                Next
            Else
                ' 非逐份打印：外层循环图框，内层循环份数
                For i As Integer = 0 To totalFrames - 1
                    Dim frame As FrameInfo = selectedFrames(i)

                    For copy As Integer = 1 To settings.Copies
                        currentTask += 1
                        progressCallback?.Invoke(CInt(currentTask / totalTasks * 100),
                            $"图框 {i + 1}/{totalFrames} - 第{copy}/{settings.Copies}份...")

                        ' 构建输出文件路径（如果是打印到文件模式）
                        Dim outputFilePath As String = Nothing
                        If Not settings.DirectPrint Then
                            ' 应用名称样板生成文件名
                            Dim fileName As String = FormatFileName(outputFileName, frame.FrameNumber, currentTask)
                            ' 添加份数标识（如果份数大于1）
                            If settings.Copies > 1 Then
                                fileName = $"{fileName}_Copy{copy}"
                            End If
                            Dim tempFilePath As String = BuildOutputPath(outputPath, fileName, settings.OutputType)
                            ' 处理重名文件
                            outputFilePath = HandleDuplicateFile(tempFilePath, settings.DuplicateHandling)
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
                    Application.SetSystemVariable("BACKGROUNDPLOT", oldBackgroundPlot)
                    _logger.Info($"已恢复后台打印设置 (BACKGROUNDPLOT = {oldBackgroundPlot})")
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
                Dim waitCount As Integer = 0
                Do While PlotFactory.ProcessPlotState <> ProcessPlotState.NotPlotting AndAlso waitCount < 100
                    System.Threading.Thread.Sleep(100)
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
                    Dim layoutId As ObjectId = layoutMgr.GetLayoutId("Model")
                    Dim layout As Layout = CType(tr.GetObject(layoutId, OpenMode.ForRead), Layout)
                    Dim layoutName As String = layout.LayoutName

                    ' 创建PlotSettings并配置
                    Dim ps As New Autodesk.AutoCAD.DatabaseServices.PlotSettings(layout.ModelType)
                    ps.CopyFrom(layout)
                    ConfigurePlotSettings(ps, frame, settings)

                    ' 创建PlotInfo
                    Dim pi As New PlotInfo()
                    pi.Layout = layoutId
                    pi.OverrideSettings = ps

                    ' 验证打印信息
                    Dim piv As New PlotInfoValidator()
                    piv.MediaMatchingPolicy = MatchingPolicy.MatchEnabled
                    Try
                        piv.Validate(pi)
                    Catch ex As Exception
                        _logger.Warning($"PlotInfo验证警告: {ex.Message}")
                    End Try

                    ' 创建打印引擎
                    Using pe As PlotEngine = PlotFactory.CreatePublishEngine()
                        ' 开始打印
                        pe.BeginPlot(Nothing, Nothing)
                        
                        ' 设置输出文件路径（如果打印到文件）
                        If Not String.IsNullOrEmpty(outputFilePath) Then
                            ' 打印到文件
                            pe.BeginDocument(pi, layoutName, Nothing, 1, True, outputFilePath)
                        Else
                            ' 直接打印到设备
                            pe.BeginDocument(pi, layoutName, Nothing, 1, False, Nothing)
                        End If
                        
                        ' 打印页面
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

            _logger.Info($"图框 {frame.FrameNumber} 第{currentCopy}/{totalCopies}份: 打印完成")

        Catch ex As Exception
            _logger.Error($"打印图框 {frame.FrameNumber} 第{currentCopy}/{totalCopies}份失败", ex)
            Throw
        End Try
    End Sub

#End Region

#Region "Helper Methods"

    ' 构建输出文件路径
    Private Function BuildOutputPath(outputPath As String, fileName As String, outputType As OutputFileType) As String
        Select Case outputType
            Case OutputFileType.DWF
                Return Path.Combine(outputPath, fileName & ".dwf")
            Case OutputFileType.DWFx
                Return Path.Combine(outputPath, fileName & ".dwfx")
            Case Else
                Return Path.Combine(outputPath, fileName & ".pdf")
        End Select
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
                    Dim piv As New PlotInfoValidator()
                    piv.MediaMatchingPolicy = MatchingPolicy.MatchEnabled
                    Try
                        piv.Validate(pi)
                    Catch ex As Exception
                        _logger.Warning($"PlotInfo验证警告: {ex.Message}")
                    End Try

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
            Dim canonicalPaperName As String = GetCanonicalPaperName(settings.PaperSize)
            psv.SetPlotConfigurationName(plotSettings, settings.PlotterName, canonicalPaperName)
        Catch ex As Exception
            _logger.Warning("设置打印机失败: " & ex.Message)
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

        ' 设置居中/偏移
        Try
            If settings.AutoCenter Then
                psv.SetPlotCentered(plotSettings, True)
            Else
                psv.SetPlotCentered(plotSettings, False)
                psv.SetPlotOrigin(plotSettings, New Point2d(settings.OffsetX, settings.OffsetY))
            End If
        Catch ex As Exception
            _logger.Error("设置居中/偏移失败: " & ex.Message)
        End Try

        ' 设置打印样式
        If Not String.IsNullOrEmpty(settings.PlotStyle) AndAlso settings.PlotStyle <> "无" Then
            Try
                psv.SetCurrentStyleSheet(plotSettings, settings.PlotStyle)
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
                Dim paperSize = ParsePaperSize(settings.PaperSize)
                Dim frameWidth As Double = Math.Abs(frame.Bounds.MaxPoint.X - frame.Bounds.MinPoint.X)
                Dim frameHeight As Double = Math.Abs(frame.Bounds.MaxPoint.Y - frame.Bounds.MinPoint.Y)
                Dim isLandscape As Boolean = If(settings.AutoRotate, frameWidth > frameHeight, settings.IsLandscape)
                Dim bestScaleIndex = CalculateBestScale(paperSize.Item1, paperSize.Item2, frameWidth, frameHeight, isLandscape)

                If bestScaleIndex >= 0 Then
                    psv.SetUseStandardScale(plotSettings, True)
                    Try
                        Dim stdScaleTypeName As String = "Scale" & _standardScaleNames(bestScaleIndex).Replace(":", "_")
                        Dim stdScaleEnum = DirectCast([Enum].Parse(GetType(StdScaleType), stdScaleTypeName), StdScaleType)
                        psv.SetStdScaleType(plotSettings, stdScaleEnum)
                    Catch
                        psv.SetUseStandardScale(plotSettings, False)
                        Dim scale = New CustomScale(_standardScaleRatios(bestScaleIndex), 1.0)
                        psv.SetCustomPrintScale(plotSettings, scale)
                    End Try
                Else
                    psv.SetUseStandardScale(plotSettings, True)
                    psv.SetStdScaleType(plotSettings, StdScaleType.ScaleToFit)
                End If
            Case PlotScaleType.FixedScale
                psv.SetUseStandardScale(plotSettings, False)
                ' CustomScale(分子, 分母)，用户输入100表示1:100
                Dim scale = New CustomScale(1.0, settings.FixedScale)
                psv.SetCustomPrintScale(plotSettings, scale)
        End Select
    End Sub

    Private Sub SetPlotRotation(plotSettings As Autodesk.AutoCAD.DatabaseServices.PlotSettings, frame As FrameInfo, settings As PlotSettings)
        Dim psv As PlotSettingsValidator = PlotSettingsValidator.Current

        ' 获取纸张尺寸
        Dim paperSize = ParsePaperSize(settings.PaperSize)
        Dim paperWidth As Double = paperSize.Item1
        Dim paperHeight As Double = paperSize.Item2
        Dim isPaperLandscape As Boolean = paperWidth > paperHeight

        Dim rotation As PlotRotation

        If settings.AutoRotate Then
            Dim frameWidth As Double = Math.Abs(frame.Bounds.MaxPoint.X - frame.Bounds.MinPoint.X)
            Dim frameHeight As Double = Math.Abs(frame.Bounds.MaxPoint.Y - frame.Bounds.MinPoint.Y)
            Dim isFrameLandscape As Boolean = frameWidth > frameHeight

            ' 自动旋转逻辑：让纸张方向与图框方向匹配
            If isFrameLandscape = isPaperLandscape Then
                ' 图框方向与纸张方向一致，不需要旋转
                rotation = PlotRotation.Degrees000
            Else
                ' 图框方向与纸张方向不一致，需要旋转90度
                rotation = PlotRotation.Degrees090
            End If
        ElseIf settings.IsLandscape Then
            ' 手动设置为横向
            If isPaperLandscape Then
                rotation = PlotRotation.Degrees000
            Else
                rotation = PlotRotation.Degrees090
            End If
        Else
            ' 手动设置为纵向
            If isPaperLandscape Then
                rotation = PlotRotation.Degrees090
            Else
                rotation = PlotRotation.Degrees000
            End If
        End If

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
        Try
            Dim count = 0
            Dim total As Integer = frames.Count

            For Each frame As FrameInfo In frames
                If Not frame.IsSelected Then Continue For

                count += 1
                progressCallback?.Invoke(CInt((count / total) * 100), $"正在拆分: {frame.FrameNumber}")

                Dim fileName As String = If(useDWGName, Path.GetFileNameWithoutExtension(_doc.Name), FormatFileName(nameTemplate, frame.FrameNumber, count))
                Dim outputFile As String = Path.Combine(outputPath, $"{fileName}.dwg")
                outputFile = HandleDuplicateFile(outputFile, duplicateHandling)

                If String.IsNullOrEmpty(outputFile) Then Continue For

                Dim script As String = $"-WBLOCK{vbCrLf}""{outputFile}""{vbCrLf}Y{vbCrLf}{frame.Bounds.MinPoint.X},{frame.Bounds.MinPoint.Y}{vbCrLf}{frame.Bounds.MaxPoint.X},{frame.Bounds.MaxPoint.Y}{vbCrLf}""{vbCrLf}"
                _doc.SendStringToExecute(script, True, False, True)
            Next

            progressCallback?.Invoke(100, "拆分完成")
        Catch ex As Exception
            _logger.Error("图纸拆分归档失败", ex)
            Throw New Exception("图纸拆分归档失败: " & ex.Message)
        End Try
    End Sub

    ' 批量生成布局
    Public Sub BatchCreateLayouts(frames As List(Of FrameInfo), progressCallback As Action(Of Integer, String), Optional modelSpaceScale As Boolean = False, Optional duplicateHandling As DuplicateFileHandling = DuplicateFileHandling.AutoRename)
        Try
            Dim count = 0
            Dim total As Integer = frames.Count

            Dim originalLtScale As Double = Application.GetSystemVariable("LTSCALE")
            Dim originalPsLtScale As Integer = Application.GetSystemVariable("PSLTSCALE")

            Using tr As Transaction = _db.TransactionManager.StartTransaction()
                Dim layoutDict = CType(tr.GetObject(_db.LayoutDictionaryId, OpenMode.ForWrite), DBDictionary)
                Dim bt = CType(tr.GetObject(_db.BlockTableId, OpenMode.ForRead), BlockTable)

                For Each frame As FrameInfo In frames
                    If Not frame.IsSelected Then Continue For

                    count += 1
                    progressCallback?.Invoke(CInt((count / total) * 100), $"正在创建布局: {frame.FrameNumber}")

                    Dim layoutName As String = HandleDuplicateLayoutName(layoutDict, frame.FrameNumber, duplicateHandling)
                    If String.IsNullOrEmpty(layoutName) Then Continue For

                    Dim layout As New Layout()
                    layout.LayoutName = layoutName
                    layoutDict.SetAt(layoutName, layout)
                    tr.AddNewlyCreatedDBObject(layout, True)

                    If modelSpaceScale Then
                        CreateViewportForLayout(tr, layout, frame)
                    End If
                Next

                tr.Commit()
            End Using

            Application.SetSystemVariable("LTSCALE", originalLtScale)
            Application.SetSystemVariable("PSLTSCALE", originalPsLtScale)

            progressCallback?.Invoke(100, "布局创建完成")
        Catch ex As Exception
            _logger.Error("批量生成布局失败", ex)
            Throw New Exception("批量生成布局失败: " & ex.Message)
        End Try
    End Sub

    Private Sub CreateViewportForLayout(tr As Transaction, layout As Layout, frame As FrameInfo)
        Dim layoutBtr = CType(tr.GetObject(layout.BlockTableRecordId, OpenMode.ForWrite), BlockTableRecord)

        Dim paperWidth As Double = A4_WIDTH
        Dim frameWidth As Double = frame.Bounds.MaxPoint.X - frame.Bounds.MinPoint.X
        Dim scaleFactor As Double = paperWidth / frameWidth

        Dim viewport As New Viewport()
        viewport.CenterPoint = New Point3d(148.5, 105, 0)
        viewport.Width = A4_WIDTH
        viewport.Height = A4_HEIGHT
        viewport.ViewCenter = New Point2d((frame.Bounds.MinPoint.X + frame.Bounds.MaxPoint.X) / 2, (frame.Bounds.MinPoint.Y + frame.Bounds.MaxPoint.Y) / 2)
        viewport.ViewHeight = frame.Bounds.MaxPoint.Y - frame.Bounds.MinPoint.Y
        viewport.LinetypeScale = scaleFactor

        layoutBtr.AppendEntity(viewport)
        tr.AddNewlyCreatedDBObject(viewport, True)
    End Sub

    ' 打印已有布局
    Public Sub PlotExistingLayouts(settings As PlotSettings, progressCallback As Action(Of Integer, String))
        Try
            Dim layouts As List(Of String) = GetLayoutList()
            Dim count = 0
            Dim total As Integer = layouts.Count

            Dim plotStyle As String = If(String.IsNullOrEmpty(settings.PlotStyle) OrElse settings.PlotStyle = "无", ".", settings.PlotStyle)

            For Each layoutName As String In layouts
                count += 1
                progressCallback?.Invoke(CInt((count / total) * 100), $"正在打印布局: {layoutName}")

                Dim sb As New StringBuilder(1024)
                sb.Append("_-PLOT" & vbLf)
                sb.Append("Y" & vbLf)
                sb.Append(layoutName & vbLf)
                sb.Append(settings.PlotterName & vbLf)
                sb.Append(settings.PaperSize & vbLf)
                sb.Append("M" & vbLf)
                sb.Append("L" & vbLf)
                sb.Append("N" & vbLf)
                sb.Append("L" & vbLf)
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
            Next

            progressCallback?.Invoke(100, "布局打印完成")
        Catch ex As Exception
            _logger.Error("打印已有布局失败", ex)
            Throw New Exception("打印已有布局失败: " & ex.Message)
        End Try
    End Sub

#End Region

#Region "Utility Functions"

    ' 高亮显示图框
    Public Sub HighlightFrames(frames As List(Of FrameInfo))
        Try
            Using tr As Transaction = _db.TransactionManager.StartTransaction()
                For Each frame As FrameInfo In frames
                    If frame.ObjectId.IsNull Then Continue For
                    Try
                        Dim ent = CType(tr.GetObject(frame.ObjectId, OpenMode.ForRead), Entity)
                        ent.Highlight()
                    Catch
                    End Try
                Next
                tr.Commit()
            End Using
        Catch ex As Exception
            _logger.Warning("高亮显示图框失败: " & ex.Message)
        End Try
    End Sub

    ' 排序图框
    Public Sub OrderFrames(frames As List(Of FrameInfo), orderType As PlotOrderType, reverse As Boolean)
        Select Case orderType
            Case PlotOrderType.LeftRight
                frames.Sort(Function(a, b)
                                If Math.Abs(a.Bounds.MinPoint.Y - b.Bounds.MinPoint.Y) < 0.001 Then
                                    Return a.Bounds.MinPoint.X.CompareTo(b.Bounds.MinPoint.X)
                                Else
                                    Return b.Bounds.MinPoint.Y.CompareTo(a.Bounds.MinPoint.Y)
                                End If
                            End Function)
            Case PlotOrderType.TopBottom
                frames.Sort(Function(a, b)
                                If Math.Abs(a.Bounds.MinPoint.X - b.Bounds.MinPoint.X) < 0.001 Then
                                    Return b.Bounds.MinPoint.Y.CompareTo(a.Bounds.MinPoint.Y)
                                Else
                                    Return a.Bounds.MinPoint.X.CompareTo(b.Bounds.MinPoint.X)
                                End If
                            End Function)
        End Select

        If reverse Then
            frames.Reverse()
        End If

        For i = 0 To frames.Count - 1
            frames(i).OrderIndex = i + 1
        Next
    End Sub

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
                Dim counter As Integer = 1
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
                Dim counter As Integer = 1
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
    Public Function FormatFileName(template As String, frameNumber As String, index As Integer) As String
        Try
            Dim result As String = template
            result = result.Replace("<DWGNAME>", Path.GetFileNameWithoutExtension(_doc.Name))
            result = result.Replace("<DWGFOLDERNAME>", Path.GetFileName(Path.GetDirectoryName(_doc.Name)))
            result = result.Replace("<CTAB>", frameNumber)
            result = result.Replace("<PAPERNAME>", "A4")
            result = result.Replace("<INDEX>", index.ToString("D3"))
            result = result.Replace("<DATE>", DateTime.Now.ToString("yyyyMMdd"))
            result = result.Replace("<TIME>", DateTime.Now.ToString("HHmmss"))

            result = ProcessBlockAttributeTemplate(result)
            result = ProcessLayerTextTemplate(result)

            Return result
        Catch ex As Exception
            _logger.Error("格式化文件名失败", ex)
            Return template
        End Try
    End Function

    Private Function ProcessBlockAttributeTemplate(template As String) As String
        Dim result As String = template
        Dim pattern As String = "<BLOCKATTR:([^>]+):([^>]+)>"
        Dim matches As System.Text.RegularExpressions.MatchCollection = System.Text.RegularExpressions.Regex.Matches(template, pattern)

        For Each match As System.Text.RegularExpressions.Match In matches
            Dim blockName As String = match.Groups(1).Value
            Dim attrName As String = match.Groups(2).Value
            Dim attrValue As String = GetBlockAttributeValue(blockName, attrName)
            result = result.Replace(match.Value, attrValue)
        Next

        Return result
    End Function

    Private Function GetBlockAttributeValue(blockName As String, attrName As String) As String
        Try
            Using tr As Transaction = _db.TransactionManager.StartTransaction()
                Dim bt As BlockTable = CType(tr.GetObject(_db.BlockTableId, OpenMode.ForRead), BlockTable)
                Dim ms As BlockTableRecord = CType(tr.GetObject(bt(BlockTableRecord.ModelSpace), OpenMode.ForRead), BlockTableRecord)

                For Each objId As ObjectId In ms
                    If objId.ObjectClass.DxfName = "INSERT" Then
                        Dim br As BlockReference = TryCast(tr.GetObject(objId, OpenMode.ForRead), BlockReference)
                        If br IsNot Nothing Then
                            Dim btr As BlockTableRecord = CType(tr.GetObject(br.BlockTableRecord, OpenMode.ForRead), BlockTableRecord)
                            If btr.Name = blockName Then
                                For Each attrId As ObjectId In br.AttributeCollection
                                    Dim attr As AttributeReference = TryCast(tr.GetObject(attrId, OpenMode.ForRead), AttributeReference)
                                    If attr IsNot Nothing AndAlso attr.Tag = attrName Then
                                        Return attr.TextString
                                    End If
                                Next
                            End If
                        End If
                    End If
                Next

                tr.Commit()
            End Using
        Catch ex As Exception
            _logger.Warning($"获取图块属性失败: {blockName}.{attrName}")
        End Try

        Return String.Empty
    End Function

    Private Function ProcessLayerTextTemplate(template As String) As String
        Dim result As String = template
        Dim pattern As String = "<LAYERTEXT:([^>]+)>"
        Dim matches As System.Text.RegularExpressions.MatchCollection = System.Text.RegularExpressions.Regex.Matches(template, pattern)

        For Each match As System.Text.RegularExpressions.Match In matches
            Dim layerName As String = match.Groups(1).Value
            Dim textValue As String = GetLayerTextValue(layerName)
            result = result.Replace(match.Value, textValue)
        Next

        Return result
    End Function

    Private Function GetLayerTextValue(layerName As String) As String
        Try
            Using tr As Transaction = _db.TransactionManager.StartTransaction()
                Dim bt As BlockTable = CType(tr.GetObject(_db.BlockTableId, OpenMode.ForRead), BlockTable)
                Dim ms As BlockTableRecord = CType(tr.GetObject(bt(BlockTableRecord.ModelSpace), OpenMode.ForRead), BlockTableRecord)

                For Each objId As ObjectId In ms
                    Dim ent As Entity = TryCast(tr.GetObject(objId, OpenMode.ForRead), Entity)
                    If ent IsNot Nothing AndAlso ent.Layer = layerName Then
                        If TypeOf ent Is DBText Then
                            Return CType(ent, DBText).TextString
                        ElseIf TypeOf ent Is MText Then
                            Return CType(ent, MText).Contents
                        End If
                    End If
                Next

                tr.Commit()
            End Using
        Catch ex As Exception
            _logger.Warning($"获取图层文字失败: {layerName}")
        End Try

        Return String.Empty
    End Function

#End Region

#Region "Page Setup"

    Private Sub CreatePageSetupForPlotter(plotterName As String, paperSize As String)
        CreatePageSetupForPlotterWithName(plotterName, paperSize, "BatchPlot_PS")
    End Sub

    Private Sub CreatePageSetupForPlotterWithName(plotterName As String, paperSize As String, pageSetupName As String)
        Using tr As Transaction = _db.TransactionManager.StartTransaction()
            Dim psDict As DBDictionary = CType(tr.GetObject(_db.PlotSettingsDictionaryId, OpenMode.ForWrite), DBDictionary)

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

#End Region

End Class
