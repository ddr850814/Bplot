Imports Autodesk.AutoCAD.ApplicationServices
Imports Autodesk.AutoCAD.DatabaseServices
Imports Autodesk.AutoCAD.Geometry

Public Class FrameDetector
    Private ReadOnly _db As Database
    Private ReadOnly _logger As Logger

    Public Sub New(db As Database)
        _db = db
        _logger = Logger.GetInstance()
    End Sub

    ' 识别图框（通过图块）
    Public Function DetectFramesByBlock(blockName As String, layerName As String, Optional settings As BatchPlotEngine.PlotSettings = Nothing) As List(Of BatchPlotEngine.FrameInfo)
        Dim frames As New List(Of BatchPlotEngine.FrameInfo)()
        Try
            Using tr As Transaction = _db.TransactionManager.StartTransaction()
                Dim bt = CType(tr.GetObject(_db.BlockTableId, OpenMode.ForRead), BlockTable)
                Dim layoutsToProcess As List(Of String) = GetLayoutsToProcess(settings)

                Dim frameIndex = 0
                For Each layoutName As String In layoutsToProcess
                    Dim spaceId As ObjectId = GetLayoutSpaceId(tr, bt, layoutName)
                    If spaceId.IsNull Then Continue For

                    Dim space = CType(tr.GetObject(spaceId, OpenMode.ForRead), BlockTableRecord)

                    For Each objId As ObjectId In space
                        If objId.ObjectClass.DxfName = "INSERT" Then
                            Dim br = TryCast(tr.GetObject(objId, OpenMode.ForRead), BlockReference)
                            If br Is Nothing Then Continue For

                            Dim btr = CType(tr.GetObject(br.BlockTableRecord, OpenMode.ForRead), BlockTableRecord)
                            Dim actualBlockName As String = btr.Name

                            Dim matchBlock As Boolean = MatchAnyPattern(actualBlockName, blockName)
                            Dim matchLayer As Boolean = MatchAnyPattern(br.Layer, layerName)

                            If matchBlock AndAlso matchLayer Then
                                frameIndex += 1
                                Dim frameInfo As New BatchPlotEngine.FrameInfo()
                                frameInfo.ObjectId = objId
                                frameInfo.BlockName = actualBlockName
                                frameInfo.LayoutName = layoutName
                                frameInfo.Bounds = br.GeometricExtents
                                frameInfo.FrameNumber = $"Frame_{frameIndex:D3}"
                                frameInfo.OrderIndex = frameIndex
                                frames.Add(frameInfo)
                            End If
                        End If
                    Next
                Next

                frames = FilterNestedFrames(frames)
                tr.Commit()
            End Using
        Catch ex As Exception
            _logger.Error("通过图块识别图框失败", ex)
        End Try
        Return frames
    End Function

    ' 通过多段线识别图框
    Public Function DetectFramesByPolyline(layerName As String, Optional settings As BatchPlotEngine.PlotSettings = Nothing) As List(Of BatchPlotEngine.FrameInfo)
        Dim frames As New List(Of BatchPlotEngine.FrameInfo)()
        Try
            Using tr As Transaction = _db.TransactionManager.StartTransaction()
                Dim bt = CType(tr.GetObject(_db.BlockTableId, OpenMode.ForRead), BlockTable)
                Dim layoutsToProcess As List(Of String) = GetLayoutsToProcess(settings)

                Dim frameIndex = 0
                For Each layoutName As String In layoutsToProcess
                    Dim spaceId As ObjectId = GetLayoutSpaceId(tr, bt, layoutName)
                    If spaceId.IsNull Then Continue For

                    Dim space = CType(tr.GetObject(spaceId, OpenMode.ForRead), BlockTableRecord)

                    For Each objId As ObjectId In space
                        If objId.ObjectClass.DxfName = "LWPOLYLINE" OrElse objId.ObjectClass.DxfName = "POLYLINE" Then
                            Dim pl = TryCast(tr.GetObject(objId, OpenMode.ForRead), Polyline)
                            If pl Is Nothing Then Continue For

                            Dim matchLayer As Boolean = String.IsNullOrEmpty(layerName) OrElse MatchAnyPattern(pl.Layer, layerName)

                            If matchLayer AndAlso pl.Closed AndAlso pl.NumberOfVertices = 4 Then
                                frameIndex += 1
                                Dim frameInfo As New BatchPlotEngine.FrameInfo()
                                frameInfo.ObjectId = objId
                                frameInfo.BlockName = "PolylineFrame"
                                frameInfo.LayoutName = layoutName
                                frameInfo.Bounds = pl.GeometricExtents
                                frameInfo.FrameNumber = $"Frame_{frameIndex:D3}"
                                frameInfo.OrderIndex = frameIndex
                                frames.Add(frameInfo)
                            End If
                        End If
                    Next
                Next

                frames = FilterNestedFrames(frames)
                tr.Commit()
            End Using
        Catch ex As Exception
            _logger.Error("通过多段线识别图框失败", ex)
        End Try
        Return frames
    End Function

    ' 通过直线识别图框
    Public Function DetectFramesByLine(layerName As String, Optional settings As BatchPlotEngine.PlotSettings = Nothing) As List(Of BatchPlotEngine.FrameInfo)
        Dim frames As New List(Of BatchPlotEngine.FrameInfo)()
        Try
            Using tr As Transaction = _db.TransactionManager.StartTransaction()
                Dim bt = CType(tr.GetObject(_db.BlockTableId, OpenMode.ForRead), BlockTable)
                Dim layoutsToProcess As List(Of String) = GetLayoutsToProcess(settings)

                Dim frameIndex = 0
                For Each layoutName As String In layoutsToProcess
                    Dim spaceId As ObjectId = GetLayoutSpaceId(tr, bt, layoutName)
                    If spaceId.IsNull Then Continue For

                    Dim space = CType(tr.GetObject(spaceId, OpenMode.ForRead), BlockTableRecord)

                    Dim lines As New List(Of Line)()
                    For Each objId As ObjectId In space
                        If objId.ObjectClass.DxfName = "LINE" Then
                            Dim line = TryCast(tr.GetObject(objId, OpenMode.ForRead), Line)
                            If line Is Nothing Then Continue For

                            Dim matchLayer As Boolean = String.IsNullOrEmpty(layerName) OrElse MatchAnyPattern(line.Layer, layerName)
                            If matchLayer Then
                                lines.Add(line)
                            End If
                        End If
                    Next

                    For i = 0 To lines.Count - 1 Step 4
                        If i + 3 < lines.Count Then
                            frameIndex += 1
                            Dim frameInfo As New BatchPlotEngine.FrameInfo()
                            frameInfo.ObjectId = lines(i).ObjectId
                            frameInfo.BlockName = "LineFrame"
                            frameInfo.LayoutName = layoutName

                            Dim minPt As New Point3d(Double.MaxValue, Double.MaxValue, Double.MaxValue)
                            Dim maxPt As New Point3d(Double.MinValue, Double.MinValue, Double.MinValue)
                            For j As Integer = i To Math.Min(i + 3, lines.Count - 1)
                                Dim line As Line = lines(j)
                                minPt = New Point3d(Math.Min(minPt.X, Math.Min(line.StartPoint.X, line.EndPoint.X)),
                                                   Math.Min(minPt.Y, Math.Min(line.StartPoint.Y, line.EndPoint.Y)),
                                                   Math.Min(minPt.Z, Math.Min(line.StartPoint.Z, line.EndPoint.Z)))
                                maxPt = New Point3d(Math.Max(maxPt.X, Math.Max(line.StartPoint.X, line.EndPoint.X)),
                                                   Math.Max(maxPt.Y, Math.Max(line.StartPoint.Y, line.EndPoint.Y)),
                                                   Math.Max(maxPt.Z, Math.Max(line.StartPoint.Z, line.EndPoint.Z)))
                            Next
                            frameInfo.Bounds = New Extents3d(minPt, maxPt)
                            frameInfo.FrameNumber = $"Frame_{frameIndex:D3}"
                            frameInfo.OrderIndex = frameIndex
                            frames.Add(frameInfo)
                        End If
                    Next
                Next

                tr.Commit()
            End Using
        Catch ex As Exception
            _logger.Error("通过直线识别图框失败", ex)
        End Try
        Return frames
    End Function

    ' 获取需要处理的布局列表
    Private Function GetLayoutsToProcess(settings As BatchPlotEngine.PlotSettings) As List(Of String)
        If settings IsNot Nothing AndAlso settings.TraverseLayouts Then
            Return GetAllLayoutList()
        Else
            Return New List(Of String) From {"Model"}
        End If
    End Function

    ' 获取布局空间ID
    Private Function GetLayoutSpaceId(tr As Transaction, bt As BlockTable, layoutName As String) As ObjectId
        If layoutName = "Model" Then
            Return bt(BlockTableRecord.ModelSpace)
        End If

        For Each objId As ObjectId In bt
            Dim btr = CType(tr.GetObject(objId, OpenMode.ForRead), BlockTableRecord)
            If btr.IsLayout Then
                Dim layout = CType(tr.GetObject(btr.LayoutId, OpenMode.ForRead), Layout)
                If layout.LayoutName = layoutName Then
                    Return objId
                End If
            End If
        Next
        Return ObjectId.Null
    End Function

    ' 获取所有布局列表（包括模型空间）
    Private Function GetAllLayoutList() As List(Of String)
        Dim layouts As New List(Of String)()
        Try
            Using tr As Transaction = _db.TransactionManager.StartTransaction()
                Dim bt = CType(tr.GetObject(_db.BlockTableId, OpenMode.ForRead), BlockTable)
                For Each objId As ObjectId In bt
                    Dim btr = CType(tr.GetObject(objId, OpenMode.ForRead), BlockTableRecord)
                    If btr.IsLayout Then
                        Dim layout = CType(tr.GetObject(btr.LayoutId, OpenMode.ForRead), Layout)
                        layouts.Add(layout.LayoutName)
                    End If
                Next
                tr.Commit()
            End Using
        Catch ex As Exception
            _logger.Error("获取布局列表失败", ex)
        End Try
        Return layouts
    End Function

    ' 判断名称是否匹配逗号分隔的多个模式
    Private Function MatchAnyPattern(name As String, pattern As String) As Boolean
        If String.IsNullOrEmpty(pattern) Then Return True
        Dim patterns As String() = pattern.Split(New Char() {","c}, StringSplitOptions.RemoveEmptyEntries)
        For Each p As String In patterns
            Dim trimmed As String = p.Trim()
            If Not String.IsNullOrEmpty(trimmed) AndAlso name Like trimmed Then
                Return True
            End If
        Next
        Return False
    End Function

    ' 过滤嵌套图框，只保留最外层
    Private Function FilterNestedFrames(frames As List(Of BatchPlotEngine.FrameInfo)) As List(Of BatchPlotEngine.FrameInfo)
        If frames Is Nothing OrElse frames.Count <= 1 Then
            Return frames
        End If

        Dim result As New List(Of BatchPlotEngine.FrameInfo)()

        For Each frame As BatchPlotEngine.FrameInfo In frames
            Dim isNested = False
            For Each other As BatchPlotEngine.FrameInfo In frames
                If frame.ObjectId = other.ObjectId Then Continue For
                If IsFrameContained(frame, other) Then
                    isNested = True
                    Exit For
                End If
            Next

            If Not isNested Then
                result.Add(frame)
            End If
        Next

        For i = 0 To result.Count - 1
            result(i).OrderIndex = i + 1
            result(i).FrameNumber = $"Frame_{i + 1:D3}"
        Next

        Return result
    End Function

    ' 判断 innerFrame 是否被 outerFrame 完全包含
    Private Function IsFrameContained(innerFrame As BatchPlotEngine.FrameInfo, outerFrame As BatchPlotEngine.FrameInfo) As Boolean
        Try
            Dim innerMin As Point3d = innerFrame.Bounds.MinPoint
            Dim innerMax As Point3d = innerFrame.Bounds.MaxPoint
            Dim outerMin As Point3d = outerFrame.Bounds.MinPoint
            Dim outerMax As Point3d = outerFrame.Bounds.MaxPoint

            Dim tolerance = 0.001
            Dim isSmaller As Boolean = ((innerMax.X - innerMin.X) < (outerMax.X - outerMin.X) - tolerance OrElse
                                       (innerMax.Y - innerMin.Y) < (outerMax.Y - outerMin.Y) - tolerance)

            Return (innerMin.X >= outerMin.X - tolerance) AndAlso
                   (innerMin.Y >= outerMin.Y - tolerance) AndAlso
                   (innerMax.X <= outerMax.X + tolerance) AndAlso
                   (innerMax.Y <= outerMax.Y + tolerance) AndAlso
                   isSmaller
        Catch
            Return False
        End Try
    End Function
End Class
