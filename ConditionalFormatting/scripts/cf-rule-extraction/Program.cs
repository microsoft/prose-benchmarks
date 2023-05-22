using CommandLine;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ColorScheme = DocumentFormat.OpenXml.Drawing.ColorScheme;
using Color2Type = DocumentFormat.OpenXml.Drawing.Color2Type;
using System.Net.Sockets;
using DocumentFormat.OpenXml;

namespace ExcelLoader
{
  public interface IGlobalOptions
  {
    public bool GZip { get; }
    public bool Verbose { get; }
  }

  public class Options : IGlobalOptions
  {
    [Option('i', "input", HelpText = "The path to the input file (or directory)")]
    public string? InputPath { get; set; }

    [Option('o', "output", HelpText = "The path to the output file (or directory)")]
    public string? OutputPath { get; set; }

    [Option("server", HelpText = "Run as a server")]
    public string? Server { get; set; }

    [Option("gzip", HelpText = "Write gzipped output")]
    public bool? GZipOpt { get; set; } // Annoyingly, the library won't work with a bool and needs a bool? - it treats "bool" as being a flag, there's no way to set to false
    public bool GZip
    {
      get => GZipOpt ?? true;
    }

    [Option('v', "verbose", HelpText = "Write success output", Default = false)]
    public bool Verbose { get; set; }
  }

  public class ServerInput
  {
    public string Input;
    public string Output;

    public ServerInput(string input, string output)
    {
      Input = input;
      Output = output;
    }
  }

  public class Program
  {
    private static int Main(string[] args)
    {
      return Parser.Default.ParseArguments<Options>(args).MapResult(
        opts =>
        {
          if (opts.Server != null)
          {
            if (opts.InputPath != null || opts.OutputPath != null)
            {
              Console.Error.WriteLine("Cannot specify \"input\" or \"output\" with \"server\"");
              return 1;
            }
            return ProcessServer(opts.Server, opts);
          }

          if (opts.InputPath == null || opts.OutputPath == null)
          {
            Console.Error.WriteLine("Must specify \"input\" and \"output\", or \"server\"");
            return 1;
          }

          if (Directory.Exists(opts.InputPath))
          {
            return ProcessDirectory(opts.InputPath, opts.OutputPath, opts);
          }

          return ProcessFile(opts.InputPath, opts.OutputPath, opts, error => Console.Error.WriteLine(error));
        },
        _ => 1);
    }

    private static int ProcessServer(string endpoint, IGlobalOptions opts)
    {
      Console.WriteLine($"Started processing server. Connecting to {endpoint}");
      var client = new TcpClient();
      client.Connect(IPEndPoint.Parse(endpoint));
      var reader = new StreamReader(client.GetStream(), Encoding.UTF8);
      var writer = new StreamWriter(client.GetStream());
      try{
        while (true)
        {
          var line = reader.ReadLine();
          if (line == null)
          {
            break;
          }

          var command = Newtonsoft.Json.JsonConvert.DeserializeObject<ServerInput>(line);

          var rv = ProcessFile(command.Input, command.Output, opts, error => Console.Error.WriteLine(error));
          if (rv != 0)
          {
            return rv;
          }
          writer.WriteLine("{}");
          writer.Flush();
        }

        return 0;
      }
      catch(Exception e) {
        writer.WriteLine(e.ToString());
        writer.Flush();
        throw;
      }
      finally {
        client.Close();
      }
    }

    private static int ProcessDirectory(string inputDir, string outputDir, IGlobalOptions opts)
    {
      Directory.CreateDirectory(outputDir);
      int rv = 0;
      Parallel.ForEach(
        Directory.GetFiles(inputDir, "*.xlsx"),
        new ParallelOptions()
        {
          MaxDegreeOfParallelism = Environment.ProcessorCount - 1
        },
        file =>
        {
          var fileRv = ProcessFile(file, outputDir, opts, error =>
          {
            var errorFile = Path.Combine(outputDir, Path.ChangeExtension(Path.GetFileName(file), "error"));
            Directory.CreateDirectory(Path.GetDirectoryName(errorFile)!);
            File.WriteAllText(errorFile, error); // UTF-8, no BOM
          });
          if (fileRv != 0) Interlocked.Exchange(ref rv, fileRv);
        }
      );
      return rv;
    }

    private static int ProcessFile(string inputPath, string outputPath, IGlobalOptions opts, Action<string> errorHandler)
    {
      var file = inputPath;
      var inputFileId = Path.GetFileName(inputPath);

      var newFile = Directory.Exists(outputPath)
        ? Path.Combine(outputPath, Path.ChangeExtension(Path.GetFileName(file), opts.GZip ? "input.json.gz" : "input.json"))
        : outputPath;
      var sw = Stopwatch.StartNew();
      var statusSuffix = "";
      JsonWorkbook? newContents = null;
      string? errorMessage = null;

      try
      {
        using var memStream = new MemoryStream();
        using var fileStream = File.OpenRead(file);
        // We need to copy into the MemoryStream to create a resizable stream.
        // ParseWorkbook code needs to be able to write into and resize the stream in order to use the "RelationshipErrorHandlerFactory" feature.
        fileStream.CopyTo(memStream);
        newContents = ParseWorkbook(memStream, Path.GetFileName(file));
      }
      catch (Exception e)
      {
        errorMessage = $"Message: {e}";
        statusSuffix += $", convert error: {e.Message}";
      }

      if (newContents != null)
      {
        try
        {
          Directory.CreateDirectory(Path.GetDirectoryName(newFile)!);
          using var fileStream = File.Open(newFile, FileMode.Create);
          ExcelLoader.JsonConvert.SerializeObject(fileStream, newContents, opts.GZip);
        }
        catch (Exception e)
        {
          errorMessage = $"Write error\nMessage: {e}";
          statusSuffix += $", write error: {e.Message}";
        }
      }

      if (errorMessage != null)
      {
        try
        {
          errorHandler(errorMessage);
        }
        catch (Exception e)
        {
          errorMessage = $"Error handling error!\n{e}\n\nOriginal error: {errorMessage}";
          Console.Error.WriteLine(errorMessage);
          statusSuffix += $", error handling error: {e.Message}";
        }
      }

      if (opts.Verbose || statusSuffix != "")
      {
        Console.WriteLine($"Done: {file}, took {sw.Elapsed}{statusSuffix}");
      }
      return errorMessage != null ? 1 : 0;
    }

