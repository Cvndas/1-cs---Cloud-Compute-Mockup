## Cloud Castle

I started this little project to learn C# syntax and language features. 
Idea: A Cloud Computing dashboard where you can upload files and download files, and which also has a chat.

STATUS: Chat works, quitting back to the dashboard only works sometimes.
The isn't very mysterious, and with a little bit of debugging it should go smoothly. However, the code has turned into such a big mess due that I think I'll leave it at this for now. I'm going to implement Cloud Computing/CloudStorage in a different project. 

Lessons learned:
- Keep state machine simple. Don't introduce states that could be merged into a single functions or operations. 
- Especially when dealing with Client+Server system, poor state machine design requires a lot of work when refactoring. 
- Think about how exceptions should be handled from the start. For example, if the connection is broken at any part of the program's execution, which part of the program needs to handle it? Certain exceptions should be handled by the same part of the program, regardless of where the exception originated from. 

fun project 
