Imports System.IO
Imports System.Collections.Generic

Public Class ConfigManager
    Private ReadOnly _configDir As String
    Private ReadOnly _logger As Logger

    Public Sub New()
        _configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BatchPlotVB", "Presets")
        _logger = Logger.GetInstance()
        EnsureDirectoryExists()
    End Sub

    Private Sub EnsureDirectoryExists()
        If Not Directory.Exists(_configDir) Then
            Directory.CreateDirectory(_configDir)
        End If
    End Sub

    Public Function GetPresetNames() As List(Of String)
        Dim presets As New List(Of String)()
        Try
            If Directory.Exists(_configDir) Then
                Dim cfgFiles As String() = Directory.GetFiles(_configDir, "*.cfg")
                For Each file As String In cfgFiles
                    presets.Add(Path.GetFileNameWithoutExtension(file))
                Next
            End If
        Catch ex As Exception
            _logger.Error("加载预配置列表失败", ex)
        End Try
        Return presets
    End Function

    Public Sub SavePreset(name As String, config As Dictionary(Of String, String))
        Try
            EnsureDirectoryExists()
            Dim filePath As String = Path.Combine(_configDir, $"{name}.cfg")
            Using writer As New StreamWriter(filePath, False)
                For Each kvp As KeyValuePair(Of String, String) In config
                    writer.WriteLine($"{kvp.Key}={kvp.Value}")
                Next
            End Using
            _logger.Info($"预配置 '{name}' 已保存")
        Catch ex As Exception
            _logger.Error($"保存预配置 '{name}' 失败", ex)
            Throw
        End Try
    End Sub

    Public Function LoadPreset(name As String) As Dictionary(Of String, String)
        Dim config As New Dictionary(Of String, String)()
        Try
            Dim filePath As String = Path.Combine(_configDir, $"{name}.cfg")
            If Not File.Exists(filePath) Then
                _logger.Warning($"预配置文件不存在: {filePath}")
                Return config
            End If

            Using reader As New StreamReader(filePath)
                Dim line As String = reader.ReadLine()
                While line IsNot Nothing
                    Dim parts As String() = line.Split("="c)
                    If parts.Length >= 2 Then
                        config(parts(0)) = parts(1)
                    End If
                    line = reader.ReadLine()
                End While
            End Using
            _logger.Info($"预配置 '{name}' 已加载")
        Catch ex As Exception
            _logger.Error($"加载预配置 '{name}' 失败", ex)
        End Try
        Return config
    End Function

    Public Function PresetExists(name As String) As Boolean
        Dim filePath As String = Path.Combine(_configDir, $"{name}.cfg")
        Return File.Exists(filePath)
    End Function

    Public Sub DeletePreset(name As String)
        Try
            Dim filePath As String = Path.Combine(_configDir, $"{name}.cfg")
            If File.Exists(filePath) Then
                File.Delete(filePath)
                _logger.Info($"预配置 '{name}' 已删除")
            End If
        Catch ex As Exception
            _logger.Error($"删除预配置 '{name}' 失败", ex)
            Throw
        End Try
    End Sub
End Class
