using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text;
using System;

namespace PlexCleaner
{
    class ProgramCommandLine
    {
        static int Main1(string[] args)
        {
            // Create some options and a parser
            Option optionThatTakesInt = new Option(
                "--int-option",
                "An option whose argument is parsed as an int",
                new Argument<int>(defaultValue: 42));
            Option optionThatTakesBool = new Option(
                "--bool-option",
                "An option whose argument is parsed as a bool",
                new Argument<bool>());
            Option optionThatTakesFileInfo = new Option(
                "--file-option",
                "An option whose argument is parsed as a FileInfo",
                new Argument<FileInfo>());

            // Add them to the root command
            var rootCommand = new RootCommand();
            rootCommand.Description = "My sample app";
            rootCommand.AddOption(optionThatTakesInt);
            rootCommand.AddOption(optionThatTakesBool);
            rootCommand.AddOption(optionThatTakesFileInfo);

            rootCommand.Handler = CommandHandler.Create<int, bool, FileInfo>((intOption, boolOption, fileOption) =>
            {
                Console.WriteLine($"The value for --int-option is: {intOption}");
                Console.WriteLine($"The value for --bool-option is: {boolOption}");
                Console.WriteLine($"The value for --file-option is: {fileOption?.FullName ?? "null"}");
            });

            // Parse the incoming args and invoke the handler
            return rootCommand.InvokeAsync(args).Result;
        }
    }
}
