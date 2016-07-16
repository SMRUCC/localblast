﻿#Region "Microsoft.VisualBasic::2fb69fc2e80913878232268c164d8017, ..\interops\localblast\LocalBLAST\LocalBLAST\BlastOutput\Reader\Blast+\2.2.28\BlastX\BlastX.vb"

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

Imports System.Text
Imports Microsoft.VisualBasic
Imports Microsoft.VisualBasic.Linq
Imports Microsoft.VisualBasic.Text
Imports SMRUCC.genomics.Interops.NCBI.Extensions.LocalBLAST.Application
Imports SMRUCC.genomics.Interops.NCBI.Extensions.LocalBLAST.BLASTOutput.Views

Namespace LocalBLAST.BLASTOutput.BlastPlus.BlastX

    Public Class BlastXHit : Inherits BBH.BestHit
        Public Property Frame As String

        Sub New()

        End Sub

        Sub New(query As Components.Query, hit As Components.HitFragment)
            Me.evalue = hit.Score.Expect
            Me.Frame = hit.ReadingFrameOffSet
            Me.HitName = hit.HitName
            Me.hit_length = hit.HitLen
            Me.identities = hit.Score.Identities
            Me.length_hit = hit.SubjectLength
            Me.length_hsp = hit.SubjectLength
            Me.length_query = hit.QueryLoci.FragmentSize
            Me.Positive = hit.Score.Positives
            Me.QueryName = query.QueryName
            Me.query_length = query.QueryLength
            Me.Score = hit.Score.Score
        End Sub

        Public Shared Function CreateObjects(query As Components.Query) As BlastXHit()
            Dim LQuery = (From x In query.Hits Select New BlastXHit(query, x)).ToArray
            Return LQuery
        End Function
    End Class

    Public Class v228_BlastX : Inherits IBlastOutput

        Public Property Queries As BlastX.Components.Query()

        Public Function BlastXHits() As BlastXHit()
            Return ExportAllBestHist(0, 0).ToArray(Function(x) DirectCast(x, BlastXHit))
        End Function

        Public Overrides Function ExportAllBestHist(Optional coverage As Double = 0.5, Optional identities_cutoff As Double = 0.15) As BBH.BestHit()
            Dim list As New List(Of BBH.BestHit)

            For Each query As Components.Query In Queries
                For Each x In BlastXHit.CreateObjects(query)
                    Call list.Add(x)
                Next
            Next

            Return list.ToArray
        End Function

        Public Overrides Function ExportBestHit(Optional coverage As Double = 0.5, Optional identities_cutoff As Double = 0.15) As BBH.BestHit()
            Throw New NotImplementedException
        End Function

        Public Overrides Function ExportOverview() As Overview
            Throw New NotImplementedException
        End Function

        Public Overrides Function Grep(Query As TextGrepMethod, Hits As TextGrepMethod) As IBlastOutput
            If Not Query Is Nothing Then
                For Each QueryObject As Components.Query In Queries
                    QueryObject.QueryName = Query(QueryObject.QueryName)
                Next
            End If
            If Not Hits Is Nothing Then
                For Each QueryObject In Queries
                    QueryObject.SubjectName = Hits(QueryObject.SubjectName)
                Next
            End If

            Return Me
        End Function

        Public Overrides Function Save(Optional FilePath As String = "", Optional Encoding As Encoding = Nothing) As Boolean
            Return Me.GetXml.SaveTo(FilePath, Encoding)
        End Function

        Public Overrides Function CheckIntegrity(QuerySource As SequenceModel.FASTA.FastaFile) As Boolean
            Throw New NotImplementedException
        End Function
    End Class
End Namespace
