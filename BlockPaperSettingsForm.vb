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
    Private WithEvents btnSave As Button
    Private btnCancel As Button

    ' 数据存储
    Private _settings As List(Of BlockPaperSetting)
    Private _plotEngine As BatchPlotEngine
    ' 保存的列表集合：列表名称 -> 该列表下的所有设置
    Private _savedLists As Dictionary(Of String, List(Of BlockPaperSetting))
    ' 当前加载到编辑区的列表名（切换前用于对比未保存修改）
    Private _currentListName As String

    Public Class BlockPaperSetting
        Public Property BlockName As String
        Public Property PlotDevice As String
        Public Property PaperSize As String
    End Class

    Public Sub New(plotEngine As BatchPlotEngine)
        InitializeComponent()
        _plotEngine = plotEngine
        _settings = New List(Of BlockPaperSetting)()
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
        AutoScaleDimensions = New Drawing.SizeF(6.0F, 12.0F)
        AutoScaleMode = AutoScaleMode.Font
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
            ' 从共享存储加载，再填充下拉框
            _savedLists = BlockPaperStore.LoadAllLists()
            cmbBlockList.Items.Clear()
            For Each listName As String In _savedLists.Keys
                cmbBlockList.Items.Add(listName)
            Next
        Catch ex As Exception
            Debug.WriteLine("加载已保存列表失败: " & ex.Message)
            _savedLists = New Dictionary(Of String, List(Of BlockPaperSetting))(StringComparer.OrdinalIgnoreCase)
        End Try
    End Sub

    ' 保存所有列表到配置文件
    Private Sub SaveAllLists()
        Try
            BlockPaperStore.SaveAllLists(_savedLists)
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
            ' 选回当前列表（如用户取消切换后的回退）无需再检查
            If String.Equals(listName, _currentListName, StringComparison.OrdinalIgnoreCase) Then Return

            ' 切换前若有未保存修改，提示确认
            If HasUnsavedChanges() Then
                If MessageBox.Show("当前列表有未保存的修改，切换将丢失，是否继续？", "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question) = DialogResult.No Then
                    ' 选回原列表（再次触发的本事件会因列表名相同直接返回）
                    Dim idx As Integer = cmbBlockList.FindStringExact(_currentListName)
                    If idx >= 0 Then
                        cmbBlockList.SelectedIndex = idx
                    Else
                        cmbBlockList.Text = _currentListName
                    End If
                    Return
                End If
            End If

            If _savedLists.ContainsKey(listName) Then
                _settings = New List(Of BlockPaperSetting)(_savedLists(listName))
                RefreshListView()
            End If
            _currentListName = listName
        Catch ex As Exception
            Debug.WriteLine("加载列表内容失败: " & ex.Message)
        End Try
    End Sub

    ' 判断当前编辑内容与已保存列表是否有差异（数量不同或任一条目不同）
    Private Function HasUnsavedChanges() As Boolean
        Dim saved As List(Of BlockPaperSetting) = Nothing
        If Not String.IsNullOrEmpty(_currentListName) AndAlso _savedLists.TryGetValue(_currentListName, saved) Then
            If _settings.Count <> saved.Count Then Return True
            For i = 0 To _settings.Count - 1
                If _settings(i).PlotDevice <> saved(i).PlotDevice OrElse
                   _settings(i).PaperSize <> saved(i).PaperSize OrElse
                   _settings(i).BlockName <> saved(i).BlockName Then
                    Return True
                End If
            Next
            Return False
        End If
        ' 无对应已保存列表时：只要有编辑内容即视为有修改
        Return _settings.Count > 0
    End Function

    ' 刷新列表视图
    Private Sub RefreshListView()
        lstSettings.Items.Clear()
        For Each setting In _settings
            Dim listItem As New ListViewItem(setting.PlotDevice)
            listItem.SubItems.Add(setting.PaperSize)
            lstSettings.Items.Add(listItem)
        Next
    End Sub

    ' 底部"保存"(OK)按钮：把当前列表合并回全部列表并持久化到配置文件
    Private Sub BtnSave_Click(sender As Object, e As EventArgs) Handles btnSave.Click
        Try
            Dim listName As String = cmbBlockList.Text.Trim()
            ' 有编辑内容但未指定列表名：提示并留在窗体，避免静默丢弃编辑
            If String.IsNullOrEmpty(listName) AndAlso _settings.Count > 0 Then
                MessageBox.Show("请先输入或选择列表名", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                DialogResult = DialogResult.None
                Return
            End If

            If Not String.IsNullOrEmpty(listName) Then
                If _settings.Count > 0 Then
                    _savedLists(listName) = New List(Of BlockPaperSetting)(_settings)
                    If Not cmbBlockList.Items.Contains(listName) Then
                        cmbBlockList.Items.Add(listName)
                    End If
                    _currentListName = listName
                ElseIf _savedLists.ContainsKey(listName) Then
                    ' 当前列表已清空则同步删除
                    _savedLists.Remove(listName)
                    cmbBlockList.Items.Remove(listName)
                    _currentListName = ""
                End If
            End If
            SaveAllLists()
        Catch ex As Exception
            Debug.WriteLine("保存块-纸张设置失败: " & ex.Message)
        End Try
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
            _currentListName = listName
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
                        _currentListName = ""
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
End Class

' 块-纸张映射持久化存储：设置窗体与打印引擎共用的唯一读写入口
' 配置格式：节 [列表名]（列表名=图块名），行 BlockName|PlotDevice|PaperSize
Public Module BlockPaperStore

    ' 配置文件路径：%AppData%\BatchPlotVB\BlockPaperLists.config
    Public ReadOnly ConfigFilePath As String = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BatchPlotVB", "BlockPaperLists.config")

    ' 从配置文件加载所有映射列表
    Public Function LoadAllLists() As Dictionary(Of String, List(Of BlockPaperSettingsForm.BlockPaperSetting))
        ' 列表名忽略大小写，避免同名不同大小写的列表被当成两个
        Dim lists As New Dictionary(Of String, List(Of BlockPaperSettingsForm.BlockPaperSetting))(StringComparer.OrdinalIgnoreCase)
        Try
            If File.Exists(ConfigFilePath) Then
                Dim lines() As String = File.ReadAllLines(ConfigFilePath)
                Dim currentList As List(Of BlockPaperSettingsForm.BlockPaperSetting) = Nothing

                For Each line As String In lines
                    Dim trimmedLine As String = line.Trim()
                    If String.IsNullOrEmpty(trimmedLine) Then Continue For

                    If trimmedLine.StartsWith("[") AndAlso trimmedLine.EndsWith("]") Then
                        ' 新列表名称（列表名=图块名）
                        currentList = New List(Of BlockPaperSettingsForm.BlockPaperSetting)()
                        lists(trimmedLine.Substring(1, trimmedLine.Length - 2)) = currentList
                    ElseIf currentList IsNot Nothing Then
                        ' 列表项：BlockName|PlotDevice|PaperSize
                        Dim parts() As String = trimmedLine.Split("|"c)
                        If parts.Length >= 3 Then
                            currentList.Add(New BlockPaperSettingsForm.BlockPaperSetting() With {
                                .BlockName = parts(0),
                                .PlotDevice = parts(1),
                                .PaperSize = parts(2)
                            })
                        End If
                    End If
                Next
            End If
        Catch ex As Exception
            Debug.WriteLine("加载块-纸张映射失败: " & ex.Message)
        End Try
        Return lists
    End Function

    ' 保存所有映射列表到配置文件（原子写，避免写一半失败把配置写坏）
    Public Sub SaveAllLists(lists As Dictionary(Of String, List(Of BlockPaperSettingsForm.BlockPaperSetting)))
        Try
            Dim dir As String = Path.GetDirectoryName(ConfigFilePath)
            If Not Directory.Exists(dir) Then
                Directory.CreateDirectory(dir)
            End If

            Dim sb As New System.Text.StringBuilder()
            For Each kvp In lists
                If kvp.Value.Count = 0 Then Continue For
                sb.AppendLine("[" & kvp.Key & "]")
                For Each setting In kvp.Value
                    sb.AppendLine(setting.BlockName & "|" & setting.PlotDevice & "|" & setting.PaperSize)
                Next
                sb.AppendLine()
            Next

            WriteTextAtomic(ConfigFilePath, sb.ToString())
        Catch ex As Exception
            Debug.WriteLine("保存块-纸张映射失败: " & ex.Message)
        End Try
    End Sub

    ' 原子写入：先写同目录随机临时文件，再 File.Replace/Move 换入；
    ' 目标被占用时退避重试，仍失败则兜底覆盖复制，避免写坏配置
    Private ReadOnly s_retryDelaysMs As Integer() = {50, 100, 200}

    Private Sub WriteTextAtomic(filePath As String, contents As String)
        Dim directory As String = Path.GetDirectoryName(filePath)
        Dim tempPath As String = Path.Combine(directory, Path.GetRandomFileName())
        Try
            File.WriteAllText(tempPath, contents)
            For attempt = 0 To s_retryDelaysMs.Length
                If attempt > 0 Then System.Threading.Thread.Sleep(s_retryDelaysMs(attempt - 1))
                Try
                    If File.Exists(filePath) Then
                        File.Replace(tempPath, filePath, filePath & ".bak", True)
                    Else
                        File.Move(tempPath, filePath)
                    End If
                    Return
                Catch
                End Try
            Next
            ' 重试窗口内始终无法换入：直接覆盖复制（失败时异常交给调用方）
            File.Copy(tempPath, filePath, True)
        Finally
            Try
                If File.Exists(tempPath) Then File.Delete(tempPath)
            Catch
            End Try
        End Try
    End Sub

    ' 在已加载的映射中查询：块名=列表名，列表内按打印设备匹配
    ' 命中返回映射项（含设备与纸张），未命中返回 Nothing
    Public Function ResolveForBlock(lists As Dictionary(Of String, List(Of BlockPaperSettingsForm.BlockPaperSetting)),
                                    blockName As String, plotDevice As String) As BlockPaperSettingsForm.BlockPaperSetting
        If lists Is Nothing OrElse lists.Count = 0 Then Return Nothing
        If String.IsNullOrEmpty(blockName) OrElse String.IsNullOrEmpty(plotDevice) Then Return Nothing

        Dim list As List(Of BlockPaperSettingsForm.BlockPaperSetting) = Nothing
        If lists.TryGetValue(blockName, list) AndAlso list IsNot Nothing Then
            For Each setting In list
                ' 打印设备名忽略大小写比较（同一设备不同大小写写法应视为相同）
                If String.Equals(setting.PlotDevice, plotDevice, StringComparison.OrdinalIgnoreCase) Then
                    Return setting
                End If
            Next
        End If
        Return Nothing
    End Function

End Module
