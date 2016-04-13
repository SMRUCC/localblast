﻿Imports System.Runtime.CompilerServices

Imports Microsoft.VisualBasic.CommandLine.Reflection
Imports Microsoft.VisualBasic.DocumentFormat.Csv.Extensions
Imports PathEntry = System.Collections.Generic.KeyValuePair(Of String, String)
Imports Microsoft.VisualBasic.Scripting.MetaData
Imports Microsoft.VisualBasic.Linq.Extensions
Imports Microsoft.VisualBasic.Parallel
Imports Microsoft.VisualBasic.DocumentFormat.Csv
Imports Microsoft.VisualBasic
Imports Microsoft.VisualBasic.ComponentModel
Imports Microsoft.VisualBasic.Parallel.Tasks
Imports LANS.SystemsBiology.NCBI.Extensions.Analysis
Imports LANS.SystemsBiology.NCBI.Extensions.LocalBLAST.Programs
Imports LANS.SystemsBiology.NCBI.Extensions.LocalBLAST.Application.BatchParallel.VennDataBuilder
Imports LANS.SystemsBiology.NCBI.Extensions.LocalBLAST.BLASTOutput.BlastPlus
Imports LANS.SystemsBiology.NCBI.Extensions.LocalBLAST.Application.BatchParallel
Imports LANS.SystemsBiology.NCBI.Extensions
Imports LANS.SystemsBiology.SequenceModel.FASTA
Imports LANS.SystemsBiology.Assembly.NCBI.GenBank.CsvExports


