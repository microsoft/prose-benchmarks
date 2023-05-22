## CORNET Benchmarks
Benchmarks for CORNET ([CORNET: Learning Table Formatting Rules By Example](https://arxiv.org/abs/2208.06032))

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