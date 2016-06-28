﻿#Region "Microsoft.VisualBasic::06b401785b8ced5b0b1ae36fdf398cbc, ..\localblast\LocalBLAST\LocalBLAST\BlastOutput\Xml\BlastOutput.vb"

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

Imports System.Xml.Serialization
Imports Microsoft.VisualBasic.Extensions
Imports Microsoft.VisualBasic.Linq.Extensions
Imports Microsoft.VisualBasic.Text

Namespace LocalBLAST.BLASTOutput.XmlFile

    Public Class Parameters
        <XmlElement("Parameters_expect")> Public Property Expect As String
        <XmlElement("Parameters_sc-match")> Public Property ScMatch As String
        <XmlElement("Parameters_sc-mismatch")> Public Property ScMismatch As String
        <XmlElement("Parameters_gap-open")> Public Property GapOpen As String
        <XmlElement("Parameters_gap-extend")> Public Property GapExtend As String
        <XmlElement("Parameters_filter")> Public Property Filter As String
    End Class

    Public Class BlastOutput : Inherits LocalBLAST.BLASTOutput.IBlastOutput

        <XmlElement("BlastOutput_program")> Public Property Program As String
        <XmlElement("BlastOutput_version")> Public Property Version As String
        <XmlElement("BlastOutput_reference")> Public Property Reference As String
        <XmlElement("BlastOutput_db")> Public Overrides Property Database As String
            Get
                Return MyBase.Database
            End Get
            Set(value As String)
                MyBase.Database = value
            End Set
        End Property
        <XmlElement("BlastOutput_query-ID")> Public Property QueryId As String
        <XmlElement("BlastOutput_query-def")> Public Property QueryDef As String
        <XmlElement("BlastOutput_query-len")> Public Property QueryLen As String
        <XmlArray("BlastOutput_param")> Public Property Param As Parameters()
        <XmlArray("BlastOutput_iterations")> Public Property Iterations As Iteration()

        Public Overrides Function Grep(QueryGrepMethod As TextGrepMethod, HitsGrepMethod As TextGrepMethod) As IBlastOutput
            Dim Queries = Me.Iterations
            If Not QueryGrepMethod Is Nothing Then
                Dim LQuery = (From idx As Integer In Iterations.Count.Sequence
                              Let Query As Iteration = Queries(idx) Select Query.GrepQuery(QueryGrepMethod)).ToArray '
            End If
            If Not HitsGrepMethod Is Nothing Then
                Dim LQuery = (From idx As Integer In Iterations.Count.Sequence
                              Let Query As Iteration = Queries(idx) Select Query.GrepHits(HitsGrepMethod)).ToArray '
            End If

            Return Me
        End Function

        Public Overrides Function CheckIntegrity(QuerySource As SequenceModel.FASTA.FastaFile) As Boolean
            Throw New NotImplementedException
        End Function

        Public Overrides Function ExportBestHit(Optional coverage As Double = 0.5, Optional identities_cutoff As Double = 0.15) As LocalBLAST.Application.BBH.BestHit()
            Return (From Iteration As Iteration In Iterations Select Iteration.BestHit(identities_cutoff)).ToArray
        End Function

        Public Overrides Function Save(Optional FilePath As String = "", Optional Encoding As Text.Encoding = Nothing) As Boolean
            If String.IsNullOrEmpty(FilePath) Then
                FilePath = Me.FilePath
            Else
                MyBase.FilePath = FilePath
            End If

            Dim Xml As String = Me.GetXml

            If Encoding Is Nothing Then Encoding = System.Text.Encoding.UTF8

            Call FileIO.FileSystem.CreateDirectory(FileIO.FileSystem.GetParentPath(FilePath))
            Return Xml.SaveTo(FilePath, encoding:=Encoding)
        End Function

        ''' <summary>
        ''' 
        ''' </summary>
        ''' <param name="XmlPath">Xml文件的文件路径</param>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Public Shared Function LoadFromFile(XmlPath As String) As BlastOutput
            Dim BlastOutput As BlastOutput = XmlPath.LoadXml(Of BlastOutput)()
            BlastOutput.FilePath = XmlPath
            Return BlastOutput
        End Function

        Public Overrides Function ExportOverview() As LocalBLAST.BLASTOutput.Views.Overview
            Dim LQuery = Me.Iterations.ToArray(Function(x) __toQuery(x))
            Return New Views.Overview With {.Queries = LQuery}
        End Function

        Private Shared Function __toQuery(query As Iteration) As Views.Query
            Dim queryName As String = query.QueryId
            Dim hits = query.Hits.ToArray(Function(x) __toHit(x, query))
            Return New Views.Query With {.Id = queryName, .Hits = hits.MatrixToVector}
        End Function

        Private Shared Function __toHit(hit As XmlFile.Hits.Hit, query As Iteration) As LocalBLAST.Application.BBH.BestHit()
            Dim list = hit.Hsps.ToArray(Function(hsp) New LocalBLAST.Application.BBH.BestHit With {
                .HitName = hit.Id & "| " & hit.Def,
                .hit_length = hit.HitLength,
                .length_hit = hit.Len,
                .identities = hsp.Identity,
                .QueryName = query.QueryId & "| " & query.QueryDef,
                .query_length = query.QueryLen,
                .length_query = hsp.AlignLen,
                .evalue = hsp.Evalue,
                .length_hsp = hsp.AlignLen,
                .Positive = hsp.Positive,
                .Score = hsp.BitScore})
            Return list
        End Function

        Public Overrides Function ExportAllBestHist(Optional coverage As Double = 0.5, Optional identities_cutoff As Double = 0.15) As LocalBLAST.Application.BBH.BestHit()
            Throw New NotImplementedException
        End Function
    End Class
End Namespace
