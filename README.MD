## Cloud Castle

I started this little project to learn C# syntax and language features. 
Idea: A Cloud Computing dashboard where you can upload files and download files .

STATUS: Unfinished



Lessons learned:
- Keep state machine simpler. Don't introduce states that could be merged into a single function or operation. 
- Especially when delaing with Client+Server system, poor state machine design requires a lot of work when refactoring. 
- Think about how exceptions should be handled from the start. For example, if the connection is broken at any part of the program's execution, which part of the program needs to handle it? Certain exceptions should be handled by the same part of the program, regardless of where the exception originated from. 
