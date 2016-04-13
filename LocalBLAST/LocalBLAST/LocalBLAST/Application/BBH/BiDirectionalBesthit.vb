﻿Imports Microsoft.VisualBasic.ComponentModel.Collection.Generic
Imports Microsoft.VisualBasic.ComponentModel.KeyValuePair
Imports Microsoft.VisualBasic.DocumentFormat.Csv.StorageProvider.Reflection

Namespace LocalBLAST.Application.BBH

    ''' <summary>
    ''' Best hit result from the binary direction blastp result.(最佳双向比对结果，BBH，直系同源)
    ''' </summary>
    ''' <remarks></remarks>
    Public Class BiDirectionalBesthit : Inherits I_BlastQueryHit
        Implements IKeyValuePair, IQueryHits

        ''' <summary>
        ''' Annotiation for protein <see cref="BiDirectionalBesthit.QueryName"></see>
        ''' </summary>
        ''' <value></value>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Public Property Description As String
        ''' <summary>
        ''' COG annotiation for protein <see cref="BiDirectionalBesthit.QueryName"></see>
        ''' </summary>
        ''' <value></value>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Public Property COG As String
        ''' <summary>
        ''' Protein length annotiation for protein <see cref="BiDirectionalBesthit.QueryName"></see>.(本蛋白质实体对象的序列长度)
        ''' </summary>
        ''' <value></value>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Public Property Length As String
        Public Property Identities As Double Implements IQueryHits.identities
        Public Property Positive As Double

        Public Shared ReadOnly Property NullValue As BiDirectionalBesthit
            Get
                Return New BiDirectionalBesthit
            End Get
        End Property

        ''' <summary>
        '''
        ''' </summary>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Public Overrides Function ToString() As String
            Return String.Format("{0} <==> {1}", QueryName, HitName)
        End Function

        Public Function ShadowCopy(Of T As BiDirectionalBesthit)() As T
            Dim data As T = Activator.CreateInstance(Of T)()
            data.QueryName = QueryName
            data.HitName = HitName
            data.COG = COG
            data.Description = Description
            data.Length = Length

            Return data
        End Function

        Public Delegate Function GetDescriptionHandle(locusId As String) As String

        Public Shared Function MatchDescription(data As BiDirectionalBesthit(), SourceDescription As GetDescriptionHandle) As BiDirectionalBesthit()
            Dim LQuery = (From ItemObject As BiDirectionalBesthit In data.AsParallel
                          Let desc As String = SourceDescription(locusId:=ItemObject.QueryName)
                          Select ItemObject.InvokeSet(NameOf(ItemObject.Description), desc)).ToArray
            Return LQuery
        End Function
    End Class

    Public Interface IBlastHit
        Property locusId As String
        Property hitName As String
    End Interface

    Public Interface IQueryHits : Inherits IBlastHit
        Property identities As Double
    End Interface

    ''' <summary>
    ''' <see cref="I_BlastQueryHit.QueryName"></see> and <see cref="I_BlastQueryHit.HitName"></see>
    ''' </summary>
    ''' <remarks></remarks>
    Public MustInherit Class I_BlastQueryHit : Implements IKeyValuePair
        Implements IBlastHit

        ''' <summary>
        ''' The query source.
        ''' </summary>
        ''' <value></value>
        ''' <returns></returns>
        ''' <remarks></remarks>
        <Column("query_name")> Public Overridable Property QueryName As String _
            Implements IKeyValuePairObject(Of String, String).Identifier, IBlastHit.locusId
        ''' <summary>
        ''' The subject hit.
        ''' </summary>
        ''' <value></value>
        ''' <returns></returns>
        ''' <remarks></remarks>
        <Column("hit_name")> Public Overridable Property HitName As String Implements IKeyValuePairObject(Of String, String).Value, IBlastHit.hitName

        ''' <summary>
        ''' 仅仅是依靠对HitName的判断来使用这个属性了解<see cref="QueryName"></see>是否已经和<see cref="HitName"></see>比对上了
        ''' </summary>
        ''' <value></value>
        ''' <returns></returns>
        ''' <remarks></remarks>
        <Ignored> Public ReadOnly Property Matched As Boolean
            Get
                Return Not String.IsNullOrEmpty(HitName) AndAlso
                    Not String.Equals(HitName, NCBI.Extensions.LocalBLAST.BLASTOutput.IBlastOutput.HITS_NOT_FOUND)
            End Get
        End Property

        <Ignored> Public ReadOnly Property SelfHit As Boolean
            Get
                Return String.Equals(QueryName, HitName, StringComparison.OrdinalIgnoreCase)
            End Get
        End Property

        Public Const BBH_ID_SEPERATOR As String = "+++++++++"

        ''' <summary>
        ''' 会忽略掉基因号ID的先后顺序的，重新按照字符先后进行排序
        ''' </summary>
        ''' <returns></returns>
        <Ignored> Public ReadOnly Property BBH_ID As String
            Get
                Dim Order As String() = (From s As String
                                         In {Me.QueryName, Me.HitName}
                                         Select str = s.ToUpper
                                         Order By str Ascending).ToArray
                Return String.Join(BBH_ID_SEPERATOR, Order)
            End Get
        End Property
    End Class

    ''' <summary>
    ''' 单向的最佳比对结果
    ''' </summary>
    ''' <remarks></remarks>
    Public Class BestHit : Inherits I_BlastQueryHit
        Implements IKeyValuePair, IQueryHits

        <Column("query_length")> Public Property query_length As Integer
        <Column("hit_length")> Public Property hit_length As Integer
        <Column("score")> Public Property Score As Double
        <Column("e-value")> Public Property evalue As Double
        <Column("identities")> Public Property identities As Double Implements IQueryHits.identities
        <Column("positive")> Public Property Positive As Double
        <Column("length_hit")> Public Property length_hit As Integer
        <Column("length_query")> Public Property length_query As Integer
        <Column("length_hsp")> Public Property length_hsp As Integer
        Public Property description As String

        Public ReadOnly Property coverage As Double
            Get
                Return Me.query_length / Me.length_query
            End Get
        End Property

        Public Overrides Function ToString() As String
            Return String.Format("{0} --> {1}; E-value:={2}", QueryName, HitName, evalue)
        End Function

        Public Function IsMatchedBesthit(Optional identities As Double = 0.15, Optional coverage As Double = 0.5) As Boolean
            If length_query = 0 Then
                Return False
            End If
            Return Me.coverage >= coverage AndAlso Me.identities >= identities
        End Function

        Public Shared Function FindByQueryName(QueryName As String, Collection As Generic.IEnumerable(Of BestHit)) As BestHit()
            Dim LQuery = (From item In Collection Where String.Equals(QueryName, item.QueryName) Select item).ToArray
            Return LQuery
        End Function

        Public Shared Function IsNullOrEmpty(Of T As BestHit)(data As Generic.IEnumerable(Of T), Optional TrimSelfAligned As Boolean = False) As Boolean
            If data.IsNullOrEmpty Then
                Return True
            End If

            If Not TrimSelfAligned Then
                Dim LQuery = (From hit As T In data.AsParallel
                              Where Not String.Equals(hit.HitName, NCBI.Extensions.LocalBLAST.BLASTOutput.IBlastOutput.HITS_NOT_FOUND)
                              Select hit).FirstOrDefault
                Return LQuery Is Nothing
            Else
                Dim LQuery = (From item As T In data.AsParallel
                              Where Not String.IsNullOrEmpty(item.QueryName) AndAlso
                              String.Equals(item.HitName, item.QueryName)
                              Select item).FirstOrDefault
                Return LQuery Is Nothing
            End If
        End Function
    End Class
End Namespace