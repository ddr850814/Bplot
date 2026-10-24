Imports System.Windows.Forms
Imports Autodesk.AutoCAD.ApplicationServices
Imports Autodesk.AutoCAD.DatabaseServices
Imports Autodesk.AutoCAD.EditorInput
Imports Autodesk.AutoCAD.Runtime

Public Class NameTemplateForm

    ' 返回的模板值
    Public Property SelectedTemplate As String = ""

    ' 存储点选结果
    Private _blockAttrResult As String = ""
    Private _layerTextResult As String = ""

    Public Sub New()
        InitializeComponent()
    End Sub

    Private Sub RadioButton_CheckedChanged(sender As Object, e As EventArgs) Handles radPaperName.CheckedChanged, radLayerText.CheckedChanged, radDWGName.CheckedChanged, radDWGFolderName.CheckedChanged, radCTab.CheckedChanged, radBlockAttr.CheckedChanged, radAutoIndex.CheckedChanged
        Dim rad = DirectCast(sender, RadioButton)
        If Not rad.Checked Then Return

        If radBlockAttr.Checked Then
            PickBlockAndAttribute()
        ElseIf radLayerText.Checked Then
            PickLayerForText()
        End If

        UpdateSelectedTemplate()
    End Sub

    ''' <summary>
    ''' 点选图块并选择属性
    ''' </summary>
    Private Sub PickBlockAndAttribute()
        Try
            Dim doc As Document = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument
            If doc Is Nothing Then
                _blockAttrResult = "<BLOCKATTR:图框:图号>"
                Return
            End If

            ' 隐藏自身和主窗口
            Hide()
            If Owner IsNot Nothing Then Owner.Hide()

            Dim ed As Editor = doc.Editor
            Dim opts As New PromptEntityOptions(vbLf & "请在图中点选一个图块参照以获取属性")
            opts.SetRejectMessage(vbLf & "请选择图块参照")
            opts.AddAllowedClass(GetType(BlockReference), True)

            Dim result As PromptEntityResult = ed.GetEntity(opts)

            If result.Status = PromptStatus.OK Then
                Dim blockName As String = ""
                Dim attrNames As New List(Of String)()

                Using tr As Transaction = doc.Database.TransactionManager.StartTransaction()
                    Dim br = CType(tr.GetObject(result.ObjectId, OpenMode.ForRead), BlockReference)
                    If br IsNot Nothing Then
                        Dim btr = CType(tr.GetObject(br.BlockTableRecord, OpenMode.ForRead), BlockTableRecord)
                        blockName = btr.Name

                        For Each attrId As ObjectId In br.AttributeCollection
                            Dim attr = TryCast(tr.GetObject(attrId, OpenMode.ForRead), AttributeReference)
                            If attr IsNot Nothing Then
                                attrNames.Add(attr.Tag)
                            End If
                        Next
                    End If
                    tr.Commit()
                End Using

                If attrNames.Count > 0 Then
                    Using selForm As New SelectionForm("选择属性", attrNames)
                        selForm.StartPosition = FormStartPosition.CenterScreen
                        If selForm.ShowDialog() = DialogResult.OK Then
                            _blockAttrResult = $"<BLOCKATTR:{blockName}:{selForm.SelectedItems}>"
                        Else
                            _blockAttrResult = $"<BLOCKATTR:{blockName}:图号>"
                        End If
                    End Using
                Else
                    _blockAttrResult = $"<BLOCKATTR:{blockName}:图号>"
                End If
            Else
                _blockAttrResult = "<BLOCKATTR:图框:图号>"
            End If

            ' 恢复主窗口和自身
            If Owner IsNot Nothing Then Owner.Show()
            Show()
            Activate()

        Catch ex As Exception
            If Owner IsNot Nothing Then Owner.Show()
            Show()
            _blockAttrResult = "<BLOCKATTR:图框:图号>"
        End Try
    End Sub

    ''' <summary>
    ''' 点选对象获取图层名
    ''' </summary>
    Private Sub PickLayerForText()
        Try
            Dim doc As Document = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument
            If doc Is Nothing Then
                _layerTextResult = "<LAYERTEXT:图号层>"
                Return
            End If

            ' 隐藏自身和主窗口
            Hide()
            If Owner IsNot Nothing Then Owner.Hide()

            Dim ed As Editor = doc.Editor
            Dim opts As New PromptEntityOptions(vbLf & "请在图中点选一个对象以获取图层名")
            opts.SetRejectMessage(vbLf & "请选择有效的对象")

            Dim result As PromptEntityResult = ed.GetEntity(opts)

            If result.Status = PromptStatus.OK Then
                Using tr As Transaction = doc.Database.TransactionManager.StartTransaction()
                    Dim ent = CType(tr.GetObject(result.ObjectId, OpenMode.ForRead), Entity)
                    If ent IsNot Nothing Then
                        _layerTextResult = $"<LAYERTEXT:{ent.Layer}>"
                    End If
                    tr.Commit()
                End Using
            Else
                _layerTextResult = "<LAYERTEXT:图号层>"
            End If

            ' 恢复主窗口和自身
            If Owner IsNot Nothing Then Owner.Show()
            Show()
            Activate()

        Catch ex As Exception
            If Owner IsNot Nothing Then Owner.Show()
            Show()
            _layerTextResult = "<LAYERTEXT:图号层>"
        End Try
    End Sub

    Private Sub UpdateSelectedTemplate()
        If radDWGFolderName.Checked Then
            SelectedTemplate = "<DWGFOLDERNAME>"
        ElseIf radDWGName.Checked Then
            SelectedTemplate = "<DWGNAME>"
        ElseIf radCTab.Checked Then
            SelectedTemplate = "<CTAB>"
        ElseIf radPaperName.Checked Then
            SelectedTemplate = "<PAPERNAME>"
        ElseIf radAutoIndex.Checked Then
            SelectedTemplate = "<INDEX>"
        ElseIf radBlockAttr.Checked Then
            SelectedTemplate = _blockAttrResult
        ElseIf radLayerText.Checked Then
            SelectedTemplate = _layerTextResult
        End If
    End Sub

    Private Sub BtnOK_Click(sender As Object, e As EventArgs) Handles btnOK.Click
        UpdateSelectedTemplate()
        DialogResult = DialogResult.OK
        Close()
    End Sub

    Private Sub BtnCancel_Click(sender As Object, e As EventArgs) Handles btnCancel.Click
        DialogResult = DialogResult.Cancel
        Close()
    End Sub
End Class
