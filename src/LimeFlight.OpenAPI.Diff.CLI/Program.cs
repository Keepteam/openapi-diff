using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LimeFlight.OpenAPI.Diff.Compare;
using LimeFlight.OpenAPI.Diff.Output;
using LimeFlight.OpenAPI.Diff.Output.Html;
using LimeFlight.OpenAPI.Diff.Output.Markdown;
using LimeFlight.OpenAPI.Diff.Utils;

namespace LimeFlight.OpenAPI.Diff.CLI
{
    class Program
    {
        private ILogger _logger;

        private readonly Lazy<HashSet<char>> _invalidPathCharsHashSet =
            new(() => Path.GetInvalidPathChars().ToHashSet());

        [Required, FileExists]
        [Option(CommandOptionType.SingleValue, ShortName = "o", LongName = "old", Description = "Path to old OpenAPI Specification file")]
        public string OldPath { get; }
        [Required, FileExists]
        [Option(CommandOptionType.SingleValue, ShortName = "n", LongName = "new", Description = "Path to new OpenAPI Specification file")]
        public string NewPath { get; }

        [Option(CommandOptionType.SingleValue, ShortName = "e", LongName = "exit", Description = "Define exit behavior. Default: Fail only if API changes broke backward compatibility")]
        public ExitTypeEnum? ExitType { get; }

        [Option(CommandOptionType.SingleValue, Description = "Export diff as markdown in given file")]
        public string Markdown { get; }

        [Option(CommandOptionType.NoValue, ShortName = "c", LongName = "console", Description = "Export diff in console")]
        public bool ToConsole { get; }

        [Option(CommandOptionType.SingleValue, Description = "Export diff as html in given file")]
        public string HTML { get; }

        [Option(CommandOptionType.SingleValue, Description = "Export diff as text in given file")]
        public string Text { get; }

        [Option(CommandOptionType.SingleValue, Description = "Set log level (default: trace)")]
        public LogLevel LogLevel { get; } = LogLevel.Trace;
        
        static int Main(string[] args)
            => CommandLineApplication.Execute<Program>(args);

        private async Task<int> OnExecute()
        {
            //setup our DI
            var serviceProvider = new ServiceCollection()
                .AddLogging(builder =>
                {
                    builder.AddFilter("", LogLevel);
                    builder.AddConsole();
                })
                .AddSingleton<IOpenAPICompare, OpenAPICompare>()
                .AddSingleton<IMarkdownRender, MarkdownRender>()
                .AddSingleton<IHtmlRender, HtmlRender>()
                .AddSingleton<IConsoleRender, ConsoleRender>()
                .AddSingleton<OpenApiDiagnosticErrorsProcessor, IgnoreOpenApiDiagnosticErrorsProcessor>()
                .AddTransient(x => (IExtensionDiff)x.GetService(typeof(ExtensionDiff)))
                .BuildServiceProvider();

            _logger = serviceProvider.GetService<ILogger<Program>>();
            _logger.LogDebug("Starting application");

            var openAPICompare = serviceProvider.GetService<IOpenAPICompare>();
            var result = openAPICompare.FromLocations(OldPath, NewPath);
            
            var renders = GetRendersByArgs(serviceProvider);
            foreach (var tuple in renders)
            {
                var renderResult = await tuple.Item1.Render(result);
                tuple.Item2(renderResult);
            }

            switch (ExitType)
            {
                case ExitTypeEnum.PrintState:
                    Console.WriteLine(result.IsChanged().DiffResult);
                    break;
                case ExitTypeEnum.FailOnChanged:
                    Environment.ExitCode = result.IsUnchanged() ? 0 : 1;
                    break;
                case null:
                    Environment.ExitCode = result.IsCompatible() ? 0 : 1;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            _logger.LogDebug("All done!");
            return Environment.ExitCode;
        }
        

        private void SaveToFile(string renderedResult, string path)
        {
            try
            {
                File.WriteAllText(path, renderedResult);
            }
            catch (Exception e)
            {
                _logger.LogError(e.ToString());
            }
        }
        
        private IEnumerable<Tuple<IRender, Action<string>>> GetRendersByArgs(ServiceProvider serviceProvider)
        {
            if (ToConsole)
            {
                yield return Tuple.Create<IRender, Action<string>>(serviceProvider.GetService<IConsoleRender>(), 
                    (result) => Console.WriteLine(result));
            }
            if (IsValidPath(HTML))
            {
                yield return Tuple.Create<IRender, Action<string>>(serviceProvider.GetService<IHtmlRender>(),
                    (result) => SaveToFile(result, HTML));
            }
            if (IsValidPath(Markdown))
            {
                yield return Tuple.Create<IRender, Action<string>>(serviceProvider.GetService<IMarkdownRender>(),
                    (result) => SaveToFile(result, Markdown));
            }
            
            if (IsValidPath(Text))
            {
                yield return Tuple.Create<IRender, Action<string>>(serviceProvider.GetService<IConsoleRender>(),
                    (result) => SaveToFile(result, Text));
            }
        }

        private bool IsValidPath(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return false;
            }

            var isValid = !filePath.Any(c => _invalidPathCharsHashSet.Value.Contains(c));
            if (isValid)
            {
                return true;
            }
            
            _logger.LogError($"Error export to \"{filePath}\"");
            return false;
        }
    }
}
