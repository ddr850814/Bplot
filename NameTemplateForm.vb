Imports System.Windows.Forms
Imports System.Drawing

Public Class NameTemplateForm

    ' 返回的模板值
    Public Property SelectedTemplate As String = ""

    Public Sub New()
        InitializeComponent()
    End Sub

    Private Sub RadioButton_CheckedChanged(sender As Object, e As EventArgs)
        ' 启用/禁用文本框
        txtBlockAttr.Enabled = radBlockAttr.Checked
        txtLayerText.Enabled = radLayerText.Checked
        UpdateSelectedTemplate()
    End Sub

    Private Sub UpdateSelectedTemplate()
        Dim template As String = ""

        If radDWGFolderName.Checked Then
            template = "<DWGFOLDERNAME>"
        ElseIf radDWGName.Checked Then
            template = "<DWGNAME>"
        ElseIf radCTab.Checked Then
            template = "<CTAB>"
        ElseIf radPaperName.Checked Then
            template = "<PAPERNAME>"
        ElseIf radAutoIndex.Checked Then
            template = "<INDEX>"
        ElseIf radBlockAttr.Checked Then
            template = txtBlockAttr.Text
        ElseIf radLayerText.Checked Then
            template = txtLayerText.Text
        End If

        SelectedTemplate = template
    End Sub

    Private Sub BtnOK_Click(sender As Object, e As EventArgs)
        UpdateSelectedTemplate()
        Me.DialogResult = DialogResult.OK
        Me.Close()
    End Sub

    Private Sub BtnCancel_Click(sender As Object, e As EventArgs)
        Me.DialogResult = DialogResult.Cancel
        Me.Close()
    End Sub
End Class
