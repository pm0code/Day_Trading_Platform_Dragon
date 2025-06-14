"Adopt a **System-Wide Holistic Review Protocol** for all source code analysis. Your primary directive is to move beyond isolated, file-specific issues and understand the broader context and interdependencies within the entire project.

**Core Principles:**

1.  **Avoid Tunnel Vision:** Do not limit your analysis to the single file where an issue might initially appear. Actively resist focusing only on local fixes.
2.  **Comprehensive Impact Assessment:** For every potential bug, vulnerability, performance bottleneck, or code smell you identify, your subsequent critical step is to look holistically at the complete project.

**When an issue is identified in a specific file or module, I need you to:**

**A. Analyze the Local Issue:**
    * Clearly describe the problem within its immediate context (the specific file, function, or line(s) of code).

**B. Expand Analysis Project-Wide (Holistic Investigation):**
    * **Trace Dependencies & Interactions:**
        * **Data Flow:** Where does the data involved in this issue originate (other files, modules, services, user input)? How is it transformed? Where is it consumed or persisted later in the system? Could issues in upstream data handling be the root cause?
        * **Control Flow:** Which other parts of the system call into this problematic code? Which other modules or services does this code call out to? Could the sequence of operations or interactions be flawed?
        * **Shared State/Resources:** Does this issue involve or impact shared state, configurations, database schemas, caches, or other resources used by multiple parts of the project?
    * **Identify Potential Ripple Effects:**
        * How might this local issue affect other modules, services, or functionalities, even if they seem unrelated at first glance?
        * Could this issue lead to inconsistent states, incorrect behavior, or failures in other parts of the application?
    * **Investigate Potential Systemic Root Causes:**
        * Is this issue a symptom of a larger architectural flaw, a misunderstanding of requirements, a missing cross-cutting concern (like error handling strategy, transaction management), or an incorrect assumption about how different components interact?
        * Are there similar patterns of this issue likely present in other parts of the codebase due to a common underlying cause or copied logic?
    * **Consider Architectural Integrity:**
        * Does this issue (or its potential fix) violate established architectural patterns, design principles (e.g., SOLID, DRY), or separation of concerns within the project?

**C. Formulate Holistic Recommendations:**
    * Beyond local fixes, suggest broader changes, refactoring in other files, or architectural adjustments if your holistic analysis indicates they are necessary to truly resolve the root cause or prevent recurrence.
    * If you identify a systemic pattern, recommend a strategy to address it across the project.
    * If you lack context about certain inter-module interactions critical to this holistic review, clearly state what information you need to complete your assessment.

**Example Interaction:**
*If you find, for instance, an insecure data handling practice in `module_A.py`:*
* *Don't just say:* "Fix data handling in `module_A.py`."
* *Instead, think and report:* "Issue found in `module_A.py`: [describe local issue].
    * Holistically, this data originates from `module_B.py`'s input processing, which also seems to lack similar validation.
    * Furthermore, `module_C.py` consumes data processed by `module_A.py` and might be vulnerable to downstream effects.
    * This suggests a project-wide need for a standardized secure input validation and data sanitization strategy to be applied in `module_A`, `module_B`, and potentially `module_C`. Could you confirm if there's a central utility for this or if one should be created?"

Your goal is to function as if you have a complete mental model of the project's architecture and data flows, or to actively seek the information to build one. This systemic approach is vital."


Start with the error list that I already provided. Go through the list and analyze holistically. identify dependencies and like a smart SW and QA engineer, root cause the issues and fix the bugs:

"Okay, let's move to the next file. However, it's critical that your knowledge is current.
Before you load or begin analyzing the *next* file:
1.  **Fetch & Re-analyze Current File:** Please first retrieve the most recent version of the *next* file from its project location on Google Drive.
2.  **Update Internal State:** Process this latest version to update your internal understanding and to identify any changes or additions I might have implemented since our last interaction with it.
3.  **Prevent Repetition:** This step is crucial to ensure that any subsequent work or suggestions from you do not duplicate efforts or re-introduce points we've already addressed and updated in the current file.
4.  **Transition:** After you've confirmed that you've synchronized with the latest version of the current file from Google Drive, then initiate work on the *next* file."