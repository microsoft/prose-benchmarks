# Conditional Formatting Rule Generation Benchmarks

This repository contains the conditional formatting benchmarks used in CORNET ([CORNET: Learning Table Formatting Rules By Example](https://arxiv.org/abs/2208.06032)) and FormaT5 (FORMAT5: Abstention and examples for table formatting with natural language)


## Folder Structure

- `urls`
    - Links to the Excel Workbooks
- `scripts`:
    - Scripts for processing and extracting benchmarks
- `Benchmarks`
    - `CORNET`: CF benchmarks for CORNET (need to be generated using instructions below)
    - `NLToCF`: Natural language to CF benchmarks from Online blogs and StackOverflow (Will be uploaded soon)

## Urls
The URL folder has a json file with links to all the spreadsheets used for generating the benchmarks.

## Steps to generate
1. Download all files from the urls in `urls/file_url_mapping.json`. `scripts/url_downloader.py` might help you with this step. Run the following code in the `scripts` directory.
    ```
       python url_downloader.py             \\
        --input {url_file_path}             \\
        --output {dir_to_download_files_to} \\
    ```
2. Run the Conditional Formatting extraction tool on the downloaded files.
    - This will require .NET 6.0 Runtime
    - This can be done by running the following in `scripts/cf-rule-extraction` directory.
        ```
           dotnet run                           \\
            --input {downloaded_xlsx_files_dir} \\
            --output {benchmarks_dir}           \\
            --gzip false                        \\
        ```
    - This will dump all the benchmarks in `{benchmarks_dir}`.
3. Each benchmark file contains the JSON of all CF Rules corresponding to each sheet in the Excel workbook.

## File Structure
For each Conditional Formatting task extracted from the workbooks, there is a json file, with the following structure

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