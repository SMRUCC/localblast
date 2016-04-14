﻿Imports Microsoft.VisualBasic.DocumentFormat.Csv.StorageProvider.Reflection
Imports Microsoft.VisualBasic.DocumentFormat.Csv.Extensions
Imports Microsoft.VisualBasic.Scripting.MetaData
Imports Microsoft.VisualBasic.CommandLine.Reflection
Imports Microsoft.VisualBasic
Imports System.Runtime.CompilerServices
Imports Microsoft.VisualBasic.Parallel

Namespace LocalBLAST.Application.BatchParallel

    ''' <summary>
    ''' The batch blast module for the preparations of the Venn diagram drawing data model.(为文氏图的绘制准备数据的批量blast模块)
    ''' </summary>
    ''' <remarks></remarks>
    '''
    <PackageNamespace("Venn.Builder",
                      Category:=APICategories.ResearchTools,
                      Publisher:="amethyst.asuka@gcmodeller.org")>
    Public Module VennDataBuilder

        ''' <summary>
        ''' The formatdb and blast operation should be include in this function pointer.(在这个句柄之中必须要包含有formatdb和blast这两个步骤)
        ''' </summary>
        ''' <param name="Query"></param>
        ''' <param name="Subject"></param>
        ''' <param name="Evalue"></param>
        ''' <param name="Export"></param>
        ''' <returns>返回blast的日志文件名</returns>
        ''' <remarks></remarks>
        Public Delegate Function INVOKE_BLAST_HANDLE(Query As String,
                                                     Subject As String,
                                                     num_threads As Integer,
                                                     Evalue As String,
                                                     EXPORT As String,
                                                     [Overrides] As Boolean) As String

        ''' <summary>
        ''' The recommended num_threads parameter for the blast operation base on the current system hardware information.
        ''' (根据当前的系统硬件配置所推荐的num_threads参数)
        ''' </summary>
        ''' <value></value>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Public ReadOnly Property RecommendedThreads As Integer
            Get
                Return Environment.ProcessorCount / 2
            End Get
        End Property

#Region "为了接口的统一的需要"

        ''' <summary>
        '''
        ''' </summary>
        ''' <param name="input">输入的文件夹，fasta序列的文件拓展名必须要为*.fasta或者*.fsa</param>
        ''' <param name="EXPORT">结果导出的文件夹，导出blast日志文件</param>
        ''' <param name="InvokedBLASTAction">所执行的blast命令，函数返回日志文件名</param>
        ''' <remarks></remarks>
        '''
        <ExportAPI("Task.Builder")>
        Public Function TaskBuilder(input As String,
                                    EXPORT As String,
                                    evalue As String,
                                    InvokedBLASTAction As INVOKE_BLAST_HANDLE,
                                    Optional [Overrides] As Boolean = False) As AlignEntry()
            Dim Files As String() = FileIO.FileSystem.GetFiles(input, FileIO.SearchOption.SearchAllSubDirectories, "*.fasta", "*.fsa").ToArray
            Dim ComboList = Microsoft.VisualBasic.ComponentModel.Comb(Of String).CreateCompleteObjectPairs(Files)

            Call FileIO.FileSystem.CreateDirectory(EXPORT)

            Dim FileList As List(Of String) = New List(Of String)

            For Each pairedList In ComboList
                For Each paired In pairedList
                    Call FileList.Add(InvokedBLASTAction(Query:=paired.Key,
                                                         Subject:=paired.Value,
                                                         Evalue:=evalue,
                                                         EXPORT:=EXPORT,
                                                         num_threads:=RecommendedThreads,
                                                         [Overrides]:=[Overrides]))
                Next
            Next

            On Error Resume Next

            Return (From path As String In FileList.AsParallel Select LogNameParser(path)).ToArray
        End Function

        ''' <summary>
        ''' The parallel edition for the invoke function <see cref="TaskBuilder"></see>.(<see cref="TaskBuilder"></see>的并行版本)
        ''' </summary>
        ''' <param name="input"></param>
        ''' <param name="export"></param>
        ''' <param name="evalue"></param>
        ''' <param name="InvokedBLASTAction"></param>
        ''' <remarks></remarks>
        '''
        <ExportAPI("Task.Builder.Parallel")>
        Public Function TaskBuilder_p(Input As String,
                                      Export As String,
                                      Evalue As String,
                                      InvokedBLASTAction As INVOKE_BLAST_HANDLE,
                                      Optional [Overrides] As Boolean = False) As AlignEntry()

            Dim Files As String() = FileIO.FileSystem.GetFiles(Input, FileIO.SearchOption.SearchAllSubDirectories, "*.fasta", "*.fsa", "*.fa").ToArray
            Dim ComboList As KeyValuePair(Of String, String)()() =
                Microsoft.VisualBasic.ComponentModel.Comb(Of String).CreateCompleteObjectPairs(Files)

            Call FileIO.FileSystem.CreateDirectory(Export)

            Dim FileList As List(Of String) = New List(Of String)

            For Each pairedList As KeyValuePair(Of String, String)() In ComboList
                Dim LQuery As String() = (From paired As KeyValuePair(Of String, String)
                                          In pairedList.AsParallel
                                          Select InvokedBLASTAction(
                                              Query:=paired.Key,
                                              Subject:=paired.Value,
                                              Evalue:=Evalue,
                                              EXPORT:=Export,
                                              num_threads:=RecommendedThreads,
                                              [Overrides]:=[Overrides])).ToArray
                Call FileList.AddRange(LQuery)
            Next

            'On Error Resume Next
            Return (From path As String In FileList.AsParallel Select LogNameParser(path)).ToArray
        End Function