    private static JsonWorkbook ParseWorkbook(SpreadsheetDocument doc, string origin)
    {
      var book = doc.WorkbookPart;
      var sharedStringTable = book.SharedStringTablePart?.SharedStringTable
        .OfType<SharedStringItem>() // filter out extLst elements
        .Select(ssi => ParseRichString(ssi)).ToArray() ?? Array.Empty<string>();
      var sheetNames = new List<string>();
      // var sheets = new Dictionary<string, JsonWorksheet>();
      var sheets = new List<JsonWorksheet>();
      foreach (var child in book.Workbook.Sheets)
      {
        // The child *should* be a Sheet, but it can be a "significant whitespace" object if the XML was
        // pretty -printed (which occurs in a couple of corpus files)
        if (!(child is Sheet)) continue;
        var sheet = (Sheet)child;

        if (!string.IsNullOrWhiteSpace(sheet.Id))
        {
          var part = book.GetPartById(sheet.Id);
          sheets.Add(ParseWorksheet(sheet.Name, part, sharedStringTable));
        }
      }

      return new JsonWorkbook(sheets, properties: ParseWorkbookProperties(book), packageProperties: ParsePackageProperties(doc.PackageProperties), origin);
    }

    /// <param name="xlsxWorkbookStream">The OpenXML code needs to be able to write into and resize the stream in order to use the "RelationshipErrorHandlerFactory" feature.</param>
    public static JsonWorkbook ParseWorkbook(Stream xlsxWorkbookStream, string origin)
    {
      using var doc = SpreadsheetDocument.Open(xlsxWorkbookStream, true, new OpenSettings()
      {
        RelationshipErrorHandlerFactory = RelationshipErrorHandler.CreateRewriterFactory((Uri partUri, string id, string uri) =>
        {
          // The System.IO.Packaging assembly has a nasty bug: it uses System.Uri to represent the hyperlinks in OpenXML documents.
          // However - Word and Excel allow you put user-entered text in there (such as "mailto" links) and doesn't
          // enforce they are valid! Unfortunately, the System.IO.Packaging guys are unwilling to fix this. So, the DocumentFormat.OpenXML
          // SDK guys have done their own hacky workaround, which is to add this grotty "RelationshipErrorHandlerFactory" thing, which
          // does some low-level rewriting of the Packaging format and retries if the Packaging code throws a Uri error. We rewrite the
          // URIs into a form that the Packaging code will accept. All a big nuisance.
          return "http://MALFORMED-REPLACED/" + WebUtility.UrlEncode(uri);
        })
      });
      return ParseWorkbook(doc, origin);
    }

