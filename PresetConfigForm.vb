Imports System.Windows.Forms
Imports System.Drawing
Imports System.IO
Imports System.Collections.Generic

Public Class PresetConfigForm
    Inherits Form

    ' 顶部输入区域控件
    Private lblName As Label
    Private txtName As TextBox
    Private lblPlotDevice As Label
    Private WithEvents cmbPlotDevice As ComboBox
    Private lblPaperSize As Label
    Private cmbPaperSize As ComboBox
    Private lblPlotStyle As Label
    Private cmbPlotStyle As ComboBox

    ' 操作按钮
    Private WithEvents btnAdd As Button
    Private WithEvents btnModify As Button
    Private WithEvents btnRemove As Button

    ' 列表视图
    Private WithEvents lstPresets As ListView
    Private colName As ColumnHeader
    Private colPlotDevice As ColumnHeader
    Private colPaperSize As ColumnHeader
    Private colPlotStyle As ColumnHeader

    ' 底部按钮
    Private WithEvents btnSave As Button
    Private WithEvents btnCancel As Button

    ' 数据
    Private _plotEngine As BatchPlotEngine
    Private _configManager As ConfigManager
    Private _presets As List(Of PresetInfo)
    Private _isEditing As Boolean = False
    Private _originalName As String = ""

    Public Class PresetInfo
        Public Property Name As String
        Public Property PlotDevice As String
        Public Property PaperSize As String
        Public Property PlotStyle As String
        ' 扩展配置项，与主窗口兼容
        Public Property Copies As String
        Public Property Collate As String
        Public Property AutoRotate As String
        Public Property AutoCenter As String
        Public Property OffsetX As String
        Public Property OffsetY As String
        Public Property NameTemplate As String
        Public Property SavePath As String
    End Class

    Public Sub New(plotEngine As BatchPlotEngine)
        InitializeComponent()
        _plotEngine = plotEngine
        _configManager = New ConfigManager()
        _presets = New List(Of PresetInfo)()
        LoadPlotDevices()
        LoadPlotStyles()
        LoadPresets()
    End Sub

    Private Sub InitializeComponent()
        Me.lblName = New System.Windows.Forms.Label()
        Me.txtName = New System.Windows.Forms.TextBox()
        Me.lblPlotDevice = New System.Windows.Forms.Label()
        Me.cmbPlotDevice = New System.Windows.Forms.ComboBox()
        Me.lblPaperSize = New System.Windows.Forms.Label()
        Me.cmbPaperSize = New System.Windows.Forms.ComboBox()
        Me.lblPlotStyle = New System.Windows.Forms.Label()
        Me.cmbPlotStyle = New System.Windows.Forms.ComboBox()
        Me.btnAdd = New System.Windows.Forms.Button()
        Me.btnModify = New System.Windows.Forms.Button()
        Me.btnRemove = New System.Windows.Forms.Button()
        Me.lstPresets = New System.Windows.Forms.ListView()
        Me.colName = CType(New System.Windows.Forms.ColumnHeader(),System.Windows.Forms.ColumnHeader)
        Me.colPlotDevice = CType(New System.Windows.Forms.ColumnHeader(),System.Windows.Forms.ColumnHeader)
        Me.colPaperSize = CType(New System.Windows.Forms.ColumnHeader(),System.Windows.Forms.ColumnHeader)
        Me.colPlotStyle = CType(New System.Windows.Forms.ColumnHeader(),System.Windows.Forms.ColumnHeader)
        Me.btnSave = New System.Windows.Forms.Button()
        Me.btnCancel = New System.Windows.Forms.Button()
        Me.SuspendLayout
        '
        'lblName
        '
        Me.lblName.Location = New System.Drawing.Point(11, 15)
        Me.lblName.Name = "lblName"
        Me.lblName.Size = New System.Drawing.Size(40, 20)
        Me.lblName.TabIndex = 0
        Me.lblName.Text = "名称"
        '
        'txtName
        '
        Me.txtName.Location = New System.Drawing.Point(7, 35)
        Me.txtName.Name = "txtName"
        Me.txtName.Size = New System.Drawing.Size(152, 21)
        Me.txtName.TabIndex = 1
        '
        'lblPlotDevice
        '
        Me.lblPlotDevice.Location = New System.Drawing.Point(175, 15)
        Me.lblPlotDevice.Name = "lblPlotDevice"
        Me.lblPlotDevice.Size = New System.Drawing.Size(60, 20)
        Me.lblPlotDevice.TabIndex = 2
        Me.lblPlotDevice.Text = "打印设备"
        '
        'cmbPlotDevice
        '
        Me.cmbPlotDevice.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
        Me.cmbPlotDevice.Location = New System.Drawing.Point(173, 35)
        Me.cmbPlotDevice.Name = "cmbPlotDevice"
        Me.cmbPlotDevice.Size = New System.Drawing.Size(152, 20)
        Me.cmbPlotDevice.TabIndex = 3
        '
        'lblPaperSize
        '
        Me.lblPaperSize.Location = New System.Drawing.Point(341, 15)
        Me.lblPaperSize.Name = "lblPaperSize"
        Me.lblPaperSize.Size = New System.Drawing.Size(60, 20)
        Me.lblPaperSize.TabIndex = 4
        Me.lblPaperSize.Text = "打印纸张"
        '
        'cmbPaperSize
        '
        Me.cmbPaperSize.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
        Me.cmbPaperSize.Location = New System.Drawing.Point(339, 35)
        Me.cmbPaperSize.Name = "cmbPaperSize"
        Me.cmbPaperSize.Size = New System.Drawing.Size(152, 20)
        Me.cmbPaperSize.TabIndex = 5
        '
        'lblPlotStyle
        '
        Me.lblPlotStyle.Location = New System.Drawing.Point(507, 15)
        Me.lblPlotStyle.Name = "lblPlotStyle"
        Me.lblPlotStyle.Size = New System.Drawing.Size(60, 20)
        Me.lblPlotStyle.TabIndex = 6
        Me.lblPlotStyle.Text = "打印样式"
        '
        'cmbPlotStyle
        '
        Me.cmbPlotStyle.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
        Me.cmbPlotStyle.Location = New System.Drawing.Point(505, 35)
        Me.cmbPlotStyle.Name = "cmbPlotStyle"
        Me.cmbPlotStyle.Size = New System.Drawing.Size(152, 20)
        Me.cmbPlotStyle.TabIndex = 7
        '
        'btnAdd
        '
        Me.btnAdd.Location = New System.Drawing.Point(96, 65)
        Me.btnAdd.Name = "btnAdd"
        Me.btnAdd.Size = New System.Drawing.Size(141, 21)
        Me.btnAdd.TabIndex = 8
        Me.btnAdd.Text = "添加"
        '
        'btnModify
        '
        Me.btnModify.Location = New System.Drawing.Point(252, 65)
        Me.btnModify.Name = "btnModify"
        Me.btnModify.Size = New System.Drawing.Size(141, 21)
        Me.btnModify.TabIndex = 9
        Me.btnModify.Text = "修改"
        '
        'btnRemove
        '
        Me.btnRemove.Location = New System.Drawing.Point(408, 65)
        Me.btnRemove.Name = "btnRemove"
        Me.btnRemove.Size = New System.Drawing.Size(141, 21)
        Me.btnRemove.TabIndex = 10
        Me.btnRemove.Text = "移除"
        '
        'lstPresets
        '
        Me.lstPresets.Columns.AddRange(New System.Windows.Forms.ColumnHeader() {Me.colName, Me.colPlotDevice, Me.colPaperSize, Me.colPlotStyle})
        Me.lstPresets.FullRowSelect = true
        Me.lstPresets.GridLines = true
        Me.lstPresets.HideSelection = false
        Me.lstPresets.Location = New System.Drawing.Point(3, 98)
        Me.lstPresets.Name = "lstPresets"
        Me.lstPresets.Size = New System.Drawing.Size(660, 263)
        Me.lstPresets.TabIndex = 11
        Me.lstPresets.UseCompatibleStateImageBehavior = false
        Me.lstPresets.View = System.Windows.Forms.View.Details
        '
        'colName
        '
        Me.colName.Text = "名称"
        Me.colName.Width = 150
        '
        'colPlotDevice
        '
        Me.colPlotDevice.Text = "打印设备"
        Me.colPlotDevice.Width = 200
        '
        'colPaperSize
        '
        Me.colPaperSize.Text = "打印纸张"
        Me.colPaperSize.Width = 150
        '
        'colPlotStyle
        '
        Me.colPlotStyle.Text = "打印样式"
        Me.colPlotStyle.Width = 140
        '
        'btnSave
        '
        Me.btnSave.Location = New System.Drawing.Point(252, 373)
        Me.btnSave.Name = "btnSave"
        Me.btnSave.Size = New System.Drawing.Size(75, 25)
        Me.btnSave.TabIndex = 12
        Me.btnSave.Text = "保存"
        '
        'btnCancel
        '
        Me.btnCancel.Location = New System.Drawing.Point(337, 373)
        Me.btnCancel.Name = "btnCancel"
        Me.btnCancel.Size = New System.Drawing.Size(75, 25)
        Me.btnCancel.TabIndex = 13
        Me.btnCancel.Text = "取消"
        '
        'PresetConfigForm
        '
        Me.ClientSize = New System.Drawing.Size(664, 409)
        Me.Controls.Add(Me.lblName)
        Me.Controls.Add(Me.txtName)
        Me.Controls.Add(Me.lblPlotDevice)
        Me.Controls.Add(Me.cmbPlotDevice)
        Me.Controls.Add(Me.lblPaperSize)
        Me.Controls.Add(Me.cmbPaperSize)
        Me.Controls.Add(Me.lblPlotStyle)
        Me.Controls.Add(Me.cmbPlotStyle)
        Me.Controls.Add(Me.btnAdd)
        Me.Controls.Add(Me.btnModify)
        Me.Controls.Add(Me.btnRemove)
        Me.Controls.Add(Me.lstPresets)
        Me.Controls.Add(Me.btnSave)
        Me.Controls.Add(Me.btnCancel)
        Me.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog
        Me.MaximizeBox = false
        Me.MinimizeBox = false
        Me.Name = "PresetConfigForm"
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen
        Me.Text = "预设配置"
        Me.TopMost = True
        Me.ResumeLayout(False)
        Me.PerformLayout

