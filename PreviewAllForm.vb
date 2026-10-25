Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.IO
Imports System.Windows.Forms

Public Class PreviewAllForm
    Inherits Form

    Private _imagePaths As List(Of String)
    Private _frames As List(Of BatchPlotEngine.FrameInfo)
    Private _thumbnails As List(Of Image)
    Private _selectedIndices As HashSet(Of Integer)
    Private _cards As List(Of Panel)

    ' 控件
    Private flowPanel As FlowLayoutPanel
    Private panelBottom As Panel
    Private lblStatus As Label
    Private btnSelectAll As Button
    Private btnDeselectAll As Button
    Private btnInvertSelect As Button
    Private btnPreviewSelected As Button
    Private btnClose As Button

    ' 用户点击预览后，需要预览的图框索引列表（关闭窗口后由外部读取）
    Public Property PendingPreviewIndices As List(Of Integer)

    ' 点选卡片时触发（索引对应 _frames），供主窗口联动高亮图上序号标注
    Public Event CardClicked(index As Integer)

    Public Sub New(imagePaths As List(Of String), frames As List(Of BatchPlotEngine.FrameInfo))
        _imagePaths = imagePaths
        _frames = frames
        _thumbnails = New List(Of Image)()
        _selectedIndices = New HashSet(Of Integer)()
        _cards = New List(Of Panel)()

        SetupForm()
        LoadThumbnails()
    End Sub

    Private Sub SetupForm()
        AutoScaleDimensions = New SizeF(6.0F, 12.0F)
        AutoScaleMode = AutoScaleMode.Font
        Text = "批量预览 - 图框总览"
        Size = New Size(960, 720)
        ' 底部按钮行宽度约 540px（320→860），最小宽度必须容纳按钮行，否则右侧按钮被裁掉
        MinimumSize = New Size(880, 480)
        StartPosition = FormStartPosition.CenterScreen
        FormBorderStyle = FormBorderStyle.Sizable
        MaximizeBox = True
        MinimizeBox = True
        TopMost = True
        BackColor = Color.FromArgb(240, 240, 240)

        ' 底部面板
        panelBottom = New Panel()
        panelBottom.Dock = DockStyle.Bottom
        panelBottom.Height = 50
        panelBottom.BackColor = Color.FromArgb(245, 245, 245)

        lblStatus = New Label()
        lblStatus.Text = "加载中..."
        lblStatus.AutoSize = True
        lblStatus.Location = New Point(12, 15)
        lblStatus.Font = New Font("Microsoft YaHei UI", 9)
        panelBottom.Controls.Add(lblStatus)

        btnSelectAll = New Button()
        btnSelectAll.Text = "全选"
        btnSelectAll.Size = New Size(70, 32)
        btnSelectAll.Location = New Point(320, 9)
        btnSelectAll.Anchor = AnchorStyles.Right Or AnchorStyles.Bottom
        AddHandler btnSelectAll.Click, AddressOf OnSelectAll
        panelBottom.Controls.Add(btnSelectAll)

        btnDeselectAll = New Button()
        btnDeselectAll.Text = "取消全选"
        btnDeselectAll.Size = New Size(85, 32)
        btnDeselectAll.Location = New Point(400, 9)
        btnDeselectAll.Anchor = AnchorStyles.Right Or AnchorStyles.Bottom
        AddHandler btnDeselectAll.Click, AddressOf OnDeselectAll
        panelBottom.Controls.Add(btnDeselectAll)

        btnInvertSelect = New Button()
        btnInvertSelect.Text = "反选"
        btnInvertSelect.Size = New Size(70, 32)
        btnInvertSelect.Location = New Point(495, 9)
        btnInvertSelect.Anchor = AnchorStyles.Right Or AnchorStyles.Bottom
        AddHandler btnInvertSelect.Click, AddressOf OnInvertSelect
        panelBottom.Controls.Add(btnInvertSelect)

        btnPreviewSelected = New Button()
        btnPreviewSelected.Text = "预览选中"
        btnPreviewSelected.Size = New Size(90, 32)
        btnPreviewSelected.Location = New Point(690, 9)
        btnPreviewSelected.Anchor = AnchorStyles.Right Or AnchorStyles.Bottom
        btnPreviewSelected.Enabled = False
        AddHandler btnPreviewSelected.Click, AddressOf OnPreviewSelected
        panelBottom.Controls.Add(btnPreviewSelected)

        btnClose = New Button()
        btnClose.Text = "关闭"
        btnClose.Size = New Size(70, 32)
        btnClose.Location = New Point(790, 9)
        btnClose.Anchor = AnchorStyles.Right Or AnchorStyles.Bottom
        AddHandler btnClose.Click, AddressOf OnClose
        panelBottom.Controls.Add(btnClose)

        Controls.Add(panelBottom)

        ' 主面板
        flowPanel = New FlowLayoutPanel()
        flowPanel.Dock = DockStyle.Fill
        flowPanel.AutoScroll = True
        flowPanel.WrapContents = True
        flowPanel.Padding = New Padding(10)
        flowPanel.BackColor = Color.FromArgb(230, 230, 230)
        Controls.Add(flowPanel)
    End Sub

    Private Sub LoadThumbnails()
        flowPanel.SuspendLayout()

        Dim thumbW As Integer = 200
        Dim thumbH As Integer = 150
        Dim pad As Integer = 8
        Dim cardW As Integer = thumbW + pad * 2
        Dim cardH As Integer = thumbH + 55

        Dim successCount As Integer = 0

        For i As Integer = 0 To _imagePaths.Count - 1
            Dim imgPath = _imagePaths(i)
            Dim frame = _frames(i)
            Dim currentIndex As Integer = i ' 闭包捕获

            ' 卡片容器
            Dim card As New Panel()
            card.Size = New Size(cardW, cardH)
            card.Margin = New Padding(6)
            card.BackColor = Color.White
            card.BorderStyle = BorderStyle.FixedSingle
            card.Tag = currentIndex

            ' 缩略图
            Dim pb As New PictureBox()
            pb.Size = New Size(thumbW, thumbH)
            pb.Location = New Point(pad, pad)
            pb.SizeMode = PictureBoxSizeMode.Zoom
            pb.BackColor = Color.FromArgb(248, 248, 248)
            pb.Tag = currentIndex

            If imgPath IsNot Nothing AndAlso File.Exists(imgPath) Then
                Try
                    Dim img = Image.FromFile(imgPath)
                    _thumbnails.Add(img)
                    pb.Image = img
                    successCount += 1
                Catch
                    _thumbnails.Add(Nothing)
                    pb.BackColor = Color.FromArgb(255, 230, 230)
                End Try
            Else
                _thumbnails.Add(Nothing)
                pb.BackColor = Color.FromArgb(255, 230, 230)
                ' 显示"无预览"文字
                Dim noImgLabel As New Label()
                noImgLabel.Text = "无预览"
                noImgLabel.Size = New Size(thumbW, thumbH)
                noImgLabel.TextAlign = ContentAlignment.MiddleCenter
                noImgLabel.Font = New Font("Microsoft YaHei UI", 9)
                noImgLabel.ForeColor = Color.FromArgb(180, 180, 180)
                noImgLabel.BackColor = Color.Transparent
                pb.Controls.Add(noImgLabel)
            End If

            card.Controls.Add(pb)

            ' 图框编号
            Dim lblNum As New Label()
            lblNum.Text = frame.FrameNumber
            lblNum.Font = New Font("Microsoft YaHei UI", 9, FontStyle.Bold)
            lblNum.ForeColor = Color.FromArgb(50, 50, 50)
            lblNum.AutoSize = False
            lblNum.Size = New Size(cardW \ 2 - pad, 20)
            lblNum.Location = New Point(pad, thumbH + pad + 2)
            lblNum.TextAlign = ContentAlignment.MiddleLeft
            lblNum.Tag = currentIndex
            card.Controls.Add(lblNum)

            ' 布局名
            Dim lblLayout As New Label()
            lblLayout.Text = frame.LayoutName
            lblLayout.Font = New Font("Microsoft YaHei UI", 8)
            lblLayout.ForeColor = Color.FromArgb(120, 120, 120)
            lblLayout.AutoSize = False
            lblLayout.Size = New Size(cardW \ 2 - pad, 20)
            lblLayout.Location = New Point(cardW \ 2, thumbH + pad + 2)
            lblLayout.TextAlign = ContentAlignment.MiddleRight
            lblLayout.Tag = currentIndex
            card.Controls.Add(lblLayout)

            ' 尺寸
            Dim lblSize As New Label()
            Dim w As Double = Math.Abs(frame.Bounds.MaxPoint.X - frame.Bounds.MinPoint.X)
            Dim h As Double = Math.Abs(frame.Bounds.MaxPoint.Y - frame.Bounds.MinPoint.Y)
            lblSize.Text = $"{w:F0} x {h:F0}"
            lblSize.Font = New Font("Microsoft YaHei UI", 8)
            lblSize.ForeColor = Color.FromArgb(150, 150, 150)
            lblSize.AutoSize = False
            lblSize.Size = New Size(thumbW, 18)
            lblSize.Location = New Point(pad, thumbH + pad + 22)
            lblSize.TextAlign = ContentAlignment.MiddleCenter
            lblSize.Tag = currentIndex
            card.Controls.Add(lblSize)

            ' 事件绑定 - 卡片和所有子控件都响应点击
            AddHandler card.Click, Sub(s, ev) ToggleSelect(currentIndex)
            AddHandler pb.Click, Sub(s, ev) ToggleSelect(currentIndex)
            AddHandler lblNum.Click, Sub(s, ev) ToggleSelect(currentIndex)
            AddHandler lblLayout.Click, Sub(s, ev) ToggleSelect(currentIndex)
            AddHandler lblSize.Click, Sub(s, ev) ToggleSelect(currentIndex)

            ' 双击预览
            AddHandler card.DoubleClick, Sub(s, ev) DoubleClickPreview(currentIndex)
            AddHandler pb.DoubleClick, Sub(s, ev) DoubleClickPreview(currentIndex)

            ' 选中态持久描边（随重绘自动恢复，不丢失）
            AddHandler card.Paint, Sub(s, ev)
                                       If _selectedIndices.Contains(currentIndex) Then
                                           Using pen As New Pen(Color.FromArgb(0, 120, 215), 2)
                                               ev.Graphics.DrawRectangle(pen, 1, 1, card.Width - 3, card.Height - 3)
                                           End Using
                                       End If
                                   End Sub

            _cards.Add(card)
            flowPanel.Controls.Add(card)
        Next

        flowPanel.ResumeLayout()
        lblStatus.Text = $"共 {_imagePaths.Count} 个图框，成功生成 {successCount} 个预览  |  点击选中，双击预览"
    End Sub

    Private Sub ToggleSelect(index As Integer)
        If _selectedIndices.Contains(index) Then
            _selectedIndices.Remove(index)
        Else
            _selectedIndices.Add(index)
        End If

        ' 通知外部（主窗口联动高亮图上序号），异常不阻塞界面
        Try
            RaiseEvent CardClicked(index)
        Catch
        End Try

        ' 更新卡片选中外观（Paint 事件持久绘制选中边框）
        ApplyCardVisual(index)

        UpdateStatus()
    End Sub

    ' 按选中状态刷新单张卡片外观：高亮底色 + Paint 事件绘制选中边框
    Private Sub ApplyCardVisual(index As Integer)
        Dim card = _cards(index)
        If _selectedIndices.Contains(index) Then
            card.BackColor = Color.FromArgb(220, 235, 252)
            card.BorderStyle = BorderStyle.None
        Else
            card.BackColor = Color.White
            card.BorderStyle = BorderStyle.FixedSingle
        End If
        card.Invalidate()
    End Sub

    Private Sub DoubleClickPreview(index As Integer)
        PendingPreviewIndices = New List(Of Integer) From {index}
        DialogResult = DialogResult.OK
        Close()
    End Sub

    Private Sub OnSelectAll(s As Object, e As EventArgs)
        _selectedIndices.Clear()
        For i = 0 To _cards.Count - 1
            _selectedIndices.Add(i)
            ApplyCardVisual(i)
        Next
        UpdateStatus()
    End Sub

    Private Sub OnDeselectAll(s As Object, e As EventArgs)
        _selectedIndices.Clear()
        For i = 0 To _cards.Count - 1
            ApplyCardVisual(i)
        Next
        UpdateStatus()
    End Sub

    Private Sub OnInvertSelect(s As Object, e As EventArgs)
        For i = 0 To _cards.Count - 1
            If _selectedIndices.Contains(i) Then
                _selectedIndices.Remove(i)
            Else
                _selectedIndices.Add(i)
            End If
            ApplyCardVisual(i)
        Next
        UpdateStatus()
    End Sub

    Private Sub OnPreviewSelected(s As Object, e As EventArgs)
        If _selectedIndices.Count = 0 Then Return
        PendingPreviewIndices = _selectedIndices.ToList()
        DialogResult = DialogResult.OK
        Close()
    End Sub

    Private Sub OnClose(s As Object, e As EventArgs)
        Close()
    End Sub

    Private Sub UpdateStatus()
        btnPreviewSelected.Enabled = (_selectedIndices.Count > 0)
        lblStatus.Text = $"共 {_imagePaths.Count} 个图框，已选中 {_selectedIndices.Count} 个  |  点击选中，双击预览"
    End Sub

    Protected Overrides Sub OnFormClosed(e As FormClosedEventArgs)
        For Each img In _thumbnails
            If img IsNot Nothing Then img.Dispose()
        Next
        _thumbnails.Clear()
        MyBase.OnFormClosed(e)
    End Sub
End Class
