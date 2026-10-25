Imports System.IO
Imports Microsoft.Win32

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
                Dim baseKeyPath = "SOFTWARE\Autodesk\AutoCAD"
                Using baseKey As RegistryKey = Registry.LocalMachine.OpenSubKey(baseKeyPath)
                    If baseKey IsNot Nothing Then
                        For Each versionKeyName As String In baseKey.GetSubKeyNames()
                            Using versionKey As RegistryKey = baseKey.OpenSubKey(versionKeyName)
                                If versionKey IsNot Nothing Then
                                    For Each subKeyName As String In versionKey.GetSubKeyNames()
                                        Using subKey As RegistryKey = versionKey.OpenSubKey(subKeyName)
                                            If subKey IsNot Nothing Then
                                                Dim installPath = TryCast(subKey.GetValue("AcadLocation"), String)
                                                If Not String.IsNullOrEmpty(installPath) AndAlso Directory.Exists(installPath) Then
                                                    ' 版本号在外层键名（R24.3），产品键（ACAD-5101:804）不含版本信息；
                                                    ' 传错会导致缓存键为产品码，后续按年份/前缀的查找全部失效
                                                    Dim version As String = ExtractVersionFromKey(versionKeyName)
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

            ' 注册表只提供安装路径，不提供 Roaming 用户数据目录：无条件补扫 UserData_ 键，
            ' 否则样式/打印机目录的首选查找分支在注册表正常时也永不生效
            TryFillUserDataPaths()

            ' 如果注册表未找到，尝试常见安装路径
            If _cachedPaths.Count = 0 Then
                TryFallbackPaths()
            End If

            Return New Dictionary(Of String, String)(_cachedPaths)
        End SyncLock
    End Function

    ' 补充用户数据目录（Roaming）扫描，填充 UserData_{年份} 键
    Private Shared Sub TryFillUserDataPaths()
        Try
            Dim appData As String = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
            For Each year As String In {"2025", "2024", "2023", "2022", "2021", "2020"}
                Dim userDataPath As String = Path.Combine(appData, "Autodesk", $"AutoCAD {year}")
                If Directory.Exists(userDataPath) Then
                    Dim key As String = $"UserData_{year}"
                    If Not _cachedPaths.ContainsKey(key) Then
                        _cachedPaths(key) = userDataPath
                    End If
                End If
            Next
        Catch
        End Try
    End Sub

    Private Shared Sub TryFallbackPaths()
        Dim programFiles As String = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
        Dim programFilesX86 As String = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
        Dim appData As String = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)

        Dim years As String() = {"2025", "2024", "2023", "2022", "2021", "2020"}

        For Each year As String In years
            ' 安装目录
            Dim installPath As String = Path.Combine(programFiles, "Autodesk", $"AutoCAD {year}")
            If Directory.Exists(installPath) Then
                _cachedPaths(year) = installPath
            Else
                installPath = Path.Combine(programFilesX86, "Autodesk", $"AutoCAD {year}")
                If Directory.Exists(installPath) Then
                    _cachedPaths(year) = installPath
                End If
            End If

            ' 用户数据目录 (Roaming)
            Dim userDataPath As String = Path.Combine(appData, "Autodesk", $"AutoCAD {year}")
            If Directory.Exists(userDataPath) Then
                _cachedPaths($"UserData_{year}") = userDataPath
            End If
        Next
    End Sub

    ' 注册表版本号(Rmajor.minor) -> 年份 显式映射表：以 major*10+minor 作为组合键，
    ' 避免 R24.0/R24.1/R24.2 被旧公式 2000+CInt(版本) 全部误算成同一年后互相合并
    Private Shared ReadOnly s_releaseYearMap As New Dictionary(Of Integer, Integer) From {
        {23 * 10 + 0, 2019},
        {23 * 10 + 1, 2020},
        {24 * 10 + 0, 2021},
        {24 * 10 + 1, 2022},
        {24 * 10 + 2, 2023},
        {24 * 10 + 3, 2024},
        {25 * 10 + 0, 2025}
    }

    Private Shared Function ExtractVersionFromKey(keyName As String) As String
        ' 例如 "R24.3" -> "2024"：拆出 major/minor 按组合键查显式映射表
        If keyName.StartsWith("R") Then
            Dim parts() As String = keyName.Substring(1).Split("."c)
            Dim major As Integer, minor As Integer
            If parts.Length >= 1 AndAlso Integer.TryParse(parts(0), major) Then
                minor = 0
                If parts.Length >= 2 Then
                    Dim minorNum As Integer
                    If Integer.TryParse(parts(1), minorNum) Then minor = minorNum
                End If
                Dim year As Integer
                ' 缓存键由映射表按 minor 区分（不同 minor 对应不同年份），不再互相合并
                If s_releaseYearMap.TryGetValue(major * 10 + minor, year) Then
                    Return year.ToString()
                End If
            End If
        End If
        ' 未知版本原样返回键名（含 minor，天然不合并）
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
                    Dim plotPath As String = Path.Combine(subDir, lang, "Plotters", "Plot Styles")
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

        For i = 0 To years.Length - 1
            For Each lang As String In {"enu", "chs"}
                Dim plotPath As String = Path.Combine(appData, "Autodesk", $"AutoCAD {years(i)}", releases(i), lang, "Plotters", "Plot Styles")
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
                    Dim plotPath As String = Path.Combine(subDir, lang, "Plotters")
                    If Directory.Exists(plotPath) Then
                        Return plotPath
                    End If
                Next
            Next
        Next

        Dim appData As String = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
        Dim years As String() = {"2024", "2023", "2022"}
        Dim releases As String() = {"R24.3", "R24.2", "R24.1"}

        For i = 0 To years.Length - 1
            For Each lang As String In {"enu", "chs"}
                Dim plotPath As String = Path.Combine(appData, "Autodesk", $"AutoCAD {years(i)}", releases(i), lang, "Plotters")
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
            Dim exePath As String = Path.Combine(kvp.Value, "Styexe.exe")
            If File.Exists(exePath) Then
                Return exePath
            End If
        Next

        Dim programFiles As String = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
        Dim programFilesX86 As String = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
        Dim years As String() = {"2024", "2023", "2022", "2021", "2020"}

        For Each year As String In years
            Dim exePath As String = Path.Combine(programFiles, "Autodesk", $"AutoCAD {year}", "Styexe.exe")
            If File.Exists(exePath) Then
                Return exePath
            End If
            ' 32 位安装目录回退（部分版本装在 Program Files (x86)）
            exePath = Path.Combine(programFilesX86, "Autodesk", $"AutoCAD {year}", "Styexe.exe")
            If File.Exists(exePath) Then
                Return exePath
            End If
        Next

        Return String.Empty
    End Function

    Public Shared Function GetSupportPath() As String
        Dim paths As Dictionary(Of String, String) = GetAutoCADPaths()

        For Each kvp In paths.Where(Function(p) Not p.Key.StartsWith("UserData_"))
            Dim supportPath As String = Path.Combine(kvp.Value, "Support")
            If Directory.Exists(supportPath) Then
                Return supportPath
            End If
        Next

        Return String.Empty
    End Function
End Class
