using CommandLine;
using CommandLine.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BackupAsana
{

    class Program
    {
        class Options
        {
            [Option('t', "token", Required = true,
              HelpText = "Asana API token")]
            public string AsanaToken { get; set; }

            [Option('w', "workspace", Required = false,
              HelpText = "Asana workspace")]
            public long WorkspaceID { get; set; }


            [Option('c', "continue", Required = false,
              HelpText = "Continue on next project")]
            public bool Continue { get; set; }

            [Option('p', "path", Required = true,
              HelpText = "Directory path for download")]
            public string Path { get; set; }

            [ParserState]
            public IParserState LastParserState { get; set; }

            [HelpOption]
            public string GetUsage()
            {
                return HelpText.AutoBuild(this,
                  (HelpText current) => HelpText.DefaultParsingErrorsHandler(this, current));
            }
        }

        static void Main(string[] args)
        {
            var options = new Options();
            if (CommandLine.Parser.Default.ParseArguments(args, options))
            {
                var timer = new Stopwatch();
                timer.Start();

                options.AsanaToken = options.AsanaToken.Trim('"');

                Task.Run(async () =>
                {
                    var asanaBackup = new AsanaBackup(options.AsanaToken, options.Path, !options.Continue);
                    await asanaBackup.BackupProjects(options.WorkspaceID);
                }).Wait();

                Console.WriteLine("Total backup {0}ms", timer.ElapsedMilliseconds);


                Console.WriteLine("ALL DONE");
            }
            Console.ReadLine();

        }
    }
}
