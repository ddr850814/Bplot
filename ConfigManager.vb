Imports System.IO

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
            Dim sb As New System.Text.StringBuilder()
            For Each kvp As KeyValuePair(Of String, String) In config
                sb.AppendLine($"{kvp.Key}={kvp.Value}")
            Next
            WriteTextAtomic(filePath, sb.ToString())
            _logger.Info($"预配置 '{name}' 已保存")
        Catch ex As Exception
            _logger.Error($"保存预配置 '{name}' 失败", ex)
            Throw
        End Try
    End Sub

    ' 原子写入配置：先写唯一临时文件，再 File.Replace/Move 换入；
    ' 目标被杀毒软件或另一 CAD 实例短暂占用时退避重试，仍失败则兜底直接覆盖复制，
    ' 避免 CAD 崩溃/文件被锁时把配置写坏
    Private Shared ReadOnly s_retryDelaysMs As Integer() = {50, 100, 200}

    Private Shared Sub WriteTextAtomic(filePath As String, contents As String)
        Dim directory As String = Path.GetDirectoryName(filePath)
        Dim tempPath As String = Path.Combine(directory, Path.GetFileName(filePath) & "." & Guid.NewGuid().ToString("N") & ".tmp")
        Try
            File.WriteAllText(tempPath, contents)
            For attempt = 0 To s_retryDelaysMs.Length
                If attempt > 0 Then System.Threading.Thread.Sleep(s_retryDelaysMs(attempt - 1))
                Try
                    If File.Exists(filePath) Then
                        File.Replace(tempPath, filePath, filePath & ".bak", True)
                    Else
                        File.Move(tempPath, filePath)
                    End If
                    Return
                Catch
                End Try
            Next
            ' 重试窗口内始终无法换入：直接覆盖复制（失败时异常交给调用方）
            File.Copy(tempPath, filePath, True)
        Finally
            Try
                If File.Exists(tempPath) Then File.Delete(tempPath)
            Catch
            End Try
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
                    ' 值中可含"="（如名称模板）：只按第一个等号拆分，Split("=") 会把值截断
                    Dim eqIdx As Integer = line.IndexOf("="c)
                    If eqIdx > 0 Then
                        config(line.Substring(0, eqIdx)) = line.Substring(eqIdx + 1)
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

#Region "UI State Management"

    Private ReadOnly _uiStateFile As String = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BatchPlotVB", "UIState.cfg")

    ''' <summary>
    ''' 保存控件状态到文件
    ''' </summary>
    Public Sub SaveUIState(state As Dictionary(Of String, String))
        Try
            Dim stateDir As String = Path.GetDirectoryName(_uiStateFile)
            If Not Directory.Exists(stateDir) Then
                Directory.CreateDirectory(stateDir)
            End If

            Dim sb As New System.Text.StringBuilder()
            For Each kvp As KeyValuePair(Of String, String) In state
                sb.AppendLine($"{kvp.Key}={kvp.Value}")
            Next
            WriteTextAtomic(_uiStateFile, sb.ToString())
            _logger.Info("控件状态已保存")
        Catch ex As Exception
            _logger.Error("保存控件状态失败", ex)
        End Try
    End Sub

    ''' <summary>
    ''' 从文件加载控件状态
    ''' </summary>
    Public Function LoadUIState() As Dictionary(Of String, String)
        Dim state As New Dictionary(Of String, String)()
        Try
            If Not File.Exists(_uiStateFile) Then
                Return state
            End If

            Using reader As New StreamReader(_uiStateFile)
                Dim line As String = reader.ReadLine()
                While line IsNot Nothing
                    ' 值中可含"="：只按第一个等号拆分，避免值被截断
                    Dim eqIdx As Integer = line.IndexOf("="c)
                    If eqIdx > 0 Then
                        state(line.Substring(0, eqIdx)) = line.Substring(eqIdx + 1)
                    End If
                    line = reader.ReadLine()
                End While
            End Using
            _logger.Info("控件状态已加载")
        Catch ex As Exception
            _logger.Error("加载控件状态失败", ex)
        End Try
        Return state
    End Function

#End Region

End Class
