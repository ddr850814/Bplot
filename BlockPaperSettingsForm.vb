Imports System.IO
Imports System.Windows.Forms

Public Class BlockPaperSettingsForm
    Inherits Form

    ' 控件声明
    Private lblBlockList As Label
    Private WithEvents cmbBlockList As ComboBox
    Private WithEvents btnSaveBlockList As Button
    Private lblPlotDevice As Label
    Private WithEvents cmbPlotDevice As ComboBox
    Private lblPaperSize As Label
    Private cmbPaperSize As ComboBox
    Private WithEvents btnAdd As Button
    Private WithEvents btnModify As Button
    Private WithEvents btnRemove As Button
    Private WithEvents lstSettings As ListView
    Private btnSave As Button
    Private btnCancel As Button

    ' 数据存储
    Private _settings As List(Of BlockPaperSetting)
    Private _plotEngine As BatchPlotEngine
    ' 保存的列表集合：列表名称 -> 该列表下的所有设置
    Private _savedLists As Dictionary(Of String, List(Of BlockPaperSetting))
    ' 配置文件路径
    Private ReadOnly _configFilePath As String

    ' 静态配置文件路径（用于静态方法访问）
    Private Shared ReadOnly _staticConfigFilePath As String = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BatchPlotVB", "BlockPaperLists.config")

    Public Class BlockPaperSetting
        Public Property BlockName As String
        Public Property PlotDevice As String
        Public Property PaperSize As String
    End Class

    ''' <summary>
    ''' 根据图块名和打印设备查找对应的纸张尺寸
    ''' </summary>
    ''' <param name="blockName">图块名</param>
    ''' <param name="plotDevice">打印设备名</param>
    ''' <returns>纸张尺寸，如果未找到则返回空字符串</returns>
    Public Shared Function GetPaperSizeForBlock(blockName As String, plotDevice As String) As String
        Try
            If String.IsNullOrEmpty(blockName) OrElse String.IsNullOrEmpty(plotDevice) Then
                Return String.Empty
            End If

            ' 从配置文件加载所有列表
            Dim savedLists As New Dictionary(Of String, List(Of BlockPaperSetting))()

            If File.Exists(_staticConfigFilePath) Then
                Dim lines() As String = File.ReadAllLines(_staticConfigFilePath)
                Dim currentListName = ""
                Dim currentList As List(Of BlockPaperSetting) = Nothing

                For Each line As String In lines
                    Dim trimmedLine As String = line.Trim()
                    If String.IsNullOrEmpty(trimmedLine) Then Continue For

                    If trimmedLine.StartsWith("[") AndAlso trimmedLine.EndsWith("]") Then
                        ' 新列表名称
                        currentListName = trimmedLine.Substring(1, trimmedLine.Length - 2)
                        currentList = New List(Of BlockPaperSetting)()
                        savedLists(currentListName) = currentList
                    ElseIf currentList IsNot Nothing Then
                        ' 列表项：BlockName|PlotDevice|PaperSize
                        Dim parts() As String = trimmedLine.Split("|"c)
                        If parts.Length >= 3 Then
                            currentList.Add(New BlockPaperSetting() With {
                                .BlockName = parts(0),
                                .PlotDevice = parts(1),
                                .PaperSize = parts(2)
                            })
                        End If
                    End If
                Next
            End If

            ' 查找与图块名同名的列表
            If savedLists.ContainsKey(blockName) Then
                Dim list = savedLists(blockName)
                ' 在列表中查找与当前打印设备匹配的设置
                For Each setting In list
                    If setting.PlotDevice = plotDevice Then
                        Return setting.PaperSize
                    End If
                Next
            End If

            Return String.Empty
        Catch ex As Exception
            Debug.WriteLine("查找图块纸张设置失败: " & ex.Message)
            Return String.Empty
        End Try
    End Function

    Public Sub New(plotEngine As BatchPlotEngine)
        InitializeComponent()
        _plotEngine = plotEngine
        _settings = New List(Of BlockPaperSetting)()
        _configFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BatchPlotVB", "BlockPaperLists.config")
        LoadSavedLists()
        LoadDevices()
    End Sub

    Private Sub InitializeComponent()
        lblBlockList = New Label()
        cmbBlockList = New ComboBox()
        btnSaveBlockList = New Button()
        lblPlotDevice = New Label()
        cmbPlotDevice = New ComboBox()
        lblPaperSize = New Label()
        cmbPaperSize = New ComboBox()
        btnAdd = New Button()
        btnModify = New Button()
        btnRemove = New Button()
        lstSettings = New ListView()
        btnSave = New Button()
        btnCancel = New Button()
        SuspendLayout()
        '
        'lblBlockList
        '
        lblBlockList.Location = New Drawing.Point(12, 15)
        lblBlockList.Name = "lblBlockList"
        lblBlockList.Size = New Drawing.Size(60, 20)
        lblBlockList.TabIndex = 0
        lblBlockList.Text = "图块列表:"
        '
        'cmbBlockList
        '
        cmbBlockList.Location = New Drawing.Point(78, 12)
        cmbBlockList.Name = "cmbBlockList"
        cmbBlockList.Size = New Drawing.Size(170, 20)
        cmbBlockList.TabIndex = 1
        '
        'btnSaveBlockList
        '
        btnSaveBlockList.Location = New Drawing.Point(263, 11)
        btnSaveBlockList.Name = "btnSaveBlockList"
        btnSaveBlockList.Size = New Drawing.Size(75, 23)
        btnSaveBlockList.TabIndex = 2
        btnSaveBlockList.Text = "保存"
        '
        'lblPlotDevice
        '
        lblPlotDevice.Location = New Drawing.Point(12, 46)
        lblPlotDevice.Name = "lblPlotDevice"
        lblPlotDevice.Size = New Drawing.Size(60, 20)
        lblPlotDevice.TabIndex = 3
        lblPlotDevice.Text = "打印设备"
        '
        'cmbPlotDevice
        '
        cmbPlotDevice.DropDownStyle = ComboBoxStyle.DropDownList
        cmbPlotDevice.Location = New Drawing.Point(12, 68)
        cmbPlotDevice.Name = "cmbPlotDevice"
        cmbPlotDevice.Size = New Drawing.Size(166, 20)
        cmbPlotDevice.TabIndex = 4
        '
        'lblPaperSize
        '
        lblPaperSize.Location = New Drawing.Point(187, 46)
        lblPaperSize.Name = "lblPaperSize"
        lblPaperSize.Size = New Drawing.Size(60, 20)
        lblPaperSize.TabIndex = 5
        lblPaperSize.Text = "打印纸张"
        '
        'cmbPaperSize
        '
        cmbPaperSize.DropDownStyle = ComboBoxStyle.DropDownList
        cmbPaperSize.Location = New Drawing.Point(187, 68)
        cmbPaperSize.Name = "cmbPaperSize"
        cmbPaperSize.Size = New Drawing.Size(166, 20)
        cmbPaperSize.TabIndex = 6
        '
        'btnAdd
        '
        btnAdd.Location = New Drawing.Point(36, 98)
        btnAdd.Name = "btnAdd"
        btnAdd.Size = New Drawing.Size(88, 25)
        btnAdd.TabIndex = 7
        btnAdd.Text = "添加"
        '
        'btnModify
        '
        btnModify.Location = New Drawing.Point(136, 98)
        btnModify.Name = "btnModify"
        btnModify.Size = New Drawing.Size(88, 25)
        btnModify.TabIndex = 8
        btnModify.Text = "修改"
        '
        'btnRemove
        '
        btnRemove.Location = New Drawing.Point(236, 98)
        btnRemove.Name = "btnRemove"
        btnRemove.Size = New Drawing.Size(88, 25)
        btnRemove.TabIndex = 9
        btnRemove.Text = "移除"
        '
        'lstSettings
        '
        lstSettings.FullRowSelect = True
        lstSettings.GridLines = True
        lstSettings.HideSelection = False
        lstSettings.Location = New Drawing.Point(12, 131)
        lstSettings.Name = "lstSettings"
        lstSettings.Size = New Drawing.Size(341, 231)
        lstSettings.TabIndex = 11
        lstSettings.UseCompatibleStateImageBehavior = False
        lstSettings.View = View.Details
        ' Details 视图必须有列才能渲染行；无列时整列表不可见且无法选中
        lstSettings.Columns.Add("打印设备", 190)
        lstSettings.Columns.Add("纸张", 130)
        '
        'btnSave
        '
        btnSave.DialogResult = DialogResult.OK
        btnSave.Location = New Drawing.Point(81, 371)
        btnSave.Name = "btnSave"
        btnSave.Size = New Drawing.Size(85, 28)
        btnSave.TabIndex = 12
        btnSave.Text = "保存"
        '
        'btnCancel
        '
        btnCancel.DialogResult = DialogResult.Cancel
        btnCancel.Location = New Drawing.Point(191, 371)
        btnCancel.Name = "btnCancel"
        btnCancel.Size = New Drawing.Size(85, 28)
        btnCancel.TabIndex = 13
        btnCancel.Text = "取消"
        '
        'BlockPaperSettingsForm
        '
        AcceptButton = btnSave
        CancelButton = btnCancel
        ClientSize = New Drawing.Size(363, 411)
        Controls.Add(lblBlockList)
        Controls.Add(cmbBlockList)
        Controls.Add(btnSaveBlockList)
        Controls.Add(lblPlotDevice)
        Controls.Add(cmbPlotDevice)
        Controls.Add(lblPaperSize)
        Controls.Add(cmbPaperSize)
        Controls.Add(btnAdd)
        Controls.Add(btnModify)
        Controls.Add(btnRemove)
        Controls.Add(lstSettings)
        Controls.Add(btnSave)
        Controls.Add(btnCancel)
        FormBorderStyle = FormBorderStyle.FixedDialog
        MaximizeBox = False
        MinimizeBox = False
        Name = "BlockPaperSettingsForm"
        StartPosition = FormStartPosition.CenterParent
        Text = "图块对应纸张设置"
        TopMost = True
        ResumeLayout(False)

    End Sub

    ' 加载已保存的列表
    Private Sub LoadSavedLists()
        Try
            _savedLists = New Dictionary(Of String, List(Of BlockPaperSetting))()
            cmbBlockList.Items.Clear()

            If File.Exists(_configFilePath) Then
                Dim lines() As String = File.ReadAllLines(_configFilePath)
                Dim currentListName = ""
                Dim currentList As List(Of BlockPaperSetting) = Nothing

                For Each line As String In lines
                    Dim trimmedLine As String = line.Trim()
                    If String.IsNullOrEmpty(trimmedLine) Then Continue For

                    If trimmedLine.StartsWith("[") AndAlso trimmedLine.EndsWith("]") Then
                        ' 新列表名称
                        currentListName = trimmedLine.Substring(1, trimmedLine.Length - 2)
                        currentList = New List(Of BlockPaperSetting)()
                        _savedLists(currentListName) = currentList
                        cmbBlockList.Items.Add(currentListName)
                    ElseIf currentList IsNot Nothing Then
                        ' 列表项：BlockName|PlotDevice|PaperSize
                        Dim parts() As String = trimmedLine.Split("|"c)
                        If parts.Length >= 3 Then
                            currentList.Add(New BlockPaperSetting() With {
                                .BlockName = parts(0),
                                .PlotDevice = parts(1),
                                .PaperSize = parts(2)
                            })
                        End If
                    End If
                Next
            End If
        Catch ex As Exception
            Debug.WriteLine("加载已保存列表失败: " & ex.Message)
            _savedLists = New Dictionary(Of String, List(Of BlockPaperSetting))()
        End Try
    End Sub

    ' 保存所有列表到配置文件
    Private Sub SaveAllLists()
        Try
            Dim dir As String = Path.GetDirectoryName(_configFilePath)
            If Not Directory.Exists(dir) Then
                Directory.CreateDirectory(dir)
            End If

            Using writer As New StreamWriter(_configFilePath, False)
                For Each kvp In _savedLists
                    If kvp.Value.Count = 0 Then Continue For
                    writer.WriteLine("[" & kvp.Key & "]")
                    For Each setting In kvp.Value
                        writer.WriteLine(setting.BlockName & "|" & setting.PlotDevice & "|" & setting.PaperSize)
                    Next
                    writer.WriteLine()
                Next
            End Using
        Catch ex As Exception
            Debug.WriteLine("保存列表失败: " & ex.Message)
        End Try
    End Sub

    ' 加载打印设备列表
    Private Sub LoadDevices()
        Try
            If _plotEngine IsNot Nothing Then
                Dim plotters As List(Of String) = _plotEngine.GetPlotterList()
                cmbPlotDevice.Items.Clear()
                For Each plotter As String In plotters
                    cmbPlotDevice.Items.Add(plotter)
                Next
            End If

            If cmbPlotDevice.Items.Count = 0 Then
                cmbPlotDevice.Items.Add("无")
            End If

            cmbPlotDevice.SelectedIndex = 0
            LoadPaperList()
        Catch
            ' 加载设备失败
        End Try
    End Sub

    ' 根据选中的打印设备加载纸张列表
    Private Sub LoadPaperList()
        Try
            Dim selectedPlotter = ""
            If cmbPlotDevice.SelectedItem IsNot Nothing Then
                selectedPlotter = cmbPlotDevice.SelectedItem.ToString()
            End If

            cmbPaperSize.Items.Clear()

            If _plotEngine IsNot Nothing AndAlso Not String.IsNullOrEmpty(selectedPlotter) AndAlso selectedPlotter <> "无" Then
                Dim papers As List(Of String) = _plotEngine.GetPaperList(selectedPlotter)
                For Each paper As String In papers
                    cmbPaperSize.Items.Add(paper)
                Next
            End If

            If cmbPaperSize.Items.Count = 0 Then
                cmbPaperSize.Items.Add("无")
            End If

            cmbPaperSize.SelectedIndex = 0
        Catch
            ' 加载纸张失败
        End Try
    End Sub

    ' 打印设备选择改变时刷新纸张列表
    Private Sub CmbPlotDevice_SelectedIndexChanged(sender As Object, e As EventArgs) Handles cmbPlotDevice.SelectedIndexChanged
        LoadPaperList()
    End Sub

    ' 下拉列表选择改变时加载对应的列表内容
    Private Sub CmbBlockList_SelectedIndexChanged(sender As Object, e As EventArgs) Handles cmbBlockList.SelectedIndexChanged
        Try
            Dim listName As String = cmbBlockList.Text.Trim()
            If String.IsNullOrEmpty(listName) Then Return

            If _savedLists.ContainsKey(listName) Then
                _settings = New List(Of BlockPaperSetting)(_savedLists(listName))
                RefreshListView()
            End If
        Catch ex As Exception
            Debug.WriteLine("加载列表内容失败: " & ex.Message)
        End Try
    End Sub

    ' 刷新列表视图
    Private Sub RefreshListView()
        lstSettings.Items.Clear()
        For Each setting In _settings
            Dim listItem As New ListViewItem(setting.PlotDevice)
            listItem.SubItems.Add(setting.PaperSize)
            lstSettings.Items.Add(listItem)
        Next
    End Sub

    ' 保存列表按钮点击事件
    Private Sub BtnSaveBlockList_Click(sender As Object, e As EventArgs) Handles btnSaveBlockList.Click
        Try
            Dim listName As String = cmbBlockList.Text.Trim()
            If String.IsNullOrEmpty(listName) Then
                MessageBox.Show("请输入列表名称", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            ' 检查是否已存在同名列表
            If _savedLists.ContainsKey(listName) Then
                If MessageBox.Show($"列表 '{listName}' 已存在，是否覆盖？", "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question) = DialogResult.No Then
                    Return
                End If
                _savedLists(listName) = New List(Of BlockPaperSetting)(_settings)
            Else
                _savedLists.Add(listName, New List(Of BlockPaperSetting)(_settings))
                If Not cmbBlockList.Items.Contains(listName) Then
                    cmbBlockList.Items.Add(listName)
                End If
            End If

            SaveAllLists()
            MessageBox.Show($"列表 '{listName}' 已保存", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)
        Catch ex As Exception
            MessageBox.Show("保存列表失败: " & ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    ' 添加按钮点击事件
    Private Sub BtnAdd_Click(sender As Object, e As EventArgs) Handles btnAdd.Click
        Try
            Dim blockName As String = cmbBlockList.Text.Trim()

            Dim plotDevice As String = cmbPlotDevice.SelectedItem?.ToString()
            Dim paperSize As String = cmbPaperSize.SelectedItem?.ToString()

            ' 检查是否已存在相同的打印设备（同一个列表中不允许相同的打印设备）
            For Each item As ListViewItem In lstSettings.Items
                If item.SubItems(0).Text = plotDevice Then
                    MessageBox.Show($"打印设备 '{plotDevice}' 已存在，同一个列表中不允许相同的打印设备", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)
                    Return
                End If
            Next

            ' 添加到列表（只显示打印设备和纸张）
            Dim listItem As New ListViewItem(plotDevice)
            listItem.SubItems.Add(paperSize)
            lstSettings.Items.Add(listItem)

            ' 添加到数据集合
            _settings.Add(New BlockPaperSetting() With {
                .BlockName = blockName,
                .PlotDevice = plotDevice,
                .PaperSize = paperSize
            })
        Catch ex As Exception
            MessageBox.Show("添加设置失败: " & ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    ' 修改按钮点击事件
    Private Sub BtnModify_Click(sender As Object, e As EventArgs) Handles btnModify.Click
        Try
            If lstSettings.SelectedItems.Count = 0 Then
                MessageBox.Show("请先选择要修改的设置", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            Dim blockName As String = cmbBlockList.Text.Trim()

            Dim selectedItem As ListViewItem = lstSettings.SelectedItems(0)
            Dim oldPlotDevice As String = selectedItem.SubItems(0).Text
            Dim oldPaperSize As String = selectedItem.SubItems(1).Text

            Dim plotDevice As String = cmbPlotDevice.SelectedItem?.ToString()
            Dim paperSize As String = cmbPaperSize.SelectedItem?.ToString()

            ' 如果修改了设备，检查是否与其他设置冲突（同一个列表中不允许相同的打印设备）
            If plotDevice <> oldPlotDevice Then
                For Each item As ListViewItem In lstSettings.Items
                    If Not ReferenceEquals(item, selectedItem) AndAlso item.SubItems(0).Text = plotDevice Then
                        MessageBox.Show($"打印设备 '{plotDevice}' 已存在，同一个列表中不允许相同的打印设备", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)
                        Return
                    End If
                Next
            End If

            ' 更新列表（只显示打印设备和纸张）
            selectedItem.SubItems(0).Text = plotDevice
            selectedItem.SubItems(1).Text = paperSize

            ' 更新数据集合（按原来的设备和纸张找到对应记录）
            For Each setting In _settings
                If setting.PlotDevice = oldPlotDevice AndAlso setting.PaperSize = oldPaperSize Then
                    setting.BlockName = blockName
                    setting.PlotDevice = plotDevice
                    setting.PaperSize = paperSize
                    Exit For
                End If
            Next
        Catch ex As Exception
            MessageBox.Show("修改设置失败: " & ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    ' 移除按钮点击事件
    Private Sub BtnRemove_Click(sender As Object, e As EventArgs) Handles btnRemove.Click
        Try
            If lstSettings.SelectedItems.Count = 0 Then
                MessageBox.Show("请先选择要移除的设置", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            Dim selectedItem As ListViewItem = lstSettings.SelectedItems(0)
            Dim plotDevice As String = selectedItem.SubItems(0).Text
            Dim paperSize As String = selectedItem.SubItems(1).Text

            If MessageBox.Show($"确定要移除打印设备 '{plotDevice}' 纸张 '{paperSize}' 的设置吗？", "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question) = DialogResult.Yes Then
                ' 从列表移除
                lstSettings.Items.Remove(selectedItem)

                ' 从数据集合移除（按设备和纸张匹配）
                For i As Integer = _settings.Count - 1 To 0 Step -1
                    If _settings(i).PlotDevice = plotDevice AndAlso _settings(i).PaperSize = paperSize Then
                        _settings.RemoveAt(i)
                        Exit For
                    End If
                Next

                ' 如果列表为空，自动删除保存的列表
                If _settings.Count = 0 Then
                    Dim listName As String = cmbBlockList.Text.Trim()
                    If Not String.IsNullOrEmpty(listName) AndAlso _savedLists.ContainsKey(listName) Then
                        _savedLists.Remove(listName)
                        cmbBlockList.Items.Remove(listName)
                        cmbBlockList.Text = ""
                        SaveAllLists()
                    End If
                End If
            End If
        Catch ex As Exception
            MessageBox.Show("移除设置失败: " & ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    ' 列表选择改变事件
    Private Sub LstSettings_SelectedIndexChanged(sender As Object, e As EventArgs) Handles lstSettings.SelectedIndexChanged
        Try
            If lstSettings.SelectedItems.Count > 0 Then
                Dim selectedItem As ListViewItem = lstSettings.SelectedItems(0)

                ' 设置打印设备
                Dim plotDevice As String = selectedItem.SubItems(0).Text
                For i = 0 To cmbPlotDevice.Items.Count - 1
                    If cmbPlotDevice.Items(i).ToString() = plotDevice Then
                        cmbPlotDevice.SelectedIndex = i
                        Exit For
                    End If
                Next

                ' 设置纸张尺寸
                Dim paperSize As String = selectedItem.SubItems(1).Text
                For i = 0 To cmbPaperSize.Items.Count - 1
                    If cmbPaperSize.Items(i).ToString() = paperSize Then
                        cmbPaperSize.SelectedIndex = i
                        Exit For
                    End If
                Next
            End If
        Catch
            ' 选择改变处理失败
        End Try
    End Sub

    ' 获取设置列表
    Public Function GetSettings() As List(Of BlockPaperSetting)
        Return _settings
    End Function

    ' 设置设置列表（用于加载已保存的设置）
    Public Sub SetSettings(settings As List(Of BlockPaperSetting))
        Try
            _settings = settings
            RefreshListView()
        Catch
            ' 设置列表失败
        End Try
    End Sub
End Class
