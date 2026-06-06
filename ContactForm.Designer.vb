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
    Friend WithEvents lblTitle As Windows.Forms.Label
    Friend WithEvents lblVersion As Windows.Forms.Label
    Friend WithEvents separator As Windows.Forms.Label
    Friend WithEvents lblSupport As Windows.Forms.Label
    Friend WithEvents separator2 As Windows.Forms.Label
    Friend WithEvents lblCopyright As Windows.Forms.Label
End Class
