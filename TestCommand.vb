Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.IO
Imports Autodesk.AutoCAD.ApplicationServices
Imports Autodesk.AutoCAD.DatabaseServices
Imports Autodesk.AutoCAD.EditorInput
Imports Autodesk.AutoCAD.Geometry
Imports Autodesk.AutoCAD.Runtime

''' <summary>
''' BTEST：批量打印插件逻辑自检（无 UI，可脚本自动运行）。
''' 结果写入 %TEMP%\BatchPlotVB_BTEST.txt 并输出到命令行。
''' 覆盖：多段线/直线图框检测、排序幂等与重编号、文件名模板净化、
'''       重名处理、批量生成布局的视口数学、拆分归档空间选择、布局列表、版本解析。
''' 注意：测试会在当前图形中创建测试图层/图元/布局，请勿在正式图纸中运行。
''' </summary>
Public Class TestCommands

    Private _report As New List(Of String)
    Private _reportPath As String = ""
    Private _pass As Integer = 0
    Private _fail As Integer = 0

    <CommandMethod("BTEST")>
    Public Sub RunSelfTest()
        _reportPath = Path.Combine(Path.GetTempPath(), "BatchPlotVB_BTEST.txt")
        _report.Add($"BatchPlotVB 逻辑自检报告  {DateTime.Now:yyyy-MM-dd HH:mm:ss}")
        _report.Add(New String("-"c, 64))
        Flush()

        Try
            Dim doc As Document = Application.DocumentManager.MdiActiveDocument
            If doc Is Nothing Then
                Fail("T0", "没有活动文档")
                Flush()
                Return
            End If

            Dim engine As New BatchPlotEngine()

            Mark("T1 多段线检测 开始") : TestPolylineDetection(engine) : Flush()
            Mark("T1b 直线检测 开始") : TestLineDetection(engine) : Flush()
            Mark("T2 排序幂等 开始") : TestOrderFramesIdempotent(engine) : Flush()
            Mark("T3 文件名模板 开始") : TestFormatFileName(engine) : Flush()
            Mark("T4 重名处理 开始") : TestDuplicateFile(engine) : Flush()
            Mark("T5 视口数学 开始") : TestCreateLayoutViewportMath(engine) : Flush()
            Mark("T6 拆分归档 开始") : TestSplitArchive(engine) : Flush()
            Mark("T7 布局列表 开始") : TestLayoutList(engine) : Flush()
            Mark("T8 版本解析 开始") : TestPathResolver() : Flush()

        Catch ex As System.Exception
            _report.Add($"FAIL [T*] 自检过程中出现未捕获异常: {ex.Message}")
            _fail += 1
        End Try

        _report.Add(New String("-"c, 64))
        _report.Add($"合计: PASS={_pass}  FAIL={_fail}")
        Flush()
    End Sub

    ' 每一步都落盘，卡死时可直接从文件看出卡在哪个测试
    Private Sub Flush()
        Try
            File.WriteAllLines(_reportPath, _report)
        Catch
        End Try
    End Sub

    Private Sub Mark(text As String)
        _report.Add($"---- {text} {DateTime.Now:HH:mm:ss.fff}")
        Try
            Dim ed As Editor = Application.DocumentManager.MdiActiveDocument?.Editor
            ed?.WriteMessage(vbLf & text)
        Catch
        End Try
    End Sub

    Private Sub Pass(name As String, detail As String)
        _pass += 1
        _report.Add($"PASS [{name}] {detail}")
    End Sub

    Private Sub Fail(name As String, detail As String)
        _fail += 1
        _report.Add($"FAIL [{name}] {detail}")
    End Sub

    ' 在指定图层上创建矩形多段线（左下 minx,miny 到右上 maxx,maxy）
    Private Sub AddRect(pl As Polyline, minX As Double, minY As Double, maxX As Double, maxY As Double)
        pl.AddVertexAt(0, New Point2d(minX, minY), 0, 0, 0)
        pl.AddVertexAt(1, New Point2d(maxX, minY), 0, 0, 0)
        pl.AddVertexAt(2, New Point2d(maxX, maxY), 0, 0, 0)
        pl.AddVertexAt(3, New Point2d(minX, maxY), 0, 0, 0)
        pl.Closed = True
    End Sub

    Private Function CreateTestLayer(tr As Transaction, db As Database, name As String) As String
        Dim lt = CType(tr.GetObject(db.LayerTableId, OpenMode.ForWrite), LayerTable)
        If Not lt.Has(name) Then
            Dim ltr As New LayerTableRecord() With {.Name = name}
            lt.Add(ltr)
            tr.AddNewlyCreatedDBObject(ltr, True)
        End If
        Return name
    End Function

    ' ========== T1 多段线图框检测：4 顶点闭合 / 5 顶点（首尾重复）闭合 / 开放折线 ==========
    Private Sub TestPolylineDetection(engine As BatchPlotEngine)
        Dim name = "T1 多段线检测"
        Try
            Dim db As Database = Application.DocumentManager.MdiActiveDocument.Database
            Using Application.DocumentManager.MdiActiveDocument.LockDocument()
                Using tr = db.TransactionManager.StartTransaction()
                    Dim ms = CType(tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite), BlockTableRecord)
                    Dim layer = CreateTestLayer(tr, db, "BTEST_POLY")

                    Dim p1 As New Polyline()
                    AddRect(p1, 0, 0, 42000, 29700)
                    p1.Layer = layer
                    ms.AppendEntity(p1)
                    tr.AddNewlyCreatedDBObject(p1, True)

                    ' 5 顶点闭合矩形：首尾点重复（本次修复项，旧代码漏检）
                    Dim p2 As New Polyline()
                    AddRect(p2, 50000, 0, 92000, 29700)
                    p2.AddVertexAt(4, p2.GetPoint2dAt(0), 0, 0, 0)
                    p2.Layer = layer
                    ms.AppendEntity(p2)
                    tr.AddNewlyCreatedDBObject(p2, True)

                    ' 开放折线：不应识别
                    Dim p3 As New Polyline()
                    p3.AddVertexAt(0, New Point2d(0, 40000), 0, 0, 0)
                    p3.AddVertexAt(1, New Point2d(1000, 41000), 0, 0, 0)
                    p3.AddVertexAt(2, New Point2d(2000, 40000), 0, 0, 0)
                    p3.Layer = layer
                    ms.AppendEntity(p3)
                    tr.AddNewlyCreatedDBObject(p3, True)

                    tr.Commit()
                End Using
            End Using

            Dim st As New BatchPlotEngine.PlotSettings() With {.TraverseLayouts = False}
            Dim frames = engine.DetectFramesByPolyline("BTEST_POLY", st)
            Dim count = If(frames, New List(Of BatchPlotEngine.FrameInfo)).Count
            If count = 2 Then
                Pass(name, $"识别到 {count} 个图框（4顶点+5顶点闭合，开放折线已排除）")
            Else
                Fail(name, $"期望识别 2 个图框，实际 {count}")
            End If
        Catch ex As System.Exception
            Fail(name, $"异常: {ex.Message}")
        End Try
    End Sub

    ' ========== T1b 直线图框检测 ==========
    Private Sub TestLineDetection(engine As BatchPlotEngine)
        Dim name = "T1b 直线检测"
        Try
            Dim db As Database = Application.DocumentManager.MdiActiveDocument.Database
            Using Application.DocumentManager.MdiActiveDocument.LockDocument()
                Using tr = db.TransactionManager.StartTransaction()
                    Dim ms = CType(tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite), BlockTableRecord)
                    Dim layer = CreateTestLayer(tr, db, "BTEST_LINE")
                    Dim pts() As Point3d = {
                        New Point3d(0, -50000, 0), New Point3d(42000, -50000, 0),
                        New Point3d(42000, -50000 + 29700, 0), New Point3d(0, -50000 + 29700, 0)}
                    For i = 0 To 3
                        Dim ln As New Line(pts(i), pts((i + 1) Mod 4)) With {.Layer = layer}
                        ms.AppendEntity(ln)
                        tr.AddNewlyCreatedDBObject(ln, True)
                    Next
                    tr.Commit()
                End Using
            End Using

            Dim st As New BatchPlotEngine.PlotSettings() With {.TraverseLayouts = False}
            Dim frames = engine.DetectFramesByLine("BTEST_LINE", st)
            Dim count = If(frames, New List(Of BatchPlotEngine.FrameInfo)).Count
            If count >= 1 Then
                Pass(name, $"识别到 {count} 个直线图框")
            Else
                Fail(name, "期望至少识别 1 个直线图框，实际 0")
            End If
        Catch ex As System.Exception
            Fail(name, $"异常: {ex.Message}")
        End Try
    End Sub

    ' ========== T2 排序幂等性 + FrameNumber 重编号（本次修复项） ==========
    Private Sub TestOrderFramesIdempotent(engine As BatchPlotEngine)
        Dim name = "T2 排序幂等与重编号"
        Try
            Dim list As New List(Of BatchPlotEngine.FrameInfo)
            For i = 1 To 6
                list.Add(New BatchPlotEngine.FrameInfo() With {
                    .Bounds = New Extents3d(New Point3d(i * 1000, 0, 0), New Point3d(i * 1000 + 500, 500, 0)),
                    .LayoutName = "",
                    .FrameNumber = $"F{i}",
                    .IsSelected = (i Mod 2 = 1),
                    .OrderIndex = i,
                    .OriginalIndex = i})
            Next

            ' 第一次：选择顺序 + 逆序（排序后应为 6,5,4,3,2,1）
            engine.OrderFrames(list, BatchPlotEngine.PlotOrderType.SelectOrder, True)
            Dim first = list.Select(Function(f) f.FrameNumber).ToList()

            ' 第二次同样参数：必须得到相同结果（旧代码会再次翻转）
            engine.OrderFrames(list, BatchPlotEngine.PlotOrderType.SelectOrder, True)
            Dim second = list.Select(Function(f) f.FrameNumber).ToList()

            Dim idempotent = first.SequenceEqual(second)

            ' 选中项（奇数号 5,3,1）按最终顺序应重编号为 Frame_001..003
            Dim selNos = list.Where(Function(f) f.IsSelected).Select(Function(f) f.FrameNumber).ToList()
            Dim renumberOk = selNos.Count = 3 AndAlso selNos(0) = "Frame_001" AndAlso selNos(1) = "Frame_002" AndAlso selNos(2) = "Frame_003"

            ' 逆序后第一个应为原 6 号（未选中，保留原编号 F6，位置序号 1）
            Dim reversedOk = list(0).OrderIndex = 1 AndAlso list(0).FrameNumber = "F6" AndAlso list(0).IsSelected = False

            If idempotent AndAlso renumberOk AndAlso reversedOk Then
                Pass(name, "两次排序结果一致；选中图框按打印顺序重编号 Frame_001..003")
            Else
                Fail(name, $"幂等={idempotent}, 重编号={renumberOk}, 逆序={reversedOk}; 顺序=[{String.Join(",", second)}], 选中=[{String.Join(",", selNos)}]")
            End If
        Catch ex As System.Exception
            Fail(name, $"异常: {ex.Message}")
        End Try
    End Sub

    ' ========== T3 文件名模板展开 / 非法字符净化 / 空值兜底 ==========
    Private Sub TestFormatFileName(engine As BatchPlotEngine)
        Dim name = "T3 文件名模板"
        Try
            Dim r1 = engine.FormatFileName("PRE_<INDEX>", "Frame_001", 3)
            Dim ok1 = r1 = "PRE_003"

            Dim r2 = engine.FormatFileName("a<b>:c*?", "Frame_001", 1)
            Dim ok2 = r2 = "a_b__c__"

            ' 全部变量替换为空时回退到图框号
            Dim r3 = engine.FormatFileName("<BLOCKATTR:none:none>", "Frame_001", 1)
            Dim ok3 = r3 = "Frame_001"

            If ok1 AndAlso ok2 AndAlso ok3 Then
                Pass(name, $"<INDEX>→{r1}；净化→{r2}；兜底→{r3}")
            Else
                Fail(name, $"INDEX={r1}(期望PRE_003)；净化={r2}(期望a_b__c__)；兜底={r3}(期望Frame_001)")
            End If
        Catch ex As System.Exception
            Fail(name, $"异常: {ex.Message}")
        End Try
    End Sub

    ' ========== T4 重名文件处理：覆盖 / 跳过 / 自动顺延 ==========
    Private Sub TestDuplicateFile(engine As BatchPlotEngine)
        Dim name = "T4 重名处理"
        Try
            Dim dir = Path.Combine(Path.GetTempPath(), "BatchPlotVB_BTEST_DUP")
            ' 清掉上一轮残留，否则顺延编号会从 (2)(3) 开始
            If Directory.Exists(dir) Then Directory.Delete(dir, True)
            Directory.CreateDirectory(dir)
            Dim baseFile = Path.Combine(dir, "dup.pdf")
            File.WriteAllText(baseFile, "x")

            Dim overw = engine.HandleDuplicateFile(baseFile, BatchPlotEngine.DuplicateFileHandling.Overwrite)
            Dim skip = engine.HandleDuplicateFile(baseFile, BatchPlotEngine.DuplicateFileHandling.Skip)
            Dim ren1 = engine.HandleDuplicateFile(baseFile, BatchPlotEngine.DuplicateFileHandling.AutoRename)
            File.WriteAllText(ren1, "x")
            Dim ren2 = engine.HandleDuplicateFile(baseFile, BatchPlotEngine.DuplicateFileHandling.AutoRename)

            Dim ok = overw = baseFile AndAlso skip = "" AndAlso
                     Path.GetFileName(ren1) = "dup(1).pdf" AndAlso Path.GetFileName(ren2) = "dup(2).pdf"
            If ok Then
                Pass(name, $"覆盖=原文件；跳过=空串；顺延→{Path.GetFileName(ren1)} / {Path.GetFileName(ren2)}")
            Else
                Fail(name, $"覆盖={overw}；跳过='{skip}'；顺延={ren1} / {ren2}")
            End If
        Catch ex As System.Exception
            Fail(name, $"异常: {ex.Message}")
        End Try
    End Sub

    ' ========== T5 批量生成布局：视口数学（本次修复项，FixedScale 1:100） ==========
    Private Sub TestCreateLayoutViewportMath(engine As BatchPlotEngine)
        Dim name = "T5 视口数学(1:100)"
        Try
            Dim db As Database = Application.DocumentManager.MdiActiveDocument.Database

            ' 选一个 A3 纸张名（找不到就取第一个）
            Dim papers = engine.GetPaperList("DWG To PDF.pc3")
            Dim paper As String = papers.FirstOrDefault(Function(p) p.Contains("A3") AndAlso p.Contains("420"))
            If paper Is Nothing AndAlso papers.Count > 0 Then paper = papers(0)
            If paper Is Nothing Then
                Fail(name, "未取到 DWG To PDF.pc3 的纸张列表")
                Return
            End If

            Dim frame As New BatchPlotEngine.FrameInfo() With {
                .Bounds = New Extents3d(New Point3d(0, 0, 0), New Point3d(42000, 29700, 0)),
                .LayoutName = "",
                .FrameNumber = "Frame_001",
                .IsSelected = True}

            Dim settings As New BatchPlotEngine.PlotSettings() With {
                .PlotterName = "DWG To PDF.pc3",
                .PaperSize = paper,
                .PlotScale = BatchPlotEngine.PlotScaleType.FixedScale,
                .FixedScale = 100,
                .AutoRotate = True,
                .AutoCenter = True,
                .Copies = 1}

            Dim frames As New List(Of BatchPlotEngine.FrameInfo) From {frame}
            ' modelSpaceScale=True 才会为布局创建视口（False 仅生成空布局）
            engine.BatchCreateLayouts(frames, settings, Nothing, True, BatchPlotEngine.DuplicateFileHandling.AutoRename)

            ' 找到生成的布局，读取其中的视口做数值校验
            Dim viewH As Double = -1, viewW As Double = -1
            Using tr = db.TransactionManager.StartTransaction()
                Dim layoutDict = CType(tr.GetObject(db.LayoutDictionaryId, OpenMode.ForRead), DBDictionary)
                For Each entry As DBDictionaryEntry In layoutDict
                    Dim lay = TryCast(tr.GetObject(entry.Value, OpenMode.ForRead), Layout)
                    If lay Is Nothing OrElse Not String.Equals(lay.LayoutName, "Frame_001", StringComparison.OrdinalIgnoreCase) Then Continue For
                    Dim btr = CType(tr.GetObject(lay.BlockTableRecordId, OpenMode.ForRead), BlockTableRecord)
                    For Each id As ObjectId In btr
                        Dim vp = TryCast(tr.GetObject(id, OpenMode.ForRead), Viewport)
                        If vp IsNot Nothing Then
                            viewW = vp.Width
                            viewH = vp.ViewHeight
                            Exit For
                        End If
                    Next
                Next
                tr.Commit()
            End Using

            If viewH < 0 Then
                Fail(name, "未在生成的布局 Frame_001 中找到视口")
                Return
            End If

            ' 1:100 固定比例：模型空间视图高度应等于图框高度 29700（±1）——与是否旋转内容无关
            ' 视口纸面宽：横向纸面=420；纵向纸面+内容旋转90°=297
            Dim heightOk = Math.Abs(viewH - 29700) <= 1
            Dim widthInfo = $"视口宽={viewW:F1}（横向纸=420 / 纵向纸+旋转=297）"
            If heightOk Then
                Pass(name, $"ViewHeight={viewH:F1}（期望29700±1）；{widthInfo}；纸张={paper}")
            Else
                Fail(name, $"ViewHeight={viewH:F1}（期望29700±1，1:100 时内容会整体缩放错误）；{widthInfo}")
            End If
        Catch ex As System.Exception
            Fail(name, $"异常: {ex.Message}")
        End Try
    End Sub

    ' ========== T6 拆分归档：模型空间 + 布局空间（本次修复项） ==========
    Private Sub TestSplitArchive(engine As BatchPlotEngine)
        TestSplitArchiveModel(engine)
        TestSplitArchiveLayout(engine)
    End Sub

    Private Sub TestSplitArchiveModel(engine As BatchPlotEngine)
        Dim name = "T6a 拆分-模型空间"
        Try
            Dim db As Database = Application.DocumentManager.MdiActiveDocument.Database
            Using Application.DocumentManager.MdiActiveDocument.LockDocument()
                Using tr = db.TransactionManager.StartTransaction()
                    Dim ms = CType(tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite), BlockTableRecord)
                    Dim layer = CreateTestLayer(tr, db, "BTEST_SPLIT_M")
                    Dim cIn As New Circle(New Point3d(1000, 1000, 0), Vector3d.ZAxis, 100) With {.Layer = layer}
                    Dim cOut As New Circle(New Point3d(100000, 100000, 0), Vector3d.ZAxis, 100) With {.Layer = layer}
                    ms.AppendEntity(cIn)
                    tr.AddNewlyCreatedDBObject(cIn, True)
                    ms.AppendEntity(cOut)
                    tr.AddNewlyCreatedDBObject(cOut, True)
                    tr.Commit()
                End Using
            End Using

            Dim outDir = Path.Combine(Path.GetTempPath(), "BatchPlotVB_BTEST_SPLIT")
            Directory.CreateDirectory(outDir)

            Dim frame As New BatchPlotEngine.FrameInfo() With {
                .Bounds = New Extents3d(New Point3d(0, 0, 0), New Point3d(42000, 29700, 0)),
                .LayoutName = "",
                .FrameNumber = "Frame_001",
                .IsSelected = True}
            Dim frames As New List(Of BatchPlotEngine.FrameInfo) From {frame}

            engine.SplitAndArchive(frames, outDir, "BT_M_<INDEX>", False, Nothing, BatchPlotEngine.DuplicateFileHandling.Overwrite)

            Dim outFile = Path.Combine(outDir, "BT_M_001.dwg")
            If Not File.Exists(outFile) Then
                Fail(name, $"输出文件不存在: {outFile}")
                Return
            End If

            ' 重开输出文件：模型空间应只剩框内圆（1 个）
            Dim circles = CountCirclesInModel(outFile)
            If circles = 1 Then
                Pass(name, "输出保留框内对象、剔除框外对象")
            Else
                Fail(name, $"期望模型空间剩 1 个圆，实际 {circles}")
            End If
        Catch ex As System.Exception
            ' 带堆栈定位（eNullObjectId 之类看不出抛出点）
            Dim stack = ex.StackTrace
            If stack IsNot Nothing Then
                Dim lines = stack.Split({Environment.NewLine, vbLf}, StringSplitOptions.RemoveEmptyEntries)
                Dim top = If(lines.Length > 0, lines(0).Trim(), "")
                Fail(name, $"异常: {ex.Message} @ {top}")
            Else
                Fail(name, $"异常: {ex.Message}")
            End If
        End Try
    End Sub

    Private Sub TestSplitArchiveLayout(engine As BatchPlotEngine)
        Dim name = "T6b 拆分-布局空间(修复验证)"
        Try
            Dim db As Database = Application.DocumentManager.MdiActiveDocument.Database
            Dim layoutName = "BTestLay"
            Dim layId As ObjectId = LayoutManager.Current.CreateLayout(layoutName)

            ' 在布局图纸空间放两个圆：一个在图框(0,0)-(420,297)内，一个远在框外
            Using Application.DocumentManager.MdiActiveDocument.LockDocument()
                Using tr = db.TransactionManager.StartTransaction()
                    Dim lay = CType(tr.GetObject(layId, OpenMode.ForRead), Layout)
                    Dim ps = CType(tr.GetObject(lay.BlockTableRecordId, OpenMode.ForWrite), BlockTableRecord)
                    Dim layer = CreateTestLayer(tr, db, "BTEST_SPLIT_P")
                    Dim cIn As New Circle(New Point3d(100, 100, 0), Vector3d.ZAxis, 10) With {.Layer = layer}
                    Dim cOut As New Circle(New Point3d(1000, 1000, 0), Vector3d.ZAxis, 10) With {.Layer = layer}
                    ps.AppendEntity(cIn)
                    tr.AddNewlyCreatedDBObject(cIn, True)
                    ps.AppendEntity(cOut)
                    tr.AddNewlyCreatedDBObject(cOut, True)
                    tr.Commit()
                End Using
            End Using

            Dim outDir = Path.Combine(Path.GetTempPath(), "BatchPlotVB_BTEST_SPLIT")
            Directory.CreateDirectory(outDir)

            Dim frame As New BatchPlotEngine.FrameInfo() With {
                .Bounds = New Extents3d(New Point3d(0, 0, 0), New Point3d(420, 297, 0)),
                .LayoutName = layoutName,
                .FrameNumber = "Frame_001",
                .IsSelected = True}
            Dim frames As New List(Of BatchPlotEngine.FrameInfo) From {frame}

            engine.SplitAndArchive(frames, outDir, "BT_P_<INDEX>", False, Nothing, BatchPlotEngine.DuplicateFileHandling.Overwrite)

            Dim outFile = Path.Combine(outDir, "BT_P_001.dwg")
            If Not File.Exists(outFile) Then
                Fail(name, $"输出文件不存在: {outFile}")
                Return
            End If

            ' 重开输出文件：布局 BTestLay 图纸空间应只剩框内圆（1 个）。
            ' 修复前 GetSplitSpaceId 匹配不到布局会回退模型空间，布局内容不被裁剪（仍 2 个圆）
            Dim circles = CountCirclesInLayout(outFile, layoutName)
            If circles = 1 Then
                Pass(name, "布局图框在布局空间裁剪（框外圆已剔除）")
            ElseIf circles = 2 Then
                Fail(name, "布局内容未被裁剪（拆分空间回退到模型空间，旧缺陷仍存在）")
            Else
                Fail(name, $"布局圆数量异常: {circles}")
            End If
        Catch ex As System.Exception
            Fail(name, $"异常: {ex.Message}")
        End Try
    End Sub

    Private Function CountCirclesInModel(dwgPath As String) As Integer
        Using sideDb As New Database(False, True)
            sideDb.ReadDwgFile(dwgPath, FileShare.Read, True, "")
            Using tr = sideDb.TransactionManager.StartTransaction()
                Dim bt = CType(tr.GetObject(sideDb.BlockTableId, OpenMode.ForRead), BlockTable)
                Dim ms = CType(tr.GetObject(bt(BlockTableRecord.ModelSpace), OpenMode.ForRead), BlockTableRecord)
                Dim n = 0
                For Each id As ObjectId In ms
                    If TypeOf tr.GetObject(id, OpenMode.ForRead) Is Circle Then n += 1
                Next
                Return n
            End Using
        End Using
    End Function

    Private Function CountCirclesInLayout(dwgPath As String, layoutName As String) As Integer
        Using sideDb As New Database(False, True)
            sideDb.ReadDwgFile(dwgPath, FileShare.Read, True, "")
            Using tr = sideDb.TransactionManager.StartTransaction()
                Dim bt = CType(tr.GetObject(sideDb.BlockTableId, OpenMode.ForRead), BlockTable)
                For Each btId As ObjectId In bt
                    Dim btr = TryCast(tr.GetObject(btId, OpenMode.ForRead), BlockTableRecord)
                    If btr Is Nothing OrElse Not btr.IsLayout OrElse btr.LayoutId.IsNull Then Continue For
                    Dim lay = TryCast(tr.GetObject(btr.LayoutId, OpenMode.ForRead), Layout)
                    If lay Is Nothing OrElse Not String.Equals(lay.LayoutName, layoutName, StringComparison.OrdinalIgnoreCase) Then Continue For
                    Dim n = 0
                    For Each id As ObjectId In btr
                        If TypeOf tr.GetObject(id, OpenMode.ForRead) Is Circle Then n += 1
                    Next
                    Return n
                Next
                Return -1
            End Using
        End Using
    End Function

    ' ========== T7 布局列表返回布局名（本次修复项） ==========
    Private Sub TestLayoutList(engine As BatchPlotEngine)
        Dim name = "T7 布局列表"
        Try
            Dim layouts = engine.GetLayoutList()
            If layouts IsNot Nothing AndAlso layouts.Any(Function(l) String.Equals(l, "BTestLay", StringComparison.OrdinalIgnoreCase)) Then
                Pass(name, $"共 {layouts.Count} 个布局，包含测试布局 BTestLay")
            Else
                Fail(name, $"布局列表未包含 BTestLay: [{String.Join(",", If(layouts, New List(Of String)))}]")
            End If
        Catch ex As System.Exception
            Fail(name, $"异常: {ex.Message}")
        End Try
    End Sub

    ' ========== T8 版本解析（信息项） ==========
    Private Sub TestPathResolver()
        Dim name = "T8 版本解析"
        Try
            Dim paths = AutoCADPathResolver.GetAutoCADPaths()
            If paths IsNot Nothing AndAlso paths.Count > 0 Then
                Pass(name, "解析到: " & String.Join("; ", paths.Select(Function(kv) $"{kv.Key}={kv.Value}")))
            Else
                Fail(name, "未解析到任何 AutoCAD 安装路径")
            End If
        Catch ex As System.Exception
            Fail(name, $"异常: {ex.Message}")
        End Try
    End Sub

#Region "BMPLOT 多文档打印引擎链路测试"

    ''' <summary>
    ''' BMPLOT：多文档（侧库）打印完整引擎链路测试。
    ''' 生成两个临时 DWG（各含一个图框+内容），按"源文件路径\PDF"语义逐文件打印，
    ''' 验证输出 PDF 落到各自文件目录（修复点：此前全部落到启动文档目录）。
    ''' 结果写入 %TEMP%\BatchPlotVB_BMPLOT.txt，PDF 产物保留供人工查看。
    ''' </summary>
    <CommandMethod("BMPLOT")>
    Public Sub RunMultiDocPlotTest()
        Dim rp = Path.Combine(Path.GetTempPath(), "BatchPlotVB_BMPLOT.txt")
        Dim rep As New List(Of String)
        rep.Add($"BMPLOT 多文档打印链路测试  {DateTime.Now:yyyy-MM-dd HH:mm:ss}")
        rep.Add(New String("-"c, 64))

        Dim testRoot = Path.Combine(Path.GetTempPath(), "BatchPlotVB_BMPLOT")
        Try
            If Directory.Exists(testRoot) Then Directory.Delete(testRoot, True)
            Directory.CreateDirectory(testRoot)

            ' 准备两个测试 DWG：各含一个 42000x29700 闭合图框 + 一个圆（各自独立目录，
            ' 对应"源文件路径\PDF"语义：输出必须落到各自目录下的 PDF 子文件夹）
            Dim files = {"A\A.dwg", "B\B.dwg"}
            For Each f In files
                Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(testRoot, f)))
                CreateTestDrawing(Path.Combine(testRoot, f), If(f.StartsWith("A"), 0, 50000))
            Next
            rep.Add($"已生成测试图纸: 2 个（{testRoot}\A\A.dwg / B\B.dwg）")

            Dim engine As New BatchPlotEngine()

            ' 动态取本机纸张名（中文版为"毫米"，硬编码英文名会匹配失败）
            Dim paperName = GetA3PaperName(engine)
            rep.Add($"使用纸张: {paperName}")

            Dim results As New List(Of String)

            For Each f In files
                Dim dwgPath = Path.Combine(testRoot, f)
                Dim name = Path.GetFileNameWithoutExtension(dwgPath)
                Try
                    engine.BeginExternalPlot(dwgPath)

                    Dim st As New BatchPlotEngine.PlotSettings() With {.TraverseLayouts = False}
                    Dim frames = engine.DetectFramesByPolyline("*", st)
                    If frames Is Nothing OrElse frames.Count = 0 Then
                        results.Add($"FAIL {name}: 未检测到图框")
                        Continue For
                    End If
                    For Each fr In frames
                        fr.IsSelected = True
                    Next
                    engine.OrderFrames(frames, BatchPlotEngine.PlotOrderType.SelectOrder, False)

                    ' 与主界面"源文件路径\PDF"语义一致：每文件输出到自己的 PDF 子目录
                    Dim settings As New BatchPlotEngine.PlotSettings() With {
                        .PlotterName = "DWG To PDF.pc3",
                        .PaperSize = paperName,
                        .PlotStyle = "无",
                        .DirectPrint = False,
                        .OutputPath = Path.Combine(Path.GetDirectoryName(dwgPath), "PDF"),
                        .NameTemplate = "BT_<DWGNAME>",
                        .PlotScale = BatchPlotEngine.PlotScaleType.FixedScale,
                        .FixedScale = 100,
                        .AutoRotate = True,
                        .AutoCenter = True,
                        .Copies = 1,
                        .Collate = False,
                        .DuplicateHandling = BatchPlotEngine.DuplicateFileHandling.Overwrite}

                    engine.ExecuteBatchPlot(frames, settings, Nothing)

                    Dim pdf = Path.Combine(settings.OutputPath, $"BT_{name}.pdf")
                    Dim ok = File.Exists(pdf) AndAlso New FileInfo(pdf).Length > 500
                    results.Add($"{If(ok, "PASS", "FAIL")} {name}: 输出={pdf} 大小={If(File.Exists(pdf), New FileInfo(pdf).Length, -1)} 字节")
                Catch ex As System.Exception
                    results.Add($"FAIL {name}: 异常 {ex.Message}")
                Finally
                    engine.EndExternalPlot()
                End Try
            Next

            Dim passAll = results.All(Function(r) r.StartsWith("PASS"))
            rep.AddRange(results)
            rep.Add(New String("-"c, 64))
            rep.Add(If(passAll, "结论: 每个文件的 PDF 均输出到各自源目录下的 PDF 文件夹（多文档路径修复生效）",
                        "结论: 存在失败项，详见上方各文件结果"))
            rep.Add("PDF 产物保留，请打开人工查看内容/方向/比例：")
            rep.AddRange(files.Select(Function(f) $"  {testRoot}\{Path.GetFileNameWithoutExtension(f)}\PDF\BT_{Path.GetFileNameWithoutExtension(f)}.pdf"))
        Catch ex As System.Exception
            rep.Add($"FAIL 测试异常: {ex.Message}")
        End Try

        File.WriteAllLines(rp, rep)
        Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(vbLf & $"BMPLOT 完成，报告: {rp}")
    End Sub

    ' 取本机 DWG To PDF.pc3 的 A3 纸张名（优先全出血 A3，找不到则第一个 A3）
    Private Function GetA3PaperName(engine As BatchPlotEngine) As String
        Dim papers = engine.GetPaperList("DWG To PDF.pc3")
        Dim paper = papers.FirstOrDefault(Function(p) p.Contains("A3") AndAlso p.Contains("420"))
        If paper Is Nothing Then paper = papers.FirstOrDefault(Function(p) p.Contains("A3"))
        If paper Is Nothing AndAlso papers.Count > 0 Then paper = papers(0)
        Return If(paper, "")
    End Function

    ' 生成测试 DWG：一个闭合矩形图框 + 一个圆
    Private Sub CreateTestDrawing(path As String, offsetX As Double)
        Using db As New Database(True, True)
            Using tr = db.TransactionManager.StartTransaction()
                Dim ms = CType(tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite), BlockTableRecord)
                Dim pl As New Polyline()
                pl.AddVertexAt(0, New Point2d(offsetX, 0), 0, 0, 0)
                pl.AddVertexAt(1, New Point2d(offsetX + 42000, 0), 0, 0, 0)
                pl.AddVertexAt(2, New Point2d(offsetX + 42000, 29700), 0, 0, 0)
                pl.AddVertexAt(3, New Point2d(offsetX, 29700), 0, 0, 0)
                pl.Closed = True
                ms.AppendEntity(pl)
                tr.AddNewlyCreatedDBObject(pl, True)

                Dim c As New Circle(New Point3d(offsetX + 1000, 1000, 0), Vector3d.ZAxis, 500)
                ms.AppendEntity(c)
                tr.AddNewlyCreatedDBObject(c, True)
                tr.Commit()
            End Using
            db.SaveAs(path, DwgVersion.Current)
        End Using
    End Sub

#End Region

#Region "BSTAMP 打印戳记效果测试"

    ''' <summary>
    ''' BSTAMP：打印戳记实际效果测试。
    ''' 分别以"戳记开/关"各打一张 PDF（同一测试图纸），报告落盘供对比。
    ''' Editor.Command 同步执行，戳记应当批生效：ON 的 PDF 应带戳记水印。
    ''' </summary>
    <CommandMethod("BSTAMP")>
    Public Sub RunPlotStampTest()
        Dim rp = Path.Combine(Path.GetTempPath(), "BatchPlotVB_BSTAMP.txt")
        Dim rep As New List(Of String)
        rep.Add($"BSTAMP 打印戳记效果测试  {DateTime.Now:yyyy-MM-dd HH:mm:ss}")
        rep.Add(New String("-"c, 64))

        Dim testRoot = Path.Combine(Path.GetTempPath(), "BatchPlotVB_BSTAMP")
        Try
            If Directory.Exists(testRoot) Then Directory.Delete(testRoot, True)
            Directory.CreateDirectory(testRoot)

            Dim dwgPath = Path.Combine(testRoot, "S.dwg")
            CreateTestDrawing(dwgPath, 0)

            Dim engine As New BatchPlotEngine()
            Dim paperName = GetA3PaperName(engine)
            rep.Add($"使用纸张: {paperName}")

            Dim outs = {"stamp_on.pdf", "stamp_off.pdf"}
            Dim stamps = {True, False}

            For i = 0 To 1
                engine.BeginExternalPlot(dwgPath)
                Try
                    Dim st As New BatchPlotEngine.PlotSettings() With {.TraverseLayouts = False}
                    Dim frames = engine.DetectFramesByPolyline("*", st)
                    For Each fr In frames
                        fr.IsSelected = True
                    Next
                    engine.OrderFrames(frames, BatchPlotEngine.PlotOrderType.SelectOrder, False)

                    Dim settings As New BatchPlotEngine.PlotSettings() With {
                        .PlotterName = "DWG To PDF.pc3",
                        .PaperSize = paperName,
                        .PlotStyle = "无",
                        .DirectPrint = False,
                        .OutputPath = testRoot,
                        .NameTemplate = Path.GetFileNameWithoutExtension(outs(i)),
                        .PlotScale = BatchPlotEngine.PlotScaleType.FixedScale,
                        .FixedScale = 100,
                        .AutoRotate = True,
                        .AutoCenter = True,
                        .Copies = 1,
                        .Collate = False,
                        .PlotStamp = stamps(i),
                        .DuplicateHandling = BatchPlotEngine.DuplicateFileHandling.Overwrite}

                    engine.ExecuteBatchPlot(frames, settings, Nothing)

                    Dim pdf = Path.Combine(testRoot, outs(i))
                    rep.Add($"{If(File.Exists(pdf), "生成", "缺失")}: {pdf}  大小={If(File.Exists(pdf), New FileInfo(pdf).Length, -1)} 字节  (PlotStamp={stamps(i)})")
                Catch ex As System.Exception
                    rep.Add($"FAIL {outs(i)}: {ex.Message}")
                Finally
                    engine.EndExternalPlot()
                End Try
            Next

            rep.Add(New String("-"c, 64))
            rep.Add("判断方法：打开两张 PDF 对比——")
            rep.Add("  · ON 带戳记水印、OFF 无 = 戳记当批生效（Editor.Command 同步执行）")
            rep.Add("  · 两张相同 = 戳记未生效，查看引擎日志排查")
            rep.Add("  · 引擎日志: %APPDATA%\BatchPlotVB\Logs\（看『同步设置打印戳记』相关行）")
        Catch ex As System.Exception
            rep.Add($"FAIL 测试异常: {ex.Message}")
        End Try

        File.WriteAllLines(rp, rep)
        Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(vbLf & $"BSTAMP 完成，报告: {rp}")
    End Sub

