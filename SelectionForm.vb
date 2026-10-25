Imports System.Drawing
Imports System.Windows.Forms

Public Class SelectionForm
    Inherits Form

    Private lstItems As ListBox
    Private btnOK As Button
    Private btnCancel As Button
    Private _selectedItems As String = ""

    Public ReadOnly Property SelectedItems As String
        Get
            Return _selectedItems
        End Get
    End Property

    Public Sub New(title As String, items As List(Of String), Optional multiSelect As Boolean = True)
        AutoScaleDimensions = New SizeF(6.0F, 12.0F)
        AutoScaleMode = AutoScaleMode.Font
        Text = title
        Size = New Size(320, 450)
        StartPosition = FormStartPosition.CenterScreen
        FormBorderStyle = FormBorderStyle.FixedDialog
        MaximizeBox = False
        MinimizeBox = False
        TopMost = True

        lstItems = New ListBox()
        lstItems.Location = New Point(12, 12)
        lstItems.Size = New Size(280, 340)
        If multiSelect Then
            lstItems.SelectionMode = SelectionMode.MultiExtended
        Else
            lstItems.SelectionMode = SelectionMode.One
        End If
        lstItems.Sorted = True
        Controls.Add(lstItems)

        For Each item As String In items
            lstItems.Items.Add(item)
        Next

        btnOK = New Button()
        btnOK.Text = "确定"
        btnOK.Location = New Point(60, 365)
        btnOK.Size = New Size(80, 28)
        btnOK.DialogResult = DialogResult.OK
        AddHandler btnOK.Click, AddressOf BtnOK_Click
        Controls.Add(btnOK)

        btnCancel = New Button()
        btnCancel.Text = "取消"
        btnCancel.Location = New Point(170, 365)
        btnCancel.Size = New Size(80, 28)
        btnCancel.DialogResult = DialogResult.Cancel
        Controls.Add(btnCancel)

        AcceptButton = btnOK
        CancelButton = btnCancel
    End Sub

    Private Sub BtnOK_Click(sender As Object, e As EventArgs)
        Dim selected As New List(Of String)()
        For Each item As Object In lstItems.SelectedItems
            selected.Add(item.ToString())
        Next
        _selectedItems = String.Join(",", selected)
    End Sub
End Class