    private static JsonWorksheet ParseWorksheet(string name, OpenXmlPart sheet, string[] sharedStringTable)
    {
      if (sheet is ChartsheetPart)
      {
        return new JsonWorksheet(
          name: name,
          type: JsonWorksheetType.chart,
          cells: null,
          rows: null,
          cols: null,
          tables: null,
          mergeCells: null,
          properties: null,
          conditionalFormatting: null
        );
      }

      if (sheet is DialogsheetPart)
      {
        return new JsonWorksheet(
          name: name,
          type: JsonWorksheetType.dialog,
          cells: null,
          rows: null,
          cols: null,
          tables: null,
          mergeCells: null,
          properties: null,
          conditionalFormatting: null
        );
      }

      if (sheet is WorksheetPart worksheet)
      {
        var cells = new List<JsonCell>();
        var rows = new List<JsonRow>();
        var cols = new List<JsonCol>();
        var tables = new List<JsonTable>();
        var mergeCells = new List<JsonMergeCell>();
        var properties = new JsonWorksheetProperties();
        var conditionalFormatting = new List<JsonConditionalFormatting>();

        foreach (var child in worksheet.Worksheet)
        {
          if (child is SheetProperties propertiesChild)
          {
            var filterMode = propertiesChild.FilterMode != null && propertiesChild.FilterMode;
            if (filterMode) properties.HasFilters = true;

            var transEvaluation = propertiesChild.TransitionEvaluation;
            if (transEvaluation != null && transEvaluation) properties.TransitionEvaluation = true;
            var transEntry = propertiesChild.TransitionEntry;
            if (transEntry != null && transEntry) properties.TransitionEntry = true;
          }
          else if (child is SheetFormatProperties formatPropertiesChild)
          {
            if (formatPropertiesChild.DefaultRowHeight != null) properties.DefaultRowHeight = formatPropertiesChild.DefaultRowHeight;
            if (formatPropertiesChild.DefaultColumnWidth != null) properties.DefaultColWidth = formatPropertiesChild.DefaultColumnWidth;
          }
          else if (child is SheetData dataChild)
          {
            uint lastRowIndex = 0;
            foreach (var rowChild in dataChild)
            {
              if (!(rowChild is Row)) continue;
              var row = (Row)rowChild;

              uint rowIndex;
              if (row.RowIndex != null)
              {
                uint row_ = row.RowIndex;
                if (row_ <= lastRowIndex)
                {
                  throw new ArgumentException($"Unexpected RowIndex = {row.RowIndex} out of order");
                }
                rowIndex = row_;
              }
              else
              {
                rowIndex = lastRowIndex + 1;
              }
              lastRowIndex = rowIndex;

              var hidden = row.Hidden != null && row.Hidden;
              var height = row.Height != null ? (double?)row.Height.Value : null;
              if (hidden || height != null)
              {
                rows.Add(new JsonRow(rowIndex, hidden, height));
              }

              uint lastColIndex = 0;
              foreach (var cellChild in row)
              {
                if (!(cellChild is Cell)) continue;
                var cell = (Cell)cellChild;

                // It's very rare and somewhat annoying, but just a few sheets in the corpus tests use the
                // XLSX "feature" that cells can have a missing CellReference, in which case the cell is taken
                // to be automatically next to the previous cell.
                uint colIndex;
                string cellReference;
                if (cell.CellReference != null)
                {
                  cellReference = cell.CellReference;
                  (uint row_, uint col_) = ParseCellReference(cell.CellReference);
                  if (row_ != rowIndex)
                  {
                    throw new ArgumentException($"Unexpected CellReference = {cell.CellReference} mismatching row in row {row.RowIndex}");
                  }
                  if (col_ <= lastColIndex)
                  {
                    // This is a genuine error. It occurs for a few files in the FUSE corpus - and Excel also complains when it
                    // opens the file and tries to "repair" it. We shouldn't repair, these are genuine errors.
                    throw new ArgumentException($"Unexpected CellReference = {cell.CellReference} out of order - likely a broken file");
                  }
                  colIndex = col_;

                  if (WriteCellReference(rowIndex, colIndex) != cellReference)
                  {
                    throw new Exception($"WriteCellReference != ParseCellReference");
                  }
                }
                else
                {
                  colIndex = lastColIndex + 1;
                  cellReference = WriteCellReference(rowIndex, colIndex);
                }
                lastColIndex = colIndex;

                cells.Add(ParseCellObject(cellReference, cell, sharedStringTable));
              }
            }
          }
          else if (child is Columns columnsChild)
          {
            foreach (Column col in columnsChild)
            {
              var hidden = col.Hidden ?? false;
              var width = col.Width != null ? (double?)col.Width.Value : null;
              cols.Add(new JsonCol(hidden, width, col.Min, col.Max));
            }
          }
          else if (child is AutoFilter filter && filter.HasChildren)
          {
            // If there is at least one filter on the sheet, the sheet is considered to be in filter mode.
            properties.HasFilters = true;
          }
          else if (child is TableParts tablesChild)
          {
            foreach (TablePart table in tablesChild)
            {
              var rawTableDefPart = sheet.GetPartById(table.Id);
              if (rawTableDefPart is TableDefinitionPart tableDef)
              {
                var tableObj = ParseTable(tableDef);
                tables.Add(tableObj);

                if (IsTableInFilterMode(tableDef)) properties.HasFilters = true;
              }
            }
          }
          else if (child is MergeCells mergeCellsChild)
          {
            foreach (MergeCell mergeCell in mergeCellsChild)
            {
              mergeCells.Add(new JsonMergeCell(mergeCell.Reference));
            }
          }
          else if (child is ConditionalFormatting cfChild)
          {
            // Adds the Conditional Formatting rules defined in the object to the list
            conditionalFormatting.AddRange(getConditionalFormattingInfo(cfChild));
          }
        }

        // remove sheets that are too large
        if (cells.Count > 100000)
        {
          return new JsonWorksheet(
            name: name,
            type: null,
            cells: null,
            rows: null,
            cols: null,
            tables: null,
            mergeCells: null,
            properties: null,
            conditionalFormatting: null
          );
        }

        return new JsonWorksheet(
          name: name,
          type: null,
          cells,
          rows: rows.Any() ? rows : null,
          cols: cols.Any() ? cols : null,
          tables: tables.Any() ? tables : null,
          mergeCells: mergeCells.Any() ? mergeCells : null,
          properties,
          conditionalFormatting: conditionalFormatting.Any() ? conditionalFormatting : null
        );
      }

      throw new ArgumentOutOfRangeException($"Unhandled case: sheet not ChartsheetPart, DialogsheetPart nor WorksheetPart: {sheet.GetType().Name}");
    }

    /// <summary>
    /// Extracts all CF Rules from the cfChild object and returns them as a list of JSON objects.
    /// </summary>
    /// <param name="cfChild"> XML object containing CF information (all rules) from a sheet </param>
    /// <returns> List of JSON objects each representing a single CF Rule </returns>
    public static List<JsonConditionalFormatting> getConditionalFormattingInfo(ConditionalFormatting cfChild)
    {
      var conditionalFormatting = new List<JsonConditionalFormatting>();

      // Range of the CF Rule
      string sqref = cfChild.SequenceOfReferences.ToString();

      // Iterating over the rules in the specified range of the CF object
      foreach (ConditionalFormattingRule cfRule in cfChild)
      {
        // Target formatting style ID of the Rule
        var FormatId = cfRule.FormatId;

        // Operation used in case of Cell Value Rule
        var op = cfRule.Operator;

        // Text argument in case of Text based Rule (TextContains, TextStartsWith, etc.)
        var text = cfRule.Text;

        // Special flags for Cell Value above/below/Top-k threshold Rules
        var aboveAvg = cfRule.AboveAverage;
        var bottom = cfRule.Bottom;
        var equalAvg = cfRule.EqualAverage;
        var percent = cfRule.Percent;

        // Time perido argument for Date Time CF Rules
        var timePeriod = cfRule.TimePeriod;

        // Type of Rule (Cell Value, Top-k, Color Scale, Blanks, TextContains, etc.)
        var type = cfRule.Type;

        // Priority of the CF Rule in application on the sheet
        var priority = cfRule.Priority;

        // Rank of the CF Rule in the sheet
        var rank = cfRule.Rank;

        // Arguments to the CF Rule
        var arguments = new List<string>();

        foreach (var cfRuleChild in cfRule)
        {
          if (cfRuleChild is Formula cfForm)
          {
            arguments.Add(cfForm.Text);
          }
          else if (cfRuleChild is ColorScale cfColorScale)
          {
            foreach (var colorVal in cfColorScale)
            {
              if (colorVal is ConditionalFormatValueObject cfColorFormalValueObject)
              {
                arguments.Add(cfColorFormalValueObject.Type.ToString());
                arguments.Add(cfColorFormalValueObject.Val);
              }
              if (colorVal is Color cfColor)
              {
                arguments.Add(cfColor.Rgb);
              }
            }
          }
        }
        // Null checks
        if (percent is null){
          percent = false;
        }
        if (aboveAvg is null){
          aboveAvg = false;
        }
        if (bottom is null){
          bottom = false;
        }
        if (equalAvg is null){
          equalAvg = false;
        }
        if (FormatId is null){
          FormatId = 0;
        }
        if (rank is null){
          rank = 0;
        }
        if (priority is null){
          priority = 0;
        }
      // Add the parsed rule to list
      conditionalFormatting.Add(new JsonConditionalFormatting(sqref, type, aboveAvg, bottom, equalAvg, percent, timePeriod, op, arguments, text, FormatId, rank, priority));
      }
      return conditionalFormatting;
    }

