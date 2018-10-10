# PROSE Public Benchmark Suite

This repository contains the [Microsoft PROSE](https://microsoft.github.io/prose/) public benchmark suite.

This suite contains benchmarks drawn from three classes of tasks:
- `Transformation.Text`: string-to-string transformation
- `Split.Text`: text-to-table transformation
- `Extraction.Text`: substring extraction from semi-structured text

See below for more detailed descriptions of each class.

## Where did this data come from? 

A subset of the benchmarks were derived from publicly redistributable data sources. The
source and license for each such benchmark are detailed in LICENSE.

The remainder of the benchmarks were synthetically generated by the PROSE team
to conform to patterns that might be observed in real-world systems and
databases. For example, numbers were randomly generated to simulate social
security numbers. No personal data was used to generate this data. There is a
small chance that synthetically generated data could inadvertently match one or
more attributes of a real person. If you have any concerns that synthetically
generated data matches attributes of a real person, please contact us at
prose-contact@microsoft.com with details. We are offering the data under a
permissive license, but to help address any concerns of this nature at the
source, we encourage you not to redistribute the data. 

## File Structure

- `Transformation.Text`
    - `<benchmark dir>`
        - `meta.json`
        - `spec.json`
    - `<benchmark dir>`
        - `meta.json`
        - `spec.json`
    - ...
- `Split.Text`
    - `<benchmark dir>`
        - `meta.json`
        - `input.txt`
        - `output.json`
    - `<benchmark dir>`
        - `meta.json`
        - `input.txt`
        - `output.json`
    - ...
- `Extraction.Text`
    - `<benchmark dir>`
        - `meta.json`
        - `input.txt`
        - `output.json`
        - `<ancestor>.<descendant>.spec.json`
    - `<benchmark dir>`
        - `meta.json`
        - `input.txt`
        - `output.json`
        - `<ancestor>.<descendant>.spec.json`
    - ...

All files are encoded in UTF-8 without BOM.

## Metadata

Each benchmark directory contains a distinguished `meta.json` file that
annotates that benchmark with descriptive metadata.

The `Features` field denotes a list of classifications describing the
transformations required to solve the task. At this time, only benchmarks in
`Transformation.Text` have these annotations.

- `Casing`: converting character cases between capitals and non-capitals
- `Concatenation`: concatenating strings
- `Conditional`: conditioning transformations on predicates
- `DateTimeRange`: computing date/time ranges
- `DateTimeRounding`: rounding dates/times
- `DateTime`: manipulating dates/times
- `Multicolumn`: transforming inputs consisting of multiple strings
- `Numeric`: manipulating numbers
- `NumericRange`: computing numeric ranges
- `NumericRounding`: rounding numbers
- `Substring`: extracting substrings

In the case that the benchmark was generated synthetically, `meta.json` will
contain field `Synthetic` with value `true` otherwise `false`.

In addition to the `meta.json` file, each benchmark has the following structure:

### Transformation.Text

A Transformation.Text benchmark consists of a single JSON file `spec.json`
containing input-output pairs. Example:

```json
{
  "Examples": [
    {
      "Input": [
        "/libero/enim7.png"
      ],
      "Output": "enim7"
    },
    {
      "Input": [
        "/"
      ],
      "Output": "root"
    },
    {
      "Input": [
        "/libero/enim9.png"
      ],
      "Output": "enim9"
    }
  ]
}
```

### Split.Text

A Split.Text benchmark consists of two files:

1. A text file `input.txt` containing the raw string from which to extract a
   table. Example:

   ```txt
   (58.326261139, 89.99508561)
   (65.889370802, 72.93175018)
   
   ```

2. A JSON file `output.json` describing the tabular structure of (1). Note that
   nonconforming rows are represented as lists populated with `null` in each
   element, so that every constituent list in `Rows` contains the same number
   of elements. Example:

   ```json
   {
     "Rows": [
       [
         "(",
         "58.326261139",
         ", ",
         "89.99508561",
         ")"
       ],
       [
         "(",
         "65.889370802",
         ", ",
         "72.93175018",
         ")"
       ],
       [
         null,
         null,
         null,
         null,
         null
       ]
     ]
   }
   ```

### Extraction.Text

An Extraction.Text benchmark consists of three or more files:

1. A text file `input.txt` containing the raw string from which extraction is
   to be performed. Example:

   ```txt
   Header0
       Subheading0a
   	Subheading0aa
   	Subheading0ba
       Subheading0b
   	Subheading0ba
   	Subheading0ca
   Header1
       Subheading1a
   	Subheading1aa
   	Subheading1ab
   Header2
       Subheading2a
   	Subheading2aa
   	Subheading2ab
       Subheading2b
   	Subheading2ba
   	Subheading2bb
   Header3
       Subheading3a
       Subheading3b
   	Subheading3ba
       Subheading3c
   	Subheading3ca
   	Subheading3cb
   ```

2. A JSON file `output.json` specifying the tree structure of (1).
   Example:

   ```json
   {
     "Property": "root",
     "Start": 0,
     "End": 363,
     "Children": [
       {
         "Property": "HeaderStruct",
         "Start": 0,
         "End": 102,
         "Children": [
           {
             "Property": "Header",
             "Start": 0,
             "End": 7,
             "Value": "Header0"
           },
           {
             "Property": "SubHeaderStruct",
             "Start": 12,
             "End": 59,
             "Children": [
               {
                 "Property": "SubHeader",
                 "Start": 12,
                 "End": 24,
                 "Value": "Subheading0a"
               },
               {
                 "Property": "SubSubHeaderStruct",
                 "Start": 26,
                 "End": 41,
                 "Children": [
                   {
                     "Property": "SubSubHeader",
                     "Start": 26,
                     "End": 39,
                     "Value": "Subheading0aa"
                   }
                 ]
               },
               {
                 "Property": "SubSubHeaderStruct",
                 "Start": 41,
                 "End": 59,
                 "Children": [
                   {
                     "Property": "SubSubHeader",
                     "Start": 41,
                     "End": 54,
                     "Value": "Subheading0ba"
                   }
                 ]
               }
             ]
           },
   ...
   ```

   Each node has a `Property` field containing a descriptive label. Each node
   also has `Start` and `End` indices denoting the node's corresponding
   character extent from `input.txt`. Each leaf node additionally has a `Value`
   field with corresponding substring, while each non-leaf node instead has a
   `Children` field containing a list of its subnodes. The distinguished
   `Property` value `root` is reserved for the root node that covers the entire
   input string.

3. One or more JSON files with the naming scheme
   `<ancestor>.<descendant>.spec.json`. Each contains examples for extracting
   strings with `Property` `<descendant>` from strings with `Property`
   `<ancestor>`.
   
   Each `.spec.json` file denotes one of two possible kinds of extractions. If
   `Kind` is `Sequence`, then the extraction is of type `string -> list of
   string`, and the task is to extract a list of substrings from the input
   string. An instance of
   `Microsoft.ProgramSynthesis.Extraction.Text.SequenceProgram` in the PROSE
   SDK can be one solution to such a task. Example:

   ```json
   {
     "Kind": "Sequence",
     "Examples": [
       {
         "Input": [
           {
             "Start": 0,
             "End": 363,
             "Value": "Header0\n    Subheading0a\n\tSubheading0aa\n\tSubheading0ba\n    Subheading0b\n\tSubheading0ba\n\tSubheading0ca\nHeader1\n    Subheading1a\n\tSubheading1aa\n\tSubheading1ab\nHeader2\n    Subheading2a\n\tSubheading2aa\n\tSubheading2ab\n    Subheading2b\n\tSubheading2ba\n\tSubheading2bb\nHeader3\n    Subheading3a\n    Subheading3b\n\tSubheading3ba\n    Subheading3c\n\tSubheading3ca\n\tSubheading3cb\n"
           }
         ],
         "Output": [
           [
             {
               "Start": 0,
               "End": 7,
               "Value": "Header0"
             },
             {
               "Start": 102,
               "End": 109,
               "Value": "Header1"
             },
             {
               "Start": 157,
               "End": 164,
               "Value": "Header2"
             },
             {
               "Start": 259,
               "End": 266,
               "Value": "Header3"
             }
           ]
         ]
       }
     ]
   }
   ```

   In this case, each input string in `Input` has a corresponding list of
   output strings in `Output` to be extracted from it.

   If `Kind` is `Field`, then the extraction is of type `string -> string`, and
   the task is to extract a substring from the input string. An instance of
   `Microsoft.ProgramSynthesis.Extraction.Text.RegionProgram` in the PROSE SDK
   can be one solution to such a task. Example:

   ```json
   {
     "Kind": "Field",
     "Examples": [
       {
         "Input": [
           {
             "Start": 0,
             "End": 102,
             "Value": "Header0\n    Subheading0a\n\tSubheading0aa\n\tSubheading0ba\n    Subheading0b\n\tSubheading0ba\n\tSubheading0ca\n"
           },
           {
             "Start": 102,
             "End": 157,
             "Value": "Header1\n    Subheading1a\n\tSubheading1aa\n\tSubheading1ab\n"
           },
           {
             "Start": 157,
             "End": 259,
             "Value": "Header2\n    Subheading2a\n\tSubheading2aa\n\tSubheading2ab\n    Subheading2b\n\tSubheading2ba\n\tSubheading2bb\n"
           },
           {
             "Start": 259,
             "End": 363,
             "Value": "Header3\n    Subheading3a\n    Subheading3b\n\tSubheading3ba\n    Subheading3c\n\tSubheading3ca\n\tSubheading3cb\n"
           }
         ],
         "Output": [
           {
             "Start": 0,
             "End": 7,
             "Value": "Header0"
           },
           {
             "Start": 102,
             "End": 109,
             "Value": "Header1"
           },
           {
             "Start": 157,
             "End": 164,
             "Value": "Header2"
           },
           {
             "Start": 259,
             "End": 266,
             "Value": "Header3"
           }
         ]
       }
     ]
   }
   ```

   In this case, `Input` is a list of strings, and its corresponding `Output`
   is a list of substrings, one for each respective input string.

---
This project has adopted the [Microsoft Open Source Code of
Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct
FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com)
with any additional questions or comments.
