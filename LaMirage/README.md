# Last Mile Repair Benchmarks

This repository contains the last mile repair benchmarks used in LaMirage ([Neurosymbolic repair for low-code formula languages](https://dl.acm.org/doi/abs/10.1145/3563327))

Please refer to [`LastMile.Repair/`](../LastMile.Repair/) folder for our extensive last mile repair 
benchmarks across multiple languages and the corresponding paper [Repair Is Nearly Generation: Multilingual Program Repair with LLMs](https://arxiv.org/abs/2208.11640) (to be presented at AAAI 2023).

## Folder Structure

- `Excel`
    - `benchmarks.json`
- `Power_Fx`:
    - `benchmarks.json`


## File Structure
For each language, there is a JSON file (`benchmarks.json`), which contains a list of dictionaries
with two keys: `Buggy` and `GroundTruth`. The `Buggy` key containing buggy code and `GroundTruth`
contains manually annotated fix for the corresponding `Buggy` code.

```JSON
[
    {
        "Buggy": <buggy code>,
        "GroundTruth": <manually annotated ground truth>
    },
    {
        "Buggy": <buggy code>,
        "GroundTruth": <manually annotated ground truth>
    
    }
    ...
]
```