﻿Imports Microsoft.VisualBasic.CommandLine.Reflection
Imports Microsoft.VisualBasic.DocumentFormat.Csv
Imports Microsoft.VisualBasic.DocumentFormat.Csv.DocumentStream
Imports Microsoft.VisualBasic.DocumentFormat.Csv.Extensions
Imports Microsoft.VisualBasic.Linq.Extensions

Partial Module CLI

    Private Function __evalueRow(hitsTags As String(), queryName As String, hashHits As Dictionary(Of String, LocalBLAST.Application.BBH.BestHit()), flip As Boolean) As RowObject
        Dim row As New DocumentStream.RowObject From {queryName}

        For Each hit As String In hitsTags

            If flip Then

                If hashHits.ContainsKey(hit) Then
                    Dim e As Double = hashHits(hit).First.evalue

                    If e = 0R Then
                        Call row.Add("1")
                    Else
                        Call row.Add(CStr(1 - e))
                    End If

                Else
                    Call row.Add("0")
                End If

            Else
                If hashHits.ContainsKey(hit) Then
                    Call row.Add(hashHits(hit).First.evalue)
                Else
                    Call row.Add("-1")
                End If
            End If


        Next

        Return row
    End Function

    Private Function __HitsRow(hitsTags As String(), queryName As String, hashHits As Dictionary(Of String, LocalBLAST.Application.BBH.BestHit())) As RowObject
        Dim row As New DocumentStream.RowObject From {queryName}

        For Each hit As String In hitsTags
            If hashHits.ContainsKey(hit) Then
                Call row.Add(hashHits(hit).Length)
            Else
                Call row.Add("0")
            End If
        Next

        Return row
    End Function

    <ExportAPI("/MAT.evalue", Usage:="/MAT.evalue /in <sbh.csv> [/out <mat.csv> /flip]")>
    Public Function EvalueMatrix(args As CommandLine.CommandLine) As Integer
        Dim sbh = args("/in").LoadCsv(Of NCBI.Extensions.LocalBLAST.Application.BBH.BestHit)
        Dim out As String = args.GetValue("/out", args("/in").TrimFileExt & ".Evalue.Csv")
        Dim contigs = (From x As LocalBLAST.Application.BBH.BestHit In sbh
                       Select x
                       Group x By x.QueryName Into Group).ToDictionary(Function(x) x.QueryName, Function(x) (From y In x.Group Select y Group y By y.HitName Into Group).ToDictionary(Function(xx) xx.HitName, elementSelector:=Function(xx) xx.Group.ToArray))
        Dim hitsTags = (From x In sbh Select x.HitName Distinct).ToArray
        Dim flip As Boolean = args.GetBoolean("/flip")
        Dim LQuery = (From contig In contigs.AsParallel Select __evalueRow(hitsTags, contig.Key, contig.Value, flip)).ToArray
        Dim hits = (From contig In contigs.AsParallel Select __HitsRow(hitsTags, contig.Key, contig.Value)).ToArray

        Dim Csv As File = New File + New RowObject("+".Join(hitsTags))
        Csv += LQuery
        Csv.Save(out, Encoding:=System.Text.Encoding.ASCII)

        Csv = New File + New RowObject("+".Join(hitsTags))
        Csv += hits

        Return Csv.Save(out & ".Hits.Csv", Encoding:=System.Text.Encoding.ASCII).CLICode
    End Function

    <ExportAPI("/SBH.Export.Large", Usage:="/SBH.Export.Large /in <blast_out.txt> [/out <bbh.csv> /identities 0.15 /coverage 0.5]")>
    Public Function ExportBBHLarge(args As CommandLine.CommandLine) As Integer
        Dim inFile As String = args("/in")
        Dim out As String = args.GetValue("/out", inFile.TrimFileExt & ".bbh.Csv")
        Dim idetities As Double = args.GetValue("/identities", 0.15)
        Dim coverage As Double = args.GetValue("/coverage", 0.5)

        Using IO As New DocumentStream.Linq.WriteStream(Of NCBI.Extensions.LocalBLAST.Application.BBH.BestHit)(out)
            Dim handle = IO.ToArray(Of NCBI.Extensions.LocalBLAST.BLASTOutput.BlastPlus.Query)(
                Function(query As LocalBLAST.BLASTOutput.BlastPlus.Query) _
                    LocalBLAST.BLASTOutput.BlastPlus.v228.SBHLines(query, coverage:=coverage, identities:=idetities))
            Call LocalBLAST.BLASTOutput.BlastPlus.Transform(inFile, 1024 * 1024 * 128, handle)

            Return 0
        End Using
    End Function

    <ExportAPI("--Export.SBH", Usage:="--Export.SBH /in <in.DIR> /prefix <queryName> /out <out.csv> [/txt]")>
    Public Function ExportSBH(args As CommandLine.CommandLine) As Integer
        Dim inDIR As String = args("/in")
        Dim query As String = args("/prefix")
        Dim isTxtLog As Boolean = args.GetBoolean("/txt")
        Dim out As String = args("/out")
        Dim lst As String() = LANS.SystemsBiology.NCBI.Extensions.Analysis.BBHLogs.LoadSBHEntry(inDIR, query)
        Dim blastp As LANS.SystemsBiology.NCBI.Extensions.LocalBLAST.Application.BBH.BestHit()()

        If isTxtLog Then
            blastp = lst.ToArray(Function(x) LANS.SystemsBiology.NCBI.Extensions.LocalBLAST.BLASTOutput.BlastPlus.Parser.TryParse(x).ExportAllBestHist)
        Else
            blastp = lst.ToArray(Function(x) x.LoadCsv(Of LANS.SystemsBiology.NCBI.Extensions.LocalBLAST.Application.BBH.BestHit).ToArray)
        End If

        Dim LQuery = (From x In blastp Select x.ToArray(Function(xx) xx, where:=Function(xx) xx.Matched)).ToArray.MatrixToList
        Return LQuery.SaveTo(out).CLICode
    End Function

    <ExportAPI("--Export.Overviews", Usage:="--Export.Overviews /blast <blastout.txt> [/out <overview.csv>]")>
    Public Function ExportOverviews(args As CommandLine.CommandLine) As Integer
        Dim inFile As String = args("/blast")
        Dim fileInfo = FileIO.FileSystem.GetFileInfo(inFile)
        Dim blastOut As LANS.SystemsBiology.NCBI.Extensions.LocalBLAST.BLASTOutput.BlastPlus.v228

        If fileInfo.Length >= 768 * 1024 * 1024 Then
            blastOut = LocalBLAST.BLASTOutput.BlastPlus.Parser.TryParseUltraLarge(inFile)
        Else
            blastOut = LocalBLAST.BLASTOutput.BlastPlus.Parser.TryParse(inFile)
        End If

        Dim overviews As LocalBLAST.Application.BBH.BestHit() =
            blastOut.ExportOverview.GetExcelData
        Dim out As String = args.GetValue("/out", inFile.TrimFileExt & "Overviews.csv")

        Return overviews.SaveTo(out).CLICode
    End Function
End Module