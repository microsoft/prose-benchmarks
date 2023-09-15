## FORMAT5 Benchmarks
Benchmarks for FormaT5 (FORMAT5: Abstention and examples for table formatting with natural language).

This contains the StackOverflow and Online Tutorial benchmarks.


## File Structure
The  benchmark JSON file (`benchmarks.json`), which contains a list of dictionaries
with four keys: `Query`, `Code`, `URL` and `Data`. The `Query` key containing natural lanaguage
utterance, `Code` containing the ground truth conditional formatting rule, `URL`
containing the source of the benchmark task (StackOverflow and Online Tutorials) 
and `Data` containing the corresponding table in the form of row-ordered list of lists.

```JSON
[
    {
        "Query": <natural lanaguage utterance>,
        "Code": <ground truth conditional formatting rule>,
        "URL": <source of the benchmark task>,
        "Data": <corresponding table>
    },
    {
        "Query": <natural lanaguage utterance>,
        "Code": <ground truth conditional formatting rule>,
        "URL": <source of the benchmark task>,
        "Data": <corresponding table>
    }
    ...
]
```