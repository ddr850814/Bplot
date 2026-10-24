Imports System.Windows.Forms
Imports Autodesk.AutoCAD.ApplicationServices
Imports Autodesk.AutoCAD.DatabaseServices
Imports Autodesk.AutoCAD.EditorInput
Imports Autodesk.AutoCAD.Geometry
Imports Autodesk.AutoCAD.Runtime

Public Class Commands

    <CommandMethod("BatchPlot")>
    Public Sub BatchPlotCommand()
        Try
            Dim doc As Document = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument
            If doc Is Nothing Then
                MessageBox.Show("请先打开一个AutoCAD文档", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            Dim frm As New MainForm()
            frm.Show()

        Catch ex As System.Exception
            MessageBox.Show("启动批量打印失败: " & ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    <CommandMethod("BPlot")>
    Public Sub BPlotCommand()
        BatchPlotCommand()
    End Sub

    <CommandMethod("BWK")>
    Public Sub BWKCommand()
        Try
            Dim doc As Document = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument
            If doc Is Nothing Then
                MessageBox.Show("请先打开一个AutoCAD文档", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            Dim ed As Editor = doc.Editor

            Dim opts As New PromptSelectionOptions()
            opts.MessageForAdding = "选择要创建包围框的对象"
            opts.AllowDuplicates = False

            Dim result As PromptSelectionResult = ed.GetSelection(opts)

            If result.Status = PromptStatus.OK Then
                Dim ss As SelectionSet = result.Value

                ' 询问偏移距离
                Dim offsetDist As Double = 0
                Dim distOpts As New PromptDistanceOptions(vbLf & "输入偏移距离（正值扩大，负值缩小）<0>: ")
                distOpts.AllowNegative = True
                distOpts.AllowZero = True
                distOpts.AllowNone = True
                distOpts.DefaultValue = 0
                Dim distResult As PromptDoubleResult = ed.GetDistance(distOpts)

                If distResult.Status = PromptStatus.OK Then
                    offsetDist = distResult.Value
                ElseIf distResult.Status = PromptStatus.None Then
                    offsetDist = 0
                Else
                    Return
                End If

                Using tr As Transaction = doc.Database.TransactionManager.StartTransaction()
                    Dim bounds As Extents3d = CalculateBoundsForCommand(doc.Database, ss, tr)

                    If bounds.MinPoint <> bounds.MaxPoint Then
                        CreateBoundingBoxForCommand(doc.Database, bounds, tr, offsetDist)
                        tr.Commit()
                        ed.WriteMessage(vbLf & "包围框创建成功！")
                    Else
                        tr.Dispose()
                        ed.WriteMessage(vbLf & "无法计算包围框。")
                    End If
                End Using
            End If

        Catch ex As System.Exception
            MessageBox.Show("创建包围框时出错: " & ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Function CalculateBoundsForCommand(db As Database, ss As SelectionSet, tr As Transaction) As Extents3d
        Dim minPt As New Point3d(Double.MaxValue, Double.MaxValue, Double.MaxValue)
        Dim maxPt As New Point3d(Double.MinValue, Double.MinValue, Double.MinValue)
        Dim hasValidObject = False

        For Each id As ObjectId In ss.GetObjectIds()
            Try
                Dim ent = CType(tr.GetObject(id, OpenMode.ForRead), Entity)
                If ent IsNot Nothing Then
                    Dim ext As Extents3d = ent.GeometricExtents
                    minPt = New Point3d(Math.Min(minPt.X, ext.MinPoint.X),
                                       Math.Min(minPt.Y, ext.MinPoint.Y),
                                       Math.Min(minPt.Z, ext.MinPoint.Z))
                    maxPt = New Point3d(Math.Max(maxPt.X, ext.MaxPoint.X),
                                       Math.Max(maxPt.Y, ext.MaxPoint.Y),
                                       Math.Max(maxPt.Z, ext.MaxPoint.Z))
                    hasValidObject = True
                End If
            Catch
            End Try
        Next

        If hasValidObject Then
            Return New Extents3d(minPt, maxPt)
        Else
            Return New Extents3d(New Point3d(0, 0, 0), New Point3d(0, 0, 0))
        End If
    End Function

    Private Sub CreateBoundingBoxForCommand(db As Database, bounds As Extents3d, tr As Transaction, offsetDist As Double)
        Dim bt = CType(tr.GetObject(db.BlockTableId, OpenMode.ForRead), BlockTable)
        Dim modelSpace = CType(tr.GetObject(bt(BlockTableRecord.ModelSpace), OpenMode.ForWrite), BlockTableRecord)

        ' 应用偏移距离
        Dim minX As Double = bounds.MinPoint.X - offsetDist
        Dim minY As Double = bounds.MinPoint.Y - offsetDist
        Dim maxX As Double = bounds.MaxPoint.X + offsetDist
        Dim maxY As Double = bounds.MaxPoint.Y + offsetDist

        Dim pl As New Polyline()
        pl.AddVertexAt(0, New Point2d(minX, minY), 0, 0, 0)
        pl.AddVertexAt(1, New Point2d(maxX, minY), 0, 0, 0)
        pl.AddVertexAt(2, New Point2d(maxX, maxY), 0, 0, 0)
        pl.AddVertexAt(3, New Point2d(minX, maxY), 0, 0, 0)
        pl.Closed = True
        pl.Layer = "0"

        modelSpace.AppendEntity(pl)
        tr.AddNewlyCreatedDBObject(pl, True)
    End Sub

End Class
