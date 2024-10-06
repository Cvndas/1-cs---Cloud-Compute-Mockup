## Cloud Castle

I started this little project to learn C# syntax and language features. 
Idea: A Cloud Computing dashboard where you can upload files and download files .

STATUS: Unfinished

Current obstacle:

Being my first time using C#, I made several wrong assumptions about how the language works. I've fixed a lot of those issues, but right now I'm facing the issue of blocking Read() calls being done on threads that need to be interruptable. I assumed that ThreadInterrupt exceptions would work in this specific situation, but that assumption was wrong. 

I'll research how ReadAsync works in combination with CancellationToken, and then I'll probably continue to finish this project. I am getting very close to the end, and most of the complex threading logic is done. 