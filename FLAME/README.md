# FLAME: A small language model for spreadsheet formulas

This folder contains the benchmarks used for the FLAME paper (https://arxiv.org/abs/2301.13779).

We carry out three tasks:

* Formula repair: we use the 200 Excel formulas released in [RING](https://github.com/microsoft/prose-benchmarks/blob/main/LastMile.Repair/Excel/benchmarks.json)
* Formula completion: we use the ground-truth entries of the formula repair task to create
completion tasks by masking various prefix lengths. See [./completion-benchmarks.json](./completion-benchmarks.json). Each entry corresponds to a task. The `Masked` key contains
the prefix (up to the special token `<mask>`) and `Completion` contains the full expected
formulas.
* Similar formula retrieval: we sampled 1000 random formulas from formulas available
in the [ENRON spreadsheet corpus ](https://github.com/SheetJS/enron_xls).

For the extended version of the FLAME paper, with a listing of all user-inspired
noise operators and a technical appendix, please the Arxiv version
at [https://arxiv.org/abs/2301.13779](https://arxiv.org/abs/2301.13779).