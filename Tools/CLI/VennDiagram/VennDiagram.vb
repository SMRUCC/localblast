﻿Imports LANS.SystemsBiology.Assembly.KEGG.DBGET.bGetObject
Imports LANS.SystemsBiology.NCBI.Extensions.Analysis
Imports Microsoft.VisualBasic.CommandLine
Imports Microsoft.VisualBasic.CommandLine.Reflection
Imports Microsoft.VisualBasic.DocumentFormat.Csv
Imports Microsoft.VisualBasic.Language.UnixBash

Partial Module CLI

    <ExportAPI("/SSBH2BH_LDM",
               Usage:="/SSBH2BH_LDM /in <ssbh.csv> [/xml /coverage 0.8 /identities 0.3 /out <out.xml>]")>
    Public Function KEGGSSOrtholog2Bh(args As CommandLine) As Integer
        Dim [in] As String = args("/in")
        Dim out As String = args.GetValue("/out", [in].TrimFileExt & ".BestHit.Xml")
        Dim isXml As Boolean = args.GetBoolean("/xml")
        Dim Xml As HitCollection
        Dim coverage As Double = args.GetValue("/coverage", 0.8)
        Dim identities As Double = args.GetValue("/identities", 0.3)

        If isXml Then
            Dim ssbh As SSDB.OrthologREST = [in].LoadXml(Of SSDB.OrthologREST)
            Xml = KEGG_API.Export(ssbh)
        Else
            Dim ssbh As IEnumerable(Of SSDB.Ortholog) = [in].LoadCsv(Of SSDB.Ortholog)
            Xml = KEGG_API.Export(ssbh, [in].BaseName)
        End If

        Return Xml.SaveAsXml(out).CLICode
    End Function

    <ExportAPI("/SSDB.Export", Usage:="/SSDB.Export /in <inDIR> [/coverage 0.8 /identities 0.3 /out <out.Xml>]")>
    Public Function KEGGSSDBExport(args As CommandLine) As Integer
        Dim [in] As String = args - "/in"
        Dim out As String = args.GetValue("/out", [in].ParentPath & "/" & [in].BaseName & ".SSDB_BBH.Xml")
        Dim Xmls As IEnumerable(Of String) = ls - l - r - wildcards("*.xml") <= [in]
        Dim coverage As Double = args.GetValue("/coverage", 0.8)
        Dim identities As Double = args.GetValue("/identities", 0.3)
        Dim SSDB As BestHit = KEGG_API.EXPORT(Xmls.Select(AddressOf LoadXml(Of SSDB.OrthologREST)), coverage, identities)
        Return SSDB.SaveAsXml(out).CLICode
    End Function
End Module