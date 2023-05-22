using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace ExcelLoader
{
  public class JsonWorkbook
  {
    public List<JsonWorksheet> Sheets;
    public JsonWorkbookProperties Properties;
    public JsonPackageProperties PackageProperties;
    public string Origin;

    public JsonWorkbook(List<JsonWorksheet> sheets, JsonWorkbookProperties properties, JsonPackageProperties packageProperties, string origin)
    {
      Sheets = sheets;
      Properties = properties;
      PackageProperties = packageProperties;
      Origin = origin;
    }
  }

  public class JsonWorksheet
  {
    public JsonWorksheetType? Type;
    public string Name;
    public List<JsonCell>? Cells;
    public List<JsonRow>? Rows;
    public List<JsonCol>? Cols;
    public List<JsonTable>? Tables;
    public List<JsonMergeCell>? MergeCells;
    public JsonWorksheetProperties? Properties;
    public List<JsonConditionalFormatting>? ConditionalFormatting;

    public JsonWorksheet(string name, JsonWorksheetType? type, List<JsonCell>? cells, List<JsonRow>? rows, List<JsonCol>? cols, List<JsonTable>? tables, List<JsonMergeCell>? mergeCells, JsonWorksheetProperties? properties, List<JsonConditionalFormatting>? conditionalFormatting)
    {
      Name = name;
      Type = type;
      Cells = cells;
      Rows = rows;
      Cols = cols;
      Tables = tables;
      MergeCells = mergeCells;
      Properties = properties;
      ConditionalFormatting = conditionalFormatting;
    }
  }

  [JsonConverter(typeof(StringEnumConverter))]
  public enum JsonWorksheetType
  {
    chart, dialog
  }

  public class JsonRow
  {
    public uint Number;
    public bool Hidden;
    public double? Height;

    public JsonRow(uint number, bool hidden, double? height)
    {
      Number = number;
      Hidden = hidden;
      Height = height;
    }
  }

  public class JsonCol
  {
    public bool Hidden;
    public double? Width;
    public uint Min;
    public uint Max;

    public JsonCol(bool hidden, double? width, uint min, uint max)
    {
      Hidden = hidden;
      Width = width;
      Min = min;
      Max = max;
    }
  }

  public class JsonTable
  {
    public string Name;
    public string Range; // The range of the table - including the header and totals rows!
    public bool HasHeadersRow;
    public bool HasTotalsRow;
    public List<string> ColumnNames;

    public JsonTable(string name, string range, bool hasHeadersRow, bool hasTotalsRow, List<string> columnNames)
    {
      Name = name;
      Range = range;
      HasHeadersRow = hasHeadersRow;
      HasTotalsRow = hasTotalsRow;
      ColumnNames = columnNames;
    }
  }

  public class JsonMergeCell
  {
    public string Ref;

    public JsonMergeCell(string @ref)
    {
      Ref = @ref;
    }
  }

  /// <summary>
  /// Class for the JSON representation of a single Conditional Formatting Rule in a spreadsheet
  /// </summary>
  public class JsonConditionalFormatting
  {
    public string SeqRef;
    public string Type;
    public bool AboveAvg;
    public bool Bottom;
    public bool EqualAvg;
    public bool Percent;
    public string TimePeriod;
    public string Operator;
    public List<string> Arguments;
    public string Text;
    public uint StyleId;
    public int Priority;
    public uint Rank;

    /// <summary>
    /// Initializes the JsonConditionalFormatting class for a single CF Rule
    /// </summary>
    /// <param name="sqref"> Range of CF Rule </param>
    /// <param name="type"> Type of CF Rule (Cell Value, TextContains, ColorScale, etc.) </param>
    /// <param name="aboveAvg"> Special flag for Cell Value above/below/Top-k rule </param>
    /// <param name="bottom"> Special flag for Cell Value above/below/Top-k rule </param>
    /// <param name="equalAvg"> Special flag for Cell Value above/below/Top-k rule </param>
    /// <param name="percent"> Special flag for Cell Value above/below/Top-k rule </param>
    /// <param name="timePeriod"> Time Period argument for Date-Time CF Rules </param>
    /// <param name="op"> Operation for Cell Value CF Rules (GreaterThan, Between, Equals, etc.) </param>
    /// <param name="arguments"> Arguments to the CF Rule (Formula in case of custom rule) </param>
    /// <param name="text"> Text argument for Text CF Rules</param>
    /// <param name="dfxId"> Target Style ID for CF Rule </param>
    /// <param name="rank"> Rank of CF Rule </param>
    /// <param name="priority"> Priority of CF Rule </param>
    public JsonConditionalFormatting(string sqref, string type, bool aboveAvg, bool bottom, bool equalAvg, bool percent, string timePeriod, string op, List<string> arguments, string text, uint dfxId, uint rank, int priority)
    {
      SeqRef = sqref;
      Type = type;
      AboveAvg = aboveAvg;
      Bottom = bottom;
      EqualAvg = equalAvg;
      Percent = percent;
      TimePeriod = timePeriod;
      Operator = op;
      Arguments = arguments;
      Text = text;
      StyleId = dfxId;
      Rank = rank;
      Priority = priority;
    }
  }

  public class JsonCell
  {
    public string reference; // cell location like B12
    public JsonCellValueType t; // Value type: non-null
    public object? v; // Value: string/number/bool/undefined based on "t" field

    public string? f; // Formula text: null iff cell is a value cell
    public JsonCellFormulaType? f_t; // Formula type, undefined for "normal"
    public uint? f_si; // Shared index for shared formulas
    public string? f_ref; // Referenced area for array + shared formulas

    public uint? s; // Style index: optional

    public JsonCell(string reference, JsonCellValueType t, object? v, string? f, JsonCellFormulaType? f_t, uint? f_si, string? f_ref, uint? s)
    {
      this.reference = reference;
      this.t = t;
      this.v = v;
      this.f = f;
      this.f_t = f_t;
      this.f_si = f_si;
      this.f_ref = f_ref;
      this.s = s;
    }
  }

  [JsonConverter(typeof(StringEnumConverter))]
  public enum JsonCellValueType
  {
    z, // Value is a Blank
    b, // Value is a boolean
    n, // Value is a string which encodes a number
    d, // Value is a string in ISO 8601 format
    e, // Value is a string which is an error name
    s // Value is a string
  }

  [JsonConverter(typeof(StringEnumConverter))]
  public enum JsonCellFormulaType
  {
    array, shared, datatable
  }

  public class JsonWorkbookProperties
  {
    [JsonProperty("r1c1RefMode")] public bool? R1C1RefMode;
    public bool? FullPrecision;
    public bool? Date1904;
    public bool? DateCompatibility;
    // List of defined names
    public List<JsonDefinedName> Names;
    // List of number formats
    public List<JsonNumFmt> NumFmts;
    // List of fonts
    public List<JsonFont> Fonts;
    // List of cell fills
    public List<JsonFill> Fills;
    // List of cell borders
    public List<JsonBorder> Borders;
    // List of cell formats
    public List<JsonCellXf> CellXfs;
    // List of Differential Cell Formats
    public List<JsonDifferentialFormats> DifferentialFormats;

    public JsonWorkbookProperties(List<JsonDefinedName> names, List<JsonNumFmt> numFmts, List<JsonFont> fonts, List<JsonFill> fills, List<JsonBorder> borders, List<JsonCellXf> cellXfs, List<JsonDifferentialFormats> diffFormats)
    {
      Names = names;
      NumFmts = numFmts;
      Fonts = fonts;
      Fills = fills;
      Borders = borders;
      CellXfs = cellXfs;
      DifferentialFormats = diffFormats;
    }
  }

  public class JsonDifferentialFormats
  {
    public int DfxId;
    public JsonFill? Fill;
    public JsonFont? Font;
    public JsonBorder? Border;

    public JsonDifferentialFormats(int id, JsonFill? fill, JsonFont? font, JsonBorder? border)
    {
      DfxId = id;
      Fill = fill;
      Font = font;
      Border = border;
    }
  }

  public class JsonWorksheetProperties
  {
    public bool? HasFilters;
    public bool? TransitionEntry;
    public bool? TransitionEvaluation;
    public double? DefaultRowHeight; // The row height for rows without specified Height
    public double? DefaultColWidth; // The col width for cols without specified Width
  }

  public class JsonDefinedName
  {
    public string Name;
    public string Ref;
    public uint? Sheet;

    public JsonDefinedName(string name, string @ref, uint? sheet)
    {
      Name = name;
      Ref = @ref;
      Sheet = sheet;
    }
  }

  public class JsonNumFmt
  {
    public string FormatCode;
    public uint NumberFormatId;

    public JsonNumFmt(uint numberFormatId, string formatCode)
    {
      NumberFormatId = numberFormatId;
      FormatCode = formatCode;
    }
  }

  public class JsonFont
  {
    public string? Name;
    public bool? Bold;
    public bool? Italic;
    public JsonFontUnderline? Underline;
    public bool? Strikethrough;
    public JsonFontVerticalAlignment? VerticalAlign;
    public double? Size;
    public JsonColor? Color;
  }

  [JsonConverter(typeof(StringEnumConverter))]
  public enum JsonFontUnderline
  {
    None,
    Single,
    Double,
    SingleAccounting,
    DoubleAccounting
  }

  [JsonConverter(typeof(StringEnumConverter))]
  public enum JsonFontVerticalAlignment
  {
    Baseline,
    Superscript,
    Subscript
  }

  public class JsonColor
  {
    [JsonProperty("argb")] public string ARGB;

    public JsonColor(string argb)
    {
      ARGB = argb;
    }
  }

  public class JsonBorder
  {
    public JsonBorderProperties? Top;
    public JsonBorderProperties? Bottom;
    public JsonBorderProperties? Left;
    public JsonBorderProperties? Right;
  }

  public class JsonBorderProperties
  {
    public JsonBorderLineType? Type;
    public JsonColor? Color;
  }

  [JsonConverter(typeof(StringEnumConverter))]
  public enum JsonBorderLineType
  {
    None,
    Thin,
    Medium,
    Dashed,
    Dotted,
    Thick,
    Double,
    Hair,
    MediumDashed,
    DashDot,
    MediumDashDot,
    DashDotDot,
    MediumDashDotDot,
    SlantDashDot
  }

  public class JsonFill
  {
    public JsonPatternFill? Pattern;
    public JsonGradientFill? Gradient;
  }

  public class JsonPatternFill
  {
    public JsonPatternType? Type;
    public JsonColor? FgColor;
    public JsonColor? BgColor;
  }

  public class JsonGradientFill
  {
    // Ignoring all properties for now, we could export them later as-needed if we require them
  }

  [JsonConverter(typeof(StringEnumConverter))]
  public enum JsonPatternType
  {
    None,
    Solid,
    MediumGray,
    DarkGray,
    LightGray,
    DarkHorizontal,
    DarkVertical,
    DarkDown,
    DarkUp,
    DarkGrid,
    DarkTrellis,
    LightHorizontal,
    LightVertical,
    LightDown,
    LightUp,
    LightGrid,
    LightTrellis,
    Gray125,
    Gray0625
  }

  public class JsonCellXf
  {
    public uint? FmtId;
    public uint? NumFmtId;
    public uint? FontId;
    public uint? FillId;
    public uint? BorderId;
    public JsonAlignment? Alignment;

    public JsonCellXf() { }
  }

  public sealed class JsonAlignment : ICloneable
  {
    public JsonHorizontalAlignment? Horizontal;
    public JsonVerticalAlignment? Vertical;
    public uint? TextRotation;
    public bool? WrapText;
    public uint? Indent;
    public int? RelativeIndent;
    public bool? JustifyLastLine;
    public bool? ShrinkToFit;
    public uint? ReadingOrder;

    public object Clone()
    {
      return MemberwiseClone();
    }
  }

  [JsonConverter(typeof(StringEnumConverter))]
  public enum JsonHorizontalAlignment
  {
    General,
    Left,
    Center,
    Right,
    Fill,
    Justify,
    CenterContinuous,
    Distributed
  }

  [JsonConverter(typeof(StringEnumConverter))]
  public enum JsonVerticalAlignment
  {
    Top,
    Center,
    Bottom,
    Justify,
    Distributed
  }

  public class JsonPackageProperties
  {
    public DateTime? Created;
    public string? Creator;
    public DateTime? Modified;
    public string? Modifier;

    public string? Subject;
    public string? Title;
    public string? Description;
    public string? Language;
  }

  public static class JsonConvert
  {
    private static readonly JsonSerializer serializer =
      JsonSerializer.Create(new JsonSerializerSettings
      {
        Formatting = Formatting.None,
        NullValueHandling = NullValueHandling.Ignore,
        ContractResolver = new DefaultContractResolver()
        {
          NamingStrategy = new CamelCaseNamingStrategy()
          {
            ProcessDictionaryKeys = false,
            OverrideSpecifiedNames = false
          }
        }
      });

    public static void SerializeObject(Stream output, object? o, bool gzip = true, bool leaveOpen = false)
    {
      if (gzip)
      {
        using var gzStream = new GZipStream(output, CompressionMode.Compress, leaveOpen);
        SerializeObject_(gzStream, o, leaveOpen);
      }
      else
      {
        SerializeObject_(output, o, leaveOpen);
      }
    }

    private static void SerializeObject_(Stream output, object? o, bool leaveOpen = false)
    {
      using var writer = new StreamWriter(output, leaveOpen: leaveOpen); // UTF8, no BOM
      serializer.Serialize(writer, o);
    }

    public static T DeserializeObject<T>(Stream input)
    {
      using var gzStream = new GZipStream(input, CompressionMode.Decompress);
      using var reader = new StreamReader(gzStream, Encoding.UTF8);
      using var reader2 = new JsonTextReader(reader);
      return serializer.Deserialize<T>(reader2)!;
    }
  }
}