    public static JsonTable ParseTable(TableDefinitionPart table)
    {
      var columnNames = new List<string>();
      foreach (TableColumn column in table.Table.TableColumns)
      {
        columnNames.Add(column.Name);
      }
      if (columnNames.Distinct().Count() != columnNames.Count)
      {
        throw new ArgumentOutOfRangeException(nameof(table), "Unexpected column names: not unique");
      }
      var (_, _, _, cols) = ParseRangeReference(table.Table.Reference);
      if (columnNames.Count != cols)
      {
        throw new ArgumentOutOfRangeException(nameof(table), "Unexpected column names: not enough provided");
      }
      return new JsonTable(
        name: table.Table.DisplayName,
        range: table.Table.Reference,
        hasHeadersRow: (table.Table.HeaderRowCount ?? 1) > 0,
        hasTotalsRow: (table.Table.TotalsRowCount ?? 0) > 0,
        columnNames
      );
    }

    private static bool IsTableInFilterMode(TableDefinitionPart table)
    {
      foreach (var child in table.Table)
      {
        if (child is AutoFilter)
        {
          // If any related table has a filter, the sheet is considered to be in filter mode.
          return true;
        }
      }
      return false;
    }

    public static JsonCell ParseCellObject(string cellReference, Cell cell, string[] sharedStringTable)
    {
      JsonCellValueType t;
      object? v;
      if (
        ((cell.DataType == null || cell.DataType.Value == CellValues.SharedString || cell.DataType.Value == CellValues.Number || cell.DataType.Value == CellValues.Error || cell.DataType.Value == CellValues.Boolean) && (cell.CellValue == null || cell.CellValue.Text == "")) ||
        (cell.DataType?.Value == CellValues.String && cell.CellValue == null)
      )
      {
        t = JsonCellValueType.z;
        v = null;
      }
      else if (cell.DataType?.Value == CellValues.SharedString)
      {
        t = JsonCellValueType.s;
        v = sharedStringTable[uint.Parse(cell.CellValue.Text)];
      }
      else if (cell.DataType?.Value == CellValues.String)
      {
        t = JsonCellValueType.s;
        v = cell.CellValue.Text;
      }
      else if (cell.DataType?.Value == CellValues.InlineString)
      {
        t = JsonCellValueType.s;
        v = ParseRichString(cell.InlineString);
      }
      else if (cell.DataType?.Value == CellValues.Error)
      {
        t = JsonCellValueType.e;
        v = cell.CellValue.Text;
      }
      else if (cell.DataType?.Value == CellValues.Boolean && uint.TryParse(cell.CellValue.Text, out var boolAsUint))
      {
        t = JsonCellValueType.b;
        v = boolAsUint != 0;
      }
      else if (cell.DataType?.Value == null || cell.DataType?.Value == CellValues.Number)
      {
        // We want to preserve the double's value exactly, so we grab and write out its string representation.
        // This avoids concern about whether the JSON libraries used are able to round-trip the double without
        // flipping the least-significant bit (or some other minor error).

        t = JsonCellValueType.n;
        v = cell.CellValue.Text;
      }
      else if (cell.DataType?.Value == CellValues.Date)
      {
        t = JsonCellValueType.d;
        v = cell.CellValue.Text;
      }
      else
      {
        throw new ArgumentOutOfRangeException($"Unhandled case: cell.DataType = {cell.DataType}; cell.CellValue = {cell.CellValue?.Text}");
      }

      string? f = null;
      JsonCellFormulaType? f_t = null;
      uint? f_si = null;
      string? f_ref = null;
      if (cell.CellFormula != null)
      {
        if (cell.CellFormula.FormulaType?.Value == null || cell.CellFormula.FormulaType?.Value == CellFormulaValues.Normal)
        {
          f = cell.CellFormula.Text;
        }
        else if (cell.CellFormula.FormulaType?.Value == CellFormulaValues.Array)
        {
          f = cell.CellFormula.Text;
          f_t = JsonCellFormulaType.array;
          f_ref = cell.CellFormula.Reference;
        }
        else if (cell.CellFormula.FormulaType?.Value == CellFormulaValues.Shared)
        {
          f = cell.CellFormula.Text;
          f_t = JsonCellFormulaType.shared;
          f_si = cell.CellFormula.SharedIndex;
          f_ref = cell.CellFormula.Reference;
        }
        else if (cell.CellFormula.FormulaType?.Value == CellFormulaValues.DataTable)
        {
          // do nothing with data tables for now
          f_t = JsonCellFormulaType.datatable;
          f_ref = cell.CellFormula.Reference;
        }
        else
        {
          throw new ArgumentOutOfRangeException($"Unhandled case: cell.CellFormula.FormulaType = {cell.CellFormula.FormulaType}");
        }
      }

      uint? s = null;
      if (cell.StyleIndex != null)
      {
        s = cell.StyleIndex;
      }

      return new JsonCell(cellReference, t, v, f, f_t, f_si, f_ref, s);
    }

