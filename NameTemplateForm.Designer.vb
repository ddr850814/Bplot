Imports System.Windows.Forms
Imports System.Drawing

Partial Class NameTemplateForm
    Inherits Form

    ' 控件声明
    Friend WithEvents grpOptions As GroupBox
    Friend WithEvents radDWGFolderName As RadioButton
    Friend WithEvents radDWGName As RadioButton
    Friend WithEvents radCTab As RadioButton
    Friend WithEvents radPaperName As RadioButton
    Friend WithEvents radAutoIndex As RadioButton
    Friend WithEvents radBlockAttr As RadioButton
    Friend WithEvents radLayerText As RadioButton
    Friend WithEvents btnOK As Button
    Friend WithEvents btnCancel As Button

    Private Sub InitializeComponent()
        Me.grpOptions = New System.Windows.Forms.GroupBox()
        Me.radDWGFolderName = New System.Windows.Forms.RadioButton()
        Me.radDWGName = New System.Windows.Forms.RadioButton()
        Me.radCTab = New System.Windows.Forms.RadioButton()
        Me.radPaperName = New System.Windows.Forms.RadioButton()
        Me.radAutoIndex = New System.Windows.Forms.RadioButton()
        Me.radBlockAttr = New System.Windows.Forms.RadioButton()
        Me.radLayerText = New System.Windows.Forms.RadioButton()
        Me.btnOK = New System.Windows.Forms.Button()
        Me.btnCancel = New System.Windows.Forms.Button()
        Me.grpOptions.SuspendLayout
        Me.SuspendLayout
        '
        'grpOptions
        '
        Me.grpOptions.Controls.Add(Me.radDWGFolderName)
        Me.grpOptions.Controls.Add(Me.radDWGName)
        Me.grpOptions.Controls.Add(Me.radCTab)
        Me.grpOptions.Controls.Add(Me.radPaperName)
        Me.grpOptions.Controls.Add(Me.radAutoIndex)
        Me.grpOptions.Controls.Add(Me.radBlockAttr)
        Me.grpOptions.Controls.Add(Me.radLayerText)
        Me.grpOptions.Location = New System.Drawing.Point(12, 12)
        Me.grpOptions.Name = "grpOptions"
        Me.grpOptions.Size = New System.Drawing.Size(215, 202)
        Me.grpOptions.TabIndex = 0
        Me.grpOptions.TabStop = false
        Me.grpOptions.Text = "选项"
        '
        'radDWGFolderName
        '
        Me.radDWGFolderName.AutoSize = true
        Me.radDWGFolderName.Location = New System.Drawing.Point(15, 25)
        Me.radDWGFolderName.Name = "radDWGFolderName"
        Me.radDWGFolderName.Size = New System.Drawing.Size(179, 16)
        Me.radDWGFolderName.TabIndex = 0
        Me.radDWGFolderName.Text = "DWG文件夹名<DWGFOLDERNAME>"
        Me.radDWGFolderName.UseVisualStyleBackColor = true
        '
        'radDWGName
        '
        Me.radDWGName.AutoSize = true
        Me.radDWGName.Checked = true
        Me.radDWGName.Location = New System.Drawing.Point(15, 50)
        Me.radDWGName.Name = "radDWGName"
        Me.radDWGName.Size = New System.Drawing.Size(131, 16)
        Me.radDWGName.TabIndex = 1
        Me.radDWGName.TabStop = true
        Me.radDWGName.Text = "DWG文件名<DWGNAME>"
        Me.radDWGName.UseVisualStyleBackColor = true
        '
        'radCTab
        '
        Me.radCTab.AutoSize = true
        Me.radCTab.Location = New System.Drawing.Point(15, 75)
        Me.radCTab.Name = "radCTab"
        Me.radCTab.Size = New System.Drawing.Size(119, 16)
        Me.radCTab.TabIndex = 2
        Me.radCTab.Text = "当前布局名<CTAB>"
        Me.radCTab.UseVisualStyleBackColor = true
        '
        'radPaperName
        '
        Me.radPaperName.AutoSize = true
        Me.radPaperName.Location = New System.Drawing.Point(15, 100)
        Me.radPaperName.Name = "radPaperName"
        Me.radPaperName.Size = New System.Drawing.Size(137, 16)
        Me.radPaperName.TabIndex = 3
        Me.radPaperName.Text = "纸张名称<PAPERNAME>"
        Me.radPaperName.UseVisualStyleBackColor = true
        '
        'radAutoIndex
        '
        Me.radAutoIndex.AutoSize = true
        Me.radAutoIndex.Location = New System.Drawing.Point(15, 125)
        Me.radAutoIndex.Name = "radAutoIndex"
        Me.radAutoIndex.Size = New System.Drawing.Size(197, 16)
        Me.radAutoIndex.TabIndex = 4
        Me.radAutoIndex.Text = "从指定数字自动增量编号<INDEX>"
        Me.radAutoIndex.UseVisualStyleBackColor = true
        '
        'radBlockAttr
        '
        Me.radBlockAttr.AutoSize = true
        Me.radBlockAttr.Location = New System.Drawing.Point(15, 150)
        Me.radBlockAttr.Name = "radBlockAttr"
        Me.radBlockAttr.Size = New System.Drawing.Size(131, 16)
        Me.radBlockAttr.TabIndex = 5
        Me.radBlockAttr.Text = "指定图块的特定属性"
        Me.radBlockAttr.UseVisualStyleBackColor = true
        '
        'radLayerText
        '
        Me.radLayerText.AutoSize = true
        Me.radLayerText.Location = New System.Drawing.Point(15, 175)
        Me.radLayerText.Name = "radLayerText"
        Me.radLayerText.Size = New System.Drawing.Size(143, 16)
        Me.radLayerText.TabIndex = 6
        Me.radLayerText.Text = "指定图层上的特定文字"
        Me.radLayerText.UseVisualStyleBackColor = true
        '
        'btnOK
        '
        Me.btnOK.Location = New System.Drawing.Point(32, 231)
        Me.btnOK.Name = "btnOK"
        Me.btnOK.Size = New System.Drawing.Size(75, 25)
        Me.btnOK.TabIndex = 1
        Me.btnOK.Text = "确定"
        Me.btnOK.UseVisualStyleBackColor = true
        '
        'btnCancel
        '
        Me.btnCancel.Location = New System.Drawing.Point(132, 231)
        Me.btnCancel.Name = "btnCancel"
        Me.btnCancel.Size = New System.Drawing.Size(75, 25)
        Me.btnCancel.TabIndex = 2
        Me.btnCancel.Text = "取消"
        Me.btnCancel.UseVisualStyleBackColor = true
        '
        'NameTemplateForm
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(6!, 12!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(238, 270)
        Me.Controls.Add(Me.btnCancel)
        Me.Controls.Add(Me.btnOK)
        Me.Controls.Add(Me.grpOptions)
        Me.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog
        Me.MaximizeBox = false
        Me.MinimizeBox = false
        Me.Name = "NameTemplateForm"
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent
        Me.Text = "名称模板"
        Me.TopMost = true
        Me.grpOptions.ResumeLayout(false)
        Me.grpOptions.PerformLayout
        Me.ResumeLayout(false)

End Sub
End Class