Namespace BlastAPI

    ''' <summary>
    ''' Generates the Venn diagram data model using the bbh orthology method.(模块之中的方法可以应用于使用直系同源来创建文氏图)
    ''' </summary>
    ''' <remarks>
    ''' 生成Venn表格所需要的步骤：
    ''' 1. 按照基因组进行导出序列数据
    ''' 2. 两两组合式的双向比对
    ''' 3.
    ''' </remarks>
    <[PackageNamespace]("VennDiagram.LDM.BBH",
                        Category:=APICategories.ResearchTools,
                        Publisher:="xie.guigang@gcmodeller.org",
                        Description:="Package for generate the data model for creates the Venn diagram using the completely combination blastp based bbh protein homologous method result.
                        BBH based method is the widely used algorithm for the cell pathway system automatically annotation, such as the kaas system in the KEGG database.")>
    Public Module VennDataModel

        Sub New()
            Call Settings.Initialize(GetType(VennDataModel))
        End Sub

#Region "Creates Handle"

        <ExportAPI("Blast_Plus.Handle.Creates", Info:="Creates the blastp+ program handle automaticaly from the environment variable.")>
        Public Function CreateHandle() As BLASTPlus
            Return New BLASTPlus(GCModeller.FileSystem.GetLocalBlast)
        End Function

        <ExportAPI("Blast_Plus.Session.New()")>
        Public Function NewBlastPlusSession(<Parameter("Blast.Bin", "The program group of the local blast program group.")>
                                        DIR As String) As BLASTPlus
            Return New BLASTPlus(DIR)
        End Function

        <ExportAPI("Blast_Handle.Create()")>
        Public Function CreateInvokeHandle(<Parameter("Session.Handle")> SessionHandle As BLASTPlus,
                                           <Parameter("Invoke.Blastp",
                                                      "If using this parameter and specific FALSE value, then the function will " &
                                                      "create the blastn handle or create the blastp handle as default.")>
                                           Optional Blastp As Boolean = True) As INVOKE_BLAST_HANDLE

            If Blastp Then
                Return BuildBLASTP_InvokeHandle(SessionHandle)
            Else
                Return BuildBLASTN_InvokeHandle(SessionHandle)
            End If
        End Function

        <ExportAPI("BlastpHandle.From.Blastbin", Info:="Creates the blastp invoke handle from the installed location of the blast program group.")>
        Public Function InvokeCreateBlastpHandle(<Parameter("Blast.Bin", "The program group of the local blast program group.")> DIR As String) As INVOKE_BLAST_HANDLE
            Return BuildBLASTP_InvokeHandle(New BLASTPlus(DIR))
        End Function
#End Region

        ''' <summary>
        ''' 可能有时候需要进行两两双向比对的数据太多了，故而需要先进行单向比对，在使用这个函数将原数据拷贝出来之后，再进行单向必对
        ''' </summary>
        ''' <param name="BlastoutputSource"></param>
        ''' <param name="EXPORT"></param>
        ''' <returns></returns>
        ''' <remarks></remarks>
        ''' <param name="TrimValue">默认是匹配上60%个Query基因组的基因数目</param>
        <ExportAPI("Source.Copy",
               Info:="There are some times the bbh data source size is too large for the bi-directionary best hit blastp, so that you needs to select the genome source first, using this method by the besthit method to filtering the raw data.")>
        Public Function SelectCopy(<Parameter("Dir.Blastoutput",
                                              "The directory which contains the blast output data for calculates the best hit data.")>
                                   BlastoutputSource As String,
                                   <Parameter("Dir.Orf_Source", "The directory which cointains the orf original raw data for the bbh test.")>
                                   CopySource As String,
                                   <Parameter("Dir.Export", "The directory which is the destination directory for copying the genome data, this data ")>
                                   EXPORT As String,
                                   Optional Identities As Double = 0.3,
                                   <Parameter("Percentage.Trim", "Default is the 60% of the number of the query genome proteins.")>
                                   Optional TrimValue As Double = 0.6) As Boolean

            Dim LQuery = (From path As PathEntry In BlastoutputSource.LoadSourceEntryList({"*.txt"}).AsParallel
                          Let Blastout = Parser.LoadBlastOutput(path.Value)
                          Where Not Blastout Is Nothing
                          Select ID = LogNameParser(path.Value).HitName,
                          Blastout,
                          Besthits = Blastout.ExportAllBestHist(Identities)).ToArray '加载单向最佳比对
            '筛选出符合条件的基因组
            Call Console.WriteLine("Blast output data parsing job done, start to screening the besthit genomes.....")
            Dim SelectedGenonesLQuery = (From GenomeData In LQuery.AsParallel
                                         Let ScreenedData = (From obj In GenomeData.Besthits Where obj.Matched Select obj).ToArray
                                         Where Not ScreenedData.IsNullOrEmpty
                                         Select Besthits = ScreenedData,
                                         GenomeData.ID,
                                         GenomeData.Blastout).ToArray
            Call Console.WriteLine("Screening {0} genomes at first time!", SelectedGenonesLQuery.Count)
            Dim n As Integer = SelectedGenonesLQuery.First.Blastout.Queries.Count * TrimValue
            SelectedGenonesLQuery = (From GenomeData In SelectedGenonesLQuery Where GenomeData.Besthits.Count >= n Select GenomeData).ToArray
            Call Console.WriteLine("Screening {0} genomes at second time which contains {1} besthit proteins at leats.", SelectedGenonesLQuery.Count, n)
            Dim LoadORFres = (From path As PathEntry
                          In CopySource.LoadSourceEntryList({"*.fasta", "*.fsa", "*.fa"})
                              Select GenomeID = path.Key, pathValue = path.Value
                              Group By GenomeID Into Group).ToDictionary(Function(obj) obj.GenomeID,
                                                                        Function(obj) (From item In obj.Group Select item.pathValue).ToArray)

            Call (From Genome In SelectedGenonesLQuery Select Genome.Besthits).MatrixAsIterator.SaveTo(EXPORT & "/Besthits.csv", False)
            Call Console.WriteLine("Start to copy genome proteins data...")

            Dim CopyLQuery = (From Genome In SelectedGenonesLQuery Select __innerCopy(LoadORFres, EXPORT, Genome.ID)).ToArray
            Call Console.WriteLine("Job done!")
            Return True
        End Function

        Private Function __innerCopy(LoadORFres As Dictionary(Of String, String()), EXPORT As String, genomeID As String) As Boolean
            If Not LoadORFres.ContainsKey(genomeID) Then
                Call Console.WriteLine($"------------------------------{genomeID} is not exists in the data source!-------------------------")
                Return False
            End If

            Dim PathList As String() = LoadORFres(genomeID)
            Dim CopyTo As String = EXPORT & "/" & FileIO.FileSystem.GetFileInfo(PathList.First).Name
            Call FileIO.FileSystem.CopyFile(PathList.First, CopyTo)
            Return True
        End Function

        ''' <summary>
        ''' 这个方法是与<see cref="BatchBlastp"></see>相反的，即使用多个Query来查询一个Subject
        ''' </summary>
        ''' <returns></returns>
        ''' <remarks></remarks>
        '''
        <ExportAPI("Blastp.Invoke_Batch", Info:="Batch parallel task scheduler.")>
        Public Function BatchBlastpRev(<Parameter("Handle.Blastp", "This handle value is the blastp handle, not blastn handle.")> Handle As BLASTPlus,
                                   <Parameter("Dir.Query", "The data directory which contains the query protein fasta data.")> Query As String,
                                   <Parameter("Path.Subject", "The file path value of the subject protein fasta data.")> Subject As String,
                                   <Parameter("Dir.Export", "The data directory for export the blastp data between the query and subject.")> EXPORT As String,
                                   <Parameter("E-Value", "The blastp except value.")> Optional Evalue As String = "1e-3",
                                   <Parameter("Exists.Overriding", "Overrides the exists blastp result if the file length is not ZERO length.")> Optional [Overrides] As Boolean = False,
                                   <Parameter("Using.Parallel")> Optional Parallel As Boolean = False) As Integer
            Dim Queries = Query.LoadSourceEntryList({"*.fasta", "*.fsa", "*.txt"})

            If Not FileIO.FileSystem.FileExists(Subject) Then
                Throw New Exception($"Could not found the subject protein database fasta file ""{Subject.ToFileURL}""!")
            End If

            Call FileIO.FileSystem.CreateDirectory(EXPORT)
            Call Handle.FormatDb(Subject, Handle.MolTypeProtein).Start(WaitForExit:=True)

            Dim LQuery As Integer()

            If Parallel Then
                LQuery = (From Path As PathEntry
                      In Queries.AsParallel
                          Select Handle.Blastp(Input:=Path.Value, TargetDb:=Subject, e:=Evalue, Output:=EXPORT & "/" & Path.Key & ".txt").Start(WaitForExit:=True)).ToArray
            Else
                Handle.NumThreads = Environment.ProcessorCount / 2
                LQuery = (From Path As PathEntry
                      In Queries
                          Select Handle.Blastp(Input:=Path.Value, TargetDb:=Subject, e:=Evalue, Output:=EXPORT & "/" & Path.Key & ".txt").Start(WaitForExit:=True)).ToArray
            End If

            Return LQuery.Sum
        End Function

        <ExportAPI("Blastp.Invoke_Batch")>
        Public Function BatchBlastp(<Parameter("Handle.Blastp", "This handle value is the blastp handle, not blastn handle.")> Handle As INVOKE_BLAST_HANDLE,
                                <Parameter("Path.Query", "The file path value of the query protein fasta data.")> Query As String,
                                <Parameter("Dir.Subject", "The data directory which contains the subject protein fasta data.")> Subject As String,
                                <Parameter("Dir.Export", "The data directory for export the blastp data between the query and subject.")> EXPORT As String,
                                <Parameter("E-Value", "The blastp except value.")> Optional Evalue As String = "1e-3",
                                <Parameter("Exists.Overriding", "Overrides the exists blastp result if the file length is not ZERO length.")>
                                    Optional [Overrides] As Boolean = False) As String()
            Dim Subjects = Subject.LoadSourceEntryList({"*.fasta", "*.fsa", "*.txt"})

            If Not FileIO.FileSystem.FileExists(Query) Then
                Throw New Exception($"Could not found the query protein fasta file ""{Query.ToFileURL}""!")
            End If

            Call FileIO.FileSystem.CreateDirectory(EXPORT)

            Dim LQuery = (From Path As PathEntry
                      In Subjects.AsParallel
                          Select Handle(Query, Subject:=Path.Value, Evalue:=Evalue, ExportDir:=EXPORT, num_threads:=Environment.ProcessorCount / 2, [Overrides]:=[Overrides])).ToArray
            Return LQuery
        End Function

        <ExportAPI("BBH.Start()", Info:="Only perfermence the bbh analysis for the query protein fasta, the subject source parameter is the fasta data dir path of the subject proteins.")>
        Public Function BBH(<Parameter("Handle.Blastp", "This handle value is the blastp handle, not blastn handle.")> Handle As INVOKE_BLAST_HANDLE,
                            <Parameter("Path.Query", "The file path value of the query protein fasta data.")> Query As String,
                            <Parameter("DIR.Subject", "The data directory which contains the subject protein fasta data.")> Subject As String,
                            <Parameter("DIR.Export", "The data directory for export the blastp data between the query and subject.")> EXPORT As String,
                            <Parameter("E-Value", "The blastp except value.")> Optional Evalue As String = "1e-3",
                            <Parameter("Exists.Overriding", "Overrides the exists blastp result if the file length is not ZERO length.")>
                            Optional [Overrides] As Boolean = False) As AlignEntry()

            If Not FileIO.FileSystem.FileExists(Query) Then
                Throw New Exception($"Could not found the query protein fasta file ""{Query.ToFileURL}""!")
            End If

            Dim Subjects = Subject.LoadSourceEntryList({"*.fasta", "*.fsa", "*.txt"})

            Call FileIO.FileSystem.CreateDirectory(EXPORT)

            Dim LQuery = (From Path As PathEntry
                      In Subjects.AsParallel
                          Select __bbh(Path, Query, Evalue, EXPORT, Handle, [Overrides])).ToArray.MatrixToList
            Return (From Path As String In LQuery.AsParallel
                    Select LogNameParser(Path)).ToArray
        End Function

        Private Function __bbh(Path As PathEntry, Query As String, Evalue As String, EXPORT As String, Handle As INVOKE_BLAST_HANDLE, [Overrides] As Boolean) As String()
            Dim Files As List(Of String) = New List(Of String)
            Call Files.Add(Handle(Query, Path.Value, 1, Evalue, EXPORT, [Overrides]))
            Call Files.Add(Handle(Path.Value, Query, 1, Evalue, EXPORT, [Overrides]))
            Return Files.ToArray
        End Function

        <ExportAPI("Integrity.Checks")>
        Public Function CheckIntegrity(<Parameter("Blastp.Handle")> Handle As INVOKE_BLAST_HANDLE,
                                       <Parameter("Dir.Source.Input", "The data directory which contains the protein sequence fasta files.")> Input As String,
                                       <Parameter("Dir.Blastp.Export", "The data directory for export the blastp result.")> EXPORT As String,
                                       <Parameter("E-value")> Optional Evalue As String = "1e-3") _
                                    As <FunctionReturns("The file log path which is not integrity.")> String()

            Dim Files As String() = FileIO.FileSystem.GetFiles(Input, FileIO.SearchOption.SearchAllSubDirectories, "*.fasta", "*.fsa", "*.fa").ToArray
            Dim ComboList = Comb(Of String).CreateCompleteObjectPairs(Files).MatrixAsIterator

            Call FileIO.FileSystem.CreateDirectory(EXPORT)

            Dim LQuery As String() = (From paired As KeyValuePair(Of String, String)
                                      In ComboList.AsParallel
                                      Let PathLog As String = BuildFileName(paired.Key, paired.Value, EXPORT)
                                      Let InternalInvoke = paired.__invokeInner(PathLog, Handle, Evalue, EXPORT)
                                      Where Not String.IsNullOrEmpty(InternalInvoke)
                                      Select InternalInvoke).ToArray
            Return LQuery
        End Function

        <Extension>
        Private Function __invokeInner(paired As KeyValuePair(Of String, String),
                                   PathLog As String,
                                   Handle As INVOKE_BLAST_HANDLE,
                                   Evalue As String,
                                   EXPORT As String) As String
            If NCBILocalBlast.FastCheckIntegrityProvider(FastaFile.Read(paired.Key), PathLog) Then
                Call Console.Write(".")
                Return ""
            Else
                Call Console.WriteLine("File ""{0}"" is incorrect!", PathLog.ToFileURL)
                Return Handle(Query:=paired.Key, Subject:=paired.Value, Evalue:=Evalue, ExportDir:=EXPORT, num_threads:=Environment.ProcessorCount / 2, [Overrides]:=True)
            End If
        End Function

        ''' <summary>
        ''' 两两组合的双向比对用来创建文氏图所需要的数据
        ''' </summary>
        ''' <param name="Handle"></param>
        ''' <param name="Input"></param>
        ''' <param name="Export"></param>
        ''' <param name="Evalue"></param>
        ''' <param name="Parallel"></param>
        ''' <param name="Overrides"></param>
        ''' <returns></returns>
        ''' <remarks></remarks>
        <ExportAPI("_Start_Task()", Info:="Completely combination of the blastp search result for creating the venn diagram data model.")>
        Public Function StartTask(<Parameter("Blastp.Handle")> Handle As INVOKE_BLAST_HANDLE,
                                  <Parameter("Dir.Source.Input", "The data directory which contains the protein sequence fasta files.")> Input As String,
                                  <Parameter("Dir.Blastp.Export", "The data directory for export the blastp result.")> Export As String,
                                  <Parameter("E-value")> Optional Evalue As String = "1e-3",
                                  <Parameter("Task.Parallel", "The task is parallelize? Default is yes!")> Optional Parallel As Boolean = True,
                                  <Parameter("Exists.Overrides", "If the target blastp output log data is not a empty file, " &
                                  "then if overrides then the blastp will be invoke again orelse function will skip this not null file.")>
                                  Optional [Overrides] As Boolean = False) As AlignEntry()

            If Parallel Then
                Return TaskBuilder_p(Input, Export, Evalue, Handle, [Overrides])
            Else
                Return TaskBuilder(Input, Export, Evalue, Handle, [Overrides])
            End If
        End Function

        ''' <summary>
        ''' 批量导出最佳比对匹配结果
        ''' </summary>
        ''' <returns></returns>
        ''' <remarks></remarks>
        ''' <param name="Source">单项最佳的两两比对的结果数据文件夹</param>
        ''' <param name="EXPORT">双向最佳的导出文件夹</param>
        ''' <param name="CDSAll">从GBK文件列表之中所导出来的蛋白质信息的汇总表</param>
        '''
        <ExportAPI("Export.Besthits")>
        Public Function ExportBidirectionalBesthit(Source As IEnumerable(Of AlignEntry),
                                                   <Parameter("Path.CDS.All.Dump")> CDSAll As String,
                                                   <Parameter("DIR.EXPORT")> EXPORT As String,
                                                   <Parameter("Null.Trim")> Optional TrimNull As Boolean = False) As BestHit()
            Return NCBI.Extensions.Analysis.ExportBidirectionalBesthit(Source, EXPORT, LoadCdsDumpInfo(CDSAll), TrimNull)
        End Function

        <ExportAPI("Orf.Dump.Load.As.Hash")>
        Public Function LoadCdsDumpInfo(Path As String) As Dictionary(Of String, GeneDumpInfo)
            Dim CsvData = Path.LoadCsv(Of GeneDumpInfo)(False)
            Dim GroupData = (From Orf In CsvData.AsParallel Select Orf Group By Orf.LocusID Into Group)
            Dim DictHash = GroupData.ToDictionary(Function(obj) obj.LocusID,
                                              Function(obj) obj.Group.First)
            Return DictHash
        End Function

        <ExportAPI("Orf.Dump.Begin.Load.As.Hash")>
        Public Function BeginLoadCdsDumpInfo(Path As String) As Task(Of String, Dictionary(Of String, GeneDumpInfo))
            Return New Task(Of String, Dictionary(Of String, GeneDumpInfo))(Path, AddressOf LoadCdsDumpInfo).Start
        End Function

        ''' <summary>
        ''' If you don't want the export bbh data contains the protein description information or just don't know how the create the information, using this function to leave it blank.
        ''' </summary>
        ''' <returns></returns>
        <ExportAPI("Orf.Hash.Null", Info:="If you don't want the export bbh data contains the protein description information or just don't know how the create the information, using this function to leave it blank.")>
        Public Function NullHash() As Dictionary(Of String, GeneDumpInfo)
            Return New Dictionary(Of String, GeneDumpInfo)
        End Function

        ''' <summary>
        '''
        ''' </summary>
        ''' <param name="data"></param>
        ''' <param name="MainIndex">
        ''' 进化比较的标尺
        ''' 假若为空字符串或者数字0以及first，都表示使用集合之中的第一个元素对象作为标尺
        ''' 假若参数值为某一个菌株的名称<see cref="BestHit.QuerySpeciesName"></see>，则会以该菌株的数据作为比对数据
        ''' 假若为last，则使用集合之中的最后一个
        ''' 对于其他的处于0-集合元素上限的数字，可以认识使用该集合之中的第i-1个元素对象
        ''' 还可以选择longest或者shortest参数值来作为最长或者最短的元素作为主标尺
        ''' 对于其他的任何无效的字符串，则默认使用第一个
        '''
        ''' </param>
        ''' <returns></returns>
        ''' <remarks></remarks>
        <ExportAPI("Generate.Venn.LDM", Info:="The trim_null parameter is TRUE, and the function will filtering all of the data which have more than one hits.")>
        Public Function DeltaMove(data As IEnumerable(Of BestHit),
                                  <Parameter("Index.Main", "The file name without the extension name of the target query fasta data.")>
                                  Optional MainIndex As String = "",
                                  <Parameter("Null.Trim")>
                                  Optional TrimNull As Boolean = False) As DocumentStream.File
            Dim DataDict = data.ToDictionary(Function(item) item.QuerySpeciesName)
            Dim IndexKey = DataDict.Keys(__parserIndex(DataDict, MainIndex))
            Dim MainData = DataDict(IndexKey)
            Call DataDict.Remove(IndexKey)

            If MainData.Hits.IsNullOrEmpty Then
                Call $"The profile data of your key ""{MainIndex}"" ---> ""{MainData.QuerySpeciesName}"" is null!".__DEBUG_ECHO
                Call "Thread exists...".__DEBUG_ECHO
                Return New DocumentStream.File
            End If

            Dim CSV = MainData.ExportCsv(TrimNull)
            Dim Species As String() = (From item In MainData.Hits.First.Hits Select item.Tag).ToArray

            For DeltaIndex As Integer = 0 To DataDict.Count - 1
                Dim SubMain = DataDict.Values(DeltaIndex)

                If SubMain.Hits.IsNullOrEmpty Then
                    Call $"Profile data {SubMain.QuerySpeciesName} is null!".__DEBUG_ECHO
                    Continue For
                End If

                Dim di As Integer = DeltaIndex
                Dim SubMainMatched = (From row In CSV Let d = 2 + 4 * di + 1 Let id As String = row(d) Where Not String.IsNullOrEmpty(id) Select id).ToArray
                Dim Notmatched = (From item As HitCollection In SubMain.Hits
                                  Where Array.IndexOf(SubMainMatched, item.QueryName) = -1
                                  Select item.QueryName,
                                  item.Description,
                                  speciesProfile = item.Hits.ToDictionary(Function(ff) ff.Tag)).ToArray

                For Each SubMainNotHitGene In Notmatched  '竖直方向遍历第n列的基因号
                    Dim row As New DocumentStream.RowObject From {SubMainNotHitGene.Description, SubMainNotHitGene.QueryName}

                    Call row.AddRange((From nnn In (4 * DeltaIndex).Sequence Select "").ToArray)

                    For Each sid As String In Species.Skip(DeltaIndex)
                        Dim matched = SubMainNotHitGene.speciesProfile(sid)
                        Call row.Add("")
                        Call row.Add(matched.HitName)
                        Call row.Add(matched.Identities)
                        Call row.Add(matched.Positive)
                    Next
                    Call CSV.Add(row)
                Next
            Next

            Return CSV
        End Function

        <ExportAPI("Venn.Source.Copy")>
        Public Function Copy(besthits As BestHit, source As String, copyTo As String) As String()
            Return besthits.SelectSourceFromHits(source, copyTo)
        End Function

        <ExportAPI("Read.Xml.Besthit")>
        Public Function ReadXml(path As String) As BestHit
            Return path.LoadXml(Of BestHit)()
        End Function

        <ExportAPI("Load.Xmls.Besthit")>
        Public Function ReadBesthitXML(dir As String) As BestHit()
            Dim files = (From path As String
                     In FileIO.FileSystem.GetFiles(dir, FileIO.SearchOption.SearchTopLevelOnly, "*.xml").AsParallel
                         Select path.LoadXml(Of BestHit)()).ToArray
            Return files
        End Function

        ''' <summary>
        ''' 计算出可能的保守区域
        ''' </summary>
        ''' <param name="bh"></param>
        ''' <returns></returns>
        ''' <remarks></remarks>
        <ExportAPI("Export.Conserved.GenomeRegion", Info:="Calculate of the conserved genome region based on the multiple genome bbh comparison result.")>
        Public Function OutputConservedCluster(bh As BestHit) As String()()
            Dim ChunkBuffer = bh.GetConservedRegions
            Dim LQuery = (From item In ChunkBuffer Select item.ToArray).ToArray
            Dim i As Integer = 1

            Call Console.WriteLine(New String("=", 200))
            Call Console.WriteLine("Conserved region on ""{0}"":", bh.QuerySpeciesName)
            Call Console.WriteLine()

            For Each Line In LQuery
                Call Console.WriteLine(i & "   ----> " & String.Join(", ", Line))
                i += 1
            Next

            Return LQuery
        End Function

        ''' <summary>
        '''
        ''' </summary>
        ''' <param name="data"></param>
        ''' <param name="index">
        ''' 进化比较的标尺
        ''' 假若为空字符串或者数字0以及first，都表示使用集合之中的第一个元素对象作为标尺
        ''' 假若参数值为某一个菌株的名称<see cref="BestHit.QuerySpeciesName"></see>，则会以该菌株的数据作为比对数据
        ''' 假若为last，则使用集合之中的最后一个
        ''' 对于其他的处于0-集合元素上限的数字，可以认识使用该集合之中的第i-1个元素对象
        ''' 还可以选择longest或者shortest参数值来作为最长或者最短的元素作为主标尺
        ''' 对于其他的任何无效的字符串，则默认使用第一个
        ''' </param>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Public Function __parserIndex(data As Dictionary(Of String, BestHit), index As String) As Integer
            If String.Equals(index, "first", StringComparison.OrdinalIgnoreCase) OrElse String.Equals(index, "0") Then
                Return 0
            ElseIf data.ContainsKey(index) Then
                Return Array.IndexOf(data.Keys.ToArray, index)
            ElseIf String.Equals(index, "last", StringComparison.OrdinalIgnoreCase) Then
                Return data.Count - 1
            ElseIf String.Equals(index, "longest", StringComparison.OrdinalIgnoreCase) Then
                Dim sid As String = (From item In data Select item Order By Len(item.Key) Descending).First.Key
                Return Array.IndexOf(data.Keys.ToArray, sid)
            ElseIf String.Equals(index, "shortest", StringComparison.OrdinalIgnoreCase) Then
                Dim sid As String = (From item In data Select item Order By Len(item.Key) Ascending).First.Key
                Return Array.IndexOf(data.Keys.ToArray, sid)
            End If
            Return 0
        End Function
    End Module
End Namespace