    private static JsonWorkbookProperties ParseWorkbookProperties(WorkbookPart book)
    {
      var colorScheme = book.ThemePart?.Theme?.ThemeElements?.ColorScheme;

      var names = new List<JsonDefinedName>();
      if (book.Workbook.DefinedNames != null)
      {
        foreach (var nameChild in book.Workbook.DefinedNames)
        {
          if (!(nameChild is DefinedName)) continue;
          var name = (DefinedName)nameChild;

          var jsonName = ParseDefinedName(name);
          names.Add(jsonName);
        }
      }

      var numFmts = new List<JsonNumFmt>();
      if (book?.WorkbookStylesPart?.Stylesheet?.NumberingFormats != null)
      {
        foreach (NumberingFormat fmtChild in book.WorkbookStylesPart.Stylesheet.NumberingFormats)
        {
          numFmts.Add(ParseNumberingFormat(fmtChild));
        }
      }

      var diffFormats = new List<JsonDifferentialFormats>();
      if (book?.WorkbookStylesPart?.Stylesheet?.DifferentialFormats != null){
        int id = 1;
        foreach (DifferentialFormat diffFmtChild in book.WorkbookStylesPart.Stylesheet.DifferentialFormats)
        {
          JsonFill? fill = null;
          JsonFont? font = null;
          JsonBorder? border = null;
          if (diffFmtChild.Fill != null)
          {
            fill = ParseFill(diffFmtChild.Fill, colorScheme);
          }
          if (diffFmtChild.Font != null)
          {
            font = ParseFont(diffFmtChild.Font, colorScheme);
          }
          if (diffFmtChild.Border != null)
          {
            border = ParseBorder(diffFmtChild.Border, colorScheme);
          }
          diffFormats.Add(new JsonDifferentialFormats(id, fill, font, border));
          id += 1;
        }
      }

      var fonts = new List<JsonFont>();
      if (book?.WorkbookStylesPart?.Stylesheet?.Fonts != null)
      {
        foreach (Font fontChild in book.WorkbookStylesPart.Stylesheet.Fonts)
        {
          fonts.Add(ParseFont(fontChild, colorScheme));
        }
      }

      var borders = new List<JsonBorder>();
      if (book?.WorkbookStylesPart?.Stylesheet?.Borders != null)
      {
        foreach (Border borderChild in book.WorkbookStylesPart.Stylesheet.Borders)
        {
          borders.Add(ParseBorder(borderChild, colorScheme));
        }
      }

      var fills = new List<JsonFill>();
      if (book?.WorkbookStylesPart?.Stylesheet?.Fills != null)
      {
        foreach (Fill fillChild in book.WorkbookStylesPart.Stylesheet.Fills)
        {
          fills.Add(ParseFill(fillChild, colorScheme));
        }
      }

      // The CellStyleFormats (<cellStyleXfs>) records are master/named cell styles, which are
      // overridden by individual cell styles.
      var cellStyleXfs = new Dictionary<uint, JsonCellXf>();
      if (book?.WorkbookStylesPart?.Stylesheet?.CellStyleFormats != null)
      {
        foreach (CellFormat fmtChild in book.WorkbookStylesPart.Stylesheet.CellStyleFormats)
        {
          cellStyleXfs[(uint)cellStyleXfs.Count] = ParseCellFormat(fmtChild, null);
        }
      }

      // The CellFormats (<cellXfs>) records are what the cells reference. These hold per-cell
      // formatting, plus a link to global/named cell styles.
      var cellXfs = new List<JsonCellXf>();
      if (book?.WorkbookStylesPart?.Stylesheet?.CellFormats != null)
      {
        foreach (CellFormat fmtChild in book.WorkbookStylesPart.Stylesheet.CellFormats)
        {
          cellXfs.Add(ParseCellFormat(fmtChild, cellStyleXfs));
        }
      }

      return new JsonWorkbookProperties(names, numFmts, fonts, fills, borders, cellXfs, diffFormats)
      {
        R1C1RefMode = book?.Workbook?.CalculationProperties?.ReferenceMode?.Value == ReferenceModeValues.R1C1,
        FullPrecision = book?.Workbook?.CalculationProperties?.FullPrecision?.Value,
        Date1904 = book?.Workbook?.WorkbookProperties?.Date1904?.Value,
        DateCompatibility = book?.Workbook?.WorkbookProperties?.DateCompatibility?.Value ??
          (book?.Workbook?.Conformance?.Value == ConformanceClass.Enumstrict ? false as bool? : null)
      };
    }

    private static JsonDefinedName ParseDefinedName(DefinedName name)
    {
      return new JsonDefinedName(
        name: name.Name,
        @ref: name.Text,
        sheet: name.LocalSheetId?.Value
      );
    }

    private static JsonNumFmt ParseNumberingFormat(NumberingFormat fmt)
    {
      return new JsonNumFmt(numberFormatId: fmt.NumberFormatId.Value, formatCode: fmt.FormatCode);
    }

