## Cloud Castle

I started this little project to learn C# syntax and language features. 
Idea: A Cloud Computing dashboard where you can upload files and download files .

Status: Broken

It would take a lot of refactoring to make this system work reliably, as right now all the send and receive functionality is all over the place and now exactly unified. It was good practice though, and I think it's alright enough for a first attempt at using the language. My next projects will make sure that protocol specific things are well defined. For example in this project, it is guaranteed that every message starts with a Flag byte, and that it ends with a "\n" (which I realized too late). Such things can be taken into account earlier on.

