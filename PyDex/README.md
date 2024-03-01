# PyDex Benchmarks

This repository contains the last mile repair benchmarks used in
PyDex: Repairing Bugs in Introductory Python Assignments
using LLMs.

If you use this data, please include the following
citation for the original data source for the PyMacer dataset.

```bibtex
@inproceedings{h2023advances,
  title={Advances in Automated Pedagogical Compile-time Error Repair},
  author={H. Padmanabha, Sharath and Shaikh, Fahad and Bansal, Mayank and Chatterjee, Debanjan and Singh, Preeti and Karkare, Amey and Kar, Purushottam},
  booktitle={Proceedings of the 16th Innovations in Software Engineering Conference},
  pages={1--11},
  year={2023}
}
```

as well as the PyDex citation.

## File Structure

Each file is associated with an assignment id, which
you can find it in the name `<id>_syntaxincorrect_correct_pair.json`.
The file is a JSON file with the following structure

```json
{
    "submission": [
        {
            "semantics_correct": <correct submission>,
            "syntax_incorrect": <last syntactically invalid submission>
        }
    ],
    "IO_example": [
        {
            "IO_number": <id>,
            "input": <input string>,
            "output": <expected output string>
        }
    ],
    "reference_solution": <correct instructor-provided program>,
    "statement": <natural languge description of task>,
    "problem_ID": <assignment id>,
}
```
