using TuneTag.Cli;
using TuneTag.Core;

var tagService = TuneTagCore.CreateDefaultTagService();
var runner = new CommandRunner(tagService, tagService);

return runner.Run(args, Console.Out, Console.Error);
