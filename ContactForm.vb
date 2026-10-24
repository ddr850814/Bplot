Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms

Partial Public Class ContactForm

    Public Sub New()
        InitializeComponent()
        ApplyModernStyle()
    End Sub

    Private Sub ApplyModernStyle()
        AddHandler btnClose.MouseEnter, AddressOf BtnClose_MouseEnter
        AddHandler btnClose.MouseLeave, AddressOf BtnClose_MouseLeave
        AddHandler btnClose.Paint, AddressOf BtnClose_Paint
        AddHandler panelHeader.Paint, AddressOf PanelHeader_Paint
    End Sub

    Private Sub BtnClose_MouseEnter(sender As Object, e As EventArgs)
        btnClose.BackColor = Color.FromArgb(0, 100, 180)
        btnClose.Cursor = Cursors.Hand
    End Sub

    Private Sub BtnClose_MouseLeave(sender As Object, e As EventArgs)
        btnClose.BackColor = Color.FromArgb(0, 120, 212)
        btnClose.Cursor = Cursors.Default
    End Sub

    Private Sub BtnClose_Paint(sender As Object, e As PaintEventArgs)
        Dim buttonPath As New GraphicsPath()
        Dim rect As Rectangle = btnClose.ClientRectangle
        rect.Width -= 1
        rect.Height -= 1
        Dim radius = 6
        buttonPath.AddArc(rect.X, rect.Y, radius * 2, radius * 2, 180, 90)
        buttonPath.AddArc(rect.Right - radius * 2, rect.Y, radius * 2, radius * 2, 270, 90)
        buttonPath.AddArc(rect.Right - radius * 2, rect.Bottom - radius * 2, radius * 2, radius * 2, 0, 90)
        buttonPath.AddArc(rect.X, rect.Bottom - radius * 2, radius * 2, radius * 2, 90, 90)
        buttonPath.CloseFigure()
        btnClose.Region = New Region(buttonPath)
    End Sub

    Private Sub PanelHeader_Paint(sender As Object, e As PaintEventArgs)
        Using shadowBrush As New SolidBrush(Color.FromArgb(30, 0, 0, 0))
            e.Graphics.FillRectangle(shadowBrush, 0, panelHeader.Height - 2, panelHeader.Width, 2)
        End Using
    End Sub

    Private Sub BtnClose_Click(sender As Object, e As EventArgs) Handles btnClose.Click
        Close()
    End Sub

End Class
