﻿Imports System.IO

Public Class Form1

    Dim vbQuote As Char = """"c
    Dim MakeFilePath As String = Application.StartupPath() & "\make.bat"
    Dim VulkanPath As String = Application.StartupPath() & "\" & "waifu2x-ncnn-vulkan.exe"
    Dim CaffePath As String = Application.StartupPath() & "\" & "waifu2x-caffe-cui.exe"
    Dim WaitScale As Integer = 0
    Dim Mode As String = "noise"
    Dim UsedExtensions As String() = {".png", ".jpg", ".jpeg", ".tif", ".tiff", ".bmp", ".tga"}

    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        ModeComboBox.SelectedIndex = My.Settings.Mode
        ComputeComboBox.SelectedIndex = My.Settings.Method
        TAAComboBox.SelectedIndex = My.Settings.TAA
        If File.Exists(CaffePath) AndAlso (Not File.Exists(VulkanPath)) Then
            ExeComboBox.SelectedIndex = 0
        ElseIf (Not File.Exists(CaffePath)) AndAlso File.Exists(VulkanPath) Then
            ExeComboBox.SelectedIndex = 1
        ElseIf File.Exists(CaffePath) AndAlso File.Exists(VulkanPath) Then
            ExeComboBox.SelectedIndex = 0
            ExeComboBox.Enabled = True
        End If
    End Sub

    Private Sub Form1_Closing(sender As Object, e As EventArgs) Handles MyBase.Closing
        My.Settings.Mode = ModeComboBox.SelectedIndex
        My.Settings.Method = ComputeComboBox.SelectedIndex
        My.Settings.TAA = TAAComboBox.SelectedIndex
    End Sub

    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click
        InputTextBox.Text = GetFolder()
    End Sub

    Private Sub Button2_Click(sender As Object, e As EventArgs) Handles Button2.Click
        OutputTextBox.Text = GetFolder()
    End Sub

    Private Sub Button3_Click(sender As Object, e As EventArgs) Handles Button3.Click
        If (Not (Directory.Exists(InputTextBox.Text) = True)) OrElse (Not (Directory.Exists(OutputTextBox.Text) = True)) Then
            MsgBox("No path specified, or path invalid!", MsgBoxStyle.Critical, "Error")
        ElseIf (Not File.Exists(CaffePath)) AndAlso (Not File.Exists(VulkanPath)) Then
            MsgBox("No Waifu2x executable found!", MsgBoxStyle.Critical, "Error")
        Else
            Button3.Text = "Running: " & Not WatchDog.Enabled
            WatchDog.Enabled = Not WatchDog.Enabled
            GroupBox1.Enabled = Not WatchDog.Enabled
            GroupBox2.Enabled = Not WatchDog.Enabled
            GroupBox3.Enabled = Not WatchDog.Enabled
        End If
    End Sub

    Private Sub ModeComboBox_SelectedIndexChanged(sender As Object, e As EventArgs) Handles ModeComboBox.SelectedIndexChanged
        Select Case ModeComboBox.SelectedIndex
            Case 0
                NumericNoise.Enabled = True
                NumericScale.Enabled = False
                Mode = "noise"
            Case 1
                NumericNoise.Enabled = False
                NumericScale.Enabled = True
                Mode = "scale"
            Case 2
                NumericNoise.Enabled = True
                NumericScale.Enabled = True
                Mode = "noise_scale"
            Case 3
                NumericNoise.Enabled = True
                NumericScale.Enabled = True
                Mode = "auto_scale"
        End Select
    End Sub
    Private Sub ExeComboBox_SelectedIndexChanged(sender As Object, e As EventArgs) Handles ExeComboBox.SelectedIndexChanged
        Select Case ExeComboBox.SelectedIndex
            Case 0
                ModeComboBox.Enabled = True
                ComputeComboBox.Enabled = True
            Case 1
                ModeComboBox.SelectedIndex = 3
                ModeComboBox.Enabled = False
                ComputeComboBox.Enabled = False
        End Select
    End Sub

    Private Sub WatchDog_Tick(sender As Object, e As EventArgs) Handles WatchDog.Tick
        Dim Source As List(Of String) = GetFileNameList(InputTextBox.Text)
        Dim Dest As List(Of String) = GetFileNameList(OutputTextBox.Text)
        Dim FileCheck As Boolean = Dest.SequenceEqual(Source)
        If FileCheck = True Or Source.Count = 0 Then
            WaitScale = Math.Min(WaitScale + 1, 100)
            WatchDog.Interval = 1000 + (WaitScale * 590)
        Else
            WaitScale = 0
            WatchDog.Interval = 1000 + (WaitScale * 590)
            Dim NewImages As New List(Of String)
            Dim DiffImages = Source.Except(Dest)
            For Each NewImage As String In DiffImages
                If File.Exists(InputTextBox.Text & "\" & NewImage) AndAlso UsedExtensions.Contains(Path.GetExtension(NewImage).ToLower) Then
                    NewImages.Add(InputTextBox.Text & "\" & NewImage)
                End If
            Next
            If NewImages.Count > 0 Then
                MakeWaifus(NewImages.ToArray)
            End If
        End If
    End Sub

    Private Sub MakeWaifus(Source As String())
        WatchDog.Enabled = False
        Dim BatchText As String = ""
        For Each OldImage As String In Source
            Dim NewImage As String = OutputTextBox.Text & "\" & Path.GetFileName(OldImage)
            Select Case ExeComboBox.SelectedIndex
                Case 0
                    BatchText += MakeCaffeCommand(OldImage, NewImage)
                Case 1
                    BatchText += MakeVulkanCommand(OldImage, NewImage)
            End Select
        Next
        File.WriteAllText(MakeFilePath, BatchText)
        Dim BuildProcess As New ProcessStartInfo(MakeFilePath)
        Select Case DebugCheckBox.Checked
            Case True
                BuildProcess.WindowStyle = ProcessWindowStyle.Normal
            Case Else
                BuildProcess.WindowStyle = ProcessWindowStyle.Hidden
        End Select
        Dim BatchProcess As Process = Process.Start(BuildProcess)
        BatchProcess.WaitForExit()
        File.Delete(MakeFilePath)
        If CheckBox1.Checked = True Then
            For Each OldImage As String In Source
                File.Delete(OldImage)
            Next
        End If
        WatchDog.Enabled = True
    End Sub

    Private Function MakeCaffeCommand(OldImage As String, NewImage As String) As String
        Dim Result As String = ""
        Result += vbQuote & CaffePath & vbQuote
        Result += " -m " & Mode
        Result += " -i " & vbQuote & OldImage & vbQuote
        Result += " -o " & vbQuote & NewImage & vbQuote
        Result += " -n " & NumericNoise.Value
        Result += " -s " & NumericScale.Value
        Result += " -t " & TAAComboBox.SelectedIndex
        Result += " -p " & ComputeComboBox.SelectedItem.ToString.ToLower
        Result += " --gpu " & NumericGPU.Value
        Result += vbNewLine & IIf(DebugCheckBox.Checked = True, "pause" & vbNewLine, "")
        Return Result
    End Function

    Private Function MakeVulkanCommand(OldImage As String, NewImage As String) As String
        Dim Result As String = ""
        Result += vbQuote & VulkanPath & vbQuote
        Result += " -i " & vbQuote & OldImage & vbQuote
        Result += " -o " & vbQuote & NewImage.Remove(NewImage.Count - 3, 3) & "png" & vbQuote
        Result += " -n " & NumericNoise.Value
        Result += " -s " & NumericScale.Value
        Result += IIf(TAAComboBox.SelectedIndex = 1, " -x ", "")
        Result += " -g " & NumericGPU.Value
        Result += vbNewLine & IIf(DebugCheckBox.Checked = True, "pause" & vbNewLine, "")
        Return Result
    End Function

    Private Function GetFolder() As String
        Using FBD As New FolderBrowserDialog
            If FBD.ShowDialog = DialogResult.OK Then
                Return FBD.SelectedPath
            End If
        End Using
        Return ""
    End Function

    Private Function GetFileNameList(Source As String) As List(Of String)
        Dim Result As New List(Of String)
        For Each File As String In Directory.GetFileSystemEntries(Source, "*.*", SearchOption.TopDirectoryOnly).ToList
            Result.Add(Path.GetFileName(File))
        Next
        Return Result
    End Function

End Class