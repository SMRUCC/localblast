﻿#Region "Microsoft.VisualBasic::a8e96f20a6cc4c16d1eb38eb4e45e1bd, ..\localblast\LocalBLAST\LocalBLAST\LocalBLAST\Application\BBH\BidirectionalBesthit_BLAST.vb"

    ' Author:
    ' 
    '       asuka (amethyst.asuka@gcmodeller.org)
    '       xieguigang (xie.guigang@live.com)
    ' 
    ' Copyright (c) 2016 GPL3 Licensed
    ' 
    ' 
    ' GNU GENERAL PUBLIC LICENSE (GPL3)
    ' 
    ' This program is free software: you can redistribute it and/or modify
    ' it under the terms of the GNU General Public License as published by
    ' the Free Software Foundation, either version 3 of the License, or
    ' (at your option) any later version.
    ' 
    ' This program is distributed in the hope that it will be useful,
    ' but WITHOUT ANY WARRANTY; without even the implied warranty of
    ' MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    ' GNU General Public License for more details.
    ' 
    ' You should have received a copy of the GNU General Public License
    ' along with this program. If not, see <http://www.gnu.org/licenses/>.

#End Region

Imports Microsoft.VisualBasic.Extensions
Imports Microsoft.VisualBasic.Terminal.STDIO
Imports Microsoft.VisualBasic.DocumentFormat.Csv.StorageProvider.Reflection
Imports Microsoft.VisualBasic.DocumentFormat.Csv.Extensions
Imports Microsoft.VisualBasic.Text
Imports Microsoft.VisualBasic.DocumentFormat.Csv
Imports LANS.SystemsBiology.SequenceModel.FASTA

Namespace LocalBLAST.Application.BBH

    ''' <summary>
    ''' A tight link between orthologs and bidirectional best hits in bacterial and archaeal genomes. BBH.(通过BLASTP操作来获取两个基因组之间的相同的蛋白质对象)
    ''' </summary>
    ''' <remarks></remarks>
    Public NotInheritable Class BidirectionalBesthit_BLAST

        ''' <summary>
        ''' 本地BLAST的中间服务
        ''' </summary>
        ''' <remarks></remarks>
        Protected Friend _LocalBLASTService As Global.LANS.SystemsBiology.NCBI.Extensions.LocalBLAST.InteropService.InteropService
        Protected Friend _WorkDir As String

        Public ReadOnly Property WorkDir As String
            Get
                Return _WorkDir
            End Get
        End Property

        Public ReadOnly Property LocalBLASTServices As LANS.SystemsBiology.NCBI.Extensions.LocalBLAST.InteropService.InteropService
            Get
                Return Me._LocalBLASTService
            End Get
        End Property

        Sub New(LocalBLAST As LANS.SystemsBiology.NCBI.Extensions.LocalBLAST.InteropService.InteropService, WorkDir As String)
            Me._WorkDir = WorkDir
            Me._LocalBLASTService = LocalBLAST
        End Sub

        ''' <summary>
        ''' 执行BLASTP操作，返回双向最佳匹配的蛋白质列表
        ''' </summary>
        ''' <param name="Query"></param>
        ''' <param name="Subject"></param>
        ''' <param name="HitsGrepMethod">对Hit蛋白质序列的FASTA数据库中的基因号的解析方法</param>
        ''' <param name="QueryGrepMethod">对Query蛋白质序列的FASTA数据库中的基因号的解析方法</param>
        ''' <param name="ExportAll">假若为真的话，则会导出所有的最佳结果，反之，则会导出直系同源的基因</param>
        ''' <returns>返回双相匹配的BestHit列表</returns>
        ''' <remarks></remarks>
        Public Function Peformance(Query As String, Subject As String,
                                   QueryGrepMethod As TextGrepMethod, HitsGrepMethod As TextGrepMethod,
                                   Optional e As String = "1e-3",
                                   Optional ExportAll As Boolean = False) As BBH.BiDirectionalBesthit()

            Call _LocalBLASTService.FormatDb(Query, _LocalBLASTService.MolTypeProtein).Start(WaitForExit:=True)
            Call _LocalBLASTService.FormatDb(Subject, _LocalBLASTService.MolTypeProtein).Start(WaitForExit:=True)

            Dim WorkDir As String = Me._WorkDir & "/" & IO.Path.GetFileNameWithoutExtension(Subject) & "/"

            Call FileIO.FileSystem.CreateDirectory(WorkDir)