    private static JsonFont ParseFont(Font font, ColorScheme? colorScheme)
    {
      return new JsonFont()
      {
        Name = font.FontName?.Val?.Value,
        Bold = font.Bold != null ? (bool?)(font.Bold.Val ?? true) : null,
        Italic = font.Italic != null ? (bool?)(font.Italic.Val ?? true) : null,
        Underline = font.Underline != null ? (JsonFontUnderline?)Enum.Parse<JsonFontUnderline>(Enum.GetName(font.Underline.Val?.Value ?? UnderlineValues.Single)!) : null,
        Strikethrough = font.Strike != null ? (bool?)(font.Strike.Val ?? true) : null,
        VerticalAlign = font.VerticalTextAlignment != null ? (JsonFontVerticalAlignment?)Enum.Parse<JsonFontVerticalAlignment>(Enum.GetName(font.VerticalTextAlignment.Val.Value)!) : null,
        Size = font.FontSize?.Val?.Value,
        Color = font.Color != null ? ParseColor(font.Color, colorScheme) : null
      };
    }

    private static JsonBorder ParseBorder(Border border, ColorScheme? colorScheme)
    {
      return new JsonBorder()
      {
        Top = border.TopBorder != null ? ParseBorderProperties(border.TopBorder, colorScheme) : null,
        Bottom = border.BottomBorder != null ? ParseBorderProperties(border.BottomBorder, colorScheme) : null,
        Left = border.LeftBorder != null ? ParseBorderProperties(border.LeftBorder, colorScheme) : null,
        Right = border.RightBorder != null ? ParseBorderProperties(border.RightBorder, colorScheme) : null
      };
    }

    private static JsonBorderProperties ParseBorderProperties(BorderPropertiesType border, ColorScheme? colorScheme)
    {
      return new JsonBorderProperties()
      {
        Type = border.Style != null ? (JsonBorderLineType?)Enum.Parse<JsonBorderLineType>(Enum.GetName(border.Style.Value)!) : null,
        Color = border.Color != null ? ParseColor(border.Color, colorScheme) : null
      };
    }

    private static JsonFill ParseFill(Fill fill, ColorScheme? colorScheme)
    {
      if (fill.PatternFill != null)
      {
        return new JsonFill()
        {
          Pattern = new JsonPatternFill()
          {
            Type = fill.PatternFill.PatternType != null ? (JsonPatternType?)Enum.Parse<JsonPatternType>(Enum.GetName(fill.PatternFill.PatternType.Value)!) : null,
            FgColor = fill.PatternFill.ForegroundColor != null ? ParseColor(fill.PatternFill.ForegroundColor, colorScheme) : null,
            BgColor = fill.PatternFill.BackgroundColor != null ? ParseColor(fill.PatternFill.BackgroundColor, colorScheme) : null
          }
        };
      }
      if (fill.GradientFill != null)
      {
        return new JsonFill()
        {
          Gradient = new JsonGradientFill() { }
        };
      }
      return new JsonFill() { };
    }

    private static JsonColor ParseColor(ColorType color, ColorScheme? colorScheme)
    {
      System.Drawing.Color sysColor = System.Drawing.Color.Transparent;
      if (color.Auto != null && color.Auto.Value)
      {
        sysColor = System.Drawing.Color.Black;
      }
      else if (color.Rgb != null)
      {
        sysColor = ParseArgb(color.Rgb.Value);
      }
      else if (color.Indexed != null)
      {
        sysColor = indexedColors.TryGetValue(color.Indexed.Value, out var indexedValue) ? indexedValue : System.Drawing.Color.Black;
      }
      else if (color.Theme != null)
      {
        // For reasons I don't at all understand, the theme="N" attributes seem to be ordered differently
        // to what the OpenXML spec says. Really annoying and random.
        var col2 = (Color2Type)colorScheme!.ElementAt(
          (int)color.Theme.Value switch
          {
            0 => 1,
            1 => 0,
            2 => 3,
            3 => 2,
            int i => i
          }
        );
        if (col2.SystemColor != null)
        {
          sysColor = col2.SystemColor.LastColor != null ? ParseArgb(col2.SystemColor.LastColor) : System.Drawing.Color.Black;
        }
        else if (col2.RgbColorModelHex != null)
        {
          sysColor = ParseArgb(col2.RgbColorModelHex.Val);
        }
      }

      if (color.Tint != null)
      {
        // TODO: Implementing tinting a theme color, by using the C# built-in HSL accessors, then
        // applying the "tinting" formula from the OpenXML spec, then converting back to ARGB.
      }

      return new JsonColor(((uint)sysColor.ToArgb()).ToString("X8"));
    }

    private static System.Drawing.Color ParseArgb(string argb)
    {
      if (argb == "")
      {
        return System.Drawing.Color.Black;
      }
      var hex = argb.StartsWith("#") ? argb[1..] : argb;
      var i = Convert.ToUInt32(hex, 16);
      if (hex.Length <= 6)
      {
        i |= 0xff000000;
      }
      return System.Drawing.Color.FromArgb((int)i);
    }

