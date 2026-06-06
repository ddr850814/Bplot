Imports System.Drawing
Imports System.IO
Imports System.Windows.Forms

Public Class MultiDocForm
    Inherits Form

    ' 文件夹选择
    Private lblFolder As Label
    Private txtFolder As TextBox
    Private WithEvents btnBrowse As Button

    ' 选项
    Private WithEvents chkOpenDocuments As CheckBox
    Private WithEvents chkUseSingleDocStyle As CheckBox

    ' 文件列表（左侧）
    Private lstFiles As ListBox

    ' 已选文件（右侧）
    Private lstSelectedFiles As ListBox

    ' 按钮
    Private WithEvents btnAdd As Button
    Private WithEvents btnRemove As Button
    Private WithEvents btnOK As Button
    Private WithEvents btnCancel As Button

    Private _files As List(Of String)
    Private _selectedFiles As List(Of String)
    Private _initialSelectedFiles As List(Of String)  ' 传入的已选择文件

    Public ReadOnly Property SelectedFiles As List(Of String)
        Get
            Return _selectedFiles
        End Get
    End Property

    ' 是否使用单文档内部打印样式
    Public ReadOnly Property UseSingleDocStyle As Boolean
        Get
            Return chkUseSingleDocStyle.Checked
        End Get
    End Property

    ' 默认构造函数
    Public Sub New()
        Me.New(Nothing)
    End Sub

    ' 带已选择文件的构造函数
    Public Sub New(initialSelectedFiles As List(Of String))
        InitializeComponent()
        _files = New List(Of String)()
        _selectedFiles = New List(Of String)()
        _initialSelectedFiles = initialSelectedFiles
    End Sub

    Private Sub InitializeComponent()
        Me.lblFolder = New System.Windows.Forms.Label()
        Me.txtFolder = New System.Windows.Forms.TextBox()
        Me.btnBrowse = New System.Windows.Forms.Button()
        Me.chkOpenDocuments = New System.Windows.Forms.CheckBox()
        Me.chkUseSingleDocStyle = New System.Windows.Forms.CheckBox()
        Me.lstFiles = New System.Windows.Forms.ListBox()
        Me.lstSelectedFiles = New System.Windows.Forms.ListBox()
        Me.btnAdd = New System.Windows.Forms.Button()
        Me.btnRemove = New System.Windows.Forms.Button()
        Me.btnOK = New System.Windows.Forms.Button()
        Me.btnCancel = New System.Windows.Forms.Button()
        Me.SuspendLayout()
        '
        'lblFolder
        '
        Me.lblFolder.Location = New System.Drawing.Point(12, 15)
        Me.lblFolder.Name = "lblFolder"
        Me.lblFolder.Size = New System.Drawing.Size(50, 20)
        Me.lblFolder.TabIndex = 0
        Me.lblFolder.Text = "文件夹:"
        '
        'txtFolder
        '
        Me.txtFolder.Location = New System.Drawing.Point(65, 12)
        Me.txtFolder.Name = "txtFolder"
        Me.txtFolder.Size = New System.Drawing.Size(480, 21)
        Me.txtFolder.TabIndex = 1
        Me.txtFolder.Text = "C:\Users\jiazheng\桌面"
        '
        'btnBrowse
        '
        Me.btnBrowse.Location = New System.Drawing.Point(555, 10)
        Me.btnBrowse.Name = "btnBrowse"
        Me.btnBrowse.Size = New System.Drawing.Size(60, 25)
        Me.btnBrowse.TabIndex = 2
        Me.btnBrowse.Text = "浏览..."
        '
        'chkOpenDocuments
        '
        Me.chkOpenDocuments.Location = New System.Drawing.Point(12, 45)
        Me.chkOpenDocuments.Name = "chkOpenDocuments"
        Me.chkOpenDocuments.Size = New System.Drawing.Size(100, 20)
        Me.chkOpenDocuments.TabIndex = 3
        Me.chkOpenDocuments.Text = "打开的文档"
        '
        'chkUseSingleDocStyle
        '
        Me.chkUseSingleDocStyle.Location = New System.Drawing.Point(400, 45)
        Me.chkUseSingleDocStyle.Name = "chkUseSingleDocStyle"
        Me.chkUseSingleDocStyle.Size = New System.Drawing.Size(180, 20)
        Me.chkUseSingleDocStyle.TabIndex = 4
        Me.chkUseSingleDocStyle.Text = "使用单文档内部打印样式"
        '
        'lstFiles
        '
        Me.lstFiles.ItemHeight = 12
        Me.lstFiles.Location = New System.Drawing.Point(12, 75)
        Me.lstFiles.Name = "lstFiles"
        Me.lstFiles.SelectionMode = System.Windows.Forms.SelectionMode.MultiExtended
        Me.lstFiles.Size = New System.Drawing.Size(280, 316)
        Me.lstFiles.TabIndex = 5
        '
        'lstSelectedFiles
        '
        Me.lstSelectedFiles.ItemHeight = 12
        Me.lstSelectedFiles.Location = New System.Drawing.Point(340, 75)
        Me.lstSelectedFiles.Name = "lstSelectedFiles"
        Me.lstSelectedFiles.SelectionMode = System.Windows.Forms.SelectionMode.MultiExtended
        Me.lstSelectedFiles.Size = New System.Drawing.Size(280, 316)
        Me.lstSelectedFiles.TabIndex = 6
        '
        'btnAdd
        '
        Me.btnAdd.Location = New System.Drawing.Point(90, 405)
        Me.btnAdd.Name = "btnAdd"
        Me.btnAdd.Size = New System.Drawing.Size(120, 30)
        Me.btnAdd.TabIndex = 7
        Me.btnAdd.Text = "添加文件"
        '
        'btnRemove
        '
        Me.btnRemove.Location = New System.Drawing.Point(420, 405)
        Me.btnRemove.Name = "btnRemove"
        Me.btnRemove.Size = New System.Drawing.Size(120, 30)
        Me.btnRemove.TabIndex = 8
        Me.btnRemove.Text = "移除文件"
        '
        'btnOK
        '
        Me.btnOK.DialogResult = System.Windows.Forms.DialogResult.OK
        Me.btnOK.Location = New System.Drawing.Point(200, 445)
        Me.btnOK.Name = "btnOK"
        Me.btnOK.Size = New System.Drawing.Size(100, 30)
        Me.btnOK.TabIndex = 9
        Me.btnOK.Text = "确定"
        '
        'btnCancel
        '
        Me.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel
        Me.btnCancel.Location = New System.Drawing.Point(350, 445)
        Me.btnCancel.Name = "btnCancel"
        Me.btnCancel.Size = New System.Drawing.Size(100, 30)
        Me.btnCancel.TabIndex = 10
        Me.btnCancel.Text = "取消"
        '
        'MultiDocForm
        '
        Me.ClientSize = New System.Drawing.Size(634, 481)
        Me.Controls.Add(Me.lblFolder)
        Me.Controls.Add(Me.txtFolder)
        Me.Controls.Add(Me.btnBrowse)
        Me.Controls.Add(Me.chkOpenDocuments)
        Me.Controls.Add(Me.chkUseSingleDocStyle)
        Me.Controls.Add(Me.lstFiles)
        Me.Controls.Add(Me.lstSelectedFiles)
        Me.Controls.Add(Me.btnAdd)
        Me.Controls.Add(Me.btnRemove)
        Me.Controls.Add(Me.btnOK)
        Me.Controls.Add(Me.btnCancel)
        Me.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog
        Me.MaximizeBox = False
        Me.MinimizeBox = False
        Me.Name = "MultiDocForm"
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen
        Me.Text = "选择文件"
        Me.TopMost = True
        Me.ResumeLayout(False)
        Me.PerformLayout()

    End Sub

    Private Sub MultiDocForm_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        ' 加载初始文件列表
        LoadFilesFromFolder(txtFolder.Text)

        ' 恢复已选择的文件
        If _initialSelectedFiles IsNot Nothing AndAlso _initialSelectedFiles.Count > 0 Then
            RestoreSelectedFiles()
        End If
    End Sub

    ' 恢复已选择的文件到右侧列表
    Private Sub RestoreSelectedFiles()
        Try
            For Each filePath As String In _initialSelectedFiles
                If File.Exists(filePath) Then
                    Dim fileName As String = Path.GetFileName(filePath)

                    ' 如果文件不在左侧列表中，先添加到左侧
                    If Not _files.Contains(filePath) Then
                        _files.Add(filePath)
                        lstFiles.Items.Add(fileName)
                    End If

                    ' 添加到右侧已选列表
                    If Not _selectedFiles.Contains(filePath) Then
                        _selectedFiles.Add(filePath)
                        lstSelectedFiles.Items.Add(fileName)
                    End If
                End If
            Next
        Catch ex As Exception
            Debug.WriteLine("恢复已选择文件失败: " & ex.Message)
        End Try
    End Sub

    Private Sub LoadFilesFromFolder(folderPath As String)
        Try
            lstFiles.Items.Clear()
            _files.Clear()

            If Directory.Exists(folderPath) Then
                ' 加载DWG文件
                Dim dwgFiles As String() = Directory.GetFiles(folderPath, "*.dwg")
                For Each file As String In dwgFiles
                    _files.Add(file)
                    lstFiles.Items.Add(Path.GetFileName(file))
                Next
            End If
        Catch ex As Exception
            MessageBox.Show("加载文件列表失败: " & ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub BtnBrowse_Click(sender As Object, e As EventArgs) Handles btnBrowse.Click
        Using fbd As New FolderBrowserDialog()
            fbd.Description = "选择文件夹"
            fbd.SelectedPath = txtFolder.Text

            If fbd.ShowDialog() = DialogResult.OK Then
                txtFolder.Text = fbd.SelectedPath
                LoadFilesFromFolder(fbd.SelectedPath)
            End If
        End Using
    End Sub

    Private Sub BtnAdd_Click(sender As Object, e As EventArgs) Handles btnAdd.Click
        ' 从左侧列表添加到右侧列表
        For Each selectedIndex As Integer In lstFiles.SelectedIndices
            Dim filePath As String = _files(selectedIndex)
            Dim fileName As String = Path.GetFileName(filePath)

            If Not _selectedFiles.Contains(filePath) Then
                _selectedFiles.Add(filePath)
                lstSelectedFiles.Items.Add(fileName)
            End If
        Next
    End Sub

    Private Sub BtnRemove_Click(sender As Object, e As EventArgs) Handles btnRemove.Click
        ' 从右侧列表移除
        For i As Integer = lstSelectedFiles.SelectedIndices.Count - 1 To 0 Step -1
            Dim index As Integer = lstSelectedFiles.SelectedIndices(i)
            _selectedFiles.RemoveAt(index)
            lstSelectedFiles.Items.RemoveAt(index)
        Next
    End Sub

    Private Sub ChkOpenDocuments_CheckedChanged(sender As Object, e As EventArgs) Handles chkOpenDocuments.CheckedChanged
        If chkOpenDocuments.Checked Then
            ' 将打开的文档自动添加到右侧已选列表（如果还没有选择）
            AddOpenDocumentsToSelected()
        End If
    End Sub

    ' 将打开的文档添加到右侧已选列表
    Private Sub AddOpenDocumentsToSelected()
        Try
            ' 获取所有打开的文档
            For Each doc As Autodesk.AutoCAD.ApplicationServices.Document In
                Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager

                If Not String.IsNullOrEmpty(doc.Name) Then
                    Dim filePath As String = doc.Name
                    Dim fileName As String = Path.GetFileName(filePath)

                    ' 如果还没有选择此文件，则添加到右侧列表
                    If Not _selectedFiles.Contains(filePath) Then
                        ' 先添加到左侧列表（如果不在）
                        If Not _files.Contains(filePath) Then
                            _files.Add(filePath)
                            lstFiles.Items.Add(fileName)
                        End If

                        ' 添加到右侧已选列表
                        _selectedFiles.Add(filePath)
                        lstSelectedFiles.Items.Add(fileName)
                    End If
                End If
            Next
        Catch ex As Exception
            Debug.WriteLine("添加打开的文档失败: " & ex.Message)
        End Try
    End Sub

    Private Sub BtnOK_Click(sender As Object, e As EventArgs) Handles btnOK.Click
        If _selectedFiles.Count = 0 Then
            MessageBox.Show("请至少选择一个文件", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)
            DialogResult = DialogResult.None
        End If
    End Sub

End Class
