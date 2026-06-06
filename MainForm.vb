Imports System.IO
Imports System.Windows.Forms
Imports System.Drawing
Imports System.Diagnostics
Imports System.Runtime.InteropServices
Imports Path = System.IO.Path
Imports Autodesk.AutoCAD.ApplicationServices
Imports Autodesk.AutoCAD.DatabaseServices
Imports Autodesk.AutoCAD.EditorInput
Imports Autodesk.AutoCAD.Geometry

Partial Public Class MainForm
    Inherits Form

    ' ========== Windows API 函数声明 ==========
    ' 查找窗口
    <DllImport("user32.dll", CharSet:=CharSet.Auto)>
    Private Shared Function FindWindow(lpClassName As String, lpWindowName As String) As IntPtr
    End Function

    ' 枚举窗口
    <DllImport("user32.dll")>
    Private Shared Function EnumWindows(lpEnumFunc As EnumWindowsProc, lParam As IntPtr) As Boolean
    End Function

    ' 获取窗口类名
    <DllImport("user32.dll", CharSet:=CharSet.Auto)>
    Private Shared Function GetClassName(hWnd As IntPtr, lpClassName As System.Text.StringBuilder, nMaxCount As Integer) As Integer
    End Function

    ' 委托定义
    Private Delegate Function EnumWindowsProc(hWnd As IntPtr, lParam As IntPtr) As Boolean

    ' ========== 左侧控件 ==========
    ' 图框形式
    Private grpFrameType As GroupBox
    Private WithEvents radPolyline As RadioButton
    Private WithEvents radBlock As RadioButton
    Private WithEvents radLine As RadioButton
    Private lblPolylineDesc As Label
    Private lblBlockDesc As Label
    Private lblLineDesc As Label

    ' 图块与图层
    Private grpBlockLayer As GroupBox
    Private WithEvents chkAutoSelect As CheckBox
    Private WithEvents btnSpecifyBlock As Button
    Private txtBlockName As TextBox
    Private WithEvents btnSelectBlock As Button
    Private txtLayerName As TextBox
    Private WithEvents btnSelectLayer As Button

    ' 输出选项
    Private grpOutputOption As GroupBox
    Private WithEvents radDirectPlot As RadioButton
    Private WithEvents radPlotToFile As RadioButton
    Private WithEvents radSplitArchive As RadioButton
    Private WithEvents radBatchCreateLayout As RadioButton
    Private WithEvents radPlotExistingLayout As RadioButton

    ' 选择图纸区域
    Private grpSelectDrawing As GroupBox
    Private WithEvents btnSelectDrawing As Button
    Private lblSelectedCount As Label
    Private WithEvents btnHighlight As Button

    ' ========== 右侧控件 ==========
    ' 打印设置
    Private grpPlotSettings As GroupBox
    Private lblPreset As Label
    Private WithEvents cmbPreset As ComboBox
    Private WithEvents btnModifyPreset As Button
    Private lblPlotDevice As Label
    Private WithEvents cmbPlotDevice As ComboBox
    Private WithEvents btnDeviceProps As Button
    Private lblPaperSize As Label
    Private cmbPaperSize As ComboBox
    Private chkAutoMatch As CheckBox
    Private lblPlotStyle As Label
    Private cmbPlotStyle As ComboBox
    Private WithEvents btnEditStyle As Button
    Private lblCopies As Label
    Private txtCopies As TextBox
    Private WithEvents chkPlotStamp As CheckBox
    Private WithEvents btnPlotStamp As Button
    Private WithEvents btnBlockSettings As Button
    Private WithEvents btnBoundingBox As Button
    Private WithEvents radAutoScale As RadioButton
    Private WithEvents radFitToPaper As RadioButton
    Private WithEvents radFixedScale As RadioButton
    Private txtScale As TextBox
    Private chkAutoRotate As CheckBox
    Private chkReverse As CheckBox
    Private radAutoCenter As RadioButton
    Private WithEvents radOffset As RadioButton
    Private lblOffsetX As Label
    Private txtOffsetX As TextBox
    Private lblOffsetY As Label
    Private txtOffsetY As TextBox
    Private radSelectOrder As RadioButton
    Private chkReverseOrder As CheckBox
    Private radLeftRight As RadioButton
    Private radTopBottom As RadioButton
    Private chkPlotByCopy As CheckBox

    ' 文件/布局名设置
    Private grpFileLayout As GroupBox
    Private lblSaveLocation As Label
    Private WithEvents radSourcePathPDF As RadioButton
    Private WithEvents radSourcePathDWF As RadioButton
    Private WithEvents radOtherPath As RadioButton
    Private lblSavePath As Label
    Private txtSavePath As TextBox
    Private WithEvents btnBrowsePath As Button
    Private lblNameTemplate As Label
    Private txtNameTemplate As TextBox
    Private txtExtension As TextBox
    Private WithEvents btnVariables As Button
    Private chkUseDWGName As CheckBox
    Private chkTraverseLayouts As CheckBox
    Private chkModelSpaceScale As CheckBox
    Private radOverwrite As RadioButton
    Private radAutoRename As RadioButton

    ' ========== 底部按钮 ==========
    Private WithEvents btnMultiDoc As Button
    Private WithEvents btnPreview As Button
    Private WithEvents btnApply As Button
    Private WithEvents btnOK As Button
    Private WithEvents btnCancel As Button
    Private WithEvents btnContact As Button

    ' 打印引擎
    Private _plotEngine As BatchPlotEngine
    Private _frames As List(Of BatchPlotEngine.FrameInfo)
    Private _multiDocFiles As List(Of String)  ' 多文档打印选中的文件列表
    Private _useSingleDocStyle As Boolean = False  ' 是否使用单文档内部打印样式

    Public Sub New()
        InitializeComponent()
        ' 检查是否在设计时运行
        If Not System.ComponentModel.LicenseManager.UsageMode = System.ComponentModel.LicenseUsageMode.Designtime Then
            _plotEngine = New BatchPlotEngine()
            _frames = New List(Of BatchPlotEngine.FrameInfo)()
            LoadSettings()
        Else
            _frames = New List(Of BatchPlotEngine.FrameInfo)()
        End If
        ' 初始化打印戳记按钮状态
        btnPlotStamp.Enabled = chkPlotStamp.Checked
        ' 初始化输出选项相关控件状态
        UpdateOutputOptionControls()
    End Sub

    Private Sub InitializeComponent()
        Me.grpFrameType = New System.Windows.Forms.GroupBox()
        Me.radPolyline = New System.Windows.Forms.RadioButton()
        Me.lblPolylineDesc = New System.Windows.Forms.Label()
        Me.radBlock = New System.Windows.Forms.RadioButton()
        Me.lblBlockDesc = New System.Windows.Forms.Label()
        Me.radLine = New System.Windows.Forms.RadioButton()
        Me.lblLineDesc = New System.Windows.Forms.Label()
        Me.grpBlockLayer = New System.Windows.Forms.GroupBox()
        Me.chkAutoSelect = New System.Windows.Forms.CheckBox()
        Me.btnSpecifyBlock = New System.Windows.Forms.Button()
        Me.txtBlockName = New System.Windows.Forms.TextBox()
        Me.btnSelectBlock = New System.Windows.Forms.Button()
        Me.txtLayerName = New System.Windows.Forms.TextBox()
        Me.btnSelectLayer = New System.Windows.Forms.Button()
        Me.grpOutputOption = New System.Windows.Forms.GroupBox()
        Me.radDirectPlot = New System.Windows.Forms.RadioButton()
        Me.radPlotToFile = New System.Windows.Forms.RadioButton()
        Me.radSplitArchive = New System.Windows.Forms.RadioButton()
        Me.radBatchCreateLayout = New System.Windows.Forms.RadioButton()
        Me.radPlotExistingLayout = New System.Windows.Forms.RadioButton()
        Me.grpSelectDrawing = New System.Windows.Forms.GroupBox()
        Me.btnSelectDrawing = New System.Windows.Forms.Button()
        Me.lblSelectedCount = New System.Windows.Forms.Label()
        Me.btnHighlight = New System.Windows.Forms.Button()
        Me.grpPlotSettings = New System.Windows.Forms.GroupBox()
        Me.lblPreset = New System.Windows.Forms.Label()
        Me.cmbPreset = New System.Windows.Forms.ComboBox()
        Me.btnModifyPreset = New System.Windows.Forms.Button()
        Me.lblPlotDevice = New System.Windows.Forms.Label()
        Me.cmbPlotDevice = New System.Windows.Forms.ComboBox()
        Me.btnDeviceProps = New System.Windows.Forms.Button()
        Me.lblPaperSize = New System.Windows.Forms.Label()
        Me.cmbPaperSize = New System.Windows.Forms.ComboBox()
        Me.chkAutoMatch = New System.Windows.Forms.CheckBox()
        Me.lblPlotStyle = New System.Windows.Forms.Label()
        Me.cmbPlotStyle = New System.Windows.Forms.ComboBox()
        Me.btnEditStyle = New System.Windows.Forms.Button()
        Me.lblCopies = New System.Windows.Forms.Label()
        Me.txtCopies = New System.Windows.Forms.TextBox()
        Me.chkPlotStamp = New System.Windows.Forms.CheckBox()
        Me.btnPlotStamp = New System.Windows.Forms.Button()
        Me.btnBlockSettings = New System.Windows.Forms.Button()
        Me.btnBoundingBox = New System.Windows.Forms.Button()
        Me.radAutoScale = New System.Windows.Forms.RadioButton()
        Me.radFitToPaper = New System.Windows.Forms.RadioButton()
        Me.radFixedScale = New System.Windows.Forms.RadioButton()
        Me.txtScale = New System.Windows.Forms.TextBox()
        Me.chkAutoRotate = New System.Windows.Forms.CheckBox()
        Me.chkReverse = New System.Windows.Forms.CheckBox()
        Me.radAutoCenter = New System.Windows.Forms.RadioButton()
        Me.radOffset = New System.Windows.Forms.RadioButton()
        Me.lblOffsetX = New System.Windows.Forms.Label()
        Me.txtOffsetX = New System.Windows.Forms.TextBox()
        Me.lblOffsetY = New System.Windows.Forms.Label()
        Me.txtOffsetY = New System.Windows.Forms.TextBox()
        Me.radSelectOrder = New System.Windows.Forms.RadioButton()
        Me.chkReverseOrder = New System.Windows.Forms.CheckBox()
        Me.radLeftRight = New System.Windows.Forms.RadioButton()
        Me.radTopBottom = New System.Windows.Forms.RadioButton()
        Me.chkPlotByCopy = New System.Windows.Forms.CheckBox()
        Me.grpFileLayout = New System.Windows.Forms.GroupBox()
        Me.lblSaveLocation = New System.Windows.Forms.Label()
        Me.radSourcePathPDF = New System.Windows.Forms.RadioButton()
        Me.radSourcePathDWF = New System.Windows.Forms.RadioButton()
        Me.radOtherPath = New System.Windows.Forms.RadioButton()
        Me.lblSavePath = New System.Windows.Forms.Label()
        Me.txtSavePath = New System.Windows.Forms.TextBox()
        Me.btnBrowsePath = New System.Windows.Forms.Button()
        Me.lblNameTemplate = New System.Windows.Forms.Label()
        Me.txtNameTemplate = New System.Windows.Forms.TextBox()
        Me.txtExtension = New System.Windows.Forms.TextBox()
        Me.btnVariables = New System.Windows.Forms.Button()
        Me.chkUseDWGName = New System.Windows.Forms.CheckBox()
        Me.chkTraverseLayouts = New System.Windows.Forms.CheckBox()
        Me.chkModelSpaceScale = New System.Windows.Forms.CheckBox()
        Me.radOverwrite = New System.Windows.Forms.RadioButton()
        Me.radAutoRename = New System.Windows.Forms.RadioButton()
        Me.btnMultiDoc = New System.Windows.Forms.Button()
        Me.btnPreview = New System.Windows.Forms.Button()
        Me.btnApply = New System.Windows.Forms.Button()
        Me.btnOK = New System.Windows.Forms.Button()
        Me.btnCancel = New System.Windows.Forms.Button()
        Me.btnContact = New System.Windows.Forms.Button()
        Me.GroupBox1 = New System.Windows.Forms.GroupBox()
        Me.Label1 = New System.Windows.Forms.Label()
        Me.GroupBox2 = New System.Windows.Forms.GroupBox()
        Me.GroupBox3 = New System.Windows.Forms.GroupBox()
        Me.GroupBox4 = New System.Windows.Forms.GroupBox()
        Me.GroupBox5 = New System.Windows.Forms.GroupBox()
        Me.grpFrameType.SuspendLayout()
        Me.grpBlockLayer.SuspendLayout()
        Me.grpOutputOption.SuspendLayout()
        Me.grpSelectDrawing.SuspendLayout()
        Me.grpPlotSettings.SuspendLayout()
        Me.grpFileLayout.SuspendLayout()
        Me.GroupBox1.SuspendLayout()
        Me.GroupBox2.SuspendLayout()
        Me.GroupBox3.SuspendLayout()
        Me.GroupBox4.SuspendLayout()
        Me.GroupBox5.SuspendLayout()
        Me.SuspendLayout()
        '
        'grpFrameType
        '
        Me.grpFrameType.Controls.Add(Me.radPolyline)
        Me.grpFrameType.Controls.Add(Me.lblPolylineDesc)
        Me.grpFrameType.Controls.Add(Me.radBlock)
        Me.grpFrameType.Controls.Add(Me.lblBlockDesc)
        Me.grpFrameType.Controls.Add(Me.radLine)
        Me.grpFrameType.Controls.Add(Me.lblLineDesc)
        Me.grpFrameType.Location = New System.Drawing.Point(9, 12)
        Me.grpFrameType.Name = "grpFrameType"
        Me.grpFrameType.Size = New System.Drawing.Size(240, 99)
        Me.grpFrameType.TabIndex = 0
        Me.grpFrameType.TabStop = False
        Me.grpFrameType.Text = "图框形式"
        '
        'radPolyline
        '
        Me.radPolyline.Location = New System.Drawing.Point(10, 20)
        Me.radPolyline.Name = "radPolyline"
        Me.radPolyline.Size = New System.Drawing.Size(80, 20)
        Me.radPolyline.TabIndex = 0
        Me.radPolyline.Text = "多段线(P):"
        '
        'lblPolylineDesc
        '
        Me.lblPolylineDesc.Location = New System.Drawing.Point(90, 22)
        Me.lblPolylineDesc.Name = "lblPolylineDesc"
        Me.lblPolylineDesc.Size = New System.Drawing.Size(120, 20)
        Me.lblPolylineDesc.TabIndex = 1
        Me.lblPolylineDesc.Text = "指定图层多段线矩形"
        '
        'radBlock
        '
        Me.radBlock.Checked = True
        Me.radBlock.Location = New System.Drawing.Point(10, 46)
        Me.radBlock.Name = "radBlock"
        Me.radBlock.Size = New System.Drawing.Size(80, 20)
        Me.radBlock.TabIndex = 2
        Me.radBlock.TabStop = True
        Me.radBlock.Text = "图块(B):"
        '
        'lblBlockDesc
        '
        Me.lblBlockDesc.Location = New System.Drawing.Point(90, 48)
        Me.lblBlockDesc.Name = "lblBlockDesc"
        Me.lblBlockDesc.Size = New System.Drawing.Size(120, 20)
        Me.lblBlockDesc.TabIndex = 3
        Me.lblBlockDesc.Text = "图框为特定图块"
        '
        'radLine
        '
        Me.radLine.Location = New System.Drawing.Point(10, 72)
        Me.radLine.Name = "radLine"
        Me.radLine.Size = New System.Drawing.Size(80, 20)
        Me.radLine.TabIndex = 4
        Me.radLine.Text = "直线(L):"
        '
        'lblLineDesc
        '
        Me.lblLineDesc.Location = New System.Drawing.Point(90, 74)
        Me.lblLineDesc.Name = "lblLineDesc"
        Me.lblLineDesc.Size = New System.Drawing.Size(120, 20)
        Me.lblLineDesc.TabIndex = 5
        Me.lblLineDesc.Text = "指定图层直线矩形"
        '
        'grpBlockLayer
        '
        Me.grpBlockLayer.Controls.Add(Me.chkAutoSelect)
        Me.grpBlockLayer.Controls.Add(Me.btnSpecifyBlock)
        Me.grpBlockLayer.Controls.Add(Me.txtBlockName)
        Me.grpBlockLayer.Controls.Add(Me.btnSelectBlock)
        Me.grpBlockLayer.Controls.Add(Me.txtLayerName)
        Me.grpBlockLayer.Controls.Add(Me.btnSelectLayer)
        Me.grpBlockLayer.Location = New System.Drawing.Point(9, 117)
        Me.grpBlockLayer.Name = "grpBlockLayer"
        Me.grpBlockLayer.Size = New System.Drawing.Size(240, 118)
        Me.grpBlockLayer.TabIndex = 1
        Me.grpBlockLayer.TabStop = False
        Me.grpBlockLayer.Text = "图块与图层"
        '
        'chkAutoSelect
        '
        Me.chkAutoSelect.Checked = True
        Me.chkAutoSelect.CheckState = System.Windows.Forms.CheckState.Checked
        Me.chkAutoSelect.Location = New System.Drawing.Point(10, 20)
        Me.chkAutoSelect.Name = "chkAutoSelect"
        Me.chkAutoSelect.Size = New System.Drawing.Size(90, 20)
        Me.chkAutoSelect.TabIndex = 0
        Me.chkAutoSelect.Text = "自动选择"
        '
        'btnSpecifyBlock
        '
        Me.btnSpecifyBlock.Location = New System.Drawing.Point(110, 18)
        Me.btnSpecifyBlock.Name = "btnSpecifyBlock"
        Me.btnSpecifyBlock.Size = New System.Drawing.Size(119, 25)
        Me.btnSpecifyBlock.TabIndex = 1
        Me.btnSpecifyBlock.Text = "指定图块或图层<"
        '
        'txtBlockName
        '
        Me.txtBlockName.Location = New System.Drawing.Point(65, 52)
        Me.txtBlockName.Name = "txtBlockName"
        Me.txtBlockName.Size = New System.Drawing.Size(163, 21)
        Me.txtBlockName.TabIndex = 3
        Me.txtBlockName.Text = "tk_*"
        '
        'btnSelectBlock
        '
        Me.btnSelectBlock.Location = New System.Drawing.Point(10, 50)
        Me.btnSelectBlock.Name = "btnSelectBlock"
        Me.btnSelectBlock.Size = New System.Drawing.Size(49, 23)
        Me.btnSelectBlock.TabIndex = 6
        Me.btnSelectBlock.Text = "图块<"
        '
        'txtLayerName
        '
        Me.txtLayerName.Location = New System.Drawing.Point(65, 83)
        Me.txtLayerName.Name = "txtLayerName"
        Me.txtLayerName.Size = New System.Drawing.Size(164, 21)
        Me.txtLayerName.TabIndex = 5
        Me.txtLayerName.Text = "*_title"
        '
        'btnSelectLayer
        '
        Me.btnSelectLayer.Location = New System.Drawing.Point(10, 81)
        Me.btnSelectLayer.Name = "btnSelectLayer"
        Me.btnSelectLayer.Size = New System.Drawing.Size(49, 23)
        Me.btnSelectLayer.TabIndex = 7
        Me.btnSelectLayer.Text = "图层<"
        '
        'grpOutputOption
        '
        Me.grpOutputOption.Controls.Add(Me.radDirectPlot)
        Me.grpOutputOption.Controls.Add(Me.radPlotToFile)
        Me.grpOutputOption.Controls.Add(Me.radSplitArchive)
        Me.grpOutputOption.Controls.Add(Me.radBatchCreateLayout)
        Me.grpOutputOption.Controls.Add(Me.radPlotExistingLayout)
        Me.grpOutputOption.Location = New System.Drawing.Point(9, 243)
        Me.grpOutputOption.Name = "grpOutputOption"
        Me.grpOutputOption.Size = New System.Drawing.Size(240, 153)
        Me.grpOutputOption.TabIndex = 2
        Me.grpOutputOption.TabStop = False
        Me.grpOutputOption.Text = "输出选项"
        '
        'radDirectPlot
        '
        Me.radDirectPlot.Checked = True
        Me.radDirectPlot.Location = New System.Drawing.Point(10, 20)
        Me.radDirectPlot.Name = "radDirectPlot"
        Me.radDirectPlot.Size = New System.Drawing.Size(134, 20)
        Me.radDirectPlot.TabIndex = 0
        Me.radDirectPlot.TabStop = True
        Me.radDirectPlot.Text = "直接批量打印(1)"
        '
        'radPlotToFile
        '
        Me.radPlotToFile.Location = New System.Drawing.Point(10, 47)
        Me.radPlotToFile.Name = "radPlotToFile"
        Me.radPlotToFile.Size = New System.Drawing.Size(150, 20)
        Me.radPlotToFile.TabIndex = 1
        Me.radPlotToFile.Text = "打印到文件(2)"
        '
        'radSplitArchive
        '
        Me.radSplitArchive.Location = New System.Drawing.Point(10, 74)
        Me.radSplitArchive.Name = "radSplitArchive"
        Me.radSplitArchive.Size = New System.Drawing.Size(150, 20)
        Me.radSplitArchive.TabIndex = 2
        Me.radSplitArchive.Text = "图纸拆分归档(3)"
        '
        'radBatchCreateLayout
        '
        Me.radBatchCreateLayout.Location = New System.Drawing.Point(10, 101)
        Me.radBatchCreateLayout.Name = "radBatchCreateLayout"
        Me.radBatchCreateLayout.Size = New System.Drawing.Size(150, 20)
        Me.radBatchCreateLayout.TabIndex = 3
        Me.radBatchCreateLayout.Text = "批量生成布局(4)"
        '
        'radPlotExistingLayout
        '
        Me.radPlotExistingLayout.Location = New System.Drawing.Point(10, 128)
        Me.radPlotExistingLayout.Name = "radPlotExistingLayout"
        Me.radPlotExistingLayout.Size = New System.Drawing.Size(120, 20)
        Me.radPlotExistingLayout.TabIndex = 4
        Me.radPlotExistingLayout.Text = "打印已有布局(5)"
        '
        'grpSelectDrawing
        '
        Me.grpSelectDrawing.Controls.Add(Me.btnSelectDrawing)
        Me.grpSelectDrawing.Controls.Add(Me.lblSelectedCount)
        Me.grpSelectDrawing.Controls.Add(Me.btnHighlight)
        Me.grpSelectDrawing.Location = New System.Drawing.Point(9, 404)
        Me.grpSelectDrawing.Name = "grpSelectDrawing"
        Me.grpSelectDrawing.Size = New System.Drawing.Size(240, 100)
        Me.grpSelectDrawing.TabIndex = 3
        Me.grpSelectDrawing.TabStop = False
        Me.grpSelectDrawing.Text = "选择图纸"
        '
        'btnSelectDrawing
        '
        Me.btnSelectDrawing.Location = New System.Drawing.Point(10, 24)
        Me.btnSelectDrawing.Name = "btnSelectDrawing"
        Me.btnSelectDrawing.Size = New System.Drawing.Size(219, 25)
        Me.btnSelectDrawing.TabIndex = 0
        Me.btnSelectDrawing.Text = "选择要处理的图纸(&S)<"
        '
        'lblSelectedCount
        '
        Me.lblSelectedCount.Location = New System.Drawing.Point(10, 65)
        Me.lblSelectedCount.Name = "lblSelectedCount"
        Me.lblSelectedCount.Size = New System.Drawing.Size(100, 20)
        Me.lblSelectedCount.TabIndex = 1
        Me.lblSelectedCount.Text = "选中图纸: 0"
        Me.lblSelectedCount.TextAlign = System.Drawing.ContentAlignment.MiddleLeft
        '
        'btnHighlight
        '
        Me.btnHighlight.Location = New System.Drawing.Point(130, 60)
        Me.btnHighlight.Name = "btnHighlight"
        Me.btnHighlight.Size = New System.Drawing.Size(99, 25)
        Me.btnHighlight.TabIndex = 2
        Me.btnHighlight.Text = "亮显(&H)"
        '
        'grpPlotSettings
        '
        Me.grpPlotSettings.Controls.Add(Me.lblPreset)
        Me.grpPlotSettings.Controls.Add(Me.cmbPreset)
        Me.grpPlotSettings.Controls.Add(Me.btnModifyPreset)
        Me.grpPlotSettings.Controls.Add(Me.lblPlotDevice)
        Me.grpPlotSettings.Controls.Add(Me.cmbPlotDevice)
        Me.grpPlotSettings.Controls.Add(Me.btnDeviceProps)
        Me.grpPlotSettings.Controls.Add(Me.lblPaperSize)
        Me.grpPlotSettings.Controls.Add(Me.cmbPaperSize)
        Me.grpPlotSettings.Controls.Add(Me.chkAutoMatch)
        Me.grpPlotSettings.Controls.Add(Me.lblPlotStyle)
        Me.grpPlotSettings.Controls.Add(Me.cmbPlotStyle)
        Me.grpPlotSettings.Controls.Add(Me.btnEditStyle)
        Me.grpPlotSettings.Controls.Add(Me.lblCopies)
        Me.grpPlotSettings.Controls.Add(Me.txtCopies)
        Me.grpPlotSettings.Controls.Add(Me.chkPlotStamp)
        Me.grpPlotSettings.Controls.Add(Me.btnPlotStamp)
        Me.grpPlotSettings.Controls.Add(Me.btnBlockSettings)
        Me.grpPlotSettings.Controls.Add(Me.btnBoundingBox)
        Me.grpPlotSettings.Location = New System.Drawing.Point(260, 12)
        Me.grpPlotSettings.Name = "grpPlotSettings"
        Me.grpPlotSettings.Size = New System.Drawing.Size(550, 164)
        Me.grpPlotSettings.TabIndex = 4
        Me.grpPlotSettings.TabStop = False
        Me.grpPlotSettings.Text = "打印设置"
        '
        'lblPreset
        '
        Me.lblPreset.Location = New System.Drawing.Point(10, 25)
        Me.lblPreset.Name = "lblPreset"
        Me.lblPreset.Size = New System.Drawing.Size(60, 20)
        Me.lblPreset.TabIndex = 0
        Me.lblPreset.Text = "预配置:"
        '
        'cmbPreset
        '
        Me.cmbPreset.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
        Me.cmbPreset.Location = New System.Drawing.Point(75, 22)
        Me.cmbPreset.Name = "cmbPreset"
        Me.cmbPreset.Size = New System.Drawing.Size(359, 20)
        Me.cmbPreset.TabIndex = 1
        '
        'btnModifyPreset
        '
        Me.btnModifyPreset.Location = New System.Drawing.Point(445, 20)
        Me.btnModifyPreset.Name = "btnModifyPreset"
        Me.btnModifyPreset.Size = New System.Drawing.Size(92, 23)
        Me.btnModifyPreset.TabIndex = 2
        Me.btnModifyPreset.Text = "修改"
        '
        'lblPlotDevice
        '
        Me.lblPlotDevice.Location = New System.Drawing.Point(10, 53)
        Me.lblPlotDevice.Name = "lblPlotDevice"
        Me.lblPlotDevice.Size = New System.Drawing.Size(60, 20)
        Me.lblPlotDevice.TabIndex = 3
        Me.lblPlotDevice.Text = "打印设备:"
        '
        'cmbPlotDevice
        '
        Me.cmbPlotDevice.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
        Me.cmbPlotDevice.Location = New System.Drawing.Point(75, 50)
        Me.cmbPlotDevice.Name = "cmbPlotDevice"
        Me.cmbPlotDevice.Size = New System.Drawing.Size(359, 20)
        Me.cmbPlotDevice.TabIndex = 4
        '
        'btnDeviceProps
        '
        Me.btnDeviceProps.Location = New System.Drawing.Point(445, 48)
        Me.btnDeviceProps.Name = "btnDeviceProps"
        Me.btnDeviceProps.Size = New System.Drawing.Size(92, 23)
        Me.btnDeviceProps.TabIndex = 5
        Me.btnDeviceProps.Text = "特性"
        '
        'lblPaperSize
        '
        Me.lblPaperSize.Location = New System.Drawing.Point(10, 79)
        Me.lblPaperSize.Name = "lblPaperSize"
        Me.lblPaperSize.Size = New System.Drawing.Size(60, 20)
        Me.lblPaperSize.TabIndex = 6
        Me.lblPaperSize.Text = "图纸尺寸:"
        '
        'cmbPaperSize
        '
        Me.cmbPaperSize.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
        Me.cmbPaperSize.Location = New System.Drawing.Point(75, 76)
        Me.cmbPaperSize.Name = "cmbPaperSize"
        Me.cmbPaperSize.Size = New System.Drawing.Size(359, 20)
        Me.cmbPaperSize.TabIndex = 7
        '
        'chkAutoMatch
        '
        Me.chkAutoMatch.Location = New System.Drawing.Point(445, 76)
        Me.chkAutoMatch.Name = "chkAutoMatch"
        Me.chkAutoMatch.Size = New System.Drawing.Size(100, 20)
        Me.chkAutoMatch.TabIndex = 8
        Me.chkAutoMatch.Text = "图块自匹配"
        '
        'lblPlotStyle
        '
        Me.lblPlotStyle.Location = New System.Drawing.Point(10, 105)
        Me.lblPlotStyle.Name = "lblPlotStyle"
        Me.lblPlotStyle.Size = New System.Drawing.Size(60, 20)
        Me.lblPlotStyle.TabIndex = 9
        Me.lblPlotStyle.Text = "打印样式:"
        '
        'cmbPlotStyle
        '
        Me.cmbPlotStyle.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
        Me.cmbPlotStyle.Location = New System.Drawing.Point(75, 102)
        Me.cmbPlotStyle.Name = "cmbPlotStyle"
        Me.cmbPlotStyle.Size = New System.Drawing.Size(359, 20)
        Me.cmbPlotStyle.TabIndex = 10
        '
        'btnEditStyle
        '
        Me.btnEditStyle.Location = New System.Drawing.Point(445, 100)
        Me.btnEditStyle.Name = "btnEditStyle"
        Me.btnEditStyle.Size = New System.Drawing.Size(92, 23)
        Me.btnEditStyle.TabIndex = 11
        Me.btnEditStyle.Text = "编辑"
        '
        'lblCopies
        '
        Me.lblCopies.Location = New System.Drawing.Point(10, 135)
        Me.lblCopies.Name = "lblCopies"
        Me.lblCopies.Size = New System.Drawing.Size(60, 20)
        Me.lblCopies.TabIndex = 12
        Me.lblCopies.Text = "打印份数:"
        Me.lblCopies.TextAlign = System.Drawing.ContentAlignment.MiddleCenter
        '
        'txtCopies
        '
        Me.txtCopies.Location = New System.Drawing.Point(75, 135)
        Me.txtCopies.Name = "txtCopies"
        Me.txtCopies.Size = New System.Drawing.Size(60, 21)
        Me.txtCopies.TabIndex = 13
        Me.txtCopies.Text = "1"
        '
        'chkPlotStamp
        '
        Me.chkPlotStamp.Location = New System.Drawing.Point(163, 135)
        Me.chkPlotStamp.Name = "chkPlotStamp"
        Me.chkPlotStamp.Size = New System.Drawing.Size(80, 20)
        Me.chkPlotStamp.TabIndex = 14
        Me.chkPlotStamp.Text = "打印戳记"
        '
        'btnPlotStamp
        '
        Me.btnPlotStamp.Location = New System.Drawing.Point(254, 132)
        Me.btnPlotStamp.Name = "btnPlotStamp"
        Me.btnPlotStamp.Size = New System.Drawing.Size(92, 23)
        Me.btnPlotStamp.TabIndex = 15
        Me.btnPlotStamp.Text = "打印戳记"
        '
        'btnBlockSettings
        '
        Me.btnBlockSettings.Location = New System.Drawing.Point(350, 132)
        Me.btnBlockSettings.Name = "btnBlockSettings"
        Me.btnBlockSettings.Size = New System.Drawing.Size(92, 23)
        Me.btnBlockSettings.TabIndex = 16
        Me.btnBlockSettings.Text = "图块设置"
        '
        'btnBoundingBox
        '
        Me.btnBoundingBox.Location = New System.Drawing.Point(445, 132)
        Me.btnBoundingBox.Name = "btnBoundingBox"
        Me.btnBoundingBox.Size = New System.Drawing.Size(92, 23)
        Me.btnBoundingBox.TabIndex = 17
        Me.btnBoundingBox.Text = "包围框bwk"
        '
        'radAutoScale
        '
        Me.radAutoScale.Location = New System.Drawing.Point(6, 20)
        Me.radAutoScale.Name = "radAutoScale"
        Me.radAutoScale.Size = New System.Drawing.Size(80, 20)
        Me.radAutoScale.TabIndex = 6
        Me.radAutoScale.Text = "自动取整"
        '
        'radFitToPaper
        '
        Me.radFitToPaper.Checked = True
        Me.radFitToPaper.Location = New System.Drawing.Point(6, 45)
        Me.radFitToPaper.Name = "radFitToPaper"
        Me.radFitToPaper.Size = New System.Drawing.Size(80, 20)
        Me.radFitToPaper.TabIndex = 7
        Me.radFitToPaper.TabStop = True
        Me.radFitToPaper.Text = "布满图纸"
        '
        'radFixedScale
        '
        Me.radFixedScale.Location = New System.Drawing.Point(6, 70)
        Me.radFixedScale.Name = "radFixedScale"
        Me.radFixedScale.Size = New System.Drawing.Size(80, 20)
        Me.radFixedScale.TabIndex = 8
        Me.radFixedScale.Text = "固定比例"
        '
        'txtScale
        '
        Me.txtScale.Location = New System.Drawing.Point(152, 69)
        Me.txtScale.Name = "txtScale"
        Me.txtScale.Size = New System.Drawing.Size(80, 21)
        Me.txtScale.TabIndex = 9
        Me.txtScale.Text = "100"
        '
        'chkAutoRotate
        '
        Me.chkAutoRotate.Checked = True
        Me.chkAutoRotate.CheckState = System.Windows.Forms.CheckState.Checked
        Me.chkAutoRotate.Location = New System.Drawing.Point(17, 17)
        Me.chkAutoRotate.Name = "chkAutoRotate"
        Me.chkAutoRotate.Size = New System.Drawing.Size(80, 20)
        Me.chkAutoRotate.TabIndex = 11
        Me.chkAutoRotate.Text = "自动旋转"
        '
        'chkReverse
        '
        Me.chkReverse.Location = New System.Drawing.Point(109, 17)
        Me.chkReverse.Name = "chkReverse"
        Me.chkReverse.Size = New System.Drawing.Size(60, 20)
        Me.chkReverse.TabIndex = 12
        Me.chkReverse.Text = "反向"
        '
        'radAutoCenter
        '
        Me.radAutoCenter.Checked = True
        Me.radAutoCenter.Location = New System.Drawing.Point(181, 17)
        Me.radAutoCenter.Name = "radAutoCenter"
        Me.radAutoCenter.Size = New System.Drawing.Size(80, 20)
        Me.radAutoCenter.TabIndex = 13
        Me.radAutoCenter.TabStop = True
        Me.radAutoCenter.Text = "自动居中"
        '
        'radOffset
        '
        Me.radOffset.Location = New System.Drawing.Point(273, 17)
        Me.radOffset.Name = "radOffset"
        Me.radOffset.Size = New System.Drawing.Size(60, 20)
        Me.radOffset.TabIndex = 14
        Me.radOffset.Text = "偏移"
        '
        'lblOffsetX
        '
        Me.lblOffsetX.Location = New System.Drawing.Point(341, 16)
        Me.lblOffsetX.Name = "lblOffsetX"
        Me.lblOffsetX.Size = New System.Drawing.Size(20, 20)
        Me.lblOffsetX.TabIndex = 15
        Me.lblOffsetX.Text = "X="
        Me.lblOffsetX.TextAlign = System.Drawing.ContentAlignment.MiddleCenter
        '
        'txtOffsetX
        '
        Me.txtOffsetX.Location = New System.Drawing.Point(372, 16)
        Me.txtOffsetX.Name = "txtOffsetX"
        Me.txtOffsetX.Size = New System.Drawing.Size(50, 21)
        Me.txtOffsetX.TabIndex = 16
        Me.txtOffsetX.Text = "0"
        '
        'lblOffsetY
        '
        Me.lblOffsetY.Location = New System.Drawing.Point(443, 16)
        Me.lblOffsetY.Name = "lblOffsetY"
        Me.lblOffsetY.Size = New System.Drawing.Size(20, 20)
        Me.lblOffsetY.TabIndex = 17
        Me.lblOffsetY.Text = "Y="
        Me.lblOffsetY.TextAlign = System.Drawing.ContentAlignment.MiddleCenter
        '
        'txtOffsetY
        '
        Me.txtOffsetY.Location = New System.Drawing.Point(474, 16)
        Me.txtOffsetY.Name = "txtOffsetY"
        Me.txtOffsetY.Size = New System.Drawing.Size(50, 21)
        Me.txtOffsetY.TabIndex = 18
        Me.txtOffsetY.Text = "0"
        '
        'radSelectOrder
        '
        Me.radSelectOrder.Checked = True
        Me.radSelectOrder.Location = New System.Drawing.Point(6, 20)
        Me.radSelectOrder.Name = "radSelectOrder"
        Me.radSelectOrder.Size = New System.Drawing.Size(80, 20)
        Me.radSelectOrder.TabIndex = 20
        Me.radSelectOrder.TabStop = True
        Me.radSelectOrder.Text = "选择顺序"
        '
        'chkReverseOrder
        '
        Me.chkReverseOrder.Location = New System.Drawing.Point(153, 20)
        Me.chkReverseOrder.Name = "chkReverseOrder"
        Me.chkReverseOrder.Size = New System.Drawing.Size(60, 20)
        Me.chkReverseOrder.TabIndex = 21
        Me.chkReverseOrder.Text = "逆序"
        '
        'radLeftRight
        '
        Me.radLeftRight.Location = New System.Drawing.Point(6, 45)
        Me.radLeftRight.Name = "radLeftRight"
        Me.radLeftRight.Size = New System.Drawing.Size(90, 20)
        Me.radLeftRight.TabIndex = 22
        Me.radLeftRight.Text = "左右，上下"
        '
        'radTopBottom
        '
        Me.radTopBottom.Location = New System.Drawing.Point(6, 70)
        Me.radTopBottom.Name = "radTopBottom"
        Me.radTopBottom.Size = New System.Drawing.Size(90, 20)
        Me.radTopBottom.TabIndex = 23
        Me.radTopBottom.Text = "上下，左右"
        '
        'chkPlotByCopy
        '
        Me.chkPlotByCopy.Location = New System.Drawing.Point(153, 70)
        Me.chkPlotByCopy.Name = "chkPlotByCopy"
        Me.chkPlotByCopy.Size = New System.Drawing.Size(80, 20)
        Me.chkPlotByCopy.TabIndex = 24
        Me.chkPlotByCopy.Text = "逐份打印"
        '
        'grpFileLayout
        '
        Me.grpFileLayout.Controls.Add(Me.lblSaveLocation)
        Me.grpFileLayout.Controls.Add(Me.radSourcePathPDF)
        Me.grpFileLayout.Controls.Add(Me.radSourcePathDWF)
        Me.grpFileLayout.Controls.Add(Me.radOtherPath)
        Me.grpFileLayout.Controls.Add(Me.lblSavePath)
        Me.grpFileLayout.Controls.Add(Me.txtSavePath)
        Me.grpFileLayout.Controls.Add(Me.btnBrowsePath)
        Me.grpFileLayout.Controls.Add(Me.lblNameTemplate)
        Me.grpFileLayout.Controls.Add(Me.txtNameTemplate)
        Me.grpFileLayout.Controls.Add(Me.txtExtension)
        Me.grpFileLayout.Controls.Add(Me.btnVariables)
        Me.grpFileLayout.Controls.Add(Me.chkUseDWGName)
        Me.grpFileLayout.Location = New System.Drawing.Point(260, 340)
        Me.grpFileLayout.Name = "grpFileLayout"
        Me.grpFileLayout.Size = New System.Drawing.Size(550, 111)
        Me.grpFileLayout.TabIndex = 25
        Me.grpFileLayout.TabStop = False
        Me.grpFileLayout.Text = "文件/布局名设置"
        '
        'lblSaveLocation
        '
        Me.lblSaveLocation.Location = New System.Drawing.Point(10, 25)
        Me.lblSaveLocation.Name = "lblSaveLocation"
        Me.lblSaveLocation.Size = New System.Drawing.Size(60, 20)
        Me.lblSaveLocation.TabIndex = 0
        Me.lblSaveLocation.Text = "保存位置"
        '
        'radSourcePathPDF
        '
        Me.radSourcePathPDF.Location = New System.Drawing.Point(80, 25)
        Me.radSourcePathPDF.Name = "radSourcePathPDF"
        Me.radSourcePathPDF.Size = New System.Drawing.Size(120, 20)
        Me.radSourcePathPDF.TabIndex = 1
        Me.radSourcePathPDF.Text = "源文件路径\PDF"
        '
        'radSourcePathDWF
        '
        Me.radSourcePathDWF.Location = New System.Drawing.Point(210, 25)
        Me.radSourcePathDWF.Name = "radSourcePathDWF"
        Me.radSourcePathDWF.Size = New System.Drawing.Size(120, 20)
        Me.radSourcePathDWF.TabIndex = 2
        Me.radSourcePathDWF.Text = "源文件路径\DWF"
        '
        'radOtherPath
        '
        Me.radOtherPath.Checked = True
        Me.radOtherPath.Location = New System.Drawing.Point(342, 25)
        Me.radOtherPath.Name = "radOtherPath"
        Me.radOtherPath.Size = New System.Drawing.Size(80, 20)
        Me.radOtherPath.TabIndex = 3
        Me.radOtherPath.TabStop = True
        Me.radOtherPath.Text = "其他路径"
        '
        'lblSavePath
        '
        Me.lblSavePath.Location = New System.Drawing.Point(10, 56)
        Me.lblSavePath.Name = "lblSavePath"
        Me.lblSavePath.Size = New System.Drawing.Size(60, 20)
        Me.lblSavePath.TabIndex = 5
        Me.lblSavePath.Text = "保存位置:"
        '
        'txtSavePath
        '
        Me.txtSavePath.Location = New System.Drawing.Point(75, 53)
        Me.txtSavePath.Name = "txtSavePath"
        Me.txtSavePath.Size = New System.Drawing.Size(380, 21)
        Me.txtSavePath.TabIndex = 6
        '
        'btnBrowsePath
        '
        Me.btnBrowsePath.Location = New System.Drawing.Point(465, 51)
        Me.btnBrowsePath.Name = "btnBrowsePath"
        Me.btnBrowsePath.Size = New System.Drawing.Size(72, 25)
        Me.btnBrowsePath.TabIndex = 7
        Me.btnBrowsePath.Text = "浏览..."
        '
        'lblNameTemplate
        '
        Me.lblNameTemplate.Location = New System.Drawing.Point(10, 86)
        Me.lblNameTemplate.Name = "lblNameTemplate"
        Me.lblNameTemplate.Size = New System.Drawing.Size(60, 20)
        Me.lblNameTemplate.TabIndex = 8
        Me.lblNameTemplate.Text = "名称样板"
        '
        'txtNameTemplate
        '
        Me.txtNameTemplate.Location = New System.Drawing.Point(75, 83)
        Me.txtNameTemplate.Name = "txtNameTemplate"
        Me.txtNameTemplate.Size = New System.Drawing.Size(324, 21)
        Me.txtNameTemplate.TabIndex = 9
        Me.txtNameTemplate.Text = "<DWGNAME>"
        '
        'txtExtension
        '
        Me.txtExtension.Location = New System.Drawing.Point(405, 83)
        Me.txtExtension.Name = "txtExtension"
        Me.txtExtension.ReadOnly = True
        Me.txtExtension.Size = New System.Drawing.Size(50, 21)
        Me.txtExtension.TabIndex = 10
        '
        'btnVariables
        '
        Me.btnVariables.Location = New System.Drawing.Point(465, 79)
        Me.btnVariables.Name = "btnVariables"
        Me.btnVariables.Size = New System.Drawing.Size(72, 25)
        Me.btnVariables.TabIndex = 11
        Me.btnVariables.Text = "变量..."
        '
        'chkUseDWGName
        '
        Me.chkUseDWGName.Location = New System.Drawing.Point(456, 25)
        Me.chkUseDWGName.Name = "chkUseDWGName"
        Me.chkUseDWGName.Size = New System.Drawing.Size(81, 20)
        Me.chkUseDWGName.TabIndex = 12
        Me.chkUseDWGName.Text = "使用DWG名"
        '
        'chkTraverseLayouts
        '
        Me.chkTraverseLayouts.Location = New System.Drawing.Point(14, 19)
        Me.chkTraverseLayouts.Name = "chkTraverseLayouts"
        Me.chkTraverseLayouts.Size = New System.Drawing.Size(75, 20)
        Me.chkTraverseLayouts.TabIndex = 12
        Me.chkTraverseLayouts.Text = "遍历布局"
        '
        'chkModelSpaceScale
        '
        Me.chkModelSpaceScale.Checked = True
        Me.chkModelSpaceScale.CheckState = System.Windows.Forms.CheckState.Checked
        Me.chkModelSpaceScale.Location = New System.Drawing.Point(154, 19)
        Me.chkModelSpaceScale.Name = "chkModelSpaceScale"
        Me.chkModelSpaceScale.Size = New System.Drawing.Size(120, 20)
        Me.chkModelSpaceScale.TabIndex = 13
        Me.chkModelSpaceScale.Text = "模型空间线型比例"
        '
        'radOverwrite
        '
        Me.radOverwrite.Location = New System.Drawing.Point(9, 19)
        Me.radOverwrite.Name = "radOverwrite"
        Me.radOverwrite.Size = New System.Drawing.Size(80, 20)
        Me.radOverwrite.TabIndex = 15
        Me.radOverwrite.Text = "覆盖重名"
        '
        'radAutoRename
        '
        Me.radAutoRename.Checked = True
        Me.radAutoRename.Location = New System.Drawing.Point(137, 19)
        Me.radAutoRename.Name = "radAutoRename"
        Me.radAutoRename.Size = New System.Drawing.Size(80, 20)
        Me.radAutoRename.TabIndex = 16
        Me.radAutoRename.TabStop = True
        Me.radAutoRename.Text = "自动顺延"
        '
        'btnMultiDoc
        '
        Me.btnMultiDoc.Location = New System.Drawing.Point(11, 517)
        Me.btnMultiDoc.Name = "btnMultiDoc"
        Me.btnMultiDoc.Size = New System.Drawing.Size(100, 25)
        Me.btnMultiDoc.TabIndex = 26
        Me.btnMultiDoc.Text = "多文档打印(&M)"
        '
        'btnPreview
        '
        Me.btnPreview.Location = New System.Drawing.Point(204, 517)
        Me.btnPreview.Name = "btnPreview"
        Me.btnPreview.Size = New System.Drawing.Size(89, 25)
        Me.btnPreview.TabIndex = 27
        Me.btnPreview.Text = "预览(&R)"
        '
        'btnApply
        '
        Me.btnApply.Location = New System.Drawing.Point(370, 517)
        Me.btnApply.Name = "btnApply"
        Me.btnApply.Size = New System.Drawing.Size(89, 25)
        Me.btnApply.TabIndex = 28
        Me.btnApply.Text = "应用(&Y)"
        '
        'btnOK
        '
        Me.btnOK.Location = New System.Drawing.Point(487, 517)
        Me.btnOK.Name = "btnOK"
        Me.btnOK.Size = New System.Drawing.Size(89, 25)
        Me.btnOK.TabIndex = 29
        Me.btnOK.Text = "确定(&O)"
        Me.btnOK.UseVisualStyleBackColor = True
        '
        'btnCancel
        '
        Me.btnCancel.Location = New System.Drawing.Point(604, 517)
        Me.btnCancel.Name = "btnCancel"
        Me.btnCancel.Size = New System.Drawing.Size(89, 25)
        Me.btnCancel.TabIndex = 30
        Me.btnCancel.Text = "取消(&C)"
        '
        'btnContact
        '
        Me.btnContact.Location = New System.Drawing.Point(721, 517)
        Me.btnContact.Name = "btnContact"
        Me.btnContact.Size = New System.Drawing.Size(89, 25)
        Me.btnContact.TabIndex = 31
        Me.btnContact.Text = "联系(&L)"
        '
        'GroupBox1
        '
        Me.GroupBox1.Controls.Add(Me.Label1)
        Me.GroupBox1.Controls.Add(Me.radAutoScale)
        Me.GroupBox1.Controls.Add(Me.radFitToPaper)
        Me.GroupBox1.Controls.Add(Me.radFixedScale)
        Me.GroupBox1.Controls.Add(Me.txtScale)
        Me.GroupBox1.Location = New System.Drawing.Point(260, 184)
        Me.GroupBox1.Name = "GroupBox1"
        Me.GroupBox1.Size = New System.Drawing.Size(263, 94)
        Me.GroupBox1.TabIndex = 32
        Me.GroupBox1.TabStop = False
        Me.GroupBox1.Text = "打印比例"
        '
        'Label1
        '
        Me.Label1.AutoSize = True
        Me.Label1.Location = New System.Drawing.Point(129, 73)
        Me.Label1.Name = "Label1"
        Me.Label1.Size = New System.Drawing.Size(17, 12)
        Me.Label1.TabIndex = 10
        Me.Label1.Text = "1:"
        '
        'GroupBox2
        '
        Me.GroupBox2.Controls.Add(Me.radSelectOrder)
        Me.GroupBox2.Controls.Add(Me.radLeftRight)
        Me.GroupBox2.Controls.Add(Me.radTopBottom)
        Me.GroupBox2.Controls.Add(Me.chkReverseOrder)
        Me.GroupBox2.Controls.Add(Me.chkPlotByCopy)
        Me.GroupBox2.Location = New System.Drawing.Point(529, 184)
        Me.GroupBox2.Name = "GroupBox2"
        Me.GroupBox2.Size = New System.Drawing.Size(281, 94)
        Me.GroupBox2.TabIndex = 33
        Me.GroupBox2.TabStop = False
        Me.GroupBox2.Text = "打印顺序"
        '
        'GroupBox3
        '
        Me.GroupBox3.Controls.Add(Me.chkAutoRotate)
        Me.GroupBox3.Controls.Add(Me.chkReverse)
        Me.GroupBox3.Controls.Add(Me.radAutoCenter)
        Me.GroupBox3.Controls.Add(Me.radOffset)
        Me.GroupBox3.Controls.Add(Me.txtOffsetY)
        Me.GroupBox3.Controls.Add(Me.lblOffsetY)
        Me.GroupBox3.Controls.Add(Me.txtOffsetX)
        Me.GroupBox3.Controls.Add(Me.lblOffsetX)
        Me.GroupBox3.Location = New System.Drawing.Point(260, 286)
        Me.GroupBox3.Name = "GroupBox3"
        Me.GroupBox3.Size = New System.Drawing.Size(550, 43)
        Me.GroupBox3.TabIndex = 34
        Me.GroupBox3.TabStop = False
        Me.GroupBox3.Text = "图纸定位"
        '
        'GroupBox4
        '
        Me.GroupBox4.Controls.Add(Me.chkTraverseLayouts)
        Me.GroupBox4.Controls.Add(Me.chkModelSpaceScale)
        Me.GroupBox4.Location = New System.Drawing.Point(260, 460)
        Me.GroupBox4.Name = "GroupBox4"
        Me.GroupBox4.Size = New System.Drawing.Size(295, 44)
        Me.GroupBox4.TabIndex = 35
        Me.GroupBox4.TabStop = False
        Me.GroupBox4.Text = "布局设置"
        '
        'GroupBox5
        '
        Me.GroupBox5.Controls.Add(Me.radOverwrite)
        Me.GroupBox5.Controls.Add(Me.radAutoRename)
        Me.GroupBox5.Location = New System.Drawing.Point(561, 460)
        Me.GroupBox5.Name = "GroupBox5"
        Me.GroupBox5.Size = New System.Drawing.Size(249, 43)
        Me.GroupBox5.TabIndex = 36
        Me.GroupBox5.TabStop = False
        Me.GroupBox5.Text = "重名文件设置"
        '
        'MainForm
        '
        Me.ClientSize = New System.Drawing.Size(820, 552)
        Me.Controls.Add(Me.GroupBox5)
        Me.Controls.Add(Me.GroupBox3)
        Me.Controls.Add(Me.GroupBox4)
        Me.Controls.Add(Me.GroupBox2)
        Me.Controls.Add(Me.GroupBox1)
        Me.Controls.Add(Me.grpFrameType)
        Me.Controls.Add(Me.grpBlockLayer)
        Me.Controls.Add(Me.grpOutputOption)
        Me.Controls.Add(Me.grpSelectDrawing)
        Me.Controls.Add(Me.grpPlotSettings)
        Me.Controls.Add(Me.grpFileLayout)
        Me.Controls.Add(Me.btnMultiDoc)
        Me.Controls.Add(Me.btnPreview)
        Me.Controls.Add(Me.btnApply)
        Me.Controls.Add(Me.btnOK)
        Me.Controls.Add(Me.btnCancel)
        Me.Controls.Add(Me.btnContact)
        Me.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog
        Me.MaximizeBox = False
        Me.MinimizeBox = False
        Me.Name = "MainForm"
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen
        Me.Text = "批量打印"
        Me.TopMost = True
        Me.grpFrameType.ResumeLayout(False)
        Me.grpBlockLayer.ResumeLayout(False)
        Me.grpBlockLayer.PerformLayout()
        Me.grpOutputOption.ResumeLayout(False)
        Me.grpSelectDrawing.ResumeLayout(False)
        Me.grpPlotSettings.ResumeLayout(False)
        Me.grpPlotSettings.PerformLayout()
        Me.grpFileLayout.ResumeLayout(False)
        Me.grpFileLayout.PerformLayout()
        Me.GroupBox1.ResumeLayout(False)
        Me.GroupBox1.PerformLayout()
        Me.GroupBox2.ResumeLayout(False)
        Me.GroupBox3.ResumeLayout(False)
        Me.GroupBox3.PerformLayout()
        Me.GroupBox4.ResumeLayout(False)
        Me.GroupBox5.ResumeLayout(False)
        Me.ResumeLayout(False)

    End Sub

    Private Sub LoadSettings()
        Try
            Dim plotters As List(Of String) = _plotEngine.GetPlotterList()
            cmbPlotDevice.Items.Clear()
            For Each plotter As String In plotters
                cmbPlotDevice.Items.Add(plotter)
            Next
            ' 如果没有项目，添加默认的"无"
            If cmbPlotDevice.Items.Count = 0 Then
                cmbPlotDevice.Items.Add("无")
            End If

            ' 获取系统默认打印机并选中
            Dim defaultPlotter As String = _plotEngine.GetSystemDefaultPlotter()
            If Not String.IsNullOrEmpty(defaultPlotter) AndAlso cmbPlotDevice.Items.Contains(defaultPlotter) Then
                cmbPlotDevice.SelectedItem = defaultPlotter
            ElseIf cmbPlotDevice.Items.Count > 0 Then
                cmbPlotDevice.SelectedIndex = 0
            End If

            ' 根据默认打印机获取纸张列表
            Dim selectedPlotter As String = If(cmbPlotDevice.SelectedItem IsNot Nothing, cmbPlotDevice.SelectedItem.ToString(), "")
            Dim papers As List(Of String) = _plotEngine.GetPaperList(selectedPlotter)
            cmbPaperSize.Items.Clear()
            For Each paper As String In papers
                cmbPaperSize.Items.Add(paper)
            Next

            ' 初始化特性按钮状态
            btnDeviceProps.Enabled = Not String.IsNullOrEmpty(selectedPlotter) AndAlso selectedPlotter.EndsWith(".pc3", StringComparison.OrdinalIgnoreCase)

            ' 选中默认纸张
            Dim defaultPaper As String = _plotEngine.GetDefaultPaper(selectedPlotter)
            If Not String.IsNullOrEmpty(defaultPaper) AndAlso cmbPaperSize.Items.Contains(defaultPaper) Then
                cmbPaperSize.SelectedItem = defaultPaper
            ElseIf cmbPaperSize.Items.Count > 0 Then
                cmbPaperSize.SelectedIndex = 0
            End If

            Dim styles As List(Of String) = _plotEngine.GetPlotStyleList()
            cmbPlotStyle.Items.Clear()
            cmbPlotStyle.Items.Add("无")
            For Each style As String In styles
                cmbPlotStyle.Items.Add(style)
            Next
            cmbPlotStyle.SelectedIndex = 0

            ' 加载预配置列表
            LoadPresetList()

            ' 默认保存路径为当前源文件路径
            Try
                Dim doc As Document = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument
                If doc IsNot Nothing AndAlso Not String.IsNullOrEmpty(doc.Name) Then
                    txtSavePath.Text = Path.GetDirectoryName(doc.Name)
                Else
                    txtSavePath.Text = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                End If
            Catch
                txtSavePath.Text = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            End Try

            ' 隐藏NumericUpDown的上下箭头按钮
            ' 设置TextBox只能输入数字
            AddNumericValidation(txtCopies, False)
            AddNumericValidation(txtScale, False)
            AddNumericValidation(txtOffsetX, True)
            AddNumericValidation(txtOffsetY, True)

            ' 初始化偏移控件状态（默认不可用，只有选中偏移时才可用）
            UpdateOffsetControls()

            ' 初始化固定比例控件状态（默认不可用，只有选中固定比例时才可用）
            UpdateScaleControls()

        Catch ex As Exception
            Debug.WriteLine("加载设置失败: " & ex.Message)
        End Try
    End Sub

    Private _configManager As ConfigManager

    ' 加载预配置列表
    Private Sub LoadPresetList()
        Try
            If _configManager Is Nothing Then
                _configManager = New ConfigManager()
            End If
            cmbPreset.Items.Clear()
            Dim presets As List(Of String) = _configManager.GetPresetNames()
            For Each presetName As String In presets
                cmbPreset.Items.Add(presetName)
            Next
        Catch ex As Exception
            Logger.GetInstance().Warning("加载预配置列表失败: " & ex.Message)
        End Try
    End Sub

    ' 预配置下拉框选择事件 - 加载选中的预配置
    Private Sub CmbPreset_SelectedIndexChanged(sender As Object, e As EventArgs) Handles cmbPreset.SelectedIndexChanged
        Try
            If cmbPreset.SelectedItem Is Nothing Then Return

            Dim presetName As String = cmbPreset.SelectedItem.ToString()

            If _configManager Is Nothing Then
                _configManager = New ConfigManager()
            End If

            If Not _configManager.PresetExists(presetName) Then
                MessageBox.Show($"找不到预配置文件: {presetName}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
                Return
            End If

            Dim config As Dictionary(Of String, String) = _configManager.LoadPreset(presetName)
            If config Is Nothing OrElse config.Count = 0 Then
                MessageBox.Show($"预配置 '{presetName}' 加载失败", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
                Return
            End If

            ApplyPresetConfig(config)
            MessageBox.Show($"预配置 '{presetName}' 已加载", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)
        Catch ex As Exception
            MessageBox.Show("加载预配置失败: " & ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    ' 应用预配置到控件
    Private Sub ApplyPresetConfig(config As Dictionary(Of String, String))
        Try
            ' 打印机（支持 PlotterName 和 PlotDevice 两种键名）
            Dim plotterName As String = ""
            If config.ContainsKey("PlotterName") AndAlso Not String.IsNullOrEmpty(config("PlotterName")) Then
                plotterName = config("PlotterName")
            ElseIf config.ContainsKey("PlotDevice") AndAlso Not String.IsNullOrEmpty(config("PlotDevice")) Then
                plotterName = config("PlotDevice")
            End If
            If Not String.IsNullOrEmpty(plotterName) AndAlso cmbPlotDevice.Items.Contains(plotterName) Then
                cmbPlotDevice.SelectedItem = plotterName
            End If

            ' 纸张尺寸
            If config.ContainsKey("PaperSize") AndAlso Not String.IsNullOrEmpty(config("PaperSize")) Then
                If cmbPaperSize.Items.Contains(config("PaperSize")) Then
                    cmbPaperSize.SelectedItem = config("PaperSize")
                End If
            End If

            ' 打印样式
            If config.ContainsKey("PlotStyle") AndAlso Not String.IsNullOrEmpty(config("PlotStyle")) Then
                If cmbPlotStyle.Items.Contains(config("PlotStyle")) Then
                    cmbPlotStyle.SelectedItem = config("PlotStyle")
                End If
            End If

            ' 打印份数
            If config.ContainsKey("Copies") Then
                txtCopies.Text = config("Copies")
            End If

            ' 逐份打印
            If config.ContainsKey("Collate") Then
                chkPlotByCopy.Checked = Boolean.Parse(config("Collate"))
            End If

            ' 自动旋转
            If config.ContainsKey("AutoRotate") Then
                chkAutoRotate.Checked = Boolean.Parse(config("AutoRotate"))
            End If

            ' 自动居中
            If config.ContainsKey("AutoCenter") Then
                Dim autoCenter As Boolean = Boolean.Parse(config("AutoCenter"))
                radAutoCenter.Checked = autoCenter
                radOffset.Checked = Not autoCenter
                UpdateOffsetControls()
            End If

            ' 偏移X
            If config.ContainsKey("OffsetX") Then
                txtOffsetX.Text = config("OffsetX")
            End If

            ' 偏移Y
            If config.ContainsKey("OffsetY") Then
                txtOffsetY.Text = config("OffsetY")
            End If

            ' 名称模板
            If config.ContainsKey("NameTemplate") Then
                txtNameTemplate.Text = config("NameTemplate")
            End If

            ' 保存路径
            If config.ContainsKey("SavePath") Then
                txtSavePath.Text = config("SavePath")
            End If
        Catch ex As Exception
            Debug.WriteLine("应用预配置失败: " & ex.Message)
        End Try
    End Sub

    Private Sub BtnBrowsePath_Click(sender As Object, e As EventArgs) Handles btnBrowsePath.Click
        Using fbd As New FolderBrowserDialog()
            fbd.Description = "选择保存路径"
            If fbd.ShowDialog() = DialogResult.OK Then
                txtSavePath.Text = fbd.SelectedPath
            End If
        End Using
    End Sub

    Private Sub BtnSelectDrawing_Click(sender As Object, e As EventArgs) Handles btnSelectDrawing.Click
        Try
            ' 检查是否有活动的 AutoCAD 文档
            Dim doc As Document = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument
            If doc Is Nothing Then
                MessageBox.Show("请先打开一个AutoCAD文档", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            Dim blockName As String = txtBlockName.Text.Trim()
            Dim layerName As String = txtLayerName.Text.Trim()

            ' 如果勾选自动选择，使用通配符匹配所有
            If chkAutoSelect.Checked Then
                blockName = "*"
                layerName = "*"
            Else
                ' 只有输入为空时才使用通配符，保留用户输入的默认值
                If String.IsNullOrEmpty(blockName) Then
                    blockName = "*"
                End If
                If String.IsNullOrEmpty(layerName) Then
                    layerName = "*"
                End If
            End If

            ' 创建临时 PlotSettings 对象来传递 TraverseLayouts 设置
            Dim settings As New BatchPlotEngine.PlotSettings()
            settings.TraverseLayouts = chkTraverseLayouts.Checked

            ' 根据图框形式选择识别方式
            If radBlock.Checked Then
                ' 图块形式：根据图块名和图层名过滤识别
                _frames = _plotEngine.DetectFrames(blockName, layerName, settings)
            ElseIf radPolyline.Checked Then
                ' 多段线形式：根据图层名过滤识别
                _frames = _plotEngine.DetectFramesByPolyline(layerName, settings)
            ElseIf radLine.Checked Then
                ' 直线形式：根据图层名过滤识别
                _frames = _plotEngine.DetectFramesByLine(layerName, settings)
            End If

            lblSelectedCount.Text = $"选中图纸: {_frames.Count}"

            ' 根据是否选取到图框来设置亮显按钮的可用性
            btnHighlight.Enabled = (_frames.Count > 0)

            If _frames.Count > 0 Then
                Dim frameType As String = If(radBlock.Checked, "图块", If(radPolyline.Checked, "多段线", "直线"))
                MessageBox.Show($"成功识别 {_frames.Count} 个{frameType}图框", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Else
                Dim msg As String = "未识别到图框，请检查以下设置:" & vbCrLf & vbCrLf
                If radBlock.Checked Then
                    msg &= "1. 图框形式: 图块" & vbCrLf
                    msg &= "2. 当前图块名过滤: " & blockName & vbCrLf
                    msg &= "3. 当前图层名过滤: " & layerName & vbCrLf & vbCrLf
                    msg &= "建议: 尝试将图块名设为 * 以匹配所有图块"
                ElseIf radPolyline.Checked Then
                    msg &= "1. 图框形式: 多段线" & vbCrLf
                    msg &= "2. 当前图层名过滤: " & layerName & vbCrLf & vbCrLf
                    msg &= "建议: 尝试将图层名设为 * 以匹配所有图层"
                Else
                    msg &= "1. 图框形式: 直线" & vbCrLf
                    msg &= "2. 当前图层名过滤: " & layerName & vbCrLf & vbCrLf
                    msg &= "建议: 尝试将图层名设为 * 以匹配所有图层"
                End If
                MessageBox.Show(msg, "未识别到图框", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            End If

        Catch ex As Exception
            MessageBox.Show("识别图框失败: " & ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub BtnHighlight_Click(sender As Object, e As EventArgs) Handles btnHighlight.Click
        Try
            If _frames Is Nothing OrElse _frames.Count = 0 Then
                MessageBox.Show("请先选择要处理的图纸", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            _plotEngine.HighlightFrames(_frames)
            MessageBox.Show($"已高亮显示 {_frames.Count} 个图框", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)

        Catch ex As Exception
            MessageBox.Show("亮显失败: " & ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub BtnBoundingBox_Click(sender As Object, e As EventArgs) Handles btnBoundingBox.Click
        Try
            Dim doc As Document = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument
            If doc Is Nothing Then
                MessageBox.Show("请先打开一个AutoCAD文档", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            ' 隐藏当前对话框
            Hide()

            ' 使用文档锁执行命令
            Using lock As DocumentLock = doc.LockDocument()
                ' 发送命令
                doc.SendStringToExecute("BWK ", True, False, True)
            End Using

            ' 使用定时器检测命令是否真正结束
            ' 包围框命令会打开对话框，需要检测对话框关闭后再等待一段时间确保命令完成
            Dim checkInterval As Integer = 500 ' 500毫秒检查一次
            Dim maxWaitTime As Integer = 60000 ' 最大等待60秒
            Dim elapsedTime As Integer = 0
            Dim dialogWasDetected As Boolean = False
            Dim dialogClosedTime As Integer = 0

            Dim timer As New Timer()
            timer.Interval = checkInterval
            AddHandler timer.Tick, Sub(s, args)
                                       Try
                                           elapsedTime += checkInterval

                                           ' 查找包围框对话框（如果有的话）
                                           Dim bwkDialog As IntPtr = FindBoundingBoxDialog()

                                           If bwkDialog <> IntPtr.Zero Then
                                               ' 检测到对话框已打开
                                               dialogWasDetected = True
                                           ElseIf dialogWasDetected Then
                                               ' 对话框曾经打开过，现在已关闭
                                               If dialogClosedTime = 0 Then
                                                   ' 记录对话框关闭时间
                                                   dialogClosedTime = elapsedTime
                                               ElseIf elapsedTime - dialogClosedTime >= 1000 Then
                                                   ' 对话框关闭后等待1秒再恢复主窗体
                                                   timer.Stop()
                                                   timer.Dispose()
                                                   ShowMainForm()
                                                   Return
                                               End If
                                           ElseIf elapsedTime > 3000 Then
                                               ' 3秒后还没有检测到对话框，直接恢复
                                               timer.Stop()
                                               timer.Dispose()
                                               ShowMainForm()
                                               Return
                                           End If

                                           ' 超时检查
                                           If elapsedTime >= maxWaitTime Then
                                               timer.Stop()
                                               timer.Dispose()
                                               ShowMainForm()
                                           End If
                                       Catch ex As Exception
                                           timer.Stop()
                                           timer.Dispose()
                                           ShowMainForm()
                                       End Try
                                   End Sub
            timer.Start()

        Catch ex As Exception
            MessageBox.Show("执行包围框命令失败: " & ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Show()
        End Try
    End Sub

    ' 查找包围框对话框窗口
    Private Function FindBoundingBoxDialog() As IntPtr
        ' 包围框对话框的可能标题
        Dim hwnd As IntPtr = FindWindow(Nothing, "包围框")
        If hwnd = IntPtr.Zero Then
            hwnd = FindWindow(Nothing, "Bounding Box")
        End If
        If hwnd = IntPtr.Zero Then
            hwnd = FindWindow(Nothing, "BWK")
        End If
        Return hwnd
    End Function

    Private Sub BtnMultiDoc_Click(sender As Object, e As EventArgs) Handles btnMultiDoc.Click
        ' 传入已选择的文件列表，保持上次的选择
        Using frm As New MultiDocForm(_multiDocFiles)
            If frm.ShowDialog() = DialogResult.OK Then
                _multiDocFiles = frm.SelectedFiles
                _useSingleDocStyle = frm.UseSingleDocStyle
                If _multiDocFiles IsNot Nothing AndAlso _multiDocFiles.Count > 0 Then
                    Dim styleMsg As String = If(_useSingleDocStyle, "（使用各文档内部打印样式）", "（使用主窗口打印样式设置）")
                    MessageBox.Show($"已选择 {_multiDocFiles.Count} 个文件，点击确定按钮开始多文档打印。{styleMsg}", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)
                End If
            End If
        End Using
    End Sub

    Private Sub BtnPreview_Click(sender As Object, e As EventArgs) Handles btnPreview.Click
        Try
            If _frames Is Nothing OrElse _frames.Count = 0 Then
                MessageBox.Show("请先选择要处理的图纸", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            Dim doc As Document = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument
            If doc Is Nothing Then
                MessageBox.Show("请先打开一个AutoCAD文档", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            Dim settings As New BatchPlotEngine.PlotSettings()
            settings.PlotterName = If(cmbPlotDevice.SelectedItem IsNot Nothing, cmbPlotDevice.SelectedItem.ToString(), "")
            settings.PaperSize = If(cmbPaperSize.SelectedItem IsNot Nothing, cmbPaperSize.SelectedItem.ToString(), "")
            settings.PlotStyle = If(cmbPlotStyle.SelectedItem IsNot Nothing, cmbPlotStyle.SelectedItem.ToString(), "")

            ' 打印比例
            If radAutoScale.Checked Then
                settings.PlotScale = BatchPlotEngine.PlotScaleType.AutoScale
            ElseIf radFitToPaper.Checked Then
                settings.PlotScale = BatchPlotEngine.PlotScaleType.FitToPaper
            Else
                settings.PlotScale = BatchPlotEngine.PlotScaleType.FixedScale
                settings.FixedScale = CDbl(Val(txtScale.Text))
            End If

            ' 图纸定位
            settings.AutoRotate = chkAutoRotate.Checked
            settings.Reverse = chkReverse.Checked
            ' IsLandscape 只在非自动旋转时使用，这里默认使用横向
            ' 实际方向由用户选择的图纸尺寸决定
            settings.IsLandscape = True
            settings.AutoCenter = radAutoCenter.Checked
            If Not radAutoCenter.Checked Then
                settings.OffsetX = CDbl(Val(txtOffsetX.Text))
                settings.OffsetY = CDbl(Val(txtOffsetY.Text))
            End If

            ' 将主窗口置后，让AutoCAD窗口显示在前面
            SendToBack()
            
            ' 使用异步执行预览，避免阻塞UI
            Task.Run(
                Async Function()
                    Try
                        ' 执行预览（在后台线程）
                        Await Task.Delay(100) ' 给窗口一点时间置后
                        
                        ' 使用Invoke在主线程执行预览
                        Invoke(
                            Sub()
                                Try
                                    _plotEngine.PreviewPlot(_frames(0), settings)
                                Catch ex As Exception
                                    MessageBox.Show("预览失败: " & ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
                                End Try
                            End Sub)
                        
                        ' 等待预览完成
                        Await Task.Delay(500)
                        
                        ' 在主线程恢复窗口
                        Invoke(
                            Sub()
                                BringToFront()
                                Activate()
                            End Sub)
                    Catch ex As Exception
                        ' 在主线程显示错误
                        Invoke(
                            Sub()
                                MessageBox.Show("预览失败: " & ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
                            End Sub)
                    End Try
                End Function)

        Catch ex As Exception
            MessageBox.Show("预览失败: " & ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub BtnApply_Click(sender As Object, e As EventArgs) Handles btnApply.Click
        Try
            SaveSettings()
            MessageBox.Show("设置已应用", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)
        Catch ex As Exception
            MessageBox.Show("应用设置失败: " & ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub BtnOK_Click(sender As Object, e As EventArgs) Handles btnOK.Click
        Try
            ' 检查是否有选中的多文档文件
            If _multiDocFiles IsNot Nothing AndAlso _multiDocFiles.Count > 0 Then
                ' 执行多文档打印
                ExecuteMultiDocPlot()
                Return
            End If

            Dim doc As Document = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument
            If doc Is Nothing Then
                MessageBox.Show("请先打开一个AutoCAD文档", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            Dim settings As New BatchPlotEngine.PlotSettings()
            settings.PlotterName = If(cmbPlotDevice.SelectedItem IsNot Nothing, cmbPlotDevice.SelectedItem.ToString(), "")
            settings.PaperSize = If(cmbPaperSize.SelectedItem IsNot Nothing, cmbPaperSize.SelectedItem.ToString(), "")
            settings.PlotStyle = If(cmbPlotStyle.SelectedItem IsNot Nothing, cmbPlotStyle.SelectedItem.ToString(), "")
            settings.OutputPath = GetActualOutputPath()
            settings.IsLandscape = chkAutoRotate.Checked
            settings.Copies = CInt(Val(txtCopies.Text))
            settings.Collate = chkPlotByCopy.Checked
            settings.NameTemplate = txtNameTemplate.Text
            settings.UseDWGName = chkUseDWGName.Checked
            ' 重名文件处理：覆盖或自动顺延
            If radOverwrite.Checked Then
                settings.DuplicateHandling = BatchPlotEngine.DuplicateFileHandling.Overwrite
            Else
                settings.DuplicateHandling = BatchPlotEngine.DuplicateFileHandling.AutoRename
            End If

            ' 打印比例
            If radAutoScale.Checked Then
                settings.PlotScale = BatchPlotEngine.PlotScaleType.AutoScale
            ElseIf radFitToPaper.Checked Then
                settings.PlotScale = BatchPlotEngine.PlotScaleType.FitToPaper
            Else
                settings.PlotScale = BatchPlotEngine.PlotScaleType.FixedScale
                settings.FixedScale = CDbl(Val(txtScale.Text))
            End If

            ' 图纸定位
            settings.AutoRotate = chkAutoRotate.Checked
            settings.Reverse = chkReverse.Checked
            settings.AutoCenter = radAutoCenter.Checked
            If Not radAutoCenter.Checked Then
                settings.OffsetX = CDbl(Val(txtOffsetX.Text))
                settings.OffsetY = CDbl(Val(txtOffsetY.Text))
            End If

            ' 打印顺序
            Dim orderType As BatchPlotEngine.PlotOrderType
            If radLeftRight.Checked Then
                orderType = BatchPlotEngine.PlotOrderType.LeftRight
            ElseIf radTopBottom.Checked Then
                orderType = BatchPlotEngine.PlotOrderType.TopBottom
            Else
                orderType = BatchPlotEngine.PlotOrderType.SelectOrder
            End If
            _plotEngine.OrderFrames(_frames, orderType, chkReverseOrder.Checked)

            ' 输出类型
            If radPlotToFile.Checked Then
                settings.OutputType = BatchPlotEngine.OutputFileType.PDF
            End If

            ' 打印区域
            If radDirectPlot.Checked Then
                settings.PlotArea = BatchPlotEngine.PlotAreaType.Window
            ElseIf radPlotExistingLayout.Checked Then
                settings.PlotArea = BatchPlotEngine.PlotAreaType.Layout
            Else
                settings.PlotArea = BatchPlotEngine.PlotAreaType.Extents
            End If

            ' 打印戳记设置
            settings.PlotStamp = chkPlotStamp.Checked

            ' 图块自匹配设置（仅在图框形式为图块时生效）
            settings.AutoMatchBlock = radBlock.Checked AndAlso chkAutoMatch.Checked
            settings.BlockNamePattern = txtBlockName.Text.Trim()

            ' 遍历布局设置
            settings.TraverseLayouts = chkTraverseLayouts.Checked

            Dim progressCallback As Action(Of Integer, String) =
                Sub(progress, message)
                    Debug.WriteLine($"{progress}%: {message}")
                End Sub

            ' 根据输出选项执行不同操作
            If radDirectPlot.Checked Then
                ' 直接批量打印
                If _frames Is Nothing OrElse _frames.Count = 0 Then
                    MessageBox.Show("请先选择要处理的图纸", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)
                    Return
                End If
                ' 设置直接打印模式
                settings.DirectPrint = True
                _plotEngine.ExecuteBatchPlot(_frames, settings, progressCallback)
                MessageBox.Show("批量打印任务已提交！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)

            ElseIf radPlotToFile.Checked Then
                ' 打印到文件
                If _frames Is Nothing OrElse _frames.Count = 0 Then
                    MessageBox.Show("请先选择要处理的图纸", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)
                    Return
                End If
                ' 设置输出路径
                Dim outputPath As String = GetActualOutputPath()
                If String.IsNullOrEmpty(outputPath) Then
                    MessageBox.Show("请先设置保存路径", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)
                    Return
                End If
                settings.OutputPath = outputPath
                ' 设置打印到文件模式
                settings.DirectPrint = False
                _plotEngine.ExecuteBatchPlot(_frames, settings, progressCallback)
                MessageBox.Show("打印到文件任务已提交！文件将保存到: " & outputPath, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)

            ElseIf radSplitArchive.Checked Then
                ' 图纸拆分归档
                If _frames Is Nothing OrElse _frames.Count = 0 Then
                    MessageBox.Show("请先选择要处理的图纸", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)
                    Return
                End If
                Dim archivePath As String = GetActualOutputPath()
                ' 传递重名文件处理设置
                Dim dupHandling As BatchPlotEngine.DuplicateFileHandling
                If radOverwrite.Checked Then
                    dupHandling = BatchPlotEngine.DuplicateFileHandling.Overwrite
                Else
                    dupHandling = BatchPlotEngine.DuplicateFileHandling.AutoRename
                End If
                _plotEngine.SplitAndArchive(_frames, archivePath, txtNameTemplate.Text, chkUseDWGName.Checked, progressCallback, dupHandling)
                MessageBox.Show("图纸拆分归档任务已提交！文件将保存到: " & archivePath, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)

            ElseIf radBatchCreateLayout.Checked Then
                ' 批量生成布局
                If _frames Is Nothing OrElse _frames.Count = 0 Then
                    MessageBox.Show("请先选择要处理的图纸", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)
                    Return
                End If
                ' 传递模型空间线型比例设置和重名文件处理设置
                Dim dupHandling2 As BatchPlotEngine.DuplicateFileHandling
                If radOverwrite.Checked Then
                    dupHandling2 = BatchPlotEngine.DuplicateFileHandling.Overwrite
                Else
                    dupHandling2 = BatchPlotEngine.DuplicateFileHandling.AutoRename
                End If
                _plotEngine.BatchCreateLayouts(_frames, progressCallback, chkModelSpaceScale.Checked, dupHandling2)
                MessageBox.Show("批量生成布局任务已提交！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)

            ElseIf radPlotExistingLayout.Checked Then
                ' 打印已有布局
                _plotEngine.PlotExistingLayouts(settings, progressCallback)
                MessageBox.Show("打印已有布局任务已提交！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)
            End If

        Catch ex As Exception
            MessageBox.Show("操作失败: " & ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub SaveSettings()
        Debug.WriteLine("保存设置...")
        Debug.WriteLine($"打印设备: {cmbPlotDevice.SelectedItem}")
        Debug.WriteLine($"图纸尺寸: {cmbPaperSize.SelectedItem}")
        Debug.WriteLine($"打印样式: {cmbPlotStyle.SelectedItem}")
        Debug.WriteLine($"保存路径: {txtSavePath.Text}")
    End Sub

    ' 执行多文档打印
    Private Sub ExecuteMultiDocPlot()
        Try
            ' 获取主窗口的设置
            Dim settings As New BatchPlotEngine.PlotSettings()

            ' 如果不使用单文档内部打印样式，则应用主窗口的设置
            If Not _useSingleDocStyle Then
                settings.PlotterName = If(cmbPlotDevice.SelectedItem IsNot Nothing, cmbPlotDevice.SelectedItem.ToString(), "")
                settings.PaperSize = If(cmbPaperSize.SelectedItem IsNot Nothing, cmbPaperSize.SelectedItem.ToString(), "")
                settings.PlotStyle = If(cmbPlotStyle.SelectedItem IsNot Nothing, cmbPlotStyle.SelectedItem.ToString(), "")
                settings.IsLandscape = chkAutoRotate.Checked

                ' 打印比例
                If radAutoScale.Checked Then
                    settings.PlotScale = BatchPlotEngine.PlotScaleType.AutoScale
                ElseIf radFitToPaper.Checked Then
                    settings.PlotScale = BatchPlotEngine.PlotScaleType.FitToPaper
                Else
                    settings.PlotScale = BatchPlotEngine.PlotScaleType.FixedScale
                    settings.FixedScale = CDbl(Val(txtScale.Text))
                End If

                ' 图纸定位
                settings.AutoRotate = chkAutoRotate.Checked
                settings.Reverse = chkReverse.Checked
                settings.AutoCenter = radAutoCenter.Checked
                If Not radAutoCenter.Checked Then
                    settings.OffsetX = CDbl(Val(txtOffsetX.Text))
                    settings.OffsetY = CDbl(Val(txtOffsetY.Text))
                End If

                ' 打印戳记设置
                settings.PlotStamp = chkPlotStamp.Checked
            End If

            ' 以下设置不受"使用单文档内部打印样式"影响
            settings.OutputPath = GetActualOutputPath()
            settings.Copies = CInt(Val(txtCopies.Text))
            settings.Collate = chkPlotByCopy.Checked
            settings.NameTemplate = txtNameTemplate.Text
            settings.UseDWGName = chkUseDWGName.Checked

            ' 打印比例（始终根据界面设置）
            If radAutoScale.Checked Then
                settings.PlotScale = BatchPlotEngine.PlotScaleType.AutoScale
            ElseIf radFitToPaper.Checked Then
                settings.PlotScale = BatchPlotEngine.PlotScaleType.FitToPaper
            Else
                settings.PlotScale = BatchPlotEngine.PlotScaleType.FixedScale
                settings.FixedScale = CDbl(Val(txtScale.Text))
            End If

            ' 图纸定位（始终根据界面设置）
            settings.AutoRotate = chkAutoRotate.Checked
            settings.AutoCenter = radAutoCenter.Checked
            If Not radAutoCenter.Checked Then
                settings.OffsetX = CDbl(Val(txtOffsetX.Text))
                settings.OffsetY = CDbl(Val(txtOffsetY.Text))
            End If

            ' 重名文件处理：覆盖或自动顺延
            If radOverwrite.Checked Then
                settings.DuplicateHandling = BatchPlotEngine.DuplicateFileHandling.Overwrite
            Else
                settings.DuplicateHandling = BatchPlotEngine.DuplicateFileHandling.AutoRename
            End If

            ' 输出类型
            If radPlotToFile.Checked Then
                settings.OutputType = BatchPlotEngine.OutputFileType.PDF
            End If

            ' 遍历布局设置
            settings.TraverseLayouts = chkTraverseLayouts.Checked

            Dim progressCallback As Action(Of Integer, String) =
                Sub(progress, message)
                    ' 进度回调
                End Sub

            ' 遍历每个文件执行打印
            Dim totalFiles As Integer = _multiDocFiles.Count
            For i = 0 To totalFiles - 1
                Dim filePath As String = _multiDocFiles(i)
                Dim fileName As String = Path.GetFileName(filePath)

                ' 打开文件
                Dim doc As Document = OpenDocument(filePath)
                If doc Is Nothing Then
                    Continue For
                End If

                ' 等待文件完全加载
                Threading.Thread.Sleep(1000)
                Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument = doc
                Threading.Thread.Sleep(500)

                ' 更新PlotEngine的文档引用
                _plotEngine.UpdateCurrentDocument()

                ' 识别图框
                Dim blockName As String = txtBlockName.Text.Trim()
                Dim layerName As String = txtLayerName.Text.Trim()

                ' 如果勾选自动选择，使用通配符匹配所有
                If chkAutoSelect.Checked Then
                    blockName = "*"
                    layerName = "*"
                Else
                    ' 只有输入为空时才使用通配符，保留用户输入的默认值
                    If String.IsNullOrEmpty(blockName) Then
                        blockName = "*"
                    End If
                    If String.IsNullOrEmpty(layerName) Then
                        layerName = "*"
                    End If
                End If

                Dim frames As List(Of BatchPlotEngine.FrameInfo) = Nothing
                If radBlock.Checked Then
                    ' 按图块识别
                    frames = _plotEngine.DetectFrames(blockName, layerName, settings)
                ElseIf radPolyline.Checked Then
                    ' 按多段线识别
                    frames = _plotEngine.DetectFramesByPolyline(layerName, settings)
                ElseIf radLine.Checked Then
                    ' 按直线识别
                    frames = _plotEngine.DetectFramesByLine(layerName, settings)
                End If
                
                If frames Is Nothing OrElse frames.Count = 0 Then
                    Continue For
                End If

                ' 打印顺序
                Dim orderType As BatchPlotEngine.PlotOrderType
                If radLeftRight.Checked Then
                    orderType = BatchPlotEngine.PlotOrderType.LeftRight
                ElseIf radTopBottom.Checked Then
                    orderType = BatchPlotEngine.PlotOrderType.TopBottom
                Else
                    orderType = BatchPlotEngine.PlotOrderType.SelectOrder
                End If
                _plotEngine.OrderFrames(frames, orderType, chkReverseOrder.Checked)

                ' 执行打印
                Try
                    ' 批量打印多文件使用DSD方式
                    settings.DirectPrint = False
                    _plotEngine.ExecuteBatchPlot(frames, settings, progressCallback)
                Catch ex As Exception
                    ' 打印失败，继续下一个文件
                End Try

                ' 更新进度
                progressCallback?.Invoke(CInt(((i + 1) / totalFiles) * 100), $"已完成: {fileName}")
            Next

            MessageBox.Show($"多文档打印任务已完成！共处理 {totalFiles} 个文件。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)

            ' 清空多文档文件列表
            _multiDocFiles.Clear()

        Catch ex As Exception
            MessageBox.Show("多文档打印失败: " & ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    ' 打开文档
    Private Function OpenDocument(filePath As String) As Document
        Try
            Dim docMgr As DocumentCollection = 
                Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager

            ' 检查文件是否已经打开
            For Each doc As Document In docMgr
                If doc.Name.Equals(filePath, StringComparison.OrdinalIgnoreCase) Then
                    docMgr.MdiActiveDocument = doc
                    Return doc
                End If
            Next

            ' 打开新文件
            Return docMgr.Open(filePath, False)
        Catch ex As Exception
            Debug.WriteLine($"打开文档失败: {ex.Message}")
            Return Nothing
        End Try
    End Function

    Private Sub BtnCancel_Click(sender As Object, e As EventArgs) Handles btnCancel.Click
        Close()
    End Sub

    Private Sub BtnContact_Click(sender As Object, e As EventArgs) Handles btnContact.Click
        Using frm As New ContactForm()
            frm.ShowDialog()
        End Using
    End Sub

    ' ========== 控件联动逻辑 ==========

    ' 偏移选项联动
    Private Sub RadOffset_CheckedChanged(sender As Object, e As EventArgs) Handles radOffset.CheckedChanged
        UpdateOffsetControls()
    End Sub

    Private Sub UpdateOffsetControls()
        Dim enabled As Boolean = radOffset.Checked
        txtOffsetX.Enabled = enabled
        txtOffsetY.Enabled = enabled
        lblOffsetX.Enabled = enabled
        lblOffsetY.Enabled = enabled
    End Sub

    ' 固定比例联动
    Private Sub RadFixedScale_CheckedChanged(sender As Object, e As EventArgs) Handles radFixedScale.CheckedChanged
        UpdateScaleControls()
    End Sub

    Private Sub RadAutoScale_CheckedChanged(sender As Object, e As EventArgs) Handles radAutoScale.CheckedChanged
        UpdateScaleControls()
    End Sub

    Private Sub RadFitToPaper_CheckedChanged(sender As Object, e As EventArgs) Handles radFitToPaper.CheckedChanged
        UpdateScaleControls()
    End Sub

    Private Sub UpdateScaleControls()
        txtScale.Enabled = radFixedScale.Checked
    End Sub

    ' 自动选择联动 - 勾选时不禁用控件，只根据图框形式禁用相应控件
    Private Sub ChkAutoSelect_CheckedChanged(sender As Object, e As EventArgs) Handles chkAutoSelect.CheckedChanged
        UpdateFrameTypeControls()
    End Sub

    ' 图框形式变化时更新控件状态
    Private Sub RadFrameType_CheckedChanged(sender As Object, e As EventArgs) Handles radBlock.CheckedChanged, radPolyline.CheckedChanged, radLine.CheckedChanged
        UpdateFrameTypeControls()
    End Sub

    ' 根据图框形式更新控件启用状态
    Private Sub UpdateFrameTypeControls()
        ' 根据图框形式决定禁用哪些控件
        If radBlock.Checked Then
            ' 图块形式：禁用图层相关控件
            txtLayerName.Enabled = False
            btnSelectLayer.Enabled = False
            ' 启用图块相关控件
            txtBlockName.Enabled = True
            btnSelectBlock.Enabled = True
            btnSpecifyBlock.Enabled = True
        Else
            ' 多段线或直线形式：禁用图块相关控件
            txtBlockName.Enabled = False
            btnSelectBlock.Enabled = False
            btnSpecifyBlock.Enabled = False
            ' 启用图层相关控件
            txtLayerName.Enabled = True
            btnSelectLayer.Enabled = True
        End If
    End Sub

    ' 获取实际输出路径
    Private Function GetActualOutputPath() As String
        Try
            Dim doc As Document = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument
            If doc Is Nothing Then
                Return txtSavePath.Text
            End If

            Dim dwgPath As String = doc.Name
            Dim dwgDir As String = Path.GetDirectoryName(dwgPath)
            Dim dwgName As String = Path.GetFileNameWithoutExtension(dwgPath)

            If radSourcePathPDF.Checked Then
                ' 源文件路径\PDF
                Dim pdfPath As String = Path.Combine(dwgDir, "PDF")
                If chkUseDWGName.Checked Then
                    pdfPath = Path.Combine(pdfPath, dwgName)
                End If
                Return pdfPath
            ElseIf radSourcePathDWF.Checked Then
                ' 源文件路径\DWF
                Dim dwfPath As String = Path.Combine(dwgDir, "DWF")
                If chkUseDWGName.Checked Then
                    dwfPath = Path.Combine(dwfPath, dwgName)
                End If
                Return dwfPath
            Else
                ' 其他路径
                If chkUseDWGName.Checked AndAlso Not String.IsNullOrEmpty(txtSavePath.Text) Then
                    Return Path.Combine(txtSavePath.Text, dwgName)
                End If
                Return txtSavePath.Text
            End If
        Catch ex As Exception
            Debug.WriteLine("获取实际输出路径失败: " & ex.Message)
            Return txtSavePath.Text
        End Try
    End Function

    ' 保存路径联动
    Private Sub RadOtherPath_CheckedChanged(sender As Object, e As EventArgs) Handles radOtherPath.CheckedChanged
        UpdateSavePathControls()
    End Sub

    Private Sub UpdateSavePathControls()
        Dim enabled As Boolean = radOtherPath.Checked
        txtSavePath.Enabled = enabled
        btnBrowsePath.Enabled = enabled
        lblSavePath.Enabled = enabled
    End Sub

    ' 打印设备切换时刷新纸张列表并更新特性按钮状态
    Private Sub CmbPlotDevice_SelectedIndexChanged(sender As Object, e As EventArgs) Handles cmbPlotDevice.SelectedIndexChanged
        Try
            If _plotEngine Is Nothing Then Return
            Dim selectedDevice As String = If(cmbPlotDevice.SelectedItem IsNot Nothing, cmbPlotDevice.SelectedItem.ToString(), "")
            If String.IsNullOrEmpty(selectedDevice) OrElse selectedDevice = "无" Then
                btnDeviceProps.Enabled = False
                Return
            End If

            ' 更新特性按钮状态：只有.pc3设备才可用
            btnDeviceProps.Enabled = selectedDevice.EndsWith(".pc3", StringComparison.OrdinalIgnoreCase)

            ' 如果处于打印到文件模式，更新扩展名
            If radPlotToFile.Checked Then
                UpdateExtensionByPlotter()
            End If

            Dim papers As List(Of String) = _plotEngine.GetPaperList(selectedDevice)
            cmbPaperSize.Items.Clear()
            For Each paper As String In papers
                cmbPaperSize.Items.Add(paper)
            Next
            If cmbPaperSize.Items.Count > 0 Then
                cmbPaperSize.SelectedIndex = 0
            End If
        Catch ex As Exception
            Debug.WriteLine("刷新纸张列表失败: " & ex.Message)
        End Try
    End Sub

    ' ========== 按钮功能实现 ==========

    ' 修改/保存预配置 - 打开预设配置管理窗口
    Private Sub BtnModifyPreset_Click(sender As Object, e As EventArgs) Handles btnModifyPreset.Click
        Try
            Using frm As New PresetConfigForm(_plotEngine)
                If frm.ShowDialog() = DialogResult.OK Then
                    ' 刷新预设下拉列表
                    RefreshPresetList()
                End If
            End Using
        Catch ex As Exception
            MessageBox.Show("打开预设配置窗口失败: " & ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    ' 刷新预设下拉列表（从配置管理窗口返回后调用）
    Private Sub RefreshPresetList()
        Try
            Dim currentSelection As String = If(cmbPreset.SelectedItem IsNot Nothing, cmbPreset.SelectedItem.ToString(), "")
            cmbPreset.Items.Clear()

            If _configManager Is Nothing Then
                _configManager = New ConfigManager()
            End If

            Dim presets As List(Of String) = _configManager.GetPresetNames()
            For Each preset As String In presets
                cmbPreset.Items.Add(preset)
            Next

            ' 恢复之前的选择
            If Not String.IsNullOrEmpty(currentSelection) AndAlso cmbPreset.Items.Contains(currentSelection) Then
                cmbPreset.SelectedItem = currentSelection
            End If
        Catch ex As Exception
            MessageBox.Show("刷新预设列表失败: " & ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    ' 设备特性 - 打开绘图仪配置编辑器（仅对.pc3设备有效）
    Private Sub BtnDeviceProps_Click(sender As Object, e As EventArgs) Handles btnDeviceProps.Click
        Try
            Dim selectedDevice As String = If(cmbPlotDevice.SelectedItem IsNot Nothing, cmbPlotDevice.SelectedItem.ToString(), "")
            If String.IsNullOrEmpty(selectedDevice) Then
                MessageBox.Show("请先选择一个打印设备", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            ' 只有.pc3设备才能打开配置编辑器
            If Not selectedDevice.EndsWith(".pc3", StringComparison.OrdinalIgnoreCase) Then
                MessageBox.Show("当前设备不是绘图仪配置文件(.pc3)，无法编辑特性。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            ' 查找.pc3文件的完整路径
            Dim pc3Path As String = FindPlotterConfigPath(selectedDevice)
            If String.IsNullOrEmpty(pc3Path) OrElse Not File.Exists(pc3Path) Then
                MessageBox.Show($"未找到绘图仪配置文件: {selectedDevice}" & vbCrLf & "将打开绘图仪管理器。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)
                ' 找不到文件时打开绘图仪管理器文件夹
                OpenPlotterManager()
            Else
                ' 使用Process.Start直接打开.pc3文件
                Try
                    Process.Start(pc3Path)
                Catch ex As Exception
                    MessageBox.Show("无法打开绘图仪配置文件: " & ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
                End Try
            End If

        Catch ex As Exception
            MessageBox.Show("打开绘图仪配置编辑器失败: " & ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    ' 打开绘图仪管理器文件夹
    Private Sub OpenPlotterManager()
        Try
            Dim plottersPath As String = AutoCADPathResolver.GetPlottersPath()
            If Not String.IsNullOrEmpty(plottersPath) AndAlso Directory.Exists(plottersPath) Then
                Process.Start("explorer.exe", plottersPath)
                Return
            End If

            ' 如果找不到，打开应用数据文件夹
            Process.Start("explorer.exe", Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData))
        Catch ex As Exception
            MessageBox.Show("无法打开绘图仪管理器: " & ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    ' 查找绘图仪配置文件(.pc3)路径
    Private Function FindPlotterConfigPath(deviceName As String) As String
        Try
            Dim plottersPath As String = AutoCADPathResolver.GetPlottersPath()
            If Not String.IsNullOrEmpty(plottersPath) Then
                Dim filePath As String = Path.Combine(plottersPath, deviceName)
                If File.Exists(filePath) Then
                    Return filePath
                End If
            End If
        Catch ex As Exception
            Logger.GetInstance().Warning("查找绘图仪配置路径失败: " & ex.Message)
        End Try
        Return String.Empty
    End Function

    ' 编辑样式 - 打开当前选中样式的AutoCAD打印样式编辑器
    Private Sub BtnEditStyle_Click(sender As Object, e As EventArgs) Handles btnEditStyle.Click
        Try
            ' 获取当前选中的打印样式
            Dim selectedStyle As String = If(cmbPlotStyle.SelectedItem IsNot Nothing, cmbPlotStyle.SelectedItem.ToString(), "")
            If String.IsNullOrEmpty(selectedStyle) OrElse selectedStyle = "无" Then
                MessageBox.Show("请先选择一个打印样式", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            ' 查找打印样式文件路径
            Dim stylePath As String = FindPlotStylePath(selectedStyle)
            If String.IsNullOrEmpty(stylePath) OrElse Not File.Exists(stylePath) Then
                MessageBox.Show($"未找到打印样式文件: {selectedStyle}.ctb" & vbCrLf & "将打开打印样式管理器。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)
                ' 找不到文件时打开样式管理器文件夹
                OpenPlotStyleManager()
            Else
                ' 使用AutoCAD的打印样式编辑器打开.ctb文件
                Try
                    Dim styexePath As String = FindStyexePath()
                    If Not String.IsNullOrEmpty(styexePath) AndAlso File.Exists(styexePath) Then
                        ' 使用Styexe.exe打开CTB文件
                        Dim psi As New ProcessStartInfo(styexePath, $"""{stylePath}""")
                        psi.UseShellExecute = False
                        Process.Start(psi)
                    Else
                        ' 如果找不到Styexe.exe，尝试使用资源管理器打开文件夹并选中文件
                        Process.Start("explorer.exe", $"/select,""{stylePath}""")
                    End If
                Catch ex As Exception
                    MessageBox.Show("无法打开打印样式文件: " & ex.Message & vbCrLf & "请手动在打印样式管理器中编辑。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
                    OpenPlotStyleManager()
                End Try
            End If

        Catch ex As Exception
            MessageBox.Show("打开打印样式编辑器失败: " & ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    ' 查找AutoCAD打印样式编辑器(Styexe.exe)路径
    Private Function FindStyexePath() As String
        Return AutoCADPathResolver.GetStyexePath()
    End Function

    ' 打开打印样式管理器文件夹
    Private Sub OpenPlotStyleManager()
        Try
            Dim stylePath As String = AutoCADPathResolver.GetPlotStylesPath()
            If Not String.IsNullOrEmpty(stylePath) AndAlso Directory.Exists(stylePath) Then
                Process.Start("explorer.exe", stylePath)
                Return
            End If

            ' 如果找不到，打开应用数据文件夹
            Process.Start("explorer.exe", Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData))
        Catch ex As Exception
            MessageBox.Show("无法打开打印样式管理器: " & ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    ' 查找打印样式文件路径
    Private Function FindPlotStylePath(styleName As String) As String
        Try
            Dim stylePath As String = AutoCADPathResolver.GetPlotStylesPath()
            If Not String.IsNullOrEmpty(stylePath) AndAlso Directory.Exists(stylePath) Then
                Dim filePath As String = Path.Combine(stylePath, styleName & ".ctb")
                If File.Exists(filePath) Then
                    Return filePath
                End If
            End If
        Catch ex As Exception
            Logger.GetInstance().Warning("查找打印样式路径失败: " & ex.Message)
        End Try
        Return String.Empty
    End Function

    ' 打印戳记 - 直接打开打印戳记设置
    Private Sub BtnPlotStamp_Click(sender As Object, e As EventArgs) Handles btnPlotStamp.Click
        Try
            Dim doc As Document = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument
            If doc Is Nothing Then
                MessageBox.Show("请先打开一个AutoCAD文档", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            ' 隐藏当前对话框
            Hide()

            ' 使用文档锁执行命令
            Using lock As DocumentLock = doc.LockDocument()
                ' 发送命令
                doc.SendStringToExecute("_.PLOTSTAMP ", True, False, True)
            End Using

            ' 使用定时器检测打印戳记对话框是否关闭
            ' 打印戳记对话框的类名为 "AcDbPlotStampDlg" 或包含 "PlotStamp"
            Dim checkInterval As Integer = 500 ' 500毫秒检查一次
            Dim maxWaitTime As Integer = 60000 ' 最大等待60秒
            Dim elapsedTime As Integer = 0
            Dim dialogDetected As Boolean = False

            Dim timer As New Timer()
            timer.Interval = checkInterval
            AddHandler timer.Tick, Sub(s, args)
                                       Try
                                           elapsedTime += checkInterval

                                           ' 查找打印戳记对话框窗口
                                           Dim plotStampDialog As IntPtr = FindPlotStampDialog()

                                           If plotStampDialog <> IntPtr.Zero Then
                                               ' 检测到对话框已打开
                                               dialogDetected = True
                                           ElseIf dialogDetected Then
                                               ' 对话框曾经打开过，现在已关闭
                                               timer.Stop()
                                               timer.Dispose()
                                               ShowMainForm()
                                               Return
                                           End If

                                           ' 超时检查
                                           If elapsedTime >= maxWaitTime Then
                                               timer.Stop()
                                               timer.Dispose()
                                               ShowMainForm()
                                           End If
                                       Catch ex As Exception
                                           timer.Stop()
                                           timer.Dispose()
                                           ShowMainForm()
                                       End Try
                                   End Sub
            timer.Start()
        Catch ex As Exception
            MessageBox.Show("打开打印戳记设置失败: " & ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Show()
        End Try
    End Sub

    ' 查找打印戳记对话框窗口
    Private Function FindPlotStampDialog() As IntPtr
        ' 打印戳记对话框的可能标题或类名
        Dim hwnd As IntPtr = FindWindow(Nothing, "打印戳记")
        If hwnd = IntPtr.Zero Then
            hwnd = FindWindow(Nothing, "Plot Stamp")
        End If
        If hwnd = IntPtr.Zero Then
            hwnd = FindWindow("AcDbPlotStampDlg", Nothing)
        End If
        If hwnd = IntPtr.Zero Then
            ' 尝试查找包含 "PlotStamp" 的窗口类
            hwnd = FindWindowByClassPattern("PlotStamp")
        End If
        Return hwnd
    End Function

    ' 根据类名模式查找窗口
    Private Function FindWindowByClassPattern(pattern As String) As IntPtr
        Dim result As IntPtr = IntPtr.Zero
        EnumWindows(Function(hWnd As IntPtr, lParam As IntPtr)
                        Dim className As New System.Text.StringBuilder(256)
                        GetClassName(hWnd, className, className.Capacity)
                        If className.ToString().Contains(pattern) Then
                            result = hWnd
                            Return False ' 停止枚举
                        End If
                        Return True ' 继续枚举
                    End Function, IntPtr.Zero)
        Return result
    End Function

    ' 安全显示主窗体
    Private Sub ShowMainForm()
        Try
            If Me.InvokeRequired Then
                Me.Invoke(New Action(Sub() Show()))
            Else
                Show()
            End If
        Catch
            Try
                Show()
            Catch
            End Try
        End Try
    End Sub

    ' 打印戳记复选框状态变化 - 控制打印戳记按钮的启用状态
    Private Sub ChkPlotStamp_CheckedChanged(sender As Object, e As EventArgs) Handles chkPlotStamp.CheckedChanged
        btnPlotStamp.Enabled = chkPlotStamp.Checked
    End Sub

    ' 输出选项变化时更新相关控件状态
    Private Sub RadOutputOption_CheckedChanged(sender As Object, e As EventArgs) Handles radDirectPlot.CheckedChanged, radPlotToFile.CheckedChanged, radSplitArchive.CheckedChanged, radBatchCreateLayout.CheckedChanged, radPlotExistingLayout.CheckedChanged
        UpdateOutputOptionControls()
    End Sub

    ' 根据输出选项更新控件启用状态
    Private Sub UpdateOutputOptionControls()
        ' 先启用所有打印设置相关控件（然后根据具体选项禁用）
        EnableAllPlotSettingsControls()

        If radDirectPlot.Checked Then
            ' 直接批量打印：禁用模型空间线型比例、覆盖重名、自动顺延；启用打印份数
            ' 禁用保存位置地址和名称样板地址及其右侧按钮
            chkModelSpaceScale.Enabled = False
            radOverwrite.Enabled = False
            radAutoRename.Enabled = False
            chkTraverseLayouts.Enabled = True
            lblCopies.Enabled = True
            txtCopies.Enabled = True
            chkPlotByCopy.Enabled = True
            lblSavePath.Enabled = False
            txtSavePath.Enabled = False
            btnBrowsePath.Enabled = False
            lblNameTemplate.Enabled = False
            txtNameTemplate.Enabled = False
            txtExtension.Enabled = False
            txtExtension.Text = ""
            btnVariables.Enabled = False
            chkUseDWGName.Enabled = False
        ElseIf radPlotToFile.Checked Then
            ' 打印到文件：根据打印机决定扩展名
            chkModelSpaceScale.Enabled = False
            radOverwrite.Enabled = True
            radAutoRename.Enabled = True
            chkTraverseLayouts.Enabled = True
            lblCopies.Enabled = False
            txtCopies.Enabled = False
            chkPlotByCopy.Enabled = False
            lblNameTemplate.Enabled = True
            txtNameTemplate.Enabled = True
            txtExtension.Enabled = True
            btnVariables.Enabled = True
            chkUseDWGName.Enabled = True
            ' 根据打印机设置扩展名
            UpdateExtensionByPlotter()
        ElseIf radSplitArchive.Checked Then
            ' 图纸拆分归档：禁用遍历布局、模型空间线型比例、打印份数
            ' 禁用打印设置中的所有控件
            chkTraverseLayouts.Enabled = False
            chkModelSpaceScale.Enabled = False
            radOverwrite.Enabled = True
            radAutoRename.Enabled = True
            lblCopies.Enabled = False
            txtCopies.Enabled = False
            chkPlotByCopy.Enabled = False
            ' 禁用打印设置分组框中的所有控件
            lblPreset.Enabled = False
            cmbPreset.Enabled = False
            btnModifyPreset.Enabled = False
            lblPlotDevice.Enabled = False
            cmbPlotDevice.Enabled = False
            btnDeviceProps.Enabled = False
            lblPaperSize.Enabled = False
            cmbPaperSize.Enabled = False
            chkAutoMatch.Enabled = False
            lblPlotStyle.Enabled = False
            cmbPlotStyle.Enabled = False
            btnEditStyle.Enabled = False
            chkPlotStamp.Enabled = False
            btnPlotStamp.Enabled = False
            btnBlockSettings.Enabled = False
            btnBoundingBox.Enabled = False
            ' 禁用打印比例分组框中的所有控件
            radAutoScale.Enabled = False
            radFitToPaper.Enabled = False
            radFixedScale.Enabled = False
            txtScale.Enabled = False
            ' 禁用图纸定位分组框中的所有控件
            chkAutoRotate.Enabled = False
            chkReverse.Enabled = False
            radAutoCenter.Enabled = False
            radOffset.Enabled = False
            txtOffsetX.Enabled = False
            lblOffsetX.Enabled = False
            txtOffsetY.Enabled = False
            lblOffsetY.Enabled = False
            ' 扩展名固定为当前文件扩展名
            txtExtension.Enabled = False
            txtExtension.Text = ".dwg"
        ElseIf radBatchCreateLayout.Checked Then
            ' 批量生成布局：禁用遍历布局、打印份数、保存位置地址、打印戳记
            chkTraverseLayouts.Enabled = False
            chkModelSpaceScale.Enabled = True
            radOverwrite.Enabled = True
            radAutoRename.Enabled = True
            lblCopies.Enabled = False
            txtCopies.Enabled = False
            chkPlotByCopy.Enabled = False
            txtExtension.Enabled = True
            txtExtension.Text = ""
            lblSavePath.Enabled = False
            txtSavePath.Enabled = False
            btnBrowsePath.Enabled = False
            chkPlotStamp.Enabled = False
            btnPlotStamp.Enabled = False
        ElseIf radPlotExistingLayout.Checked Then
            ' 打印已有布局：禁用遍历布局、模型空间线型比例、覆盖重名、自动顺延、打印份数
            ' 禁用保存位置地址和名称样板地址及其右侧按钮
            chkTraverseLayouts.Enabled = False
            chkModelSpaceScale.Enabled = False
            radOverwrite.Enabled = False
            radAutoRename.Enabled = False
            lblCopies.Enabled = False
            txtCopies.Enabled = False
            chkPlotByCopy.Enabled = False
            lblSavePath.Enabled = False
            txtSavePath.Enabled = False
            btnBrowsePath.Enabled = False
            lblNameTemplate.Enabled = False
            txtNameTemplate.Enabled = False
            txtExtension.Enabled = False
            txtExtension.Text = ""
            btnVariables.Enabled = False
            chkUseDWGName.Enabled = False
            ' 禁用图框形式中的所有控件
            radPolyline.Enabled = False
            radBlock.Enabled = False
            radLine.Enabled = False
            lblPolylineDesc.Enabled = False
            lblBlockDesc.Enabled = False
            lblLineDesc.Enabled = False
            ' 禁用图块与图层中的所有控件
            chkAutoSelect.Enabled = False
            btnSpecifyBlock.Enabled = False
            txtBlockName.Enabled = False
            btnSelectBlock.Enabled = False
            txtLayerName.Enabled = False
            btnSelectLayer.Enabled = False
            ' 禁用选择图纸区域中的所有控件
            btnSelectDrawing.Enabled = False
            lblSelectedCount.Enabled = False
            btnHighlight.Enabled = False
            ' 禁用打印设置中的所有控件
            lblPreset.Enabled = False
            cmbPreset.Enabled = False
            btnModifyPreset.Enabled = False
            lblPlotDevice.Enabled = False
            cmbPlotDevice.Enabled = False
            btnDeviceProps.Enabled = False
            lblPaperSize.Enabled = False
            cmbPaperSize.Enabled = False
            chkAutoMatch.Enabled = False
            lblPlotStyle.Enabled = False
            cmbPlotStyle.Enabled = False
            btnEditStyle.Enabled = False
            chkPlotStamp.Enabled = False
            btnPlotStamp.Enabled = False
            btnBlockSettings.Enabled = False
            btnBoundingBox.Enabled = False
            ' 禁用打印比例中的所有控件
            radAutoScale.Enabled = False
            radFitToPaper.Enabled = False
            radFixedScale.Enabled = False
            txtScale.Enabled = False
            ' 禁用打印顺序中的所有控件
            radSelectOrder.Enabled = False
            radLeftRight.Enabled = False
            radTopBottom.Enabled = False
            chkReverseOrder.Enabled = False
            chkPlotByCopy.Enabled = False
            ' 禁用图纸定位中的所有控件
            chkAutoRotate.Enabled = False
            chkReverse.Enabled = False
            radAutoCenter.Enabled = False
            radOffset.Enabled = False
            txtOffsetX.Enabled = False
            lblOffsetX.Enabled = False
            txtOffsetY.Enabled = False
            lblOffsetY.Enabled = False
        End If
    End Sub

    ' 启用所有打印设置相关控件
    Private Sub EnableAllPlotSettingsControls()
        ' 图框形式分组框
        radPolyline.Enabled = True
        radBlock.Enabled = True
        radLine.Enabled = True
        lblPolylineDesc.Enabled = True
        lblBlockDesc.Enabled = True
        lblLineDesc.Enabled = True
        ' 图块与图层分组框
        chkAutoSelect.Enabled = True
        btnSpecifyBlock.Enabled = True
        txtBlockName.Enabled = True
        btnSelectBlock.Enabled = True
        txtLayerName.Enabled = True
        btnSelectLayer.Enabled = True
        ' 选择图纸区域
        btnSelectDrawing.Enabled = True
        lblSelectedCount.Enabled = True
        ' 亮显按钮只在选取到图框时才可用
        btnHighlight.Enabled = (_frames IsNot Nothing AndAlso _frames.Count > 0)
        ' 打印设置分组框
        lblPreset.Enabled = True
        cmbPreset.Enabled = True
        btnModifyPreset.Enabled = True
        lblPlotDevice.Enabled = True
        cmbPlotDevice.Enabled = True
        btnDeviceProps.Enabled = True
        lblPaperSize.Enabled = True
        cmbPaperSize.Enabled = True
        chkAutoMatch.Enabled = True
        lblPlotStyle.Enabled = True
        cmbPlotStyle.Enabled = True
        btnEditStyle.Enabled = True
        chkPlotStamp.Enabled = True
        btnPlotStamp.Enabled = chkPlotStamp.Checked
        btnBlockSettings.Enabled = True
        btnBoundingBox.Enabled = True
        ' 打印比例分组框
        radAutoScale.Enabled = True
        radFitToPaper.Enabled = True
        radFixedScale.Enabled = True
        txtScale.Enabled = True
        ' 打印顺序分组框
        radSelectOrder.Enabled = True
        radLeftRight.Enabled = True
        radTopBottom.Enabled = True
        chkReverseOrder.Enabled = True
        chkPlotByCopy.Enabled = True
        ' 图纸定位分组框
        chkAutoRotate.Enabled = True
        chkReverse.Enabled = True
        radAutoCenter.Enabled = True
        radOffset.Enabled = True
        txtOffsetX.Enabled = True
        lblOffsetX.Enabled = True
        txtOffsetY.Enabled = True
        lblOffsetY.Enabled = True
        ' 保存位置和名称样板
        lblSavePath.Enabled = True
        txtSavePath.Enabled = True
        btnBrowsePath.Enabled = True
        lblNameTemplate.Enabled = True
        txtNameTemplate.Enabled = True
        txtExtension.Enabled = True
        btnVariables.Enabled = True
        chkUseDWGName.Enabled = True
    End Sub

    ' 图块设置 - 设置图框图块对应的纸张
    Private Sub BtnBlockSettings_Click(sender As Object, e As EventArgs) Handles btnBlockSettings.Click
        Try
            ' 显示图块对应纸张设置对话框
            Using frm As New BlockPaperSettingsForm(_plotEngine)
                If frm.ShowDialog() = DialogResult.OK Then
                    Dim settings = frm.GetSettings()
                    If settings.Count > 0 Then
                        MessageBox.Show($"已保存 {settings.Count} 个图块纸张设置", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)
                    End If
                End If
            End Using
        Catch ex As Exception
            MessageBox.Show("图块设置失败: " & ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    ' 变量 - 显示名称模板选择对话框
    Private Sub BtnVariables_Click(sender As Object, e As EventArgs) Handles btnVariables.Click
        Try
            Using form As New NameTemplateForm()
                If form.ShowDialog(Me) = DialogResult.OK Then
                    ' 将选中的模板追加到文本框
                    If String.IsNullOrEmpty(txtNameTemplate.Text) OrElse txtNameTemplate.Text = "<DWGNAME>" Then
                        txtNameTemplate.Text = form.SelectedTemplate
                    Else
                        txtNameTemplate.Text = txtNameTemplate.Text & "_" & form.SelectedTemplate
                    End If
                End If
            End Using
        Catch ex As Exception
            MessageBox.Show("显示变量对话框失败: " & ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    ' 指定图块或图层 - 在图中点选图块或图层
    Private Sub BtnSpecifyBlock_Click(sender As Object, e As EventArgs) Handles btnSpecifyBlock.Click
        Try
            Dim doc As Document = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument
            If doc Is Nothing Then
                MessageBox.Show("请先打开一个AutoCAD文档", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            ' 根据图框形式决定点选方式
            If radBlock.Checked Then
                ' 图块形式：点选图块
                PickBlockReference(doc)
            ElseIf radPolyline.Checked OrElse radLine.Checked Then
                ' 多段线或直线形式：点选图层
                PickLayer(doc)
            End If

        Catch ex As Exception
            MessageBox.Show("指定图块或图层失败: " & ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Show()
        End Try
    End Sub

    ' 点选图块参照
    Private Sub PickBlockReference(doc As Document)
        Try
            Dim ed As Editor = doc.Editor

            ' 提示用户选择图块
            Dim opts As New PromptEntityOptions("选择图框图块")
            opts.SetRejectMessage("请选择图块参照")
            opts.AddAllowedClass(GetType(BlockReference), True)

            Dim result As PromptEntityResult = ed.GetEntity(opts)

            If result.Status = PromptStatus.OK Then
                Using tr As Transaction = doc.Database.TransactionManager.StartTransaction()
                    Dim br = CType(tr.GetObject(result.ObjectId, OpenMode.ForRead), BlockReference)
                    If br IsNot Nothing Then
                        Dim btr = CType(tr.GetObject(br.BlockTableRecord, OpenMode.ForRead), BlockTableRecord)

                        ' 填充图块名和图层名
                        txtBlockName.Text = btr.Name
                        txtLayerName.Text = br.Layer

                        MessageBox.Show($"已选择图块: {btr.Name}" & vbCrLf & $"图层: {br.Layer}", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)
                    End If
                    tr.Commit()
                End Using
            End If

        Catch ex As Exception
            MessageBox.Show("点选图块失败: " & ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    ' 点选图层
    Private Sub PickLayer(doc As Document)
        Try
            Dim ed As Editor = doc.Editor

            ' 提示用户选择对象来获取图层
            Dim opts As New PromptEntityOptions("选择图框对象以获取图层")
            opts.SetRejectMessage("请选择有效的对象")

            Dim result As PromptEntityResult = ed.GetEntity(opts)

            If result.Status = PromptStatus.OK Then
                Using tr As Transaction = doc.Database.TransactionManager.StartTransaction()
                    Dim ent = CType(tr.GetObject(result.ObjectId, OpenMode.ForRead), Entity)
                    If ent IsNot Nothing Then
                        ' 填充图层名
                        txtLayerName.Text = ent.Layer

                        MessageBox.Show($"已选择图层: {ent.Layer}", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)
                    End If
                    tr.Commit()
                End Using
            End If

        Catch ex As Exception
            MessageBox.Show("点选图层失败: " & ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    ' 选择图块按钮点击
    Private Sub BtnSelectBlock_Click(sender As Object, e As EventArgs) Handles btnSelectBlock.Click
        Try
            Dim doc As Document = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument
            If doc Is Nothing Then
                MessageBox.Show("请先打开一个AutoCAD文档", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            _plotEngine.UpdateCurrentDocument()
            Dim blocks As List(Of String) = _plotEngine.GetBlockList()
            If blocks Is Nothing OrElse blocks.Count = 0 Then
                MessageBox.Show("当前文档中没有找到图块", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            Using frm As New SelectionForm("选择图块", blocks)
                If frm.ShowDialog() = DialogResult.OK Then
                    txtBlockName.Text = frm.SelectedItems
                End If
            End Using

        Catch ex As Exception
            MessageBox.Show("获取图块列表失败: " & ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    ' 选择图层按钮点击
    Private Sub BtnSelectLayer_Click(sender As Object, e As EventArgs) Handles btnSelectLayer.Click
        Try
            Dim doc As Document = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument
            If doc Is Nothing Then
                MessageBox.Show("请先打开一个AutoCAD文档", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            _plotEngine.UpdateCurrentDocument()
            Dim layers As List(Of String) = _plotEngine.GetLayerList()
            If layers Is Nothing OrElse layers.Count = 0 Then
                MessageBox.Show("当前文档中没有找到图层", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            Using frm As New SelectionForm("选择图层", layers)
                If frm.ShowDialog() = DialogResult.OK Then
                    txtLayerName.Text = frm.SelectedItems
                End If
            End Using

        Catch ex As Exception
            MessageBox.Show("获取图层列表失败: " & ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    ' 为TextBox添加数字验证
    Private Sub AddNumericValidation(txt As TextBox, allowDecimal As Boolean)
        AddHandler txt.KeyPress, Sub(sender As Object, e As KeyPressEventArgs)
            ' 允许退格键
            If e.KeyChar = ChrW(Keys.Back) Then
                Return
            End If
            
            ' 允许负号（只能在开头）
            If e.KeyChar = "-"c Then
                If txt.SelectionStart = 0 AndAlso Not txt.Text.Contains("-") Then
                    Return
                End If
                e.Handled = True
                Return
            End If
            
            ' 允许小数点（仅当允许小数时）
            If allowDecimal AndAlso (e.KeyChar = "."c OrElse e.KeyChar = ","c) Then
                If Not txt.Text.Contains(".") AndAlso Not txt.Text.Contains(",") Then
                    e.KeyChar = "."c
                    Return
                End If
                e.Handled = True
                Return
            End If
            
            ' 只允许数字
            If Not Char.IsDigit(e.KeyChar) Then
                e.Handled = True
            End If
        End Sub
        
        ' 失去焦点时验证
        AddHandler txt.LostFocus, Sub(sender As Object, e As EventArgs)
            If String.IsNullOrWhiteSpace(txt.Text) Then
                txt.Text = "0"
            Else
                ' 尝试解析为数字
                Dim value As Double
                If Double.TryParse(txt.Text, value) Then
                    If allowDecimal Then
                        txt.Text = value.ToString()
                    Else
                        txt.Text = CInt(value).ToString()
                    End If
                Else
                    txt.Text = "0"
                End If
            End If
        End Sub
    End Sub

    ' 根据打印设备设置扩展名
    Private Sub UpdateExtensionByPlotter()
        Try
            Dim plotterName As String = ""
            If cmbPlotDevice.SelectedItem IsNot Nothing Then
                plotterName = cmbPlotDevice.SelectedItem.ToString()
            End If
            
            If String.IsNullOrEmpty(plotterName) OrElse plotterName = "无" Then
                txtExtension.Text = ".plt"
                Return
            End If
            
            ' 根据打印机名称判断扩展名
            Dim plotterNameLower As String = plotterName.ToLower()
            If plotterNameLower.Contains("pdf") Then
                txtExtension.Text = ".pdf"
            ElseIf plotterNameLower.Contains("dwfx") Then
                txtExtension.Text = ".dwfx"
            ElseIf plotterNameLower.Contains("dwf") Then
                txtExtension.Text = ".dwf"
            ElseIf plotterNameLower.Contains("png") Then
                txtExtension.Text = ".png"
            ElseIf plotterNameLower.Contains("jpg") OrElse plotterNameLower.Contains("jpeg") Then
                txtExtension.Text = ".jpg"
            ElseIf plotterNameLower.Contains("tiff") OrElse plotterNameLower.Contains("tif") Then
                txtExtension.Text = ".tif"
            ElseIf plotterNameLower.Contains("bmp") Then
                txtExtension.Text = ".bmp"
            Else
                ' 默认使用 .plt
                txtExtension.Text = ".plt"
            End If
        Catch ex As Exception
            Debug.WriteLine("获取打印机扩展名失败: " & ex.Message)
            txtExtension.Text = ".plt"
        End Try
    End Sub

    ' 输出类型改变时更新扩展名
    Private Sub RadSourcePathPDF_CheckedChanged(sender As Object, e As EventArgs) Handles radSourcePathPDF.CheckedChanged
        If radPlotToFile.Checked AndAlso radSourcePathPDF.Checked Then
            UpdateExtensionByPlotter()
        End If
    End Sub

    Private Sub RadSourcePathDWF_CheckedChanged(sender As Object, e As EventArgs) Handles radSourcePathDWF.CheckedChanged
        If radPlotToFile.Checked AndAlso radSourcePathDWF.Checked Then
            UpdateExtensionByPlotter()
        End If
    End Sub

End Class
