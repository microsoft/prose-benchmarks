# Conditional Formatting Rule Generation Benchmarks

This repository contains the conditional formatting benchmarks used in CORNET ([CORNET: Learning Table Formatting Rules By Example](https://arxiv.org/abs/2208.06032)) and FormaT5 (FORMAT5: Abstention and examples for table formatting with natural language)

## Benchmark Status
**All Benchmarks Coming Soon!**

## Folder Structure

- `urls`
    - Links to the Excel Workbooks
- `scripts`:
    - Scripts for processing and extracting benchmarks
- `Benchmarks`
    - `CORNET`: CF benchmarks for CORNET (need to be generated using instructions below)
    - `NLToCF`: Natural language to CF benchmarks from Online blogs and StackOverflow

## Urls
The URL folder has a json file with links to all the spreadsheets used for generating the benchmarks.

## Steps to generate
1. Download all files from the urls. `scripts/download_urls.py` will help you in this.
2. Run the Conditional Formatting extraction tool on the downloaded files.
    - This will require .NET 6.0 Runtime
    - This can be done by running `dotnet run {downloaded_xlsx_files_dir} {benchmarks_dir}`
    - This will dump all the benchmarks in `{benchmarks_dir}`.
3. Each benchmark file contains the JSON of all CF Rules corresponding to each Excel workbook.

## File Structure
For each Conditional Formatting task extracted from the workbooks, there is a json file, with the following structur

```JSON
[
    {
        "Data": <List of cell values in the column>,
        "Category": <Category of rule (Template-based or Custom)>,
        "Type": <Type of rule (Text, Numeric, DateTime)>
        "Rule": Conditional Formatting Rule
    },
    {
        ...
    }
]
```