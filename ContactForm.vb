Imports System.Drawing
Imports System.Windows.Forms

Public Class ContactForm

    Private WithEvents btnClose As Button

    Public Sub New()
        InitializeComponent()
    End Sub

    Private Sub InitializeComponent()
        Me.lblTitle = New System.Windows.Forms.Label()
        Me.lblVersion = New System.Windows.Forms.Label()
        Me.separator = New System.Windows.Forms.Label()
        Me.lblSupport = New System.Windows.Forms.Label()
        Me.separator2 = New System.Windows.Forms.Label()
        Me.btnClose = New System.Windows.Forms.Button()
        Me.lblCopyright = New System.Windows.Forms.Label()
        Me.SuspendLayout
        '
        'lblTitle
        '
        Me.lblTitle.Font = New System.Drawing.Font("微软雅黑", 16!, System.Drawing.FontStyle.Bold)
        Me.lblTitle.Location = New System.Drawing.Point(-8, 20)
        Me.lblTitle.Name = "lblTitle"
        Me.lblTitle.Size = New System.Drawing.Size(400, 35)
        Me.lblTitle.TabIndex = 0
        Me.lblTitle.Text = "批量打印插件"
        Me.lblTitle.TextAlign = System.Drawing.ContentAlignment.MiddleCenter
        '
        'lblVersion
        '
        Me.lblVersion.Location = New System.Drawing.Point(-8, 60)
        Me.lblVersion.Name = "lblVersion"
        Me.lblVersion.Size = New System.Drawing.Size(400, 20)
        Me.lblVersion.TabIndex = 1
        Me.lblVersion.Text = "版本: Beta"
        Me.lblVersion.TextAlign = System.Drawing.ContentAlignment.MiddleCenter
        '
        'separator
        '
        Me.separator.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D
        Me.separator.Location = New System.Drawing.Point(42, 90)
        Me.separator.Name = "separator"
        Me.separator.Size = New System.Drawing.Size(300, 2)
        Me.separator.TabIndex = 2
        '
        'lblSupport
        '
        Me.lblSupport.Font = New System.Drawing.Font("微软雅黑", 10.5!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(134,Byte))
        Me.lblSupport.Location = New System.Drawing.Point(-8, 110)
        Me.lblSupport.Name = "lblSupport"
        Me.lblSupport.Size = New System.Drawing.Size(400, 25)
        Me.lblSupport.TabIndex = 3
        Me.lblSupport.Text = "jiazheng-hqc@cnpc.com.cn"
        Me.lblSupport.TextAlign = System.Drawing.ContentAlignment.MiddleCenter
        '
        'separator2
        '
        Me.separator2.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D
        Me.separator2.Location = New System.Drawing.Point(42, 207)
        Me.separator2.Name = "separator2"
        Me.separator2.Size = New System.Drawing.Size(300, 2)
        Me.separator2.TabIndex = 6
        '
        'btnClose
        '
        Me.btnClose.Location = New System.Drawing.Point(152, 219)
        Me.btnClose.Name = "btnClose"
        Me.btnClose.Size = New System.Drawing.Size(80, 30)
        Me.btnClose.TabIndex = 7
        Me.btnClose.Text = "关闭"
        '
        'lblCopyright
        '
        Me.lblCopyright.Font = New System.Drawing.Font("微软雅黑", 8!)
        Me.lblCopyright.ForeColor = System.Drawing.Color.Gray
        Me.lblCopyright.Location = New System.Drawing.Point(0, 275)
        Me.lblCopyright.Name = "lblCopyright"
        Me.lblCopyright.Size = New System.Drawing.Size(400, 20)
        Me.lblCopyright.TabIndex = 8
        Me.lblCopyright.Text = "© 2024 BatchPlot Developer. All rights reserved."
        Me.lblCopyright.TextAlign = System.Drawing.ContentAlignment.MiddleCenter
        '
        'ContactForm
        '
        Me.ClientSize = New System.Drawing.Size(384, 261)
        Me.Controls.Add(Me.lblTitle)
        Me.Controls.Add(Me.lblVersion)
        Me.Controls.Add(Me.separator)
        Me.Controls.Add(Me.lblSupport)
        Me.Controls.Add(Me.separator2)
        Me.Controls.Add(Me.btnClose)
        Me.Controls.Add(Me.lblCopyright)
        Me.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog
        Me.MaximizeBox = false
        Me.MinimizeBox = false
        Me.Name = "ContactForm"
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen
        Me.Text = "联系作者"
        Me.TopMost = True
        Me.ResumeLayout(False)

    End Sub

    Private Sub BtnClose_Click(sender As Object, e As EventArgs) Handles btnClose.Click
        Close()
    End Sub

End Class
