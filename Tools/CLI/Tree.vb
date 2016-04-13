﻿Imports LANS.SystemsBiology.NCBI.Extensions.LocalBLAST.Application
Imports LANS.SystemsBiology.SequenceModel.FASTA
Imports Microsoft.VisualBasic.CommandLine.Reflection
Imports Microsoft.VisualBasic.DocumentFormat.Csv
Imports Microsoft.VisualBasic.Linq.Extensions
Imports Microsoft.VisualBasic
Imports Microsoft.VisualBasic.Text
Imports Microsoft.VisualBasic.DocumentFormat.Csv.DocumentStream

Partial Module CLI

    <ExportAPI("--Export.Fasta", Usage:="--Export.Fasta /hits <query-hits.csv> /query <query.fasta> /subject <subject.fasta>")>
    Public Function ExportFasta(args As CommandLine.CommandLine) As Integer
        Dim hist = args("/hits").LoadCsv(Of BBH.BestHit)
        Dim query = New FastaFile(args("/query")).ToDictionary(Function(x) x.Title.Split.First)
        Dim subject = New FastaFile(args("/subject")).ToDictionary(Function(x) x.Title.Split.First)
        Dim AllLocus As String() = hist.ToArray(Function(x) x.QueryName).Join(hist.ToArray(Function(x) x.HitName)).Distinct.ToArray
        Dim GetFasta = (From id As String In AllLocus Where query.ContainsKey(id) Select query(id)).ToList
        Call GetFasta.Add((From id As String In AllLocus Where subject.ContainsKey(id) Select subject(id)).ToArray)

        Dim out As String = args("/hits").TrimFileExt & ".fasta"
        Return New FastaFile(GetFasta).Save(out).CLICode
    End Function

    <ExportAPI("/Identities.Matrix", Usage:="/Identities.Matrix /hit <sbh/bbh.csv> [/out <out.csv> /cut 0.65]")>
    Public Function IdentitiesMAT(args As CommandLine.CommandLine) As Integer
        Dim hit As String = args("/hit")
        Dim cut As Double = args.GetValue("/cut", 0.65)
        Dim out As String = args.GetValue("/out", hit.TrimFileExt & $"_cut={cut}.csv")
        Dim hits = hit.LoadCsv(Of BBH.BBHIndex)
        Dim Grep As TextGrepMethod = TextGrepScriptEngine.Compile("tokens ' ' first").Method
        For Each x As BBH.BBHIndex In hits
            x.QueryName = Grep(x.QueryName)
            x.HitName = Grep(x.HitName)
        Next
        Dim Groups = (From x As BBH.BBHIndex
                      In hits.AsParallel
                      Select x
                      Group x By x.QueryName Into Group) _
                           .ToDictionary(Function(x) x.QueryName,
                                         Function(x) (From n As BBH.BBHIndex
                                                      In x.Group
                                                      Select n
                                                      Group n By n.HitName Into Group) _
                                                           .ToDictionary(Function(xx) xx.HitName,
                                                                         Function(xx) xx.Group.First))
        Dim allKeys As String() = Groups.Keys.ToArray
        Dim MAT As File = New File + "locus".Join(allKeys)

        For Each query In Groups
            Dim row As New RowObject(query.Key)
            Dim hash As Dictionary(Of String, BBH.BBHIndex) = query.Value

            For Each key As String In allKeys
                If hash.ContainsKey(key) Then
                    row += CStr(hash(key).identities)
                Else
                    row += "0"
                End If
            Next

            MAT += row
        Next

        Return MAT > out
    End Function
End Module