    private static readonly IDictionary<uint, System.Drawing.Color> indexedColors = new Dictionary<uint, System.Drawing.Color>
    {
      [0] = System.Drawing.Color.FromArgb(unchecked((int)0xFF000000)),
      [1] = System.Drawing.Color.FromArgb(unchecked((int)0xFFFFFFFF)),
      [2] = System.Drawing.Color.FromArgb(unchecked((int)0xFFFF0000)),
      [3] = System.Drawing.Color.FromArgb(unchecked((int)0xFF00FF00)),
      [4] = System.Drawing.Color.FromArgb(unchecked((int)0xFF0000FF)),
      [5] = System.Drawing.Color.FromArgb(unchecked((int)0xFFFFFF00)),
      [6] = System.Drawing.Color.FromArgb(unchecked((int)0xFFFF00FF)),
      [7] = System.Drawing.Color.FromArgb(unchecked((int)0xFF00FFFF)),
      [8] = System.Drawing.Color.FromArgb(unchecked((int)0xFF000000)),
      [9] = System.Drawing.Color.FromArgb(unchecked((int)0xFFFFFFFF)),
      [10] = System.Drawing.Color.FromArgb(unchecked((int)0xFFFF0000)),
      [11] = System.Drawing.Color.FromArgb(unchecked((int)0xFF00FF00)),
      [12] = System.Drawing.Color.FromArgb(unchecked((int)0xFF0000FF)),
      [13] = System.Drawing.Color.FromArgb(unchecked((int)0xFFFFFF00)),
      [14] = System.Drawing.Color.FromArgb(unchecked((int)0xFFFF00FF)),
      [15] = System.Drawing.Color.FromArgb(unchecked((int)0xFF00FFFF)),
      [16] = System.Drawing.Color.FromArgb(unchecked((int)0xFF800000)),
      [17] = System.Drawing.Color.FromArgb(unchecked((int)0xFF008000)),
      [18] = System.Drawing.Color.FromArgb(unchecked((int)0xFF000080)),
      [19] = System.Drawing.Color.FromArgb(unchecked((int)0xFF808000)),
      [20] = System.Drawing.Color.FromArgb(unchecked((int)0xFF800080)),
      [21] = System.Drawing.Color.FromArgb(unchecked((int)0xFF008080)),
      [22] = System.Drawing.Color.FromArgb(unchecked((int)0xFFC0C0C0)),
      [23] = System.Drawing.Color.FromArgb(unchecked((int)0xFF808080)),
      [24] = System.Drawing.Color.FromArgb(unchecked((int)0xFF9999FF)),
      [25] = System.Drawing.Color.FromArgb(unchecked((int)0xFF993366)),
      [26] = System.Drawing.Color.FromArgb(unchecked((int)0xFFFFFFCC)),
      [27] = System.Drawing.Color.FromArgb(unchecked((int)0xFFCCFFFF)),
      [28] = System.Drawing.Color.FromArgb(unchecked((int)0xFF660066)),
      [29] = System.Drawing.Color.FromArgb(unchecked((int)0xFFFF8080)),
      [30] = System.Drawing.Color.FromArgb(unchecked((int)0xFF0066CC)),
      [31] = System.Drawing.Color.FromArgb(unchecked((int)0xFFCCCCFF)),
      [32] = System.Drawing.Color.FromArgb(unchecked((int)0xFF000080)),
      [33] = System.Drawing.Color.FromArgb(unchecked((int)0xFFFF00FF)),
      [34] = System.Drawing.Color.FromArgb(unchecked((int)0xFFFFFF00)),
      [35] = System.Drawing.Color.FromArgb(unchecked((int)0xFF00FFFF)),
      [36] = System.Drawing.Color.FromArgb(unchecked((int)0xFF800080)),
      [37] = System.Drawing.Color.FromArgb(unchecked((int)0xFF800000)),
      [38] = System.Drawing.Color.FromArgb(unchecked((int)0xFF008080)),
      [39] = System.Drawing.Color.FromArgb(unchecked((int)0xFF0000FF)),
      [40] = System.Drawing.Color.FromArgb(unchecked((int)0xFF00CCFF)),
      [41] = System.Drawing.Color.FromArgb(unchecked((int)0xFFCCFFFF)),
      [42] = System.Drawing.Color.FromArgb(unchecked((int)0xFFCCFFCC)),
      [43] = System.Drawing.Color.FromArgb(unchecked((int)0xFFFFFF99)),
      [44] = System.Drawing.Color.FromArgb(unchecked((int)0xFF99CCFF)),
      [45] = System.Drawing.Color.FromArgb(unchecked((int)0xFFFF99CC)),
      [46] = System.Drawing.Color.FromArgb(unchecked((int)0xFFCC99FF)),
      [47] = System.Drawing.Color.FromArgb(unchecked((int)0xFFFFCC99)),
      [48] = System.Drawing.Color.FromArgb(unchecked((int)0xFF3366FF)),
      [49] = System.Drawing.Color.FromArgb(unchecked((int)0xFF33CCCC)),
      [50] = System.Drawing.Color.FromArgb(unchecked((int)0xFF99CC00)),
      [51] = System.Drawing.Color.FromArgb(unchecked((int)0xFFFFCC00)),
      [52] = System.Drawing.Color.FromArgb(unchecked((int)0xFFFF9900)),
      [53] = System.Drawing.Color.FromArgb(unchecked((int)0xFFFF6600)),
      [54] = System.Drawing.Color.FromArgb(unchecked((int)0xFF666699)),
      [55] = System.Drawing.Color.FromArgb(unchecked((int)0xFF969696)),
      [56] = System.Drawing.Color.FromArgb(unchecked((int)0xFF003366)),
      [57] = System.Drawing.Color.FromArgb(unchecked((int)0xFF339966)),
      [58] = System.Drawing.Color.FromArgb(unchecked((int)0xFF003300)),
      [59] = System.Drawing.Color.FromArgb(unchecked((int)0xFF333300)),
      [60] = System.Drawing.Color.FromArgb(unchecked((int)0xFF993300)),
      [61] = System.Drawing.Color.FromArgb(unchecked((int)0xFF993366)),
      [62] = System.Drawing.Color.FromArgb(unchecked((int)0xFF333399)),
      [63] = System.Drawing.Color.FromArgb(unchecked((int)0xFF333333)),
      [64] = System.Drawing.Color.Transparent, // "System foreground", is this correct?
      [65] = System.Drawing.Color.Transparent, // "System background", is this correct?
      [72] = System.Drawing.Color.Black, // This one seems common, no idea what it is?
      [81] = System.Drawing.Color.Black, // Some random tooltip color, is this correct?
    };

