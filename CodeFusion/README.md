# CodeFusion Data and Benchmarks

This repository contains the conditional formatting benchmarks used in CodeFusion (CodeFusion: A Pre-trained Diffusion Model for Code Generation)

## Folder Structure

- `Benchmarks`
    - Training and Testing data for Python, M and CF Rules
- `Pretraining Data`
    - Unannotated code used for pretraining for Python, M and CF Rules

## File Structure
For each language, there is a json file, in the Benchmarks folder with the following structure

```JSON
[
    {
        "ID": <Benchmark ID>,
        "NL Utterance": <Natural Language instruction for generating the code>,
        "Code": <The target code corresponding to the utterance in the language>
    },
    {
        ...
    }
]
```

Note: Conditional Formatting benchmarks additionally also have `Data` and `Labels` field for execution match.