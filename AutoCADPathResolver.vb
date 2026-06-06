Imports Microsoft.Win32
Imports System.IO

Public Class AutoCADPathResolver

    Private Shared _cachedPaths As Dictionary(Of String, String)
    Private Shared ReadOnly _lock As New Object()

    Public Shared Function GetAutoCADPaths() As Dictionary(Of String, String)
        If _cachedPaths IsNot Nothing Then
            Return New Dictionary(Of String, String)(_cachedPaths)
        End If

        SyncLock _lock
            If _cachedPaths IsNot Nothing Then
                Return New Dictionary(Of String, String)(_cachedPaths)
            End If

            _cachedPaths = New Dictionary(Of String, String)()

            ' 从注册表获取AutoCAD安装路径
            Try
                Dim baseKeyPath As String = "SOFTWARE\Autodesk\AutoCAD"
                Using baseKey As RegistryKey = Registry.LocalMachine.OpenSubKey(baseKeyPath)
                    If baseKey IsNot Nothing Then
                        For Each versionKeyName As String In baseKey.GetSubKeyNames()
                            Using versionKey As RegistryKey = baseKey.OpenSubKey(versionKeyName)
                                If versionKey IsNot Nothing Then
                                    For Each subKeyName As String In versionKey.GetSubKeyNames()
                                        Using subKey As RegistryKey = versionKey.OpenSubKey(subKeyName)
                                            If subKey IsNot Nothing Then
                                                Dim installPath As String = TryCast(subKey.GetValue("AcadLocation"), String)
                                                If Not String.IsNullOrEmpty(installPath) AndAlso Directory.Exists(installPath) Then
                                                    Dim version As String = ExtractVersionFromKey(subKeyName)
                                                    If Not String.IsNullOrEmpty(version) AndAlso Not _cachedPaths.ContainsKey(version) Then
                                                        _cachedPaths(version) = installPath
                                                    End If
                                                End If
                                            End If
                                        End Using
                                    Next
                                End If
                            End Using
                        Next
                    End If
                End Using
            Catch ex As Exception
                Logger.GetInstance().Warning("从注册表读取AutoCAD路径失败: " & ex.Message)
            End Try

            ' 如果注册表未找到，尝试常见安装路径
            If _cachedPaths.Count = 0 Then
                TryFallbackPaths()
            End If

            Return New Dictionary(Of String, String)(_cachedPaths)
        End SyncLock
    End Function

    Private Shared Sub TryFallbackPaths()
        Dim programFiles As String = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
        Dim programFilesX86 As String = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
        Dim appData As String = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)

        Dim years As String() = {"2025", "2024", "2023", "2022", "2021", "2020"}

        For Each year As String In years
            ' 安装目录
            Dim installPath As String = IO.Path.Combine(programFiles, "Autodesk", $"AutoCAD {year}")
            If Directory.Exists(installPath) Then
                _cachedPaths(year) = installPath
            Else
                installPath = IO.Path.Combine(programFilesX86, "Autodesk", $"AutoCAD {year}")
                If Directory.Exists(installPath) Then
                    _cachedPaths(year) = installPath
                End If
            End If

            ' 用户数据目录 (Roaming)
            Dim userDataPath As String = IO.Path.Combine(appData, "Autodesk", $"AutoCAD {year}")
            If Directory.Exists(userDataPath) Then
                _cachedPaths($"UserData_{year}") = userDataPath
            End If
        Next
    End Sub

    Private Shared Function ExtractVersionFromKey(keyName As String) As String
        ' 例如 "R24.3" -> "2024"
        If keyName.StartsWith("R") Then
            Dim versionNum As Double
            If Double.TryParse(keyName.Substring(1), versionNum) Then
                ' R24.x 对应 2024
                Return (2000 + CInt(versionNum)).ToString()
            End If
        End If
        Return keyName
    End Function

    Public Shared Function GetPlotStylesPath() As String
        Dim paths As Dictionary(Of String, String) = GetAutoCADPaths()

        ' 优先从用户数据目录查找
        For Each kvp In paths.Where(Function(p) p.Key.StartsWith("UserData_"))
            Dim basePath As String = kvp.Value
            Dim subDirs As String() = Directory.GetDirectories(basePath, "R*")
            For Each subDir As String In subDirs
                For Each lang As String In {"enu", "chs"}
                    Dim plotPath As String = IO.Path.Combine(subDir, lang, "Plotters", "Plot Styles")
                    If Directory.Exists(plotPath) Then
                        Return plotPath
                    End If
                Next
            Next
        Next

        ' 回退到硬编码路径
        Dim appData As String = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
        Dim years As String() = {"2024", "2023", "2022"}
        Dim releases As String() = {"R24.3", "R24.2", "R24.1"}

        For i As Integer = 0 To years.Length - 1
            For Each lang As String In {"enu", "chs"}
                Dim plotPath As String = IO.Path.Combine(appData, "Autodesk", $"AutoCAD {years(i)}", releases(i), lang, "Plotters", "Plot Styles")
                If Directory.Exists(plotPath) Then
                    Return plotPath
                End If
            Next
        Next

        Return String.Empty
    End Function

    Public Shared Function GetPlottersPath() As String
        Dim paths As Dictionary(Of String, String) = GetAutoCADPaths()

        For Each kvp In paths.Where(Function(p) p.Key.StartsWith("UserData_"))
            Dim basePath As String = kvp.Value
            Dim subDirs As String() = Directory.GetDirectories(basePath, "R*")
            For Each subDir As String In subDirs
                For Each lang As String In {"enu", "chs"}
                    Dim plotPath As String = IO.Path.Combine(subDir, lang, "Plotters")
                    If Directory.Exists(plotPath) Then
                        Return plotPath
                    End If
                Next
            Next
        Next

        Dim appData As String = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
        Dim years As String() = {"2024", "2023", "2022"}
        Dim releases As String() = {"R24.3", "R24.2", "R24.1"}

        For i As Integer = 0 To years.Length - 1
            For Each lang As String In {"enu", "chs"}
                Dim plotPath As String = IO.Path.Combine(appData, "Autodesk", $"AutoCAD {years(i)}", releases(i), lang, "Plotters")
                If Directory.Exists(plotPath) Then
                    Return plotPath
                End If
            Next
        Next

        Return String.Empty
    End Function

    Public Shared Function GetStyexePath() As String
        Dim paths As Dictionary(Of String, String) = GetAutoCADPaths()

        For Each kvp In paths.Where(Function(p) Not p.Key.StartsWith("UserData_"))
            Dim exePath As String = IO.Path.Combine(kvp.Value, "Styexe.exe")
            If File.Exists(exePath) Then
                Return exePath
            End If
        Next

        Dim programFiles As String = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
        Dim years As String() = {"2024", "2023", "2022", "2021", "2020"}

        For Each year As String In years
            Dim exePath As String = IO.Path.Combine(programFiles, "Autodesk", $"AutoCAD {year}", "Styexe.exe")
            If File.Exists(exePath) Then
                Return exePath
            End If
        Next

        Return String.Empty
    End Function

    Public Shared Function GetSupportPath() As String
        Dim paths As Dictionary(Of String, String) = GetAutoCADPaths()

        For Each kvp In paths.Where(Function(p) Not p.Key.StartsWith("UserData_"))
            Dim supportPath As String = IO.Path.Combine(kvp.Value, "Support")
            If Directory.Exists(supportPath) Then
                Return supportPath
            End If
        Next

        Return String.Empty
    End Function
End Class
