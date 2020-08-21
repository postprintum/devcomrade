# Introduction 
**DevComrade** is a Windows copy/paste/run productivity improvement tool for developers. 

Copy-pasting from the online docs, StackOverflow or numerous blogs can be a tedious and sometimes even a dangerous task. Does the following sound familiar: you paste some text from a web page into a Terminal command line, and it gets executed immediately, before you even have a chance to edit it? Only because there was a CR/LF character at the end of the clipboard text.

Or, have you ever been annoyed with some broken formatting, indentation, inconsistent tabs/spaces when you paste a piece of code into Visual Studio Code editor, a blog post or an email message? A typical workaround for that is to use the good old `Notepad.exe` as a buffer.

Now I have two dedicated hotkeys for that, **<kbd>Win</kbd>+<kbd>Ins</kbd> (paste as single line) and <kbd>Win</kbd>+<kbd>Shift</kbd>+<kbd>Ins</kbd> (paste as multiple lines)**, which uniformly work across all apps and browsers and remove the trailing empty lines and the last line's CR/LF ending.

One other source of disappointment for me has always been how custom keyboard hotkeys work with Windows Shell shortcuts. It is a common struggle to find a convenient hotkey combination that still can be assigned to start a custom app. E.g., it is impossible to use <kbd>Win</kbd>+<kbd>Shift|Alt|Ctrl</kbd>+<kbd>Key</kbd> combos for that. And when it *can* be assigned, [it may take up to 10 seconds](https://superuser.com/q/426947/246232) for the program to actually start when the hotkey is pressed (of course, we can run `taskkill /im ApplicationFrameHost.exe /f` to [fix that](https://superuser.com/a/961761), if we dare).

**DevComrade** has been made to solve this problem, too. It allows assigning a customizable action to (almost) any hotkey combination, and comes with an extensive set of predefined actions for pasting text and launching apps. Additional actions can be added as [C# scriptlets](https://github.com/dotnet/roslyn/wiki/Scripting-API-Samples) in the `.config` file.

When it comes to pasting text, **DevComrade** is different from many similar utilities (e.g., from the still-excellent [Puretext](https://stevemiller.net/puretext/)) in how it uses [Win32 simulated input API](https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput) to elaborately feed the text into the currently active window, character by character as though it was typed by a person. For example, it works well with Google's [Secure Shell App Chrome extension](https://chrome.google.com/webstore/detail/secure-shell-app/pnhechapfaindjhompbnflcldabbghjo?hl=en).

**DevComrade** is a free and open-source software licensed under [Apache License 2.0](https://www.apache.org/licenses/LICENSE-2.0). It's built with [.NET Core 3.1 SDK](https://dotnet.microsoft.com/download/dotnet-core/3.1) and uses Windows Forms for its very simple, context-menu-style UI. 

**It is still very much a work in progress**. Some CI logic for publishing a Chocolatey package (including a code-signed executable) will be implemented soon and this page will be updated. 

**Meanwhile, feel free to clone this repo, compile it and try it out**:

- Download and install [.NET Core 3.1 SDK](https://download.visualstudio.microsoft.com/download/pr/547f9f81-599a-4b58-9322-d1d158385df6/ebe3e02fd54c29487ac32409cb20d352/dotnet-sdk-3.1.401-win-x64.exe), if you haven't got it already.

- Download and unzip [the source](https://github.com/postprintum/devcomrade/archive/main.zip), or use `git` to clone the repo to a folder of your choice, e.g.:
    ```
    mkdir DevComradeRepo && cd DevComradeRepo
    git clone https://github.com/postprintum/devcomrade .
    ```
- Build and run:
    ```
    .\Package\make-and-run.bat
    ```
- Or do that manually:
    ```
    dotnet publish -r win10-x64 -c Release --self-contained false -p:PublishTrimmed=false .\DevComrade
    
    start .\DevComrade\bin\Release\netcoreapp3.1\win10-x64\publish\DevComrade.exe
    ```
Once run, DevComrade shows up as <img src="./Art/BulbIcon.ico" alt="DevComrade Icon" height="16"/> icon in the system tray. Some of the features to try out:

- Press <kbd>Win</kbd>+<kbd>F10</kbd> to see the list of the available shortcuts and actions.
- Copy some code into the Clipboard and try <kbd>Alt</kbd>+<kbd>Ins</kbd>, to see it pasted into the instant internal Notepad pop-up window. Hit <kbd>Esc</kbd> to simply hide it when finished, or <kbd>Win</kbd>+<kbd>&#x5c;</kbd> to open it again.
- Press <kbd>Shift</kbd>+<kbd>Win</kbd>+<kbd>E</kbd> to open Windows Terminal then <kbd>Win</kbd>+<kbd>Ins</kbd> to paste the Clipboard's content as a single line of text. It won't get executed until your press <kbd>Enter</kbd>.
- Copy any URL into clipboard (e.g., from a console window output, spaces and broken lines are OK), then press <kbd>Shift</kbd>+<kbd>Win</kbd>+<kbd>O</kbd> to open it in your default web browser.

This tool has been working well for my own personal needs, but outside that its future depends on your feedback. Feel free to [open an issue](https://github.com/postprintum/devcomrade/issues) or [send me a DM on Twitter](https://twitter.com/noseratio).

<hr>

<img src="./Art/menu.jpg" alt="DevComrade Win+F10 Menu" width="800"/>
<img src="./Art/notepad.jpg" alt="DevComrade Alt+Ins Notepad" width="800"/>

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