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

                Using tr As Transaction = doc.Database.TransactionManager.StartTransaction()
                    Dim bounds As Extents3d = CalculateBoundsForCommand(doc.Database, ss, tr)

                    If bounds.MinPoint <> bounds.MaxPoint Then
                        CreateBoundingBoxForCommand(doc.Database, bounds, tr)
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

    Private Sub CreateBoundingBoxForCommand(db As Database, bounds As Extents3d, tr As Transaction)
        Dim bt = CType(tr.GetObject(db.BlockTableId, OpenMode.ForRead), BlockTable)
        Dim modelSpace = CType(tr.GetObject(bt(BlockTableRecord.ModelSpace), OpenMode.ForWrite), BlockTableRecord)

        Dim pl As New Polyline()
        pl.AddVertexAt(0, New Point2d(bounds.MinPoint.X, bounds.MinPoint.Y), 0, 0, 0)
        pl.AddVertexAt(1, New Point2d(bounds.MaxPoint.X, bounds.MinPoint.Y), 0, 0, 0)
        pl.AddVertexAt(2, New Point2d(bounds.MaxPoint.X, bounds.MaxPoint.Y), 0, 0, 0)
        pl.AddVertexAt(3, New Point2d(bounds.MinPoint.X, bounds.MaxPoint.Y), 0, 0, 0)
        pl.Closed = True
        pl.Layer = "0"

        modelSpace.AppendEntity(pl)
        tr.AddNewlyCreatedDBObject(pl, True)
    End Sub

End Class
