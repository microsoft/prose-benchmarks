# MBUPP

It is an improved version of MBPP dataset, suitable for user-like code generation evaluation. We observed multiple issues with the current test set in MBPP. Some of these issues are: 
1. Queries asking for specific method/technique to be used, which is not tested for in evaluation pipeline. Like `Write a function to sort the array using counting sort.`
    Evaluation is done based on the generated code being able to pass all test cases. 
2. Queries instruct for generation of code without providing sufficient context. Like `Write a function to calculate electricity bill.`
3. Multiple duplicate instances present in the test case, where the underlying task and test cases remain same.
4. Incorrect test cases. Like for this query `Write a function to merge two dictionaries.` the test cases have `["assert merge_dict({'a': 100, 'b': 200},{'x': 300, 'y': 200})=={'x': 300, 'y': 200, 'a': 100, 'b': 200}", "assert merge_dict({'a':900,'b':900,'d':900},{'a':900,'b':900,'d':900})=={'a':900,'b':900,'d':900,'a':900,'b':900,'d':900}", ...] `
    Here the second test case consists of an invalid dictionary, `{'a':900,'b':900,'d':900,'a':900,'b':900,'d':900}`
5. Issue of data contamination, as these dataset are also used while training models.
We propose the improved benchmark, generated while leveraging LLMs, covering all theses aspect of sufficiently specifying detailing within the query and modifying the test cases to handle for all possible correct responses.

## Structure of files within the repo:
Current repository consists of 466 sample each, extracted from test set of MBPP (500). These are:
- MBPP: current version of test samples, after cleaning for duplicates and completely under-specified queries.
- MBPP_UpdatedNL: updating the NL, for a rephrased version, which more aligns with the task and reduced possibility of data leakage,
- MBPP_UpdatedTC: updating the test cases, over which the evaluation depends to make it more flexible and incorporate all possible acceptable response formats for the given query.
- MBUPP-MBPP_UpdatedNL_TC: proposed MBUPP benchmark with both components of improved query and test cases.

## Components with each file:
- 'task_id': same as original MBPP, unique identifier to each sample
- 'text': current user query to generation the desired code (can be original or updated, depending on benchmark)
- 'test_list': list of original assertions present within the MBPP dataset
- 'updated_test_list': dictionary of type and possible response of test case in terms of its key-value pairs. This remains same as test_list, incase we do not update the test cases
- 'code': ground truth/the desired code for the task
