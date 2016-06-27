﻿Imports System.Text.RegularExpressions
Imports System.Xml.Serialization
Imports LANS.SystemsBiology.ComponentModel.Loci
Imports LANS.SystemsBiology.NCBI.Extensions.LocalBLAST.BLASTOutput.ComponentModel
Imports Microsoft.VisualBasic.Language
Imports Microsoft.VisualBasic.Linq

Namespace LocalBLAST.BLASTOutput.BlastPlus

    Public Class SubjectHit

        <XmlAttribute> Public Property Name As String
        ''' <summary>
        ''' hit蛋白质序列的全长
        ''' </summary>
        ''' <returns></returns>
        <XmlAttribute> Public Property Length As Long
        <XmlElement> Public Property Score As Score

        Public Property Hsp As HitSegment()

        ''' <summary>
        ''' Hit position on the query sequence.
        ''' </summary>
        ''' <returns></returns>
        Public Overridable ReadOnly Property QueryLocation As Location
            Get
                Dim left As Integer = If(Hsp.IsNullOrEmpty, 0, Hsp.First.Query.Left)
                Dim right As Integer = If(Hsp.IsNullOrEmpty, 0, Hsp.Last.Query.Right)
                Return New Location(left, right)
            End Get
        End Property

        Public Overridable ReadOnly Property SubjectLocation As Location
            Get
                Dim left As Integer = If(Hsp.IsNullOrEmpty, 0, Hsp.First.Sbjct.Left)
                Dim right As Integer = If(Hsp.IsNullOrEmpty, 0, Hsp.Last.Sbjct.Right)
                Return New Location(left, right)
            End Get
        End Property

        ''' <summary>
        ''' 高分区的hit片段的长度
        ''' </summary>
        ''' <returns></returns>
        Public ReadOnly Property LengthHit As Integer
            Get
                Dim LQuery As IEnumerable(Of Integer) =
                    LinqAPI.Exec(Of Integer) <= From Segment As HitSegment
                                                In Hsp
                                                Select From ch As Char
                                                       In Segment.Query.SequenceData
                                                       Where ch = "-"c
                                                       Select 1
                Dim value As Integer = LQuery.Sum
                Return Score.Gaps.Denominator - value  ' 减去插入的空格就是比对上的长度了
            End Get
        End Property

        Public ReadOnly Property LengthQuery As Integer
            Get
                Dim LQuery As Integer() =
                    LinqAPI.Exec(Of Integer) <= From Segment As HitSegment
                                                In Hsp
                                                Select From ch As Char
                                                       In Segment.Sbjct.SequenceData
                                                       Where ch = "-"c
                                                       Select 1
                Dim value As Integer = LQuery.Sum
                Return Score.Gaps.Denominator - value
            End Get
        End Property

        Public Overrides Function ToString() As String
            Return String.Format("Name: {0}, Length: {1}", Name, Length)
        End Function

        Public Const NO_HITS_FOUND As String = "No hits found"

        ''' <summary>
        '''
        ''' </summary>
        ''' <param name="text"></param>
        ''' <returns></returns>
        ''' <remarks>请不要在这里使用.AsParallel拓展方法，以保持各个片段的顺序关系</remarks>
        Public Shared Function GetItems(text As String) As SubjectHit()
            If InStr(text, NO_HITS_FOUND) Then
                Return New SubjectHit() {}
            End If

            Dim Tokens = Regex.Split(text, "^>", RegexOptions.Multiline).Skip(1).ToArray
            Dim LQuery As SubjectHit() = Tokens.ToArray(AddressOf SubjectHit.TryParse)
            Return LQuery
        End Function

        Protected Const PAIRWISE As String = "Query\s+\d+\s+.+?\s+\d+.+?Sbjct\s+\d+\s+.+?\s+\d+"

        Public Shared Function TryParse(Text As String) As SubjectHit
            Dim name As String = Strings.Split(Text, "Length=").First.TrimA
            Dim Length As Long = CLng(Text.Match("Length=\d+").RegexParseDouble)

            Dim strHsp As String() =
                Regex.Matches(Text,
                              PAIRWISE,
                              RegexOptions.Singleline +
                              RegexOptions.IgnoreCase).ToArray

            Dim hit As New SubjectHit With {
                .Score = Score.TryParse(Of Score)(Text),
                .Name = name,
                .Length = Length,
                .Hsp = ParseHitSegments(strHsp)
            }

            Return hit
        End Function

        Protected Shared Function ParseHitSegments(TextLines As String()) As HitSegment()
            Dim Hsp As HitSegment() = New HitSegment(TextLines.Length - 1) {}

            For i As Integer = 0 To TextLines.Length - 1
                Dim buffer As String() =
                    LinqAPI.Exec(Of String) <= From s As String
                                               In TextLines(i).lTokens
                                               Select s.Replace(vbCr, "")
                Hsp(i) = HitSegment.TryParse(buffer)
            Next

            Return Hsp
        End Function
    End Class
End Namespace