#End Region

#Region "BUI 窗体显示与 DPI 缩放测试"

    ''' <summary>
    ''' BUI：窗体显示测试。在当前进程 DPI 下实例化各窗体并截图，
    ''' 输出实际缩放数据与 PNG 截图，供人工判断布局是否截断/重叠。
    ''' 注：进程 DPI 取决于启动环境；如需验证 150% 缩放，请将系统显示缩放
    ''' 调到 150% 后重启 AutoCAD 再执行本命令。
    ''' </summary>
    <CommandMethod("BUI")>
    Public Sub RunUiCaptureTest()
        Dim rp = Path.Combine(Path.GetTempPath(), "BatchPlotVB_BUI.txt")
        Dim rep As New List(Of String)
        Dim outDir = Path.Combine(Path.GetTempPath(), "BatchPlotVB_BUI")
        Try
            If Directory.Exists(outDir) Then Directory.Delete(outDir, True)
            Directory.CreateDirectory(outDir)

            Dim dpi As Single
            Using g = Graphics.FromHwnd(IntPtr.Zero)
                dpi = g.DpiX
            End Using
            rep.Add($"BUI 窗体显示测试  {DateTime.Now:yyyy-MM-dd HH:mm:ss}")
            rep.Add($"当前进程 DPI: {dpi:F0}（96=100%，120=125%，144=150%）")
            rep.Add(New String("-"c, 64))

            Dim engine As New BatchPlotEngine()

            CaptureForm(rep, outDir, "MainForm",
                        Function() New MainForm(),
                        New Size(820, 552))
            CaptureForm(rep, outDir, "MultiDocForm",
                        Function() New MultiDocForm(Nothing, Nothing),
                        New Size(634, 481))
            CaptureForm(rep, outDir, "PresetConfigForm",
                        Function() New PresetConfigForm(engine),
                        New Size(664, 409))
            CaptureForm(rep, outDir, "BlockPaperSettingsForm",
                        Function() New BlockPaperSettingsForm(engine),
                        New Size(363, 411))
            CaptureForm(rep, outDir, "PreviewAllForm",
                        Function() New PreviewAllForm(New List(Of String), New List(Of BatchPlotEngine.FrameInfo)),
                        New Size(960, 720))

            rep.Add(New String("-"c, 64))
            rep.Add("判断方法：打开下方 PNG 截图，检查文字是否被截断、控件是否重叠错位。")
            rep.Add($"截图目录: {outDir}")
        Catch ex As System.Exception
            rep.Add($"FAIL 测试异常: {ex.Message}")
        End Try

        File.WriteAllLines(rp, rep)
        Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(vbLf & $"BUI 完成，报告: {rp}")
    End Sub

    Private Sub CaptureForm(rep As List(Of String), outDir As String, name As String,
                            create As Func(Of System.Windows.Forms.Form), designSize As Size)
        Dim png = Path.Combine(outDir, $"ui_{name}.png")
        Try
            Using frm = create()
                frm.TopMost = False
                frm.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen
                frm.Show()
                System.Windows.Forms.Application.DoEvents()
                Dim w = Math.Max(frm.Width, 50)
                Dim h = Math.Max(frm.Height, 50)
                Using bmp As New Bitmap(w, h)
                    frm.DrawToBitmap(bmp, New Rectangle(0, 0, w, h))
                    bmp.Save(png, ImageFormat.Png)
                End Using
                Dim scale = frm.Width / designSize.Width
                rep.Add($"{name}: 设计={designSize.Width}x{designSize.Height}  实际={frm.Width}x{frm.Height}  缩放比={scale:F2}  AutoScaleMode={frm.AutoScaleMode}")
                rep.Add($"  截图: {png}")
                frm.Close()
            End Using
        Catch ex As System.Exception
            rep.Add($"{name}: 截图失败 {ex.Message}")
        End Try
    End Sub

#End Region

End Class
