# Introduction 
**DevComrade** is a Windows copy/paste and launcher productivity tool for developers. 

Pasting code from StackOverflow or numerous blogs can be a tedious and sometimes even a dangerous task :) Did that ever happened to you, when you just pasted some text into a Terminal command line and it instantly got executed as a command, only because it had CR/LF at the end in the clipboard? Or have you ever been annoyed with some broken formatting, indentation, inconsistent tabs/spaces when you just pasted some code into your blog editor? With a typical solution to that being to to fire up Windows Notepad and use it as a buffer for pasting?

One other thing is Windows Shell keyboard shortcuts. It's a common struggle to find a convenient hotkey  combination that can be assigned (e.g., it's impossible to use <kbd>Win</kbd>+<kbd>Shift | Alt | Ctrl</kbd>+<kbd>Key</kbd> combos), and when it can, it may take up to 5 seconds for the program to actually launch when the hotkey is pressed. 

**DevComrade** is aimed to solve these problems. It allows assigning a customizable action to (almost) any hotkey combination, and comes with an extensive set of predefined actions for pasting text and launching apps. Additional actions can be added as [C# scripts](https://github.com/dotnet/roslyn/wiki/Scripting-API-Samples).

When it comes to pasting text, **DevComrade** is different from some similar applications (e.g., from still excellent [Puretext](https://stevemiller.net/puretext/)) in that it uses [Win32 simulated input API](https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput) to elaboratively feed the text to the currently active window, character-by-character.

**DevComrade** is an open-source software licensed under [Apache License 2.0](https://www.apache.org/licenses/LICENSE-2.0). It's build with [.NET Core 3.1 SDK](https://dotnet.microsoft.com/download/dotnet-core/thank-you/sdk-3.1.302-windows-x64-installer) and has a very minimal menu UI using Windows Forms and native Win32 APIs. It is very much a work in progress. Some CI logic to publish a Chocolatey with a signed executable will be implemented soon. Meanwhile, feel free to clone this repo, compile it and try it out:

```
dotnet publish -r win-x64 -c Release
```
Then run `.\DevComrade\bin\Release\netcoreapp3.1\DevComrade.exe`

Once run, it shows up as bulb icon in the system tray. Try some features:

- Press <kbd>Win</kbd>+<kbd>F10</kbd> to see the list of the available shortcuts and actions.
- Copy some code into Clipboard and try <kbd>Alt</kbd>+<kbd>Ins</kbd> to paste into the internal Notepad. 
- Press <kbd>Shift</kbd>+<kbd>Win</kbd>+<kbd>E</kbd> to open Windows Terminal then <kbd>Win</kbd>+<kbd>Ins</kbd> to paste the Clipboard's content as a single line of text. It won't get executed until your press <kbd>Enter</kbd>.

Oh, and don't hesitate to [follow the author on Twitter](https://twitter.com/noseratio) for any updates, if interested :)

*This page will be updated soon.*

<!---
# Getting Started
TODO: See above, Guide users through getting your code up and running on their own system. In this section you can talk about:
1.	Installation process
2.	Software dependencies
3.	Latest releases
4.	API references

# Build and Test
TODO: Describe and show how to build your code and run the tests. 

# Contribute
TODO: Explain how other users and developers can contribute to make your code better. 

If you want to learn more about creating good readme files then refer the following [guidelines](https://docs.microsoft.com/en-us/azure/devops/repos/git/create-a-readme?view=azure-devops). You can also seek inspiration from the below readme files:
- [ASP.NET Core](https://github.com/aspnet/Home)
- [Visual Studio Code](https://github.com/Microsoft/vscode)
- [Chakra Core](https://github.com/Microsoft/ChakraCore)

--> 