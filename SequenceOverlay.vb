Imports Autodesk.AutoCAD.ApplicationServices
Imports Autodesk.AutoCAD.Colors
Imports Autodesk.AutoCAD.DatabaseServices
Imports Autodesk.AutoCAD.Geometry

''' <summary>
''' 图框序号临时标注（借鉴 batchPrintZWCAD 的 TemporarySequenceOverlay 思路）：
''' 在专用"不可打印"图层上为每个待打印图框绘制红色边框和打印顺序序号，
''' 供打印前在图上直观核对数量与顺序；支持高亮联动，窗口关闭/重新选择时自动清理。
''' </summary>
Public Class FrameSequenceOverlay
    Implements IDisposable

    ' 专用临时图层：红色、不可打印，出图/预览时引擎自动跳过
    Private Const LayerName As String = "BATCHPLOT_TEMP_OVERLAY"
    ' 序号文字样式：宋体，保证中文/数字显示正常
    Private Const TextStyleName As String = "BATCHPLOT_TEMP_TEXT"
    ' 高亮时线宽放大倍数（避免粗框挡住框内内容，只略粗于普通框）
    Private Const HighlightWidthScale As Double = 1.3

    Private ReadOnly _doc As Document
    Private ReadOnly _entityIds As New List(Of ObjectId)
    Private ReadOnly _groups As New Dictionary(Of BatchPlotEngine.FrameInfo, OverlayEntityGroup)
    Private _highlighted As BatchPlotEngine.FrameInfo

    Private Class OverlayEntityGroup
        Public FrameId As ObjectId
        Public LabelIds As New List(Of ObjectId)
        Public NormalWidth As Double
        Public HighlightWidth As Double
    End Class

    Public Sub New(doc As Document)
        _doc = doc
    End Sub

    ''' <summary>按给定顺序在图上绘制序号红框；重复调用会先清除上一批标注。</summary>
    Public Sub Show(frames As IList(Of BatchPlotEngine.FrameInfo))
        ' 整批重建时先清除旧标注（不刷屏，避免两次 Regen）
        Clear(False)

        Using docLock As DocumentLock = _doc.LockDocument()
            Using tr As Transaction = _doc.Database.TransactionManager.StartTransaction()
                Dim db As Database = _doc.Database
                Dim layerId As ObjectId = EnsureLayer(tr, db)
                Dim textStyleId As ObjectId = EnsureTextStyle(tr, db)

                For i As Integer = 0 To frames.Count - 1
                    Try
                        DrawFrameOverlay(tr, db, frames(i), (i + 1).ToString(), layerId, textStyleId)
                    Catch
                        ' 单个图框绘制失败只跳过该框，不让整批标注失败，
                        ' 也不能把中途失败的图元留在未提交事务里
                    End Try
                Next

                tr.Commit()
            End Using
        End Using
        UpdateScreen()
    End Sub

    ''' <summary>高亮指定图框（黄色、略粗线宽），例如预览列表选中联动。</summary>
    Public Sub SetHighlight(frame As BatchPlotEngine.FrameInfo)
        If frame Is _highlighted Then Return
        Try
            Using docLock As DocumentLock = _doc.LockDocument()
                Using tr As Transaction = _doc.Database.TransactionManager.StartTransaction()
                    ' 只恢复上一行、点亮当前行，避免删除/重画整批标注造成卡顿
                    ApplyHighlight(tr, _highlighted, False)
                    ApplyHighlight(tr, frame, True)
                    tr.Commit()
                    _highlighted = frame
                End Using
            End Using
            UpdateScreen()
        Catch
            ' 高亮切换失败不影响主流程，下一次整批 Show 会重新同步状态
        End Try
    End Sub

    Public Sub Clear()
        Clear(True)
    End Sub

    Public Sub Clear(repaint As Boolean)
        If _entityIds.Count = 0 AndAlso _groups.Count = 0 Then
            _highlighted = Nothing
            If repaint Then UpdateScreen()
            Return
        End If

        Dim cleared As Boolean = False
        Try
            Using docLock As DocumentLock = _doc.LockDocument()
                Using tr As Transaction = _doc.Database.TransactionManager.StartTransaction()
                    Dim db As Database = _doc.Database
                    EnsureLayerUnlocked(tr, db)

                    ' 按已跟踪 ID 删除；ID 丢失时按图层兜底扫描（只扫模型空间与布局）
                    Dim trackedIds As Integer = _entityIds.Count
                    For Each id As ObjectId In _entityIds
                        EraseEntityIfAlive(tr, id)
                    Next
                    If trackedIds = 0 Then
                        EraseAllEntitiesOnOverlayLayer(tr, db)
                    End If

                    tr.Commit()
                    cleared = True
                End Using
            End Using
        Catch
            ' 删除失败时保留已跟踪 ID，供下次重试
        End Try

        If cleared Then
            _entityIds.Clear()
            _groups.Clear()
            _highlighted = Nothing
        End If
        If repaint Then UpdateScreen()
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        Clear(False)
    End Sub

    ' ========== 内部绘制 ==========

    Private Sub DrawFrameOverlay(tr As Transaction, db As Database,
                                 frame As BatchPlotEngine.FrameInfo, label As String,
                                 layerId As ObjectId, textStyleId As ObjectId)
        Dim minX As Double = frame.Bounds.MinPoint.X
        Dim minY As Double = frame.Bounds.MinPoint.Y
        Dim maxX As Double = frame.Bounds.MaxPoint.X
        Dim maxY As Double = frame.Bounds.MaxPoint.Y
        Dim width As Double = maxX - minX
        Dim height As Double = maxY - minY

        ' 非标准图元误判出的图框可能出现 0/负/超大尺寸，不设防会导致画框崩溃
        If Not IsSaneLength(width) OrElse Not IsSaneLength(height) Then Return

        Dim minSide As Double = Math.Min(width, height)
        ' 红框比打印窗口略大一圈，避免粗框压住图框线
        Dim padding As Double = Math.Max(minSide * 0.035, 10.0)
        ' 序号文字：字高约为短边 10%，多位序号按框宽收窄
        Dim textHeight As Double = Math.Max(Math.Min(minSide * 0.1, width / Math.Max(label.Length, 1)), 0.001)

        Dim ownerId As ObjectId = GetFrameOwnerId(tr, frame)
        If ownerId.IsNull Then Return
        Dim owner As BlockTableRecord = CType(tr.GetObject(ownerId, OpenMode.ForWrite), BlockTableRecord)

        Dim isHighlight As Boolean = (frame Is _highlighted)
        Dim color As Color = If(isHighlight,
                                Color.FromColorIndex(ColorMethod.ByAci, 2),
                                Color.FromColorIndex(ColorMethod.ByAci, 1))
        Dim normalWidth As Double = GetFrameWidth(minSide)
        Dim frameWidth As Double = If(isHighlight, normalWidth * HighlightWidthScale, normalWidth)

        Dim group As New OverlayEntityGroup()
        group.NormalWidth = normalWidth
        group.HighlightWidth = normalWidth * HighlightWidthScale

        ' 红色边框（闭合多段线，随图框所在空间绘制到对应块表记录）
        Dim pl As New Polyline(4)
        pl.Closed = True
        pl.Color = color
        pl.LayerId = layerId
        pl.LineWeight = GetLineWeight(minSide)
        pl.ConstantWidth = frameWidth
        pl.AddVertexAt(0, New Point2d(minX - padding, minY - padding), 0, 0, 0)
        pl.AddVertexAt(1, New Point2d(maxX + padding, minY - padding), 0, 0, 0)
        pl.AddVertexAt(2, New Point2d(maxX + padding, maxY + padding), 0, 0, 0)
        pl.AddVertexAt(3, New Point2d(minX - padding, maxY + padding), 0, 0, 0)
        group.FrameId = AppendEntity(tr, owner, pl)

        ' 居中序号文字（对齐点必须在设置对齐模式之后再赋值）
        Dim center As New Point3d((minX + maxX) / 2.0, (minY + maxY) / 2.0, 0)
        Dim txt As New DBText()
        txt.TextString = label
        txt.Height = textHeight
        txt.HorizontalMode = TextHorizontalMode.TextCenter
        txt.VerticalMode = TextVerticalMode.TextVerticalMid
        txt.Color = color
        txt.LayerId = layerId
        If Not textStyleId.IsNull Then txt.TextStyleId = textStyleId
        txt.AlignmentPoint = center
        txt.Position = center
        Try
            txt.AdjustAlignment(db)
        Catch
        End Try
        group.LabelIds.Add(AppendEntity(tr, owner, txt))

        _groups(frame) = group
    End Sub

    ' 图框所在空间（模型空间或某布局）的块表记录：优先取图框实体自身，
    ' 直线凑框等场景没有主实体时取相关实体，保证布局内图框的红框画在对应布局里
    Private Function GetFrameOwnerId(tr As Transaction, frame As BatchPlotEngine.FrameInfo) As ObjectId
        Dim probeId As ObjectId = frame.ObjectId
        If probeId.IsNull AndAlso frame.RelatedObjectIds.Count > 0 Then
            probeId = frame.RelatedObjectIds(0)
        End If
        If probeId.IsNull OrElse probeId.IsErased Then Return ObjectId.Null

        Dim ent = TryCast(tr.GetObject(probeId, OpenMode.ForRead), Entity)
        If ent Is Nothing Then Return ObjectId.Null
        Return ent.OwnerId
    End Function

    Private Function AppendEntity(tr As Transaction, owner As BlockTableRecord, ent As Entity) As ObjectId
        Dim id As ObjectId = owner.AppendEntity(ent)
        tr.AddNewlyCreatedDBObject(ent, True)
        _entityIds.Add(id)
        Return id
    End Function

    ' ========== 高亮 ==========

    Private Sub ApplyHighlight(tr As Transaction, frame As BatchPlotEngine.FrameInfo, highlight As Boolean)
        If frame Is Nothing Then Return

        Dim group As OverlayEntityGroup = Nothing
        If Not _groups.TryGetValue(frame, group) Then Return

        Dim color As Color = If(highlight,
                                Color.FromColorIndex(ColorMethod.ByAci, 2),
                                Color.FromColorIndex(ColorMethod.ByAci, 1))

        If Not group.FrameId.IsNull AndAlso Not group.FrameId.IsErased Then
            Dim pl = TryCast(tr.GetObject(group.FrameId, OpenMode.ForWrite, False), Polyline)
            If pl IsNot Nothing AndAlso Not pl.IsErased Then
                pl.Color = color
                pl.ConstantWidth = If(highlight, group.HighlightWidth, group.NormalWidth)
            End If
        End If

        For Each id As ObjectId In group.LabelIds
            If id.IsNull OrElse id.IsErased Then Continue For
            Dim ent = TryCast(tr.GetObject(id, OpenMode.ForWrite, False), Entity)
            If ent IsNot Nothing AndAlso Not ent.IsErased Then
                ent.Color = color
            End If
        Next
    End Sub

    ' ========== 图层与文字样式 ==========

    Private Function EnsureLayer(tr As Transaction, db As Database) As ObjectId
        Dim table As LayerTable = CType(tr.GetObject(db.LayerTableId, OpenMode.ForRead), LayerTable)
        If table.Has(LayerName) Then
            Dim existing As LayerTableRecord = CType(tr.GetObject(table(LayerName), OpenMode.ForWrite), LayerTableRecord)
            existing.IsOff = False
            existing.IsFrozen = False
            existing.IsLocked = False
            existing.IsPlottable = False
            existing.Color = Color.FromColorIndex(ColorMethod.ByAci, 1)
            Return existing.ObjectId
        End If

        table.UpgradeOpen()
        Dim record As New LayerTableRecord()
        record.Name = LayerName
        record.Color = Color.FromColorIndex(ColorMethod.ByAci, 1)
        record.IsPlottable = False
        Dim id As ObjectId = table.Add(record)
        tr.AddNewlyCreatedDBObject(record, True)
        Return id
    End Function

    Private Function EnsureTextStyle(tr As Transaction, db As Database) As ObjectId
        Dim table As TextStyleTable = CType(tr.GetObject(db.TextStyleTableId, OpenMode.ForRead), TextStyleTable)
        If table.Has(TextStyleName) Then
            Dim existing As TextStyleTableRecord = CType(tr.GetObject(table(TextStyleName), OpenMode.ForWrite), TextStyleTableRecord)
            existing.FileName = "simsun.ttc"
            existing.XScale = 1.0
            existing.ObliquingAngle = 0
            Return existing.ObjectId
        End If

        table.UpgradeOpen()
        Dim record As New TextStyleTableRecord()
        record.Name = TextStyleName
        record.FileName = "simsun.ttc"
        record.XScale = 1.0
        record.ObliquingAngle = 0
        Dim id As ObjectId = table.Add(record)
        tr.AddNewlyCreatedDBObject(record, True)
        Return id
    End Function

    ' ========== 清理辅助 ==========

    Private Sub EnsureLayerUnlocked(tr As Transaction, db As Database)
        Dim table As LayerTable = CType(tr.GetObject(db.LayerTableId, OpenMode.ForRead), LayerTable)
        If Not table.Has(LayerName) Then Return
        Dim layer As LayerTableRecord = CType(tr.GetObject(table(LayerName), OpenMode.ForWrite), LayerTableRecord)
        layer.IsOff = False
        layer.IsFrozen = False
        layer.IsLocked = False
    End Sub

    Private Sub EraseEntityIfAlive(tr As Transaction, id As ObjectId)
        If id.IsNull OrElse id.IsErased Then Return
        Dim ent = TryCast(tr.GetObject(id, OpenMode.ForWrite, False), Entity)
        If ent IsNot Nothing AndAlso Not ent.IsErased Then
            ent.Erase()
        End If
    End Sub

    ' ID 丢失时的兜底：按图层名扫描模型空间与各布局删除残留标注
    Private Sub EraseAllEntitiesOnOverlayLayer(tr As Transaction, db As Database)
        Dim blockTable As BlockTable = CType(tr.GetObject(db.BlockTableId, OpenMode.ForRead), BlockTable)
        For Each btrId As ObjectId In blockTable
            Dim btr As BlockTableRecord = CType(tr.GetObject(btrId, OpenMode.ForRead), BlockTableRecord)
            If Not btr.IsLayout Then Continue For

            For Each entId As ObjectId In btr
                Dim ent = TryCast(tr.GetObject(entId, OpenMode.ForRead, False), Entity)
                If ent Is Nothing OrElse ent.IsErased Then Continue For
                If String.Equals(ent.Layer, LayerName, StringComparison.OrdinalIgnoreCase) Then
                    ent.UpgradeOpen()
                    ent.Erase()
                End If
            Next
        Next
    End Sub

    Private Sub UpdateScreen()
        Try
            _doc.Editor.UpdateScreen()
        Catch
        End Try
    End Sub

    ' ========== 尺寸分级 ==========

    Private Shared Function IsSaneLength(value As Double) As Boolean
        If Double.IsNaN(value) OrElse Double.IsInfinity(value) Then Return False
        Return value > 0.000000001 AndAlso value < 1000000000.0
    End Function

    ' 红框常量宽度随图框尺寸分档，避免小框线过粗、大框线过细
    Private Shared Function GetFrameWidth(minSide As Double) As Double
        If minSide < 200.0 Then
            Return 0.5
        ElseIf minSide < 800.0 Then
            Return 1.0
        ElseIf minSide < 3000.0 Then
            Return 2.0
        Else
            Return 4.0
        End If
    End Function

    Private Shared Function GetLineWeight(minSide As Double) As LineWeight
        If minSide < 200.0 Then
            Return LineWeight.LineWeight025
        ElseIf minSide < 800.0 Then
            Return LineWeight.LineWeight035
        ElseIf minSide < 3000.0 Then
            Return LineWeight.LineWeight050
        Else
            Return LineWeight.LineWeight100
        End If
    End Function

End Class
