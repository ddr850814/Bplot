<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class ContactForm
    Inherits System.Windows.Forms.Form

    'Form 重写 Dispose，以清理组件列表。
    <System.Diagnostics.DebuggerNonUserCode()> _
    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then
                components.Dispose()
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    'Windows 窗体设计器所必需的
    Private components As System.ComponentModel.IContainer
    Friend WithEvents btnClose As System.Windows.Forms.Button
    Friend WithEvents lblTitle As System.Windows.Forms.Label
    Friend WithEvents lblVersion As System.Windows.Forms.Label
    Friend WithEvents separator As System.Windows.Forms.Label
    Friend WithEvents lblSupport As System.Windows.Forms.Label
    Friend WithEvents separator2 As System.Windows.Forms.Label
    Friend WithEvents lblCopyright As System.Windows.Forms.Label
    Friend WithEvents panelHeader As System.Windows.Forms.Panel
    Friend WithEvents panelContent As System.Windows.Forms.Panel

    '注意: 以下过程是 Windows 窗体设计器所必需的
    '可以使用 Windows 窗体设计器修改它。
    '不要使用代码编辑器修改它。
    <System.Diagnostics.DebuggerStepThrough()> _
    Private Sub InitializeComponent()
        Me.panelHeader = New System.Windows.Forms.Panel()
        Me.lblTitle = New System.Windows.Forms.Label()
        Me.lblVersion = New System.Windows.Forms.Label()
        Me.panelContent = New System.Windows.Forms.Panel()
        Me.separator = New System.Windows.Forms.Label()
        Me.lblSupport = New System.Windows.Forms.Label()
        Me.separator2 = New System.Windows.Forms.Label()
        Me.btnClose = New System.Windows.Forms.Button()
        Me.lblCopyright = New System.Windows.Forms.Label()
        Me.panelHeader.SuspendLayout
        Me.panelContent.SuspendLayout
        Me.SuspendLayout
        '
        'panelHeader
        '
        Me.panelHeader.BackColor = System.Drawing.Color.FromArgb(CType(CType(0,Byte),Integer), CType(CType(120,Byte),Integer), CType(CType(212,Byte),Integer))
        Me.panelHeader.Controls.Add(Me.lblTitle)
        Me.panelHeader.Controls.Add(Me.lblVersion)
        Me.panelHeader.Dock = System.Windows.Forms.DockStyle.Top
        Me.panelHeader.Location = New System.Drawing.Point(0, 0)
        Me.panelHeader.Name = "panelHeader"
        Me.panelHeader.Size = New System.Drawing.Size(384, 120)
        Me.panelHeader.TabIndex = 0
        '
        'lblTitle
        '
        Me.lblTitle.Font = New System.Drawing.Font("微软雅黑", 16!, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, CType(134,Byte))
        Me.lblTitle.ForeColor = System.Drawing.Color.White
        Me.lblTitle.Location = New System.Drawing.Point(0, 35)
        Me.lblTitle.Name = "lblTitle"
        Me.lblTitle.Size = New System.Drawing.Size(384, 35)
        Me.lblTitle.TabIndex = 1
        Me.lblTitle.Text = "批量打印插件"
        Me.lblTitle.TextAlign = System.Drawing.ContentAlignment.MiddleCenter
        '
        'lblVersion
        '
        Me.lblVersion.Font = New System.Drawing.Font("微软雅黑", 9!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(134,Byte))
        Me.lblVersion.ForeColor = System.Drawing.Color.FromArgb(CType(CType(200,Byte),Integer), CType(CType(220,Byte),Integer), CType(CType(240,Byte),Integer))
        Me.lblVersion.Location = New System.Drawing.Point(0, 80)
        Me.lblVersion.Name = "lblVersion"
        Me.lblVersion.Size = New System.Drawing.Size(384, 20)
        Me.lblVersion.TabIndex = 2
        Me.lblVersion.Text = "版本: Alpha 1.0"
        Me.lblVersion.TextAlign = System.Drawing.ContentAlignment.MiddleCenter
        '
        'panelContent
        '
        Me.panelContent.BackColor = System.Drawing.Color.White
        Me.panelContent.Controls.Add(Me.separator)
        Me.panelContent.Controls.Add(Me.lblSupport)
        Me.panelContent.Controls.Add(Me.separator2)
        Me.panelContent.Controls.Add(Me.btnClose)
        Me.panelContent.Controls.Add(Me.lblCopyright)
        Me.panelContent.Dock = System.Windows.Forms.DockStyle.Fill
        Me.panelContent.Location = New System.Drawing.Point(0, 120)
        Me.panelContent.Name = "panelContent"
        Me.panelContent.Size = New System.Drawing.Size(384, 201)
        Me.panelContent.TabIndex = 1
        '
        'separator
        '
        Me.separator.BackColor = System.Drawing.Color.FromArgb(CType(CType(230,Byte),Integer), CType(CType(230,Byte),Integer), CType(CType(230,Byte),Integer))
        Me.separator.Location = New System.Drawing.Point(42, 20)
        Me.separator.Name = "separator"
        Me.separator.Size = New System.Drawing.Size(300, 1)
        Me.separator.TabIndex = 0
        '
        'lblSupport
        '
        Me.lblSupport.Font = New System.Drawing.Font("微软雅黑", 10.5!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(134,Byte))
        Me.lblSupport.ForeColor = System.Drawing.Color.FromArgb(CType(CType(64,Byte),Integer), CType(CType(64,Byte),Integer), CType(CType(64,Byte),Integer))
        Me.lblSupport.Location = New System.Drawing.Point(0, 40)
        Me.lblSupport.Name = "lblSupport"
        Me.lblSupport.Size = New System.Drawing.Size(384, 25)
        Me.lblSupport.TabIndex = 1
        Me.lblSupport.Text = "📧  jiazheng-hqc@cnpc.com.cn"
        Me.lblSupport.TextAlign = System.Drawing.ContentAlignment.MiddleCenter
        '
        'separator2
        '
        Me.separator2.BackColor = System.Drawing.Color.FromArgb(CType(CType(230,Byte),Integer), CType(CType(230,Byte),Integer), CType(CType(230,Byte),Integer))
        Me.separator2.Location = New System.Drawing.Point(42, 85)
        Me.separator2.Name = "separator2"
        Me.separator2.Size = New System.Drawing.Size(300, 1)
        Me.separator2.TabIndex = 2
        '
        'btnClose
        '
        Me.btnClose.BackColor = System.Drawing.Color.FromArgb(CType(CType(0,Byte),Integer), CType(CType(120,Byte),Integer), CType(CType(212,Byte),Integer))
        Me.btnClose.FlatAppearance.BorderSize = 0
        Me.btnClose.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnClose.Font = New System.Drawing.Font("微软雅黑", 10!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(134,Byte))
        Me.btnClose.ForeColor = System.Drawing.Color.White
        Me.btnClose.Location = New System.Drawing.Point(142, 100)
        Me.btnClose.Name = "btnClose"
        Me.btnClose.Size = New System.Drawing.Size(100, 35)
        Me.btnClose.TabIndex = 3
        Me.btnClose.Text = "关 闭"
        Me.btnClose.UseVisualStyleBackColor = false
        '
        'lblCopyright
        '
        Me.lblCopyright.Font = New System.Drawing.Font("微软雅黑", 8!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(134,Byte))
        Me.lblCopyright.ForeColor = System.Drawing.Color.FromArgb(CType(CType(150,Byte),Integer), CType(CType(150,Byte),Integer), CType(CType(150,Byte),Integer))
        Me.lblCopyright.Location = New System.Drawing.Point(0, 145)
        Me.lblCopyright.Name = "lblCopyright"
        Me.lblCopyright.Size = New System.Drawing.Size(384, 20)
        Me.lblCopyright.TabIndex = 4
        Me.lblCopyright.Text = "© 2024 BatchPlot Developer. All rights reserved."
        Me.lblCopyright.TextAlign = System.Drawing.ContentAlignment.MiddleCenter
        '
        'ContactForm
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(6!, 12!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(384, 321)
        Me.Controls.Add(Me.panelContent)
        Me.Controls.Add(Me.panelHeader)
        Me.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog
        Me.MaximizeBox = false
        Me.MinimizeBox = false
        Me.Name = "ContactForm"
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen
        Me.Text = "关于"
        Me.TopMost = true
        Me.panelHeader.ResumeLayout(false)
        Me.panelContent.ResumeLayout(false)
        Me.ResumeLayout(false)

End Sub
End Class
