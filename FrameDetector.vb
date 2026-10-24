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

                            ' 动态块插入后的 BlockTableRecord 指向匿名块 *U###，须解析原始动态块定义名再匹配
                            Dim actualBlockName As String = br.Name
                            If br.IsDynamicBlock Then
                                Try
                                    Dim dynBtr = CType(tr.GetObject(br.DynamicBlockTableRecord, OpenMode.ForRead), BlockTableRecord)
                                    actualBlockName = dynBtr.Name
                                Catch
                                    ' 解析失败退回匿名名：仅该动态块匹配不上，不影响其余检测
                                End Try
                            End If

                            Dim matchBlock As Boolean = MatchAnyPattern(actualBlockName, blockName)
                            Dim matchLayer As Boolean = MatchAnyPattern(br.Layer, layerName)

                            If matchBlock AndAlso matchLayer AndAlso IsEntityUsable(tr, br) Then
                                Dim extents As Extents3d
                                Try
                                    extents = br.GeometricExtents
                                Catch
                                    ' 空块/无可见几何会抛异常：跳过该实体，不得中断整次检测
                                    Continue For
                                End Try

                                frameIndex += 1
                                Dim frameInfo As New BatchPlotEngine.FrameInfo()
                                frameInfo.ObjectId = objId
                                frameInfo.BlockName = actualBlockName
                                frameInfo.LayoutName = layoutName
                                frameInfo.Bounds = extents
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
                            Dim entity = TryCast(tr.GetObject(objId, OpenMode.ForRead), Entity)
                            If entity Is Nothing Then Continue For

                            Dim matchLayer As Boolean = String.IsNullOrEmpty(layerName) OrElse MatchAnyPattern(entity.Layer, layerName)
                            If Not matchLayer OrElse Not IsEntityUsable(tr, entity) Then Continue For

                            ' LWPOLYLINE 与老式 POLYLINE（Polyline2d）都支持
                            Dim bounds As Extents3d
                            If TypeOf entity Is Polyline Then
                                Dim pl = CType(entity, Polyline)
                                If Not pl.Closed OrElse pl.NumberOfVertices <> 4 Then Continue For
                                bounds = pl.GeometricExtents
                            ElseIf TypeOf entity Is Polyline2d Then
                                Dim pl2d = CType(entity, Polyline2d)
                                If Not pl2d.Closed Then Continue For
                                Dim vertexCount = 0
                                For Each vid As ObjectId In pl2d
                                    vertexCount += 1
                                Next
                                If vertexCount <> 4 Then Continue For
                                bounds = pl2d.GeometricExtents
                            Else
                                Continue For
                            End If

                            frameIndex += 1
                            AddPolylineFrame(frames, objId, layoutName, bounds, frameIndex)
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

    ' 通过直线识别图框：四条独立线段首尾拼合成矩形（几何闭环搜索，替换原先"按顺序每 4 条凑一框"）
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

                    ' 收集线段：直线每条一段；直线型开放多段线整条作为一段
                    Dim segments As New List(Of CadSegment)()
                    For Each objId As ObjectId In space
                        If objId.ObjectClass.DxfName = "LINE" Then
                            Dim line = TryCast(tr.GetObject(objId, OpenMode.ForRead), Line)
                            If line Is Nothing Then Continue For
                            If Not String.IsNullOrEmpty(layerName) AndAlso Not MatchAnyPattern(line.Layer, layerName) Then Continue For
                            If Not IsEntityUsable(tr, line) Then Continue For

                            Dim seg As New CadSegment()
                            seg.StartPoint = line.StartPoint
                            seg.EndPoint = line.EndPoint
                            seg.SourceId = line.ObjectId
                            If seg.StartPoint.DistanceTo(seg.EndPoint) > 1e-6 Then
                                segments.Add(seg)
                            End If
                        ElseIf objId.ObjectClass.DxfName = "LWPOLYLINE" OrElse objId.ObjectClass.DxfName = "POLYLINE" Then
                            Dim entity = TryCast(tr.GetObject(objId, OpenMode.ForRead), Entity)
                            If entity Is Nothing Then Continue For
                            If Not String.IsNullOrEmpty(layerName) AndAlso Not MatchAnyPattern(entity.Layer, layerName) Then Continue For
                            If Not IsEntityUsable(tr, entity) Then Continue For

                            Dim seg As CadSegment = Nothing
                            If TryGetStraightOpenPolylineSegment(tr, entity, seg) Then
                                segments.Add(seg)
                            End If
                        End If
                    Next

                    If segments.Count < 4 Then Continue For

                    ' 四线闭环搜索
                    Dim finder As New FourLineFinder(segments)
                    Dim rectangles = finder.Find()

                    For Each rect In rectangles
                        frameIndex += 1
                        Dim frameInfo As New BatchPlotEngine.FrameInfo()
                        frameInfo.ObjectId = segments(rect.SegmentIndices(0)).SourceId
                        frameInfo.BlockName = "LineFrame"
                        frameInfo.LayoutName = layoutName
                        frameInfo.Bounds = rect.Bounds
                        frameInfo.FrameNumber = $"Frame_{frameIndex:D3}"
                        frameInfo.OrderIndex = frameIndex

                        ' 记录四条边对应的源对象（同一实体可能被拆为多条线段，去重）
                        Dim usedIds As New HashSet(Of ObjectId)()
                        For Each idx As Integer In rect.SegmentIndices
                            If usedIds.Add(segments(idx).SourceId) Then
                                frameInfo.RelatedObjectIds.Add(segments(idx).SourceId)
                            End If
                        Next
                        frames.Add(frameInfo)
                    Next
                Next

                frames = FilterNestedFrames(frames)
                tr.Commit()
            End Using
        Catch ex As Exception
            _logger.Error("通过直线识别图框失败", ex)
        End Try
        Return frames
    End Function

    ' 实体可见性检查：实体隐藏、图层关闭或冻结时跳过（与 CAD 屏幕显示一致）
    Private Function IsEntityUsable(tr As Transaction, ent As Entity) As Boolean
        Try
            If Not ent.Visible Then Return False
            Dim layerTtr = CType(tr.GetObject(ent.LayerId, OpenMode.ForRead), LayerTableRecord)
            If layerTtr.IsOff OrElse layerTtr.IsFrozen Then Return False
            Return True
        Catch
            Return True
        End Try
    End Function

    ' 记录一条多段线图框
    Private Sub AddPolylineFrame(frames As List(Of BatchPlotEngine.FrameInfo), objId As ObjectId, layoutName As String, bounds As Extents3d, ByRef frameIndex As Integer)
        Dim frameInfo As New BatchPlotEngine.FrameInfo()
        frameInfo.ObjectId = objId
        frameInfo.BlockName = "PolylineFrame"
        frameInfo.LayoutName = layoutName
        frameInfo.Bounds = bounds
        frameInfo.FrameNumber = $"Frame_{frameIndex:D3}"
        frameInfo.OrderIndex = frameIndex
        frames.Add(frameInfo)
    End Sub

    ' 将开放多段线整体提取为一条直线段边（允许中间共线顶点，禁止圆弧/折线/回折）
    Private Function TryGetStraightOpenPolylineSegment(tr As Transaction, ent As Entity, ByRef segment As CadSegment) As Boolean
        Dim points As New List(Of Point3d)()

        If TypeOf ent Is Polyline Then
            Dim pl = CType(ent, Polyline)
            If pl.Closed OrElse pl.NumberOfVertices < 2 Then Return False
            For index = 0 To pl.NumberOfVertices - 1
                If index < pl.NumberOfVertices - 1 AndAlso Math.Abs(pl.GetBulgeAt(index)) > 1e-9 Then Return False
                points.Add(pl.GetPoint3dAt(index))
            Next
        ElseIf TypeOf ent Is Polyline2d Then
            Dim pl2d = CType(ent, Polyline2d)
            If pl2d.Closed Then Return False
            Dim vertices As New List(Of Vertex2d)()
            For Each vertexId As ObjectId In pl2d
                Dim vertex = TryCast(tr.GetObject(vertexId, OpenMode.ForRead, False), Vertex2d)
                If vertex IsNot Nothing Then vertices.Add(vertex)
            Next
            If vertices.Count < 2 Then Return False
            For index = 0 To vertices.Count - 1
                If index < vertices.Count - 1 AndAlso Math.Abs(vertices(index).Bulge) > 1e-9 Then Return False
                points.Add(vertices(index).Position)
            Next
        Else
            Return False
        End If

        Dim startPoint = points(0)
        Dim endPoint = points(points.Count - 1)
        Dim dx = endPoint.X - startPoint.X
        Dim dy = endPoint.Y - startPoint.Y
        Dim lengthSquared = dx * dx + dy * dy
        If lengthSquared <= 1e-12 Then Return False

        Dim length = Math.Sqrt(lengthSquared)
        Dim distanceTolerance = Math.Max(1e-6, length * 1e-8)
        Dim parameterTolerance = distanceTolerance / length
        Dim previousParameter = -parameterTolerance
        For Each pt As Point3d In points
            Dim cross = (pt.X - startPoint.X) * dy - (pt.Y - startPoint.Y) * dx
            If Math.Abs(cross) / length > distanceTolerance Then Return False

            Dim parameter = ((pt.X - startPoint.X) * dx + (pt.Y - startPoint.Y) * dy) / lengthSquared
            If parameter < -parameterTolerance OrElse parameter > 1.0 + parameterTolerance _
                OrElse parameter + parameterTolerance < previousParameter Then
                Return False
            End If
            previousParameter = parameter
        Next

        segment = New CadSegment()
        segment.StartPoint = startPoint
        segment.EndPoint = endPoint
        segment.SourceId = ent.ObjectId
        Return True
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
    Public Shared Function MatchAnyPattern(name As String, pattern As String) As Boolean
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
            ' 跨布局（含模型/图纸空间）坐标互不相干：不同布局的图框不构成嵌套，否则会被误删漏打
            If Not String.Equals(innerFrame.LayoutName, outerFrame.LayoutName, StringComparison.OrdinalIgnoreCase) Then
                Return False
            End If

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

    ' 由 Line 或开放多段线提取的线段
    Private Class CadSegment
        Public StartPoint As Point3d
        Public EndPoint As Point3d
        Public SourceId As ObjectId
    End Class

    ' 四线闭环搜索结果
    Private Class FoundRectangle
        Public Bounds As Extents3d
        Public SegmentIndices As Integer()
    End Class

    ' 四条独立线段首尾拼合矩形的几何搜索（按边长 0.1%、下限 0.01 图面单位的端点容差）
    Private Class FourLineFinder
        Private ReadOnly _segments As List(Of CadSegment)
        Private ReadOnly _lengths As Double()
        Private ReadOnly _cell As Double
        Private ReadOnly _grid As New Dictionary(Of String, List(Of Integer))()
        Private ReadOnly _foundKeys As New HashSet(Of String)()
        Private ReadOnly _rectangles As New List(Of FoundRectangle)()

        ' 角点附近只取最近若干条连接边，避免密集端点造成组合爆炸
        Private Const MaximumConnectionsAtCorner As Integer = 8

        Public Sub New(segments As List(Of CadSegment))
            _segments = segments
            ReDim _lengths(segments.Count - 1)
            Dim maxTol = 0.01
            For i = 0 To segments.Count - 1
                _lengths(i) = segments(i).StartPoint.DistanceTo(segments(i).EndPoint)
                maxTol = Math.Max(maxTol, EndpointTolerance(_lengths(i)))
            Next
            _cell = maxTol

            ' 端点空间哈希：单元尺寸取最大容差，3×3 邻域查询即可覆盖任意查询容差
            For i = 0 To segments.Count - 1
                AddEndpoint(segments(i).StartPoint, i)
                AddEndpoint(segments(i).EndPoint, i)
            Next
        End Sub

        Public Function Find() As List(Of FoundRectangle)
            If _segments.Count < 4 Then Return _rectangles
            For i1 = 0 To _segments.Count - 1
                ExploreCycle(i1, _segments(i1).StartPoint, _segments(i1).EndPoint)
                ExploreCycle(i1, _segments(i1).EndPoint, _segments(i1).StartPoint)
            Next
            Return _rectangles
        End Function

        Private Function EndpointTolerance(segmentLength As Double) As Double
            Return Math.Max(0.01, segmentLength * 0.001)
        End Function

        Private Function CellKey(pt As Point3d) As String
            Return CType(Math.Floor(pt.X / _cell), Long).ToString() & "," & CType(Math.Floor(pt.Y / _cell), Long).ToString()
        End Function

        Private Sub AddEndpoint(pt As Point3d, segIndex As Integer)
            Dim key = CellKey(pt)
            Dim bucket As List(Of Integer) = Nothing
            If Not _grid.TryGetValue(key, bucket) Then
                bucket = New List(Of Integer)()
                _grid.Add(key, bucket)
            End If
            bucket.Add(segIndex)
        End Sub

        Private Function FindConnected(excluded As HashSet(Of Integer), pt As Point3d, tolerance As Double) As List(Of Integer)
            Dim candidates As New List(Of KeyValuePair(Of Integer, Double))()
            Dim seen As New HashSet(Of Integer)()
            Dim cx = CType(Math.Floor(pt.X / _cell), Long)
            Dim cy = CType(Math.Floor(pt.Y / _cell), Long)
            For gx = cx - 1 To cx + 1
                For gy = cy - 1 To cy + 1
                    Dim bucket As List(Of Integer) = Nothing
                    If _grid.TryGetValue(gx.ToString() & "," & gy.ToString(), bucket) Then
                        For Each segIndex As Integer In bucket
                            If excluded.Contains(segIndex) OrElse Not seen.Add(segIndex) Then Continue For
                            Dim distance = Math.Min(_segments(segIndex).StartPoint.DistanceTo(pt), _segments(segIndex).EndPoint.DistanceTo(pt))
                            If distance <= tolerance Then
                                candidates.Add(New KeyValuePair(Of Integer, Double)(segIndex, distance))
                            End If
                        Next
                    End If
                Next
            Next
            candidates.Sort(Function(left, right) left.Value.CompareTo(right.Value))
            Dim result As New List(Of Integer)()
            For index = 0 To Math.Min(MaximumConnectionsAtCorner, candidates.Count) - 1
                result.Add(candidates(index).Key)
            Next
            Return result
        End Function

        Private Function TryOtherEnd(segIndex As Integer, pt As Point3d, tolerance As Double, ByRef otherEnd As Point3d) As Boolean
            Dim s = _segments(segIndex)
            If IsNear(s.StartPoint, pt, tolerance) Then
                otherEnd = s.EndPoint
                Return True
            End If
            If IsNear(s.EndPoint, pt, tolerance) Then
                otherEnd = s.StartPoint
                Return True
            End If
            Return False
        End Function

        Private Shared Function IsNear(a As Point3d, b As Point3d, tol As Double) As Boolean
            Dim dx = a.X - b.X
            Dim dy = a.Y - b.Y
            Dim dz = a.Z - b.Z
            Return dx * dx + dy * dy + dz * dz <= tol * tol
        End Function

        ' 从线段 i1 的 pointA→pointB 方向出发，按 i2/i3/i4 三层展开搜索首尾闭环
        Private Sub ExploreCycle(i1 As Integer, pointA As Point3d, pointB As Point3d)
            Dim toleranceAtB = EndpointTolerance(_lengths(i1))
            Dim excluded1 As New HashSet(Of Integer)()
            excluded1.Add(i1)
            Dim connectedAtB = FindConnected(excluded1, pointB, toleranceAtB)

            For Each i2 As Integer In connectedAtB
                Dim joinTolerance12 = Math.Max(toleranceAtB, EndpointTolerance(_lengths(i2)))
                Dim pointC As Point3d = Nothing
                If Not TryOtherEnd(i2, pointB, joinTolerance12, pointC) Then Continue For

                Dim toleranceAtC = EndpointTolerance(_lengths(i2))
                Dim excluded2 As New HashSet(Of Integer)(excluded1)
                excluded2.Add(i2)
                Dim connectedAtC = FindConnected(excluded2, pointC, toleranceAtC)

                For Each i3 As Integer In connectedAtC
                    Dim joinTolerance23 = Math.Max(toleranceAtC, EndpointTolerance(_lengths(i3)))
                    Dim pointD As Point3d = Nothing
                    If Not TryOtherEnd(i3, pointC, joinTolerance23, pointD) Then Continue For

                    Dim toleranceAtD = EndpointTolerance(_lengths(i3))
                    Dim excluded3 As New HashSet(Of Integer)(excluded2)
                    excluded3.Add(i3)
                    Dim connectedAtD = FindConnected(excluded3, pointD, toleranceAtD)

                    For Each i4 As Integer In connectedAtD
                        Dim joinTolerance34 = Math.Max(toleranceAtD, EndpointTolerance(_lengths(i4)))
                        Dim backToA As Point3d = Nothing
                        If Not TryOtherEnd(i4, pointD, joinTolerance34, backToA) Then Continue For

                        Dim closeTolerance = Math.Max(EndpointTolerance(_lengths(i4)), EndpointTolerance(_lengths(i1)))
                        If Not IsNear(backToA, pointA, closeTolerance) Then Continue For

                        ' 求四条边的无限延长线交点得到角点
                        Dim cornerA, cornerB, cornerC, cornerD As Point3d
                        If Not TryIntersectLines(_segments(i4), _segments(i1), cornerA) Then Continue For
                        If Not TryIntersectLines(_segments(i1), _segments(i2), cornerB) Then Continue For
                        If Not TryIntersectLines(_segments(i2), _segments(i3), cornerC) Then Continue For
                        If Not TryIntersectLines(_segments(i3), _segments(i4), cornerD) Then Continue For

                        Dim corners = New Point3d() {cornerA, cornerB, cornerC, cornerD}
                        Dim bounds As Extents3d
                        If Not TryBuildRectangleFromCorners(corners, bounds) Then Continue For

                        ' 按排序后的线段索引去重（同一矩形会从不同起点被找到）
                        Dim ids = New Integer() {i1, i2, i3, i4}
                        Array.Sort(ids)
                        If Not _foundKeys.Add(String.Join(",", ids)) Then Continue For

                        Dim rect As New FoundRectangle()
                        rect.Bounds = bounds
                        rect.SegmentIndices = ids
                        _rectangles.Add(rect)
                    Next
                Next
            Next
        End Sub

        ' 求两条直线的无限延长线交点；平行或重合时返回 False
        Private Shared Function TryIntersectLines(a As CadSegment, b As CadSegment, ByRef intersection As Point3d) As Boolean
            Dim dx1 = a.EndPoint.X - a.StartPoint.X
            Dim dy1 = a.EndPoint.Y - a.StartPoint.Y
            Dim dx2 = b.EndPoint.X - b.StartPoint.X
            Dim dy2 = b.EndPoint.Y - b.StartPoint.Y
            Dim denom = dx1 * dy2 - dy1 * dx2
            If Math.Abs(denom) < 1e-12 Then Return False

            Dim t = ((b.StartPoint.X - a.StartPoint.X) * dy2 - (b.StartPoint.Y - a.StartPoint.Y) * dx2) / denom
            intersection = New Point3d(a.StartPoint.X + t * dx1, a.StartPoint.Y + t * dy1, 0)
            Return True
        End Function

        ' 将 4 个角点验证为矩形：非退化 + 共面 + 邻边垂直 + 对边平行等长 + 对角线等长 + 中点重合
        Private Shared Function TryBuildRectangleFromCorners(corners As Point3d(), ByRef bounds As Extents3d) As Boolean
            Dim minX = Double.MaxValue, minY = Double.MaxValue, minZ = Double.MaxValue
            Dim maxX = Double.MinValue, maxY = Double.MinValue, maxZ = Double.MinValue
            For Each p As Point3d In corners
                minX = Math.Min(minX, p.X)
                minY = Math.Min(minY, p.Y)
                minZ = Math.Min(minZ, p.Z)
                maxX = Math.Max(maxX, p.X)
                maxY = Math.Max(maxY, p.Y)
                maxZ = Math.Max(maxZ, p.Z)
            Next
            Dim boxWidth = maxX - minX
            Dim boxHeight = maxY - minY
            If boxWidth <= 1e-6 OrElse boxHeight <= 1e-6 Then Return False

            Dim tolerance = Math.Max(boxWidth, boxHeight) * 0.001
            If maxZ - minZ > tolerance Then Return False

            ' 角点互异
            For i = 0 To 3
                For j = i + 1 To 3
                    If SamePoint(corners(i), corners(j), tolerance) Then Return False
                Next
            Next

            Dim edgeX(3) As Double
            Dim edgeY(3) As Double
            Dim edgeLength(3) As Double
            For index = 0 To 3
                Dim nextIndex = (index + 1) Mod 4
                edgeX(index) = corners(nextIndex).X - corners(index).X
                edgeY(index) = corners(nextIndex).Y - corners(index).Y
                edgeLength(index) = Math.Sqrt(edgeX(index) * edgeX(index) + edgeY(index) * edgeY(index))
                If edgeLength(index) <= tolerance Then Return False
            Next

            ' 邻边垂直
            Const directionTolerance = 0.001
            For index = 0 To 3
                Dim nextIndex = (index + 1) Mod 4
                Dim normalizedDot = Math.Abs(edgeX(index) * edgeX(nextIndex) + edgeY(index) * edgeY(nextIndex)) / (edgeLength(index) * edgeLength(nextIndex))
                If normalizedDot > directionTolerance Then Return False
            Next

            ' 对边平行且等长
            Dim parallel02 = Math.Abs(edgeX(0) * edgeY(2) - edgeY(0) * edgeX(2)) / (edgeLength(0) * edgeLength(2))
            Dim parallel13 = Math.Abs(edgeX(1) * edgeY(3) - edgeY(1) * edgeX(3)) / (edgeLength(1) * edgeLength(3))
            If parallel02 > directionTolerance OrElse parallel13 > directionTolerance _
                OrElse Math.Abs(edgeLength(0) - edgeLength(2)) > Math.Max(tolerance, edgeLength(0) * 0.001) _
                OrElse Math.Abs(edgeLength(1) - edgeLength(3)) > Math.Max(tolerance, edgeLength(1) * 0.001) Then
                Return False
            End If

            ' 对角线等长
            Dim d02 = corners(0).DistanceTo(corners(2))
            Dim d13 = corners(1).DistanceTo(corners(3))
            If Math.Abs(d02 - d13) > tolerance Then Return False

            ' 中点重合
            Dim mid02 As New Point3d((corners(0).X + corners(2).X) / 2.0, (corners(0).Y + corners(2).Y) / 2.0, 0)
            Dim mid13 As New Point3d((corners(1).X + corners(3).X) / 2.0, (corners(1).Y + corners(3).Y) / 2.0, 0)
            If mid02.DistanceTo(mid13) > tolerance Then Return False

            bounds = New Extents3d(New Point3d(minX, minY, minZ), New Point3d(maxX, maxY, maxZ))
            Return True
        End Function

        Private Shared Function SamePoint(a As Point3d, b As Point3d, tolerance As Double) As Boolean
            Return Math.Abs(a.X - b.X) <= tolerance AndAlso Math.Abs(a.Y - b.Y) <= tolerance
        End Function
    End Class
End Class
