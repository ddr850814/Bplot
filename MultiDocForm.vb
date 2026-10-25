Imports System.IO
Imports System.Windows.Forms

Public Class MultiDocForm
    Inherits Form

    ' 文件夹选择
    Private lblFolder As Label
    Private WithEvents txtFolder As TextBox
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
    Private WithEvents btnMoveUp As Button
    Private WithEvents btnMoveDown As Button
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
        Me.New(Nothing, Nothing)
    End Sub

    ' 带已选择文件的构造函数
    Public Sub New(initialSelectedFiles As List(Of String))
        Me.New(initialSelectedFiles, Nothing)
    End Sub

    ' 带已选择文件和默认文件夹的构造函数
    Public Sub New(initialSelectedFiles As List(Of String), defaultFolder As String)
        InitializeComponent()
        _files = New List(Of String)()
        _selectedFiles = New List(Of String)()
        _initialSelectedFiles = initialSelectedFiles

        ' 设置默认文件夹路径
        If Not String.IsNullOrEmpty(defaultFolder) AndAlso Directory.Exists(defaultFolder) Then
            txtFolder.Text = defaultFolder
        Else
            ' 尝试获取当前打开文档的目录
            Try
                Dim doc As Autodesk.AutoCAD.ApplicationServices.Document =
                    Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument
                If doc IsNot Nothing AndAlso Not String.IsNullOrEmpty(doc.Name) Then
                    Dim docDir As String = Path.GetDirectoryName(doc.Name)
                    If Directory.Exists(docDir) Then
                        txtFolder.Text = docDir
                    End If
                End If
            Catch
                ' 忽略错误，使用默认路径
            End Try
        End If
    End Sub

    Private Sub InitializeComponent()
        lblFolder = New Label()
        txtFolder = New TextBox()
        btnBrowse = New Button()
        chkOpenDocuments = New CheckBox()
        chkUseSingleDocStyle = New CheckBox()
        lstFiles = New ListBox()
        lstSelectedFiles = New ListBox()
        btnAdd = New Button()
        btnRemove = New Button()
        btnMoveUp = New Button()
        btnMoveDown = New Button()
        btnOK = New Button()
        btnCancel = New Button()
        SuspendLayout()
        '
        'lblFolder
        '
        lblFolder.Location = New Drawing.Point(12, 15)
        lblFolder.Name = "lblFolder"
        lblFolder.Size = New Drawing.Size(50, 20)
        lblFolder.TabIndex = 0
        lblFolder.Text = "文件夹:"
        '
        'txtFolder
        '
        txtFolder.Location = New Drawing.Point(65, 12)
        txtFolder.Name = "txtFolder"
        txtFolder.Size = New Drawing.Size(480, 21)
        txtFolder.TabIndex = 1
        '
        'btnBrowse
        '
        btnBrowse.Location = New Drawing.Point(555, 10)
        btnBrowse.Name = "btnBrowse"
        btnBrowse.Size = New Drawing.Size(60, 25)
        btnBrowse.TabIndex = 2
        btnBrowse.Text = "浏览..."
        '
        'chkOpenDocuments
        '
        chkOpenDocuments.Location = New Drawing.Point(12, 45)
        chkOpenDocuments.Name = "chkOpenDocuments"
        chkOpenDocuments.Size = New Drawing.Size(100, 20)
        chkOpenDocuments.TabIndex = 3
        chkOpenDocuments.Text = "打开的文档"
        '
        'chkUseSingleDocStyle
        '
        chkUseSingleDocStyle.Location = New Drawing.Point(400, 45)
        chkUseSingleDocStyle.Name = "chkUseSingleDocStyle"
        chkUseSingleDocStyle.Size = New Drawing.Size(180, 20)
        chkUseSingleDocStyle.TabIndex = 4
        chkUseSingleDocStyle.Text = "使用单文档内部打印样式"
        '
        'lstFiles
        '
        lstFiles.ItemHeight = 12
        lstFiles.Location = New Drawing.Point(12, 75)
        lstFiles.Name = "lstFiles"
        lstFiles.SelectionMode = SelectionMode.MultiExtended
        lstFiles.Size = New Drawing.Size(280, 316)
        lstFiles.TabIndex = 5
        '
        'lstSelectedFiles
        '
        lstSelectedFiles.ItemHeight = 12
        lstSelectedFiles.Location = New Drawing.Point(340, 75)
        lstSelectedFiles.Name = "lstSelectedFiles"
        lstSelectedFiles.SelectionMode = SelectionMode.MultiExtended
        lstSelectedFiles.Size = New Drawing.Size(280, 316)
        lstSelectedFiles.TabIndex = 6
        '
        'btnAdd
        '
        btnAdd.Location = New Drawing.Point(20, 405)
        btnAdd.Name = "btnAdd"
        btnAdd.Size = New Drawing.Size(120, 30)
        btnAdd.TabIndex = 7
        btnAdd.Text = "添加文件"
        '
        'btnMoveUp
        '
        btnMoveUp.Location = New Drawing.Point(160, 405)
        btnMoveUp.Name = "btnMoveUp"
        btnMoveUp.Size = New Drawing.Size(120, 30)
        btnMoveUp.TabIndex = 11
        btnMoveUp.Text = "上移"
        '
        'btnMoveDown
        '
        btnMoveDown.Location = New Drawing.Point(290, 405)
        btnMoveDown.Name = "btnMoveDown"
        btnMoveDown.Size = New Drawing.Size(120, 30)
        btnMoveDown.TabIndex = 12
        btnMoveDown.Text = "下移"
        '
        'btnRemove
        '
        btnRemove.Location = New Drawing.Point(420, 405)
        btnRemove.Name = "btnRemove"
        btnRemove.Size = New Drawing.Size(120, 30)
        btnRemove.TabIndex = 8
        btnRemove.Text = "移除文件"
        '
        'btnOK
        '
        btnOK.DialogResult = DialogResult.OK
        btnOK.Location = New Drawing.Point(200, 445)
        btnOK.Name = "btnOK"
        btnOK.Size = New Drawing.Size(100, 30)
        btnOK.TabIndex = 9
        btnOK.Text = "确定"
        '
        'btnCancel
        '
        btnCancel.DialogResult = DialogResult.Cancel
        btnCancel.Location = New Drawing.Point(350, 445)
        btnCancel.Name = "btnCancel"
        btnCancel.Size = New Drawing.Size(100, 30)
        btnCancel.TabIndex = 10
        btnCancel.Text = "取消"
        '
        'MultiDocForm
        '
        AutoScaleDimensions = New Drawing.SizeF(6.0F, 12.0F)
        AutoScaleMode = AutoScaleMode.Font
        AcceptButton = btnOK
        CancelButton = btnCancel
        ClientSize = New Drawing.Size(634, 481)
        Controls.Add(lblFolder)
        Controls.Add(txtFolder)
        Controls.Add(btnBrowse)
        Controls.Add(chkOpenDocuments)
        Controls.Add(chkUseSingleDocStyle)
        Controls.Add(lstFiles)
        Controls.Add(lstSelectedFiles)
        Controls.Add(btnAdd)
        Controls.Add(btnRemove)
        Controls.Add(btnMoveUp)
        Controls.Add(btnMoveDown)
        Controls.Add(btnOK)
        Controls.Add(btnCancel)
        FormBorderStyle = FormBorderStyle.FixedDialog
        MaximizeBox = False
        MinimizeBox = False
        Name = "MultiDocForm"
        StartPosition = FormStartPosition.CenterScreen
        Text = "选择文件"
        TopMost = True
        ResumeLayout(False)
        PerformLayout()

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
                ' 加载DWG文件，按自然排序（图2 < 图10），避免数字图号乱序
                Dim dwgFiles As String() = Directory.GetFiles(folderPath, "*.dwg")
                Array.Sort(dwgFiles, Function(a, b) NaturalCompare(Path.GetFileName(a), Path.GetFileName(b)))
                For Each file As String In dwgFiles
                    _files.Add(file)
                    lstFiles.Items.Add(Path.GetFileName(file))
                Next
            End If
        Catch ex As Exception
            MessageBox.Show("加载文件列表失败: " & ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    ' 自然排序比较：连续数字段按数值比较（图2 < 图10），其余字符按不区分大小写比较
    Private Shared Function NaturalCompare(x As String, y As String) As Integer
        If x Is Nothing Then x = String.Empty
        If y Is Nothing Then y = String.Empty
        Dim ix As Integer = 0
        Dim iy As Integer = 0
        While ix < x.Length AndAlso iy < y.Length
            If Char.IsDigit(x(ix)) AndAlso Char.IsDigit(y(iy)) Then
                Dim sx As Integer = ix
                Dim sy As Integer = iy
                While ix < x.Length AndAlso Char.IsDigit(x(ix))
                    ix += 1
                End While
                While iy < y.Length AndAlso Char.IsDigit(y(iy))
                    iy += 1
                End While
                Dim nx As String = x.Substring(sx, ix - sx).TrimStart("0"c)
                Dim ny As String = y.Substring(sy, iy - sy).TrimStart("0"c)
                If nx.Length <> ny.Length Then
                    Return nx.Length.CompareTo(ny.Length)
                End If
                Dim numeric As Integer = String.Compare(nx, ny, StringComparison.Ordinal)
                If numeric <> 0 Then
                    Return numeric
                End If
            Else
                Dim cx As Char = Char.ToUpperInvariant(x(ix))
                Dim cy As Char = Char.ToUpperInvariant(y(iy))
                If cx <> cy Then
                    Return cx.CompareTo(cy)
                End If
                ix += 1
                iy += 1
            End If
        End While
        Return x.Length.CompareTo(y.Length)
    End Function

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

    ' 支持直接输入文件夹路径并按回车加载
    Private Sub TxtFolder_KeyDown(sender As Object, e As KeyEventArgs) Handles txtFolder.KeyDown
        If e.KeyCode = Keys.Enter Then
            e.Handled = True
            e.SuppressKeyPress = True
            Dim folderPath As String = txtFolder.Text.Trim()
            If Directory.Exists(folderPath) Then
                LoadFilesFromFolder(folderPath)
            Else
                MessageBox.Show("文件夹不存在: " & folderPath, "错误", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            End If
        End If
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

    ' 上移右侧已选列表中的选中文件
    Private Sub BtnMoveUp_Click(sender As Object, e As EventArgs) Handles btnMoveUp.Click
        MoveSelectedFiles(-1)
    End Sub

    ' 下移右侧已选列表中的选中文件
    Private Sub BtnMoveDown_Click(sender As Object, e As EventArgs) Handles btnMoveDown.Click
        MoveSelectedFiles(1)
    End Sub

    ' 上移/下移右侧已选列表中的选中文件（delta=-1 上移，1 下移），打印顺序即列表顺序
    Private Sub MoveSelectedFiles(delta As Integer)
        Dim indices As New List(Of Integer)()
        For Each idx As Integer In lstSelectedFiles.SelectedIndices
            indices.Add(idx)
        Next
        If indices.Count = 0 Then Return

        If delta < 0 Then
            indices.Sort()
            For Each idx As Integer In indices
                If idx > 0 Then SwapSelectedFiles(idx, idx - 1)
            Next
        Else
            indices.Sort()
            indices.Reverse()
            For Each idx As Integer In indices
                If idx < lstSelectedFiles.Items.Count - 1 Then SwapSelectedFiles(idx, idx + 1)
            Next
        End If
    End Sub

    ' 交换右侧列表两个位置的数据与显示，并保持选中状态跟随移动
    Private Sub SwapSelectedFiles(a As Integer, b As Integer)
        Dim tmpPath As String = _selectedFiles(a)
        _selectedFiles(a) = _selectedFiles(b)
        _selectedFiles(b) = tmpPath
        Dim tmpName As Object = lstSelectedFiles.Items(a)
        lstSelectedFiles.Items(a) = lstSelectedFiles.Items(b)
        lstSelectedFiles.Items(b) = tmpName
        Dim aSelected As Boolean = lstSelectedFiles.GetSelected(a)
        Dim bSelected As Boolean = lstSelectedFiles.GetSelected(b)
        lstSelectedFiles.SetSelected(a, bSelected)
        lstSelectedFiles.SetSelected(b, aSelected)
    End Sub

    Private Sub ChkOpenDocuments_CheckedChanged(sender As Object, e As EventArgs) Handles chkOpenDocuments.CheckedChanged
        If chkOpenDocuments.Checked Then
            ' 将打开的文档自动添加到右侧已选列表（如果还没有选择）
            AddOpenDocumentsToSelected()
        Else
            ' 取消勾选：反向移除右侧列表中所有当前打开的文档
            RemoveOpenDocumentsFromSelected()
        End If
    End Sub

    ' 从右侧已选列表移除所有当前打开的文档（与勾选时的自动追加相对应）
    Private Sub RemoveOpenDocumentsFromSelected()
        Try
            For i As Integer = _selectedFiles.Count - 1 To 0 Step -1
                Dim isOpen As Boolean = False
                For Each doc As Autodesk.AutoCAD.ApplicationServices.Document In
                    Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager
                    If String.Equals(doc.Name, _selectedFiles(i), StringComparison.OrdinalIgnoreCase) Then
                        isOpen = True
                        Exit For
                    End If
                Next
                If isOpen Then
                    _selectedFiles.RemoveAt(i)
                    lstSelectedFiles.Items.RemoveAt(i)
                End If
            Next
        Catch ex As Exception
            Debug.WriteLine("移除打开的文档失败: " & ex.Message)
        End Try
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
