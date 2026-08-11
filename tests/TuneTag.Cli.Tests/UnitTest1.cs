using TuneTag.Core;

namespace TuneTag.Cli.Tests;

public sealed class CommandRunnerTests
{
    [Fact]
    public void Help_PrintsCommandsAndReturnsZero()
    {
        var runner = CreateRunner();
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = runner.Run(["--help"], output, error);

        Assert.Equal(CommandRunner.ExitSuccess, exitCode);
        Assert.Contains("inspect", output.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("set", output.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("audit", output.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public void UnknownCommand_ReturnsUsageError()
    {
        var runner = CreateRunner();
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = runner.Run(["nope"], output, error);

        Assert.Equal(CommandRunner.ExitUsageError, exitCode);
        Assert.Contains("Unknown command", error.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Inspect_PrintsTagsForMixedFormatFolder()
    {
        var folder = CreateFixtureFolder("sample.mp3", "sample.flac", "sample.m4a");
        var runner = CreateRunner();
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = runner.Run(["inspect", folder], output, error);

        Assert.Equal(CommandRunner.ExitSuccess, exitCode);
        var stdout = output.ToString();
        Assert.Contains("sample.mp3", stdout, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sample.flac", stdout, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sample.m4a", stdout, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Format:", stdout, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public void Set_RequiresAtLeastOneExplicitField()
    {
        var folder = CreateFixtureFolder("sample.mp3");
        var runner = CreateRunner();
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = runner.Run(["set", folder], output, error);

        Assert.Equal(CommandRunner.ExitUsageError, exitCode);
        Assert.Contains("requires at least one explicit field", error.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Set_AlbumAndYear_UpdatesEveryMatchingFile()
    {
        var folder = CreateFixtureFolder("sample.mp3", "sample.flac", "sample.m4a");
        var runner = CreateRunner();
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = runner.Run(["set", "--album", "Batch Album", "--year", "1999", folder], output, error);

        Assert.Equal(CommandRunner.ExitSuccess, exitCode);
        Assert.Equal(string.Empty, error.ToString());

        var service = TuneTagCore.CreateDefaultTagService();
        var files = Directory.EnumerateFiles(folder)
            .Where(static path => path.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ||
                                  path.EndsWith(".flac", StringComparison.OrdinalIgnoreCase) ||
                                  path.EndsWith(".m4a", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.NotEmpty(files);

        foreach (var file in files)
        {
            var tags = service.Read(file);
            Assert.Equal("Batch Album", tags.Album);
            Assert.Equal((uint)1999, tags.Year);
        }
    }

    [Fact]
    public void Inspect_CollectsErrorsWithoutAbortingBatch()
    {
        var folder = CreateFixtureFolder("sample.mp3");
        File.WriteAllBytes(Path.Combine(folder, "broken.mp3"), [0x01, 0x02, 0x03, 0x04, 0x05]);

        var runner = CreateRunner();
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = runner.Run(["inspect", folder], output, error);

        Assert.Equal(CommandRunner.ExitProcessingError, exitCode);

        var stdout = output.ToString();
        var stderr = error.ToString();

        Assert.Contains("sample.mp3", stdout, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("broken.mp3", stderr, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Errors", stderr, StringComparison.OrdinalIgnoreCase);
    }

    private static CommandRunner CreateRunner()
    {
        var service = TuneTagCore.CreateDefaultTagService();
        return new CommandRunner(service, service);
    }

    private static string CreateFixtureFolder(params string[] fixtureNames)
    {
        var fixtureRoot = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        Assert.True(Directory.Exists(fixtureRoot), $"Fixtures folder missing at: {fixtureRoot}");

        var workingDir = Path.Combine(Path.GetTempPath(), $"tunetag-cli-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workingDir);

        foreach (var fixtureName in fixtureNames)
        {
            var sourcePath = Path.Combine(fixtureRoot, fixtureName);
            Assert.True(File.Exists(sourcePath), $"Fixture file missing: {sourcePath}");

            var destinationPath = Path.Combine(workingDir, fixtureName);
            File.Copy(sourcePath, destinationPath, overwrite: true);
        }

        return workingDir;
    }
}