    private static JsonCellXf ParseCellFormat(CellFormat fmt, Dictionary<uint, JsonCellXf>? masterFormats)
    {
      uint? fmtId = null;
      uint? numFmtId = null;
      uint? fontId = null;
      uint? fillId = null;
      uint? borderId = null;
      JsonAlignment? alignment = null;

      if (masterFormats == null)
      {
        if (fmt.FormatId != null)
        {
          throw new ArgumentOutOfRangeException(nameof(fmt), "Unexpected cell xfId for master format");
        }
      }
      else
      {
        uint? masterId = null;
        if (fmt.FormatId != null)
        {
          masterId = fmt.FormatId.Value;
        }
        else if (masterFormats.Count == 1)
        {
          masterId = 0;
        }

        if (masterId != null)
        {
          numFmtId = masterFormats[masterId.Value].NumFmtId;
          fontId = masterFormats[masterId.Value].FontId;
          fillId = masterFormats[masterId.Value].FillId;
          borderId = masterFormats[masterId.Value].BorderId;
          alignment = (JsonAlignment?)masterFormats[masterId.Value].Alignment?.Clone();
        }
      }

      if (fmt.FormatId != null)
      {
        fmtId = fmt.FormatId.Value;
      }
      if (fmt.NumberFormatId != null && (fmt.ApplyNumberFormat ?? true))
      {
        numFmtId = fmt.NumberFormatId;
      }
      if (fmt.FontId != null && (fmt.ApplyFont ?? true))
      {
        fontId = fmt.FontId;
      }
      if (fmt.FillId != null && (fmt.ApplyFill ?? true))
      {
        fillId = fmt.FillId;
      }
      if (fmt.BorderId != null && (fmt.ApplyBorder ?? true))
      {
        borderId = fmt.BorderId;
      }
      if (fmt.Alignment != null && (fmt.ApplyAlignment ?? true))
      {
        if (alignment == null) alignment = new JsonAlignment();
        if (fmt.Alignment.Horizontal != null) alignment.Horizontal = Enum.Parse<JsonHorizontalAlignment>(Enum.GetName(fmt.Alignment.Horizontal.Value)!);
        if (fmt.Alignment.Vertical != null) alignment.Vertical = Enum.Parse<JsonVerticalAlignment>(Enum.GetName(fmt.Alignment.Vertical.Value)!);
        if (fmt.Alignment.TextRotation != null) alignment.TextRotation = fmt.Alignment.TextRotation;
        if (fmt.Alignment.WrapText != null) alignment.WrapText = fmt.Alignment.WrapText;
        if (fmt.Alignment.Indent != null) alignment.Indent = fmt.Alignment.Indent;
        if (fmt.Alignment.RelativeIndent != null) alignment.RelativeIndent = fmt.Alignment.RelativeIndent;
        if (fmt.Alignment.JustifyLastLine != null) alignment.JustifyLastLine = fmt.Alignment.JustifyLastLine;
        if (fmt.Alignment.ShrinkToFit != null) alignment.ShrinkToFit = fmt.Alignment.ShrinkToFit;
        if (fmt.Alignment.ReadingOrder != null) alignment.ReadingOrder = fmt.Alignment.ReadingOrder;
      }


      return new JsonCellXf
      {
        FmtId = fmtId,
        NumFmtId = numFmtId,
        FontId = fontId,
        FillId = fillId,
        BorderId = borderId,
        Alignment = alignment
      };
    }

    private static JsonPackageProperties ParsePackageProperties(PackageProperties props)
    {
      return new JsonPackageProperties
      {
        Created = props.Created,
        Creator = props.Creator,
        Modified = props.Modified,
        Modifier = props.LastModifiedBy,
        Subject = props.Subject,
        Title = props.Title,
        Description = props.Description,
        Language = props.Language
      };
    }

    private static string ParseRichString(RstType ssi)
    {
      var sb = new StringBuilder();
      foreach (OpenXmlElement child in ssi)
      {
        if (child is Text tChild)
        {
          sb.Append(tChild.Text);
        }
        else if (child is Run rChild)
        {
          sb.Append(rChild.Text.Text);
        }
        else if (child is PhoneticRun rPhChild)
        {
          sb.Append(rPhChild.Text.Text);
        }
        else if (child is PhoneticProperties)
        {
          // ignore
        }
        else
        {
          throw new ApplicationException("Unexpected SharedStringItem child: " + child.GetType());
        }
      }
      return sb.ToString();
    }

    private static (uint row, uint col) ParseCellReference(string cf)
    {
      uint col = 0, row = 0;
      int i = 0;
      for (; i < cf.Length; ++i)
      {
        var c = cf[i];
        if (c < 'A' || c > 'Z') break;
        col = col * 26 + (uint)(c - 'A') + 1;
      }
      for (; i < cf.Length; ++i)
      {
        var c = cf[i];
        if (c < '0' || c > '9') break;
        row = row * 10 + (uint)(c - '0');
      }
      if (i != cf.Length || col < 1 || row < 1)
      {
        throw new ArgumentException($"Invalid CellReference = ${cf}");
      }
      return (row, col);
    }
    private static (uint row, uint col, uint rows, uint cols) ParseRangeReference(string rf)
    {
      var cfs = rf.Split(':', 2);
      var (row, col) = ParseCellReference(cfs[0]);
      if (cfs.Length == 1)
      {
        return (row, col, 1, 1);
      }
      var (row2, col2) = ParseCellReference(cfs[1]);
      if (row2 < row || col2 < col)
      {
        throw new ArgumentException($"Invalid RangeReference = ${rf}");
      }
      return (row, col, row2 - row + 1, col2 - col + 1);
    }
    private static string WriteCellReference(uint row, uint col)
    {
      var sb = new StringBuilder();
      while (col > 0)
      {
        var remainder = (col - 1) % 26;
        sb.Insert(0, (char)('A' + remainder));
        col = (col - remainder) / 26;
      }
      sb.Append(row);
      return sb.ToString();
    }
  }

}