#End Region

        ''' <summary>
        ''' 这个函数相比较于<see cref="TaskBuilder_p"/>更加高效
        ''' </summary>
        ''' <param name="inputDIR"></param>
        ''' <param name="outDIR"></param>
        ''' <param name="evalue"></param>
        ''' <param name="blastTask"></param>
        ''' <param name="[overrides]"></param>
        ''' <param name="num_threads"></param>
        ''' <returns>返回日志文件列表</returns>
        Public Function ParallelTask(inputDIR As String,
                                     outDIR As String,
                                     evalue As String,
                                     blastTask As INVOKE_BLAST_HANDLE,
                                     Optional [overrides] As Boolean = False,
                                     Optional num_threads As Integer = -1) As AlignEntry()
            Dim Files As String() = FileIO.FileSystem.GetFiles(inputDIR,
                                                               FileIO.SearchOption.SearchAllSubDirectories,
                                                               "*.fasta",
                                                               "*.fsa",
                                                               "*.fa").ToArray
            Dim ComboList As KeyValuePair(Of String, String)()() =
                Microsoft.VisualBasic.ComponentModel.Comb(Of String).CreateCompleteObjectPairs(Files)
            Dim taskList As Func(Of String)() = (From task In ComboList.MatrixToList.AsParallel
                                                 Let taskHandle As Func(Of String) =
                                                     Function() blastTask(Query:=task.Key,
                                                                          Subject:=task.Value,
                                                                          Evalue:=evalue,
                                                                          EXPORT:=outDIR,
                                                                          num_threads:=RecommendedThreads,
                                                                          [Overrides]:=[overrides])
                                                 Select taskHandle).ToArray

            Call $"Fasta source is {Files.Length} genomes...".__DEBUG_ECHO
            Call $"Build bbh task list of {taskList.Length} tasks...".__DEBUG_ECHO
            Call FileIO.FileSystem.CreateDirectory(outDIR)
            Call App.StartGC(True)
            Call "Start BLAST threads...".__DEBUG_ECHO
            Call $"     {NameOf(num_threads)} => {num_threads}".__DEBUG_ECHO
            Call $"     {NameOf(taskList)}    => {taskList.Length}".__DEBUG_ECHO
            Call Console.WriteLine(New String("+", 200))

            Dim fileList As String() = ServicesFolk.BatchTask(taskList,
                                                              numThreads:=num_threads, ' 启动批量本地blast操作
                                                              TimeInterval:=10)
            'On Error Resume Next
            Return (From path As String In fileList.AsParallel Select LogNameParser(path)).ToArray
        End Function

        Public Const QUERY_LINKS_SUBJECT As String = "_vs__"

        ''' <summary>
        ''' 去掉了fasta文件的后缀名
        ''' </summary>
        ''' <param name="Query"></param>
        ''' <param name="Subject"></param>
        ''' <param name="ExportDIR"></param>
        ''' <returns></returns>
        ''' <remarks></remarks>
        '''
        <ExportAPI("Build.Entry")>
        Public Function BuildFileName(Query As String, Subject As String, ExportDIR As String) As String
            Query = IO.Path.GetFileNameWithoutExtension(Query)
            Subject = IO.Path.GetFileNameWithoutExtension(Subject)
            Return $"{ExportDIR}/{Query}{QUERY_LINKS_SUBJECT}{Subject}.txt"
        End Function

        ''' <summary>
        ''' 尝试从给出的日志文件名之中重新解析出比对的对象列表
        ''' </summary>
        ''' <param name="path"></param>
        ''' <returns></returns>
        ''' <remarks></remarks>
        '''
        <ExportAPI("Entry.Parsing")>
        Public Function LogNameParser(path As String) As AlignEntry
            Dim ID As String = IO.Path.GetFileNameWithoutExtension(path).Replace(".besthit", "")
            Dim Temp As String() = Strings.Split(ID, QUERY_LINKS_SUBJECT)
            Return New AlignEntry With {
                .QueryName = Temp.First,
                .FilePath = path,
                .HitName = Temp.Last
            }
        End Function

        ''' <summary>
        ''' 批量两两比对blastp，以用于生成文氏图的分析数据
        ''' </summary>
        ''' <param name="ServiceHandle"></param>
        ''' <returns></returns>
        ''' <remarks></remarks>
        '''
        <ExportAPI("Get.Blastp.Handle")>
        <Extension>
        Public Function BuildBLASTP_InvokeHandle(ServiceHandle As LANS.SystemsBiology.NCBI.Extensions.LocalBLAST.Programs.BLASTPlus) As INVOKE_BLAST_HANDLE
            Dim Handle As INVOKE_BLAST_HANDLE = Function(Query As String, Subject As String,
                                                         num_threads As Integer,
                                                         Evalue As String,
                                                         ExportDir As String,
                                                         [Overrides] As Boolean) __blastpHandle(ServiceHandle, Query:=Query,
                                                                                                               Evalue:=Evalue,
                                                                                                               ExportDir:=ExportDir,
                                                                                                               Num_Threads:=num_threads,
                                                                                                               [Overrides]:=[Overrides],
                                                                                                               Subject:=Subject)
            Return Handle
        End Function

        ''' <summary>
        '''
        ''' </summary>
        ''' <param name="ServiceHandle"></param>
        ''' <param name="Query"></param>
        ''' <param name="Subject"></param>
        ''' <param name="Num_Threads"></param>
        ''' <param name="Evalue"></param>
        ''' <param name="ExportDir"></param>
        ''' <param name="Overrides">当目标文件存在并且长度不为零的时候，是否进行覆盖，假若为否，则直接忽略过这个文件</param>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Private Function __blastpHandle(ServiceHandle As LANS.SystemsBiology.NCBI.Extensions.LocalBLAST.Programs.BLASTPlus,
                                        Query As String,
                                        Subject As String,
                                        Num_Threads As Integer,
                                        Evalue As String,
                                        ExportDir As String,
                                        [Overrides] As Boolean) As String

            Dim LogOut As String = BuildFileName(Query, Subject, ExportDir)

            If IsAvailable(LogOut) Then
                If Not [Overrides] Then
                    Call Console.Write(".")
                    Return LogOut  '文件已经存在，则会更具是否进行覆写这个参数来决定是否需要进行blast操作
                End If
            End If

            ServiceHandle.NumThreads = Num_Threads

            Call ServiceHandle.FormatDb(Query, ServiceHandle.MolTypeProtein).Start(True)
            Call ServiceHandle.FormatDb(Subject, ServiceHandle.MolTypeProtein).Start(True)
            Call ServiceHandle.Blastp(Query, Subject, LogOut, Evalue).Start(True)

            Return LogOut
        End Function

        <ExportAPI("Get.Blastn.Handle")>
        <Extension>
        Public Function BuildBLASTN_InvokeHandle(service As LANS.SystemsBiology.NCBI.Extensions.LocalBLAST.Programs.BLASTPlus) As INVOKE_BLAST_HANDLE
            Dim Handle As INVOKE_BLAST_HANDLE = AddressOf New __handle With {.service = service}.invokeHandle
            Return Handle
        End Function

        Private Class __handle

            Public service As LANS.SystemsBiology.NCBI.Extensions.LocalBLAST.Programs.BLASTPlus

            Public Function invokeHandle(Query As String, Subject As String, num_threads As Integer, Evalue As String, ExportDir As String, [Overrides] As Boolean) As String
                Dim LogOut As String = BuildFileName(Query, Subject, ExportDir)

                If IsAvailable(LogOut) Then                    '文件已经存在，则会更具是否进行覆写这个参数来决定是否需要进行blast操作
                    If Not [Overrides] Then
                        Call Console.Write(".")
                        Return LogOut
                    End If
                End If

                service.NumThreads = num_threads

                Call service.FormatDb(Query, service.MolTypeNucleotide).Start(True)
                Call service.FormatDb(Subject, service.MolTypeNucleotide).Start(True)
                Call service.Blastp(Query, Subject, LogOut, Evalue).Start(True)

                Return LogOut
            End Function
        End Class
    End Module
End Namespace