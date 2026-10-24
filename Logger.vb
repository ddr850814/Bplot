Imports System.IO
Imports System.Text

Public Class Logger
    Private Shared _instance As Logger
    Private Shared ReadOnly _lock As New Object()
    Private ReadOnly _logFilePath As String
    Private ReadOnly _logLevel As LogLevel

    Public Enum LogLevel
        Debug = 0
        Info = 1
        Warning = 2
        [Error] = 3
    End Enum

    Private Sub New()
        Dim logDir As String = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BatchPlotVB", "Logs")
        If Not Directory.Exists(logDir) Then
            Directory.CreateDirectory(logDir)
        End If
        _logFilePath = Path.Combine(logDir, $"BatchPlot_{DateTime.Now:yyyyMMdd}.log")
        _logLevel = LogLevel.Debug
    End Sub

    Public Shared Function GetInstance() As Logger
        If _instance Is Nothing Then
            SyncLock _lock
                If _instance Is Nothing Then
                    _instance = New Logger()
                End If
            End SyncLock
        End If
        Return _instance
    End Function

    Public Sub Debug(message As String)
        WriteLog(LogLevel.Debug, message)
    End Sub

    Public Sub Info(message As String)
        WriteLog(LogLevel.Info, message)
    End Sub

    Public Sub Warning(message As String)
        WriteLog(LogLevel.Warning, message)
    End Sub

    Public Sub [Error](message As String, Optional ex As Exception = Nothing)
        Dim fullMessage As String = message
        If ex IsNot Nothing Then
            fullMessage &= $" | Exception: {ex.Message} | StackTrace: {ex.StackTrace}"
        End If
        WriteLog(LogLevel.Error, fullMessage)
    End Sub

    Private Sub WriteLog(level As LogLevel, message As String)
        If level < _logLevel Then Return

        Try
            Dim logEntry As String = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level.ToString().ToUpper()}] {message}"
            SyncLock _lock
                File.AppendAllText(_logFilePath, logEntry & Environment.NewLine, Encoding.UTF8)
            End SyncLock
        Catch
            ' 日志写入失败时不抛出异常
        End Try
    End Sub
End Class
