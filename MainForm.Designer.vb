<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class MainForm
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
    Friend WithEvents grpPlotScale As System.Windows.Forms.GroupBox
    Friend WithEvents grpPlotOrder As System.Windows.Forms.GroupBox
    Friend WithEvents grpPaperPosition As System.Windows.Forms.GroupBox
    Friend WithEvents Label1 As System.Windows.Forms.Label
    Friend WithEvents grpNameConflict As System.Windows.Forms.GroupBox
    Friend WithEvents grpLayoutSettings As System.Windows.Forms.GroupBox
End Class