End Sub

    Private Sub LoadPlotDevices()
        Try
            cmbPlotDevice.Items.Clear()
            Dim devices As List(Of String) = _plotEngine.GetPlotterList()
            For Each device As String In devices
                cmbPlotDevice.Items.Add(device)
            Next
            If cmbPlotDevice.Items.Count > 0 Then
                cmbPlotDevice.SelectedIndex = 0
            End If
        Catch ex As Exception
            MessageBox.Show("加载打印设备失败: " & ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub LoadPaperSizes(plotterName As String)
        Try
            cmbPaperSize.Items.Clear()
            If String.IsNullOrEmpty(plotterName) Then Return

            Dim papers As List(Of String) = _plotEngine.GetPaperList(plotterName)
            For Each paper As String In papers
                cmbPaperSize.Items.Add(paper)
            Next
            If cmbPaperSize.Items.Count > 0 Then
                cmbPaperSize.SelectedIndex = 0
            End If
        Catch ex As Exception
            MessageBox.Show("加载打印纸张失败: " & ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub LoadPlotStyles()
        Try
            cmbPlotStyle.Items.Clear()
            cmbPlotStyle.Items.Add("无")
            Dim styles As List(Of String) = _plotEngine.GetPlotStyleList()
            For Each style As String In styles
                cmbPlotStyle.Items.Add(style)
            Next
            cmbPlotStyle.SelectedIndex = 0
        Catch ex As Exception
            MessageBox.Show("加载打印样式失败: " & ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub LoadPresets()
        Try
            lstPresets.Items.Clear()
            _presets.Clear()

            Dim presetNames As List(Of String) = _configManager.GetPresetNames()
            For Each name As String In presetNames
                Dim config As Dictionary(Of String, String) = _configManager.LoadPreset(name)
                Dim preset As New PresetInfo()
                preset.Name = name
                ' 支持新旧两种键名格式
                preset.PlotDevice = GetConfigValue(config, "PlotDevice", GetConfigValue(config, "PlotterName", ""))
                preset.PaperSize = GetConfigValue(config, "PaperSize", "")
                preset.PlotStyle = GetConfigValue(config, "PlotStyle", "无")
                ' 加载扩展配置项
                preset.Copies = GetConfigValue(config, "Copies", "1")
                preset.Collate = GetConfigValue(config, "Collate", "True")
                preset.AutoRotate = GetConfigValue(config, "AutoRotate", "True")
                preset.AutoCenter = GetConfigValue(config, "AutoCenter", "True")
                preset.OffsetX = GetConfigValue(config, "OffsetX", "0")
                preset.OffsetY = GetConfigValue(config, "OffsetY", "0")
                preset.NameTemplate = GetConfigValue(config, "NameTemplate", "")
                preset.SavePath = GetConfigValue(config, "SavePath", "")
                _presets.Add(preset)

                ' 添加到列表视图
                Dim item As New ListViewItem(preset.Name)
                item.SubItems.Add(preset.PlotDevice)
                item.SubItems.Add(preset.PaperSize)
                item.SubItems.Add(preset.PlotStyle)
                item.Tag = preset
                lstPresets.Items.Add(item)
            Next
        Catch ex As Exception
            MessageBox.Show("加载预设配置失败: " & ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Function GetConfigValue(config As Dictionary(Of String, String), key As String, defaultValue As String) As String
        If config.ContainsKey(key) Then
            Return config(key)
        End If
        Return defaultValue
    End Function

    Private Sub CmbPlotDevice_SelectedIndexChanged(sender As Object, e As EventArgs) Handles cmbPlotDevice.SelectedIndexChanged
        If cmbPlotDevice.SelectedItem IsNot Nothing Then
            LoadPaperSizes(cmbPlotDevice.SelectedItem.ToString())
        End If
    End Sub

    Private Sub BtnAdd_Click(sender As Object, e As EventArgs) Handles btnAdd.Click
        Try
            Dim name As String = txtName.Text.Trim()
            If String.IsNullOrEmpty(name) Then
                MessageBox.Show("请输入预设名称", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            ' 检查是否已存在
            For Each preset As PresetInfo In _presets
                If preset.Name.Equals(name, StringComparison.OrdinalIgnoreCase) Then
                    MessageBox.Show("该预设名称已存在", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)
                    Return
                End If
            Next

            Dim newPreset As New PresetInfo()
            newPreset.Name = name
            newPreset.PlotDevice = If(cmbPlotDevice.SelectedItem IsNot Nothing, cmbPlotDevice.SelectedItem.ToString(), "")
            newPreset.PaperSize = If(cmbPaperSize.SelectedItem IsNot Nothing, cmbPaperSize.SelectedItem.ToString(), "")
            newPreset.PlotStyle = If(cmbPlotStyle.SelectedItem IsNot Nothing, cmbPlotStyle.SelectedItem.ToString(), "无")

            _presets.Add(newPreset)

            ' 添加到列表视图
            Dim item As New ListViewItem(newPreset.Name)
            item.SubItems.Add(newPreset.PlotDevice)
            item.SubItems.Add(newPreset.PaperSize)
            item.SubItems.Add(newPreset.PlotStyle)
            item.Tag = newPreset
            lstPresets.Items.Add(item)

            ' 保存到文件
            SavePresetToFile(newPreset)

            ' 清空输入
            txtName.Clear()
            MessageBox.Show("预设添加成功", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)
        Catch ex As Exception
            MessageBox.Show("添加预设失败: " & ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub BtnModify_Click(sender As Object, e As EventArgs) Handles btnModify.Click
        Try
            If lstPresets.SelectedItems.Count = 0 Then
                MessageBox.Show("请选择要修改的预设", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            Dim item As ListViewItem = lstPresets.SelectedItems(0)
            Dim preset As PresetInfo = CType(item.Tag, PresetInfo)

            If Not _isEditing Then
                ' 进入编辑模式
                _isEditing = True
                _originalName = preset.Name
                btnModify.Text = "确认"
                btnAdd.Enabled = False
                btnRemove.Enabled = False

                ' 填充输入框
                txtName.Text = preset.Name
                SelectComboBoxItem(cmbPlotDevice, preset.PlotDevice)
                SelectComboBoxItem(cmbPlotStyle, preset.PlotStyle)
                ' 纸张需要在设备选择后加载
                LoadPaperSizes(preset.PlotDevice)
                SelectComboBoxItem(cmbPaperSize, preset.PaperSize)
            Else
                ' 确认修改
                Dim newName As String = txtName.Text.Trim()
                If String.IsNullOrEmpty(newName) Then
                    MessageBox.Show("请输入预设名称", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)
                    Return
                End If

                ' 检查名称是否冲突（排除自己）
                If Not newName.Equals(_originalName, StringComparison.OrdinalIgnoreCase) Then
                    For Each p As PresetInfo In _presets
                        If p.Name.Equals(newName, StringComparison.OrdinalIgnoreCase) Then
                            MessageBox.Show("该预设名称已存在", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)
                            Return
                        End If
                    Next
                End If

                ' 如果名称改变，删除旧文件
                If Not newName.Equals(_originalName, StringComparison.OrdinalIgnoreCase) Then
                    _configManager.DeletePreset(_originalName)
                End If

                ' 更新预设信息
                preset.Name = newName
                preset.PlotDevice = If(cmbPlotDevice.SelectedItem IsNot Nothing, cmbPlotDevice.SelectedItem.ToString(), "")
                preset.PaperSize = If(cmbPaperSize.SelectedItem IsNot Nothing, cmbPaperSize.SelectedItem.ToString(), "")
                preset.PlotStyle = If(cmbPlotStyle.SelectedItem IsNot Nothing, cmbPlotStyle.SelectedItem.ToString(), "无")

                ' 更新列表视图
                item.Text = preset.Name
                item.SubItems(1).Text = preset.PlotDevice
                item.SubItems(2).Text = preset.PaperSize
                item.SubItems(3).Text = preset.PlotStyle

                ' 保存到文件
                SavePresetToFile(preset)

                ' 退出编辑模式
                _isEditing = False
                _originalName = ""
                btnModify.Text = "修改"
                btnAdd.Enabled = True
                btnRemove.Enabled = True
                txtName.Clear()

                MessageBox.Show("预设修改成功", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)
            End If
        Catch ex As Exception
            MessageBox.Show("修改预设失败: " & ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub BtnRemove_Click(sender As Object, e As EventArgs) Handles btnRemove.Click
        Try
            If lstPresets.SelectedItems.Count = 0 Then
                MessageBox.Show("请选择要移除的预设", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            Dim item As ListViewItem = lstPresets.SelectedItems(0)
            Dim preset As PresetInfo = CType(item.Tag, PresetInfo)

            If MessageBox.Show($"确定要移除预设 '{preset.Name}' 吗？", "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question) = DialogResult.Yes Then
                ' 从列表中移除
                _presets.Remove(preset)
                lstPresets.Items.Remove(item)

                ' 删除文件
                _configManager.DeletePreset(preset.Name)

                MessageBox.Show("预设移除成功", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)
            End If
        Catch ex As Exception
            MessageBox.Show("移除预设失败: " & ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub BtnSave_Click(sender As Object, e As EventArgs) Handles btnSave.Click
        Me.DialogResult = DialogResult.OK
        Me.Close()
    End Sub

    Private Sub BtnCancel_Click(sender As Object, e As EventArgs) Handles btnCancel.Click
        Me.DialogResult = DialogResult.Cancel
        Me.Close()
    End Sub

    Private Sub LstPresets_SelectedIndexChanged(sender As Object, e As EventArgs) Handles lstPresets.SelectedIndexChanged
        If _isEditing Then Return

        If lstPresets.SelectedItems.Count > 0 Then
            Dim item As ListViewItem = lstPresets.SelectedItems(0)
            Dim preset As PresetInfo = CType(item.Tag, PresetInfo)

            txtName.Text = preset.Name
            SelectComboBoxItem(cmbPlotDevice, preset.PlotDevice)
            SelectComboBoxItem(cmbPlotStyle, preset.PlotStyle)
            LoadPaperSizes(preset.PlotDevice)
            SelectComboBoxItem(cmbPaperSize, preset.PaperSize)
        End If
    End Sub

    Private Sub SelectComboBoxItem(cmb As ComboBox, value As String)
        For i As Integer = 0 To cmb.Items.Count - 1
            If cmb.Items(i).ToString().Equals(value, StringComparison.OrdinalIgnoreCase) Then
                cmb.SelectedIndex = i
                Return
            End If
        Next
        If cmb.Items.Count > 0 Then
            cmb.SelectedIndex = 0
        End If
    End Sub

    Private Sub SavePresetToFile(preset As PresetInfo)
        Dim config As New Dictionary(Of String, String)()
        ' 使用与主窗口兼容的键名
        config("PlotterName") = preset.PlotDevice
        config("PaperSize") = preset.PaperSize
        config("PlotStyle") = preset.PlotStyle
        ' 保存扩展配置项
        config("Copies") = If(String.IsNullOrEmpty(preset.Copies), "1", preset.Copies)
        config("Collate") = If(String.IsNullOrEmpty(preset.Collate), "True", preset.Collate)
        config("AutoRotate") = If(String.IsNullOrEmpty(preset.AutoRotate), "True", preset.AutoRotate)
        config("AutoCenter") = If(String.IsNullOrEmpty(preset.AutoCenter), "True", preset.AutoCenter)
        config("OffsetX") = If(String.IsNullOrEmpty(preset.OffsetX), "0", preset.OffsetX)
        config("OffsetY") = If(String.IsNullOrEmpty(preset.OffsetY), "0", preset.OffsetY)
        config("NameTemplate") = If(String.IsNullOrEmpty(preset.NameTemplate), "", preset.NameTemplate)
        config("SavePath") = If(String.IsNullOrEmpty(preset.SavePath), "", preset.SavePath)
        _configManager.SavePreset(preset.Name, config)
    End Sub
End Class
