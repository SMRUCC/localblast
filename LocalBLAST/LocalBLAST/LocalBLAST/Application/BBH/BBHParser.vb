﻿Imports Microsoft.VisualBasic.DocumentFormat.Csv.StorageProvider.Reflection
Imports Microsoft.VisualBasic.DocumentFormat.Csv.Extensions
Imports Microsoft.VisualBasic.Scripting.MetaData
Imports Microsoft.VisualBasic.CommandLine.Reflection
Imports Microsoft.VisualBasic
Imports LANS.SystemsBiology.Assembly.Expasy.Database
Imports LANS.SystemsBiology.Assembly
Imports Microsoft.VisualBasic.DocumentFormat.Csv
Imports Microsoft.VisualBasic.Linq
Imports LANS.SystemsBiology.Assembly.Expasy.AnnotationsTool
Imports System.Runtime.CompilerServices

Namespace LocalBLAST.Application.BBH

    ''' <summary>
    ''' BBH解析的时候，是不会区分方向的，所以只要保证编号是一致的就会解析出结果，这个不需要担心
    ''' </summary>
    <PackageNamespace("BBHParser", Publisher:="xie.guigang@gcmodeller.org")>
    Public Module BBHParser

        <Extension>
        Private Function __hash(source As IEnumerable(Of BestHit),
                                identities As Double,
                                coverage As Double) As Dictionary(Of String, Dictionary(Of String, BestHit))

            Dim hash = (From x As BestHit
                        In source
                        Where x.IsMatchedBesthit(identities, coverage)
                        Select x
                        Group x By x.QueryName Into Group) _
                             .ToDictionary(Function(x) x.QueryName,
                                           Function(x) (From hit As BestHit
                                                        In x.Group
                                                        Select hit
                                                        Group hit By hit.HitName Into Group) _
                                                             .ToDictionary(Function(xx) xx.HitName,
                                                                           Function(xx) xx.Group.First))
            Return hash
        End Function

        ''' <summary>
        ''' 导出所有的双向最佳比对结果
        ''' </summary>
        ''' <param name="bhQvS"></param>
        ''' <param name="bhSvQ"></param>
        ''' <returns></returns>
        ''' <remarks>
        ''' 取出所有符合条件的单向最佳的记录
        ''' </remarks>
        '''
        <ExportAPI("BBH.All")>
        Public Function GetDirreBhAll2(bhSvQ As BestHit(),
                                       bhQvS As BestHit(),
                                       Optional identities As Double = -1,
                                       Optional coverage As Double = -1) As BiDirectionalBesthit()

            Dim sHash As Dictionary(Of String, Dictionary(Of String, BestHit)) = bhSvQ.__hash(identities, coverage)
            Dim qHash As Dictionary(Of String, Dictionary(Of String, BestHit)) = bhQvS.__hash(identities, coverage)
            Dim result As New List(Of BiDirectionalBesthit)

            VBDebugger.Mute = True

            For Each qId As String In (qHash.Keys.ToList + bhSvQ.ToArray(Function(x) x.HitName)).Distinct
                If String.IsNullOrEmpty(qId) OrElse String.Equals(qId, "HITS_NOT_FOUND") Then
                    Continue For
                End If

                If Not qHash.ContainsKey(qId) Then
                    result += New BiDirectionalBesthit With {.QueryName = qId}
                Else
                    For Each hit As BestHit In qHash(qId).Values
                        If Not sHash.ContainsKey(hit.HitName) Then
                            result += New BiDirectionalBesthit With {.QueryName = qId}
                        Else
                            If Not sHash(hit.HitName).ContainsKey(qId) Then
                                result += New BiDirectionalBesthit With {.QueryName = qId}
                            Else
                                Dim subject = sHash(hit.HitName)(qId)
                                result += New BiDirectionalBesthit With {
                                    .QueryName = qId,
                                    .HitName = hit.HitName,
                                    .Identities = Math.Max(hit.identities, subject.identities),
                                    .Length = hit.length_hit,
                                    .Positive = Math.Max(hit.Positive, subject.Positive)
                                }
                            End If
                        End If
                    Next
                End If
            Next

            VBDebugger.Mute = False

            Return (From x As BiDirectionalBesthit
                    In result
                    Select x
                    Group x By x.QueryName Into Group) _
                         .ToArray(Function(x) If(x.Group.Count = 1,
                         x.Group.ToArray,
                         (From o As BiDirectionalBesthit
                          In x.Group
                          Where Not String.IsNullOrEmpty(o.HitName)
                          Select o).ToArray)).MatrixToVector
        End Function

        ''' <summary>
        '''
        ''' </summary>
        ''' <param name="hits">假设这里面的hits都是通过了cutoff了的数据</param>
        ''' <returns></returns>
        <Extension> Public Function TopHit(hits As IEnumerable(Of BestHit)) As BestHit
            Dim LQuery = (From x As BestHit
                          In hits
                          Select x,
                              score = x.identities + x.coverage
                          Order By score Descending).First.x
            Return LQuery
        End Function

        Private Function __generateBBH(hits As String(), Id As String, row As LocalBLAST.Application.BBH.BestHit) As BiDirectionalBesthit
            If Array.IndexOf(hits, Id) > -1 Then _
                Return New BiDirectionalBesthit With {  ' 可以双向匹配
                    .QueryName = row.QueryName,
                    .HitName = row.HitName,
                    .Length = row.query_length,
                    .Identities = row.identities,
                    .Positive = row.Positive
                }

            Return New BiDirectionalBesthit With {
                .QueryName = row.QueryName,
                .HitName = "",
                .Length = row.query_length
            }
        End Function

        ''' <summary>
        ''' 导出所有的双向最佳比对结果
        ''' </summary>
        ''' <param name="SvQ"></param>
        ''' <param name="QvS"></param>
        ''' <returns></returns>
        ''' <remarks>
        ''' 取出所有符合条件的单向最佳的记录
        ''' </remarks>
        '''
        <ExportAPI("BBH.All")>
        Public Function GetDirreBhAll2(SvQ As DocumentStream.File, QvS As DocumentStream.File) As BBH.BiDirectionalBesthit()
            Dim bhSvQ As LocalBLAST.Application.BBH.BestHit() = SvQ.AsDataSource(Of LocalBLAST.Application.BBH.BestHit)(False).ToArray
            Dim bhQvS As LocalBLAST.Application.BBH.BestHit() = QvS.AsDataSource(Of LocalBLAST.Application.BBH.BestHit)(False).ToArray
            Return GetDirreBhAll2(bhSvQ, bhQvS)
        End Function

        ''' <summary>
        ''' 获取双向的最佳匹配结果.(只取出第一个最好的结果)
        ''' </summary>
        ''' <param name="QvS"></param>
        ''' <param name="SvQ"></param>
        ''' <returns></returns>
        ''' <remarks></remarks>
        '''
        <ExportAPI("BBH")>
        Public Function BBHTop(SvQ As DocumentStream.File, QvS As DocumentStream.File) As BBH.BiDirectionalBesthit()
            Dim bhSvQ As LocalBLAST.Application.BBH.BestHit() = SvQ.AsDataSource(Of LocalBLAST.Application.BBH.BestHit)(False).ToArray
            Dim bhQvS As LocalBLAST.Application.BBH.BestHit() = QvS.AsDataSource(Of LocalBLAST.Application.BBH.BestHit)(False).ToArray
            Dim besthits = GetBBHTop(bhQvS, bhSvQ)

            Return besthits
        End Function

        ''' <summary>
        ''' 导出所有的双向最佳比对结果，只要能够在双方的列表之中匹配上，则认为是最佳双向匹配
        ''' </summary>
        ''' <param name="SvQ"></param>
        ''' <param name="QvS"></param>
        ''' <returns></returns>
        ''' <remarks>
        ''' 取出所有符合条件的单向最佳的记录
        ''' </remarks>
        '''
        <ExportAPI("BBH.All")>
        Public Function GetDirreBhAll(SvQ As DocumentStream.File, QvS As DocumentStream.File) As DocumentStream.File
            Dim bbh = BBHParser.GetDirreBhAll2(SvQ, QvS)
            Return bbh.ToCsvDoc(False)
        End Function

        ''' <summary>
        ''' 获取双向的最佳匹配结果.(只取出第一个最好的结果)
        ''' </summary>
        ''' <param name="QvS"></param>
        ''' <param name="SvQ"></param>
        ''' <returns></returns>
        ''' <remarks></remarks>
        '''
        <ExportAPI("BBH")>
        Public Function get_DiReBh(SvQ As DocumentStream.File, QvS As DocumentStream.File) As DocumentStream.File
            Dim besthits = BBHTop(SvQ, QvS)
            Return besthits.ToCsvDoc(False)
        End Function

        ''' <summary>
        ''' 假若没有最佳比对，则HitName为空值
        ''' </summary>
        ''' <param name="Query"></param>
        ''' <param name="SubjectVsQuery"></param>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Private Function __topBesthit(Query As BestHit, SubjectVsQuery As BestHit()) As BiDirectionalBesthit
            If SubjectVsQuery.IsNullOrEmpty Then '匹配不上，则返回空的hitname
                Return New BiDirectionalBesthit With {
                    .QueryName = Query.QueryName,
                    .Length = Query.query_length
                }
            End If

            Dim Subject = SubjectVsQuery.First()
            Dim HitsName As String = Subject.HitName  'Subject对象为反向比对结果，其Hitname属性自然为正向比对的Query对象属性
            Dim BestHit = New BiDirectionalBesthit With {
                .QueryName = Query.QueryName,
                .Length = Query.query_length
            }

            If String.Equals(Query.QueryName, HitsName) Then '可以双向匹配
                BestHit.HitName = Query.HitName
                BestHit.Identities = Math.Max(Query.identities, Subject.identities)
                BestHit.Positive = Math.Max(Query.Positive, Subject.Positive)
            End If

            Return BestHit
        End Function

        <Extension>
        Private Function __bhHash(source As IEnumerable(Of BestHit),
                                  identities As Double,
                                  coverage As Double) As Dictionary(Of String, BestHit)

            Return (From x As BestHit
                    In source
                    Where x.IsMatchedBesthit(identities, coverage)
                    Select x
                    Group x By x.QueryName Into Group) _
                         .ToDictionary(Function(x) x.QueryName,
                                       Function(x) x.Group.TopHit)
        End Function

        ''' <summary>
        ''' Only using the first besthit paired result for the orthology data, if the query have no matches then using an empty string for the hit name.
        ''' (只使用第一个做为最佳的双向结果，假若匹配不上，Hitname属性会为空字符串)
        ''' </summary>
        ''' <param name="qvs"></param>
        ''' <param name="svq"></param>
        ''' <returns></returns>
        ''' <remarks></remarks>
        '''
        <ExportAPI("BBH")>
        Public Function GetBBHTop(qvs As BestHit(),
                                  svq As BestHit(),
                                  Optional identities As Double = -1,
                                  Optional coverage As Double = -1) As BiDirectionalBesthit()

            Dim qHash As Dictionary(Of String, BestHit) = qvs.__bhHash(identities, coverage)
            Dim shash As Dictionary(Of String, BestHit) = svq.__bhHash(identities, coverage)
            Dim result As New List(Of BiDirectionalBesthit)

            VBDebugger.Mute = True

            For Each qId As String In (qHash.Keys.ToList + shash.Values.ToArray(Function(x) x.HitName)).Distinct
                If String.IsNullOrEmpty(qId) OrElse String.Equals(qId, "HITS_NOT_FOUND") Then
                    Continue For
                End If

                Dim query As BestHit = qHash.TryGetValue(qId)
                If query Is Nothing Then
                    result += New BiDirectionalBesthit With {.QueryName = qId}
                Else
                    Dim subject As BestHit = shash.TryGetValue(query.HitName)
                    If subject Is Nothing Then
                        result += New BiDirectionalBesthit With {.QueryName = qId}
                    Else
                        result += New BiDirectionalBesthit With {
                            .QueryName = qId,
                            .HitName = query.HitName,
                            .Identities = Math.Max(query.identities, subject.identities),
                            .Length = query.length_hit,
                            .Positive = Math.Max(query.Positive, subject.Positive)
                        }
                    End If
                End If
            Next

            VBDebugger.Mute = False

            Return result.ToArray
        End Function

        <ExportAPI("EnzymeClassification")>
        Public Function EnzymeClassification(Expasy As NomenclatureDB, bh As BBH.BestHit()) As T_EnzymeClass_BLAST_OUT()
            Dim EnzymeClasses As T_EnzymeClass_BLAST_OUT() = API.GenerateBasicDocument(Expasy.Enzymes)
            Dim LQuery = (From enzPre As T_EnzymeClass_BLAST_OUT
                          In EnzymeClasses.AsParallel
                          Select enzPre.__export(bh)).ToArray
            Return LQuery.MatrixToVector
        End Function

        <Extension>
        Private Function __export(enzPre As T_EnzymeClass_BLAST_OUT, bh As BBH.BestHit()) As T_EnzymeClass_BLAST_OUT()
            Dim GetbhLQuery = (From item As BBH.BestHit
                               In bh
                               Where String.Equals(item.HitName, enzPre.UniprotMatched, StringComparison.OrdinalIgnoreCase)
                               Select item).ToArray

            If Not GetbhLQuery.IsNullOrEmpty Then
                Dim Linq = (From bhItem As BBH.BestHit In GetbhLQuery
                            Select New T_EnzymeClass_BLAST_OUT With {
                                .Class = enzPre.Class,
                                .EValue = bhItem.evalue,
                                .Identity = bhItem.identities,
                                .ProteinId = bhItem.QueryName,
                                .UniprotMatched = enzPre.UniprotMatched}).ToArray
                Return Linq
            Else
                Return Nothing
            End If
        End Function
    End Module
End Namespace