#If DEBUG Then
            Call LocalBLASTServices.Blastp(Query, Subject, String.Format("{0}\BLASTP_QUERY_TO_SUBJECT.dat", WorkDir))
#Else
            Call _LocalBLASTService.Blastp(Query, Subject, String.Format("{0}/bbh_query.vs.subject.txt", WorkDir), e).Start(WaitForExit:=True)
#End If

            Dim Log = _LocalBLASTService.GetLastLogFile
            Call Log.Grep(QueryGrepMethod, Nothing) '由于在MetaCyc数据库之中的FASTA文件的标题格式都是一样的，故而在这里就都使用一样的方法来解析名称了
            Dim Log_QvS As Microsoft.VisualBasic.DocumentFormat.Csv.DocumentStream.File = If(ExportAll, Log.ExportAllBestHist, Log.ExportBestHit).ToCsvDoc

            Call Trim(LANS.SystemsBiology.SequenceModel.FASTA.FastaFile.Read(Subject), Log_QvS, HitsGrepMethod)
            Call Log_QvS.Save(String.Format("{0}\{1}_vs_{2}.csv", WorkDir, IO.Path.GetFileNameWithoutExtension(Query), IO.Path.GetFileNameWithoutExtension(Subject)), False)

#If DEBUG Then
            Call LocalBLASTServices.Blastp(Subject, Query, String.Format("{0}\BLASTP_SUBJECT_TO_QUERY.dat", WorkDir))
#Else
            Call _LocalBLASTService.Blastp(Subject, Query, String.Format("{0}/bbh_subject.vs.query.txt", WorkDir), e).Start(WaitForExit:=True)
#End If

            Log = _LocalBLASTService.GetLastLogFile
            Call Log.Grep(HitsGrepMethod, Nothing)
            Dim Log_SvQ = If(ExportAll, Log.ExportAllBestHist, Log.ExportBestHit)
            Call Trim(LANS.SystemsBiology.SequenceModel.FASTA.FastaFile.Read(Query), Log_SvQ.ToCsvDoc, QueryGrepMethod)
            Call Log_SvQ.SaveTo(String.Format("{0}\{1}_vs_{2}.csv", WorkDir, IO.Path.GetFileNameWithoutExtension(Subject), IO.Path.GetFileNameWithoutExtension(Query)), False)

            Call Printf("END_OF_BIDIRECTION_BLAST():: start to build up the best mathced protein pair.")

            Dim result = If(ExportAll, GetDirreBhAll2(Log_SvQ.ToCsvDoc, Log_QvS), BBHTop(Log_SvQ.ToCsvDoc, Log_QvS)) '????顺序反了？
            Return result
        End Function

        Protected Friend Shared Sub Trim(FsaDatabase As FastaFile, Data As DocumentStream.File, GrepMethod As TextGrepMethod)
            If GrepMethod Is Nothing Then
                Return
            End If

            For i As Integer = 1 To Data.Count - 1
                Dim row = Data(i)
                If Not String.IsNullOrEmpty(row(1)) Then
                    row(1) = GrepMethod(row(1))
                End If
            Next
        End Sub

        ''' <summary>
        ''' 自身进行比较去除最佳比对获取旁系同源
        ''' </summary>
        ''' <param name="Fasta"></param>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Public Function Paralogs(Fasta As String, GrepMethod As TextGrepMethod) As BBH.BestHit()
            Dim Output As String = Me._WorkDir & "/" & IO.Path.GetFileNameWithoutExtension(Fasta) & "_paralogs.txt"

            Call Me._LocalBLASTService.FormatDb(Fasta, dbType:=_LocalBLASTService.MolTypeProtein).Start(WaitForExit:=True)
            Call Me._LocalBLASTService.Blastp(Fasta, Fasta, Output).Start(WaitForExit:=True)

            Dim Log = Me._LocalBLASTService.GetLastLogFile

            Call Log.Grep(AddressOf GrepMethod.Invoke, AddressOf GrepMethod.Invoke)

            Dim bh = Log.ExportAllBestHist '符合最佳条件，但是不是自身的记录都是旁系同源
            bh = (From besthit In bh.AsParallel Where Not String.Equals(besthit.QueryName, besthit.HitName, StringComparison.OrdinalIgnoreCase) Select besthit).ToArray

            Return bh
        End Function
    End Class
End Namespace
