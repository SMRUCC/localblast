﻿Imports Microsoft.VisualBasic.CommandLine.Reflection
Imports Microsoft.VisualBasic.DocumentFormat.Csv
Imports Microsoft.VisualBasic
Imports Microsoft.VisualBasic.Parallel
Imports LANS.SystemsBiology.NCBI.Extensions.LocalBLAST.Application.BatchParallel
Imports LANS.SystemsBiology.NCBI.Extensions.Analysis.BBHLogs
Imports LANS.SystemsBiology.NCBI.Extensions.LocalBLAST.Application.BBH
Imports LANS.SystemsBiology.Localblast.Extensions.VennDiagram.BlastAPI
Imports LANS.SystemsBiology.NCBI.Extensions.LocalBLAST.BLASTOutput
Imports Microsoft.VisualBasic.Text
Imports Microsoft.VisualBasic.Linq
Imports System.Windows.Forms

Partial Module CLI

    <ExportAPI("/sbh2bbh", Usage:="/sbh2bbh /qvs <qvs.sbh.csv> /svq <svq.sbh.csv> [/all /out <bbh.csv>]")>
    Public Function BBHExport2(args As CommandLine.CommandLine) As Integer
        Dim qvs As String = args("/qvs")
        Dim svq As String = args("/svq")
        Dim qsbh = qvs.LoadCsv(Of BestHit)
        Dim ssbh = svq.LoadCsv(Of BestHit)
        Dim all As Boolean = args.GetBoolean("/all")
        Dim bbh As BiDirectionalBesthit() = If(all, BBHParser.GetDirreBhAll2(qsbh.ToArray, ssbh.ToArray), BBHParser.BBHTop(qsbh.ToArray, ssbh.ToArray))
        Dim out As String = args.GetValue("/out", qvs.TrimFileExt & ".bbh.csv")
        Return bbh.SaveTo(out).CLICode
    End Function

    <ExportAPI("/bbh.Export", Usage:="/bbh.Export /query <query.blastp_out> /subject <subject.blast_out> [/out <bbh.csv> /evalue 1e-3 /coverage 0.85 /identities 0.3]")>
    Public Function BBHExportFile(args As CommandLine.CommandLine) As Integer
        Dim query As String = args("/query")
        Dim subject As String = args("/subject")
        Dim out As String = args.GetValue("/out", query.TrimFileExt & "_bbh.csv")
        Dim evalue As Double = args.GetValue("/evalue", 0.001)
        Dim coverage As Double = args.GetValue("/coverage", 0.85)
        Dim identities As Double = args.GetValue("/identities", 0.3)
        Dim queryOUT = NCBI.Extensions.LocalBLAST.BLASTOutput.BlastPlus.TryParseUltraLarge(query)
        Dim subjectOUT = NCBI.Extensions.LocalBLAST.BLASTOutput.BlastPlus.TryParseUltraLarge(subject)
        Dim bbh = BBHParser.GetDirreBhAll2(queryOUT.ExportAllBestHist(coverage, identities), subjectOUT.ExportAllBestHist(coverage, identities))
        Return bbh.SaveTo(out).CLICode
    End Function

    <ExportAPI("/SBH.Trim", Usage:="/SBH.Trim /in <sbh.csv> /evalue <evalue> [/identities 0.15 /coverage 0.5 /out <out.csv>]")>
    Public Function SBHTrim(args As CommandLine.CommandLine) As Integer
        Dim inFile As String = args("/in")
        Dim evalue As Double = args.GetDouble("/evalue")
        Dim out As String = args.GetValue("/out", inFile.TrimFileExt & "." & args("/evalue") & ".Csv")
        Dim readStream As New DocumentStream.Linq.DataStream(inFile)
        Dim coverage As Double = args.GetValue("/coverage", 0.5)
        Dim identities As Double = args.GetValue("/identities", 0.15)

        Using writeStream As New DocumentStream.Linq.WriteStream(Of BestHit)(out)

            Call $"Cutoffs ==>".__DEBUG_ECHO
            Call $"   {NameOf(coverage)}   := {coverage}".__DEBUG_ECHO
            Call $"   {NameOf(identities)} := {identities}".__DEBUG_ECHO
            Call $"   {NameOf(evalue)}     := {evalue}".__DEBUG_ECHO

            Call readStream.ForEachBlock(Of BestHit)(
                Sub(hits)
                    hits = (From x As BestHit
                            In hits
                            Where x.evalue <= evalue AndAlso
                                x.IsMatchedBesthit(identities, coverage) AndAlso
                                Not x.SelfHit
                            Select x).ToArray
                    Call writeStream.Flush(hits)
                End Sub)

            Return 0
        End Using
    End Function

    <ExportAPI("/BBH.Merge", Usage:="/BBH.Merge /in <inDIR> [/out <out.csv>]")>
    Public Function MergeBBH(args As CommandLine.CommandLine) As Integer
        Dim inDIR As String = args("/in")
        Dim out As String = args.GetValue("/out", inDIR & ".bbh.Csv")
        Dim LQuery = (From file As String
                      In FileIO.FileSystem.GetFiles(inDIR, FileIO.SearchOption.SearchTopLevelOnly, "*.csv").AsParallel
                      Select file.LoadCsv(Of BiDirectionalBesthit)).MatrixToList
        Return LQuery.SaveTo(out)
    End Function

    ''' <summary>
    ''' 两两比对
    ''' </summary>
    ''' <returns></returns>
    '''
    <ExportAPI("/venn.BlastAll",
               Usage:="/venn.BlastAll /query <queryDIR> /out <outDIR> [/num_threads <-1> /evalue 10 /overrides /all /coverage <0.8> /identities <0.3>]",
               Info:="Completely paired combos blastp bbh operations for the venn diagram or network builder.")>
    <ParameterInfo("/num_threads", True,
                   Description:="The number of the parallel blast task in this command, default value is -1 which means the number of the blast threads is determined by system automatically.")>
    <ParameterInfo("/all", True,
                   Description:="If this parameter is represent, then all of the paired best hit will be export, otherwise only the top best will be export.")>
    <ParameterInfo("/query", False,
                   Description:="Recommended format of the fasta title is that the fasta title only contains gene locus_tag.")>
    Public Function vennBlastAll(args As CommandLine.CommandLine) As Integer
        Dim queryDIR As String = args("/query")
        Dim out As String = args("/out")
        Dim numThreads As Integer = args.GetValue("/num_threads", -1)
        Dim evalue As String = args.GetValue("/evalue", "10")
        Dim [overrides] As Boolean = args.GetBoolean("/overrides")

        If numThreads <= 0 Then
            numThreads = LQuerySchedule.Recommended_NUM_THREADS
        End If

        Dim blastapp As String = GCModeller.FileSystem.GetLocalBlast
        Dim blastplus As New LocalBLAST.Programs.BLASTPlus(blastapp)
        Dim blastpHandle = blastplus.BuildBLASTP_InvokeHandle
        Dim outList = ParallelTask(queryDIR, out, evalue, blastpHandle, [overrides], numThreads)

        '  从这里开始导出最佳双向比对的结果
        Dim entryList = outList.ToList.BuildBBHEntry
        Dim isAll As Boolean = args.GetBoolean("/all")
        Dim coverage As Double = args.GetValue("/coverage", 0.8)
        Dim identities As Double = args.GetValue("/identities", 0.3)

        Return entryList.__exportBBH(isAll, coverage, identities, "", outDIR:=out & "/BBH_OUT/")
    End Function

    <ExportAPI("/venn.BBH",
               Info:="2. Build venn table and bbh data from the blastp result out or sbh data cache.",
               Usage:="/venn.BBH /imports <blastp_out.DIR> [/skip-load /query <queryName> /all /coverage <0.6> /identities <0.3> /out <outDIR>]")>
    <ParameterInfo("/skip-load", True,
                   Description:="If the data source in the imports directory is already the sbh data source, then using this parameter to skip the blastp file parsing.")>
    Public Function VennBBH(args As CommandLine.CommandLine) As Integer
        Dim importsDIR As String = args("/imports")
        Dim all As Boolean = args.GetBoolean("/all")
        Dim coverage As Double = args.GetValue("/coverage", 0.6)
        Dim identities As Double = args.GetValue("/identities", 0.3)
        Dim out As String = args.GetValue("/out", importsDIR & ".vennBBH/")
        Dim skipLoads As Boolean = args.GetBoolean("/skip-load")
        Dim sbhEntries As AlignEntry() = If(Not skipLoads, ExportLogData(importsDIR.LoadEntries, out & "/sbh/"), importsDIR.LoadEntries("*.csv"))
        Dim vennModel = ExportBidirectionalBesthit(sbhEntries, out & "/venn/")  ' 导出的是Top数据,  all的不好做
        Dim query As String = args.GetValue("/query", "")
        Dim VennTable As DocumentStream.File = DeltaMove(vennModel, query)
        Return VennTable > (out & "/Venn.Csv")
    End Function

    <ExportAPI("/venn.sbh.thread", Usage:="/venn.sbh.thread /in <blastp.txt> [/out <out.sbh.csv> /coverage <0.6> /identities <0.3>]")>
    Public Function SBHThread(args As CommandLine.CommandLine) As Integer
        Dim blastp As String = args("/in")
        Dim out As String = args.GetValue("/out", blastp.TrimFileExt & ".sbh.csv")
        Dim coverage As Double = args.GetValue("/coverage", 0.6)
        Dim identities As Double = args.GetValue("/identities", 0.3)
        Dim logs = BlastPlus.TryParse(blastp)
        Dim GrepMethod = TextGrepScriptEngine.Compile("tokens ' ' first").Method
        Dim GrepOperation As GrepOperation = New GrepOperation(GrepMethod, GrepMethod)
        Call GrepOperation.Grep(logs)
        Return logs.ExportAllBestHist(coverage, identities).SaveTo(out).CLICode
    End Function

    ''' <summary>
    ''' 1
    ''' </summary>
    ''' <param name="args"></param>
    ''' <returns></returns>
    <ExportAPI("/venn.cache",
               Info:="1. Creates the sbh cache data for the downstream bbh analysis.",
               Usage:="/venn.cache /imports <blastp.DIR> [/out <sbh.out.DIR> /coverage <0.6> /identities <0.3>]")>
    Public Function VennCache(args As CommandLine.CommandLine) As Integer
        Dim importsDIR As String = args("/imports")
        Dim coverage As Double = args.GetValue("/coverage", 0.6)
        Dim identities As Double = args.GetValue("/identities", 0.3)
        Dim out As String = args.GetValue("/out", importsDIR & ".venn-SBH/")
        Dim files = FileIO.FileSystem.GetFiles(importsDIR, FileIO.SearchOption.SearchAllSubDirectories, "*.txt")
        Dim CLI As String() =
            files.ToArray(
            Function(blastp) $"{GetType(CLI).API(NameOf(SBHThread))} /in {blastp.CliPath} /out {(out & "/" & blastp.BaseName & ".csv").CliPath} /coverage {coverage} /identities {identities}")

        Return App.SelfFolks(CLI, LQuerySchedule.Recommended_NUM_THREADS)
    End Function
End Module