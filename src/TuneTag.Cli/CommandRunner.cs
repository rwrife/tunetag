using System.Globalization;
using TuneTag.Core.Models;
using TuneTag.Core.Services;

namespace TuneTag.Cli;

public sealed class CommandRunner
{
    public const int ExitSuccess = 0;
    public const int ExitUsageError = 1;
    public const int ExitProcessingError = 2;

    private static readonly string[] DefaultSupportedExtensions =
    [
        ".mp3",
        ".flac",
        ".ogg",
        ".m4a",
        ".mp4",
        ".m4b",
        ".aac",
        ".alac"
    ];

    private readonly ITagReader _reader;
    private readonly ITagWriter _writer;
    private readonly HashSet<string> _supportedExtensions;

    public CommandRunner(ITagReader reader, ITagWriter writer, IEnumerable<string>? supportedExtensions = null)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));

        _supportedExtensions = new HashSet<string>(
            (supportedExtensions ?? DefaultSupportedExtensions).Select(NormalizeExtension),
            StringComparer.OrdinalIgnoreCase);
    }

    public int Run(string[] args, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (args.Length == 0 || IsHelpToken(args[0]))
        {
            WriteRootHelp(output);
            return ExitSuccess;
        }

        var command = args[0].Trim().ToLowerInvariant();
        var commandArgs = args[1..];

        return command switch
        {
            "inspect" => RunInspect(commandArgs, output, error),
            "set" => RunSet(commandArgs, output, error),
            "audit" => RunAudit(commandArgs, output, error),
            _ => HandleUnknownCommand(command, output, error)
        };
    }

    private static int HandleUnknownCommand(string command, TextWriter output, TextWriter error)
    {
        error.WriteLine($"Unknown command: {command}");
        error.WriteLine();
        WriteRootHelp(output);
        return ExitUsageError;
    }

    private int RunInspect(string[] args, TextWriter output, TextWriter error)
    {
        if (args.Length == 1 && IsHelpToken(args[0]))
        {
            WriteInspectHelp(output);
            return ExitSuccess;
        }

        var pathArguments = ParsePlainPathArguments(args, "inspect", output, error);
        if (pathArguments is null)
        {
            return ExitUsageError;
        }

        return ExecuteInspect(pathArguments, output, error);
    }

    private int RunSet(string[] args, TextWriter output, TextWriter error)
    {
        if (args.Length == 1 && IsHelpToken(args[0]))
        {
            WriteSetHelp(output);
            return ExitSuccess;
        }

        var parseResult = ParseSetArguments(args, output, error);
        if (!parseResult.Success)
        {
            return ExitUsageError;
        }

        return ExecuteSet(parseResult.Options!, parseResult.Paths!, output, error);
    }

    private int RunAudit(string[] args, TextWriter output, TextWriter error)
    {
        if (args.Length == 1 && IsHelpToken(args[0]))
        {
            WriteAuditHelp(output);
            return ExitSuccess;
        }

        var pathArguments = ParsePlainPathArguments(args, "audit", output, error);
        if (pathArguments is null)
        {
            return ExitUsageError;
        }

        return ExecuteAudit(pathArguments, output, error);
    }

    private int ExecuteInspect(IReadOnlyList<string> pathArguments, TextWriter output, TextWriter error)
    {
        var errors = new List<string>();
        var files = ResolveAudioFiles(pathArguments, errors);

        if (files.Count == 0)
        {
            if (errors.Count == 0)
            {
                errors.Add("No supported audio files were found in the provided paths.");
            }

            PrintErrors(errors, error);
            return ExitProcessingError;
        }

        var successCount = 0;

        foreach (var filePath in files)
        {
            try
            {
                var tags = _reader.Read(filePath);
                WriteTagBlock(output, filePath, tags);
                successCount++;
            }
            catch (Exception ex)
            {
                errors.Add($"{filePath}: {ex.Message}");
            }
        }

        output.WriteLine($"Processed {files.Count} file(s): {successCount} succeeded, {errors.Count} failed.");

        if (errors.Count > 0)
        {
            PrintErrors(errors, error);
            return ExitProcessingError;
        }

        return ExitSuccess;
    }

    private int ExecuteSet(SetOptions options, IReadOnlyList<string> pathArguments, TextWriter output, TextWriter error)
    {
        if (!options.HasAnyField)
        {
            error.WriteLine("The set command requires at least one explicit field option.");
            error.WriteLine("Example: tunetag set --album \"X\" --year 1999 <path>");
            return ExitUsageError;
        }

        var errors = new List<string>();
        var files = ResolveAudioFiles(pathArguments, errors);

        if (files.Count == 0)
        {
            if (errors.Count == 0)
            {
                errors.Add("No supported audio files were found in the provided paths.");
            }

            PrintErrors(errors, error);
            return ExitProcessingError;
        }

        var successCount = 0;

        foreach (var filePath in files)
        {
            try
            {
                var existing = _reader.Read(filePath);
                options.ApplyTo(existing);
                _writer.Write(filePath, existing);
                output.WriteLine($"Updated: {filePath}");
                successCount++;
            }
            catch (Exception ex)
            {
                errors.Add($"{filePath}: {ex.Message}");
            }
        }

        output.WriteLine($"Processed {files.Count} file(s): {successCount} updated, {errors.Count} failed.");

        if (errors.Count > 0)
        {
            PrintErrors(errors, error);
            return ExitProcessingError;
        }

        return ExitSuccess;
    }

    private int ExecuteAudit(IReadOnlyList<string> pathArguments, TextWriter output, TextWriter error)
    {
        var errors = new List<string>();
        var files = ResolveAudioFiles(pathArguments, errors);

        if (files.Count == 0)
        {
            if (errors.Count == 0)
            {
                errors.Add("No supported audio files were found in the provided paths.");
            }

            PrintErrors(errors, error);
            return ExitProcessingError;
        }

        var results = new List<AuditTrack>();

        foreach (var filePath in files)
        {
            try
            {
                results.Add(new AuditTrack(filePath, _reader.Read(filePath)));
            }
            catch (Exception ex)
            {
                errors.Add($"{filePath}: {ex.Message}");
            }
        }

        var findings = new List<string>();

        foreach (var result in results)
        {
            var missingFields = new List<string>();

            AddMissingFieldIfBlank(missingFields, "Title", result.Tags.Title);
            AddMissingFieldIfBlank(missingFields, "Artist", result.Tags.Artist);
            AddMissingFieldIfBlank(missingFields, "Album", result.Tags.Album);
            AddMissingFieldIfBlank(missingFields, "Genre", result.Tags.Genre);
            AddMissingFieldIfZero(missingFields, "Year", result.Tags.Year);
            AddMissingFieldIfZero(missingFields, "TrackNumber", result.Tags.TrackNumber);

            if (missingFields.Count > 0)
            {
                findings.Add($"Missing fields in {result.Path}: {string.Join(", ", missingFields)}");
            }
        }

        AddInconsistencyIfAny(findings, "Album", results.Select(static track => track.Tags.Album));
        AddInconsistencyIfAny(findings, "AlbumArtist", results.Select(static track => track.Tags.AlbumArtist));
        AddInconsistencyIfAny(findings, "Genre", results.Select(static track => track.Tags.Genre));
        AddInconsistencyIfAny(findings, "Year", results.Select(static track => track.Tags.Year?.ToString(CultureInfo.InvariantCulture)));

        output.WriteLine($"Audited {files.Count} file(s). Read failures: {errors.Count}. Findings: {findings.Count}.");

        if (findings.Count == 0 && errors.Count == 0)
        {
            output.WriteLine("Audit passed: no missing or inconsistent core fields detected.");
            return ExitSuccess;
        }

        if (findings.Count > 0)
        {
            output.WriteLine("Audit findings:");
            foreach (var finding in findings)
            {
                output.WriteLine($"- {finding}");
            }
        }

        if (errors.Count > 0)
        {
            PrintErrors(errors, error);
        }

        return ExitProcessingError;
    }

    private IReadOnlyList<string>? ParsePlainPathArguments(
        string[] args,
        string commandName,
        TextWriter output,
        TextWriter error)
    {
        if (args.Length == 0)
        {
            error.WriteLine($"The {commandName} command requires at least one path.");
            WriteCommandHelp(commandName, output);
            return null;
        }

        foreach (var arg in args)
        {
            if (arg.StartsWith('-'))
            {
                error.WriteLine($"Unknown option for {commandName}: {arg}");
                WriteCommandHelp(commandName, output);
                return null;
            }
        }

        return args;
    }

    private ParseSetResult ParseSetArguments(string[] args, TextWriter output, TextWriter error)
    {
        if (args.Length == 0)
        {
            error.WriteLine("The set command requires at least one field and one path.");
            WriteSetHelp(output);
            return ParseSetResult.Fail;
        }

        var options = new SetOptions();
        var paths = new List<string>();

        for (var i = 0; i < args.Length; i++)
        {
            var token = args[i];

            if (!token.StartsWith("--", StringComparison.Ordinal))
            {
                paths.Add(token);
                continue;
            }

            switch (token)
            {
                case "--title":
                    if (!TryReadOptionValue(args, ref i, token, out var title, error))
                    {
                        return ParseSetResult.Fail;
                    }

                    options.Title = title;
                    options.HasTitle = true;
                    break;

                case "--artist":
                    if (!TryReadOptionValue(args, ref i, token, out var artist, error))
                    {
                        return ParseSetResult.Fail;
                    }

                    options.Artist = artist;
                    options.HasArtist = true;
                    break;

                case "--album":
                    if (!TryReadOptionValue(args, ref i, token, out var album, error))
                    {
                        return ParseSetResult.Fail;
                    }

                    options.Album = album;
                    options.HasAlbum = true;
                    break;

                case "--album-artist":
                    if (!TryReadOptionValue(args, ref i, token, out var albumArtist, error))
                    {
                        return ParseSetResult.Fail;
                    }

                    options.AlbumArtist = albumArtist;
                    options.HasAlbumArtist = true;
                    break;

                case "--genre":
                    if (!TryReadOptionValue(args, ref i, token, out var genre, error))
                    {
                        return ParseSetResult.Fail;
                    }

                    options.Genre = genre;
                    options.HasGenre = true;
                    break;

                case "--composer":
                    if (!TryReadOptionValue(args, ref i, token, out var composer, error))
                    {
                        return ParseSetResult.Fail;
                    }

                    options.Composer = composer;
                    options.HasComposer = true;
                    break;

                case "--comment":
                    if (!TryReadOptionValue(args, ref i, token, out var comment, error))
                    {
                        return ParseSetResult.Fail;
                    }

                    options.Comment = comment;
                    options.HasComment = true;
                    break;

                case "--year":
                    if (!TryReadUIntOptionValue(args, ref i, token, out var year, error))
                    {
                        return ParseSetResult.Fail;
                    }

                    options.Year = year;
                    options.HasYear = true;
                    break;

                case "--track":
                    if (!TryReadUIntOptionValue(args, ref i, token, out var track, error))
                    {
                        return ParseSetResult.Fail;
                    }

                    options.Track = track;
                    options.HasTrack = true;
                    break;

                case "--disc":
                    if (!TryReadUIntOptionValue(args, ref i, token, out var disc, error))
                    {
                        return ParseSetResult.Fail;
                    }

                    options.Disc = disc;
                    options.HasDisc = true;
                    break;

                default:
                    error.WriteLine($"Unknown option for set: {token}");
                    WriteSetHelp(output);
                    return ParseSetResult.Fail;
            }
        }

        if (paths.Count == 0)
        {
            error.WriteLine("The set command requires at least one path.");
            WriteSetHelp(output);
            return ParseSetResult.Fail;
        }

        return new ParseSetResult(true, options, paths);
    }

    private IReadOnlyList<string> ResolveAudioFiles(IReadOnlyList<string> pathArguments, ICollection<string> errors)
    {
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pathArgument in pathArguments)
        {
            if (string.IsNullOrWhiteSpace(pathArgument))
            {
                continue;
            }

            var fullPath = Path.GetFullPath(pathArgument);

            if (File.Exists(fullPath))
            {
                files.Add(fullPath);
                continue;
            }

            if (!Directory.Exists(fullPath))
            {
                errors.Add($"Path not found: {pathArgument}");
                continue;
            }

            foreach (var filePath in Directory.EnumerateFiles(fullPath, "*", SearchOption.AllDirectories))
            {
                if (_supportedExtensions.Contains(NormalizeExtension(Path.GetExtension(filePath))))
                {
                    files.Add(filePath);
                }
            }
        }

        return files
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void WriteTagBlock(TextWriter output, string filePath, TrackTags tags)
    {
        output.WriteLine($"FILE: {filePath}");
        output.WriteLine($"  Title: {FormatText(tags.Title)}");
        output.WriteLine($"  Artist: {FormatText(tags.Artist)}");
        output.WriteLine($"  Album: {FormatText(tags.Album)}");
        output.WriteLine($"  AlbumArtist: {FormatText(tags.AlbumArtist)}");
        output.WriteLine($"  Track: {FormatNumber(tags.TrackNumber)}");
        output.WriteLine($"  Disc: {FormatNumber(tags.DiscNumber)}");
        output.WriteLine($"  Year: {FormatNumber(tags.Year)}");
        output.WriteLine($"  Genre: {FormatText(tags.Genre)}");
        output.WriteLine($"  Composer: {FormatText(tags.Composer)}");
        output.WriteLine($"  Comment: {FormatText(tags.Comment)}");
        output.WriteLine($"  Format: {GetFormat(tags)}");
        output.WriteLine();
    }

    private static string GetFormat(TrackTags tags)
    {
        return tags.RawFields.TryGetValue("format", out var format) && !string.IsNullOrWhiteSpace(format)
            ? format
            : "<unknown>";
    }

    private static void AddMissingFieldIfBlank(ICollection<string> missingFields, string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            missingFields.Add(name);
        }
    }

    private static void AddMissingFieldIfZero(ICollection<string> missingFields, string name, uint? value)
    {
        if (!value.HasValue || value.Value == 0)
        {
            missingFields.Add(name);
        }
    }

    private static void AddInconsistencyIfAny(ICollection<string> findings, string fieldName, IEnumerable<string?> values)
    {
        var distinct = values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (distinct.Length > 1)
        {
            findings.Add($"Inconsistent {fieldName} values: {string.Join(" | ", distinct)}");
        }
    }

    private static bool TryReadOptionValue(
        string[] args,
        ref int index,
        string option,
        out string value,
        TextWriter error)
    {
        if (index + 1 >= args.Length)
        {
            error.WriteLine($"Missing value for {option}.");
            value = string.Empty;
            return false;
        }

        value = args[++index];
        return true;
    }

    private static bool TryReadUIntOptionValue(
        string[] args,
        ref int index,
        string option,
        out uint value,
        TextWriter error)
    {
        value = 0;
        if (!TryReadOptionValue(args, ref index, option, out var textValue, error))
        {
            return false;
        }

        if (!uint.TryParse(textValue, NumberStyles.None, CultureInfo.InvariantCulture, out value))
        {
            error.WriteLine($"Invalid numeric value for {option}: {textValue}");
            return false;
        }

        return true;
    }

    private static string FormatText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "<empty>" : value;
    }

    private static string FormatNumber(uint? value)
    {
        return value.HasValue && value.Value > 0
            ? value.Value.ToString(CultureInfo.InvariantCulture)
            : "<empty>";
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return string.Empty;
        }

        return extension.StartsWith('.') ? extension : $".{extension}";
    }

    private static bool IsHelpToken(string token)
    {
        return token is "-h" or "--help" or "help";
    }

    private static void PrintErrors(IReadOnlyList<string> errors, TextWriter error)
    {
        if (errors.Count == 0)
        {
            return;
        }

        error.WriteLine($"Errors ({errors.Count}):");
        foreach (var line in errors)
        {
            error.WriteLine($"- {line}");
        }
    }

    private static void WriteRootHelp(TextWriter output)
    {
        output.WriteLine("tunetag - headless audio tag operations");
        output.WriteLine();
        output.WriteLine("Usage:");
        output.WriteLine("  tunetag <command> [options] <path...>");
        output.WriteLine();
        output.WriteLine("Commands:");
        output.WriteLine("  inspect <path...>                Inspect and print normalized tags");
        output.WriteLine("  set [field options] <path...>    Batch-set explicit fields only");
        output.WriteLine("  audit <path...>                  Report missing or inconsistent fields");
        output.WriteLine();
        output.WriteLine("Run 'tunetag <command> --help' for command-specific usage.");
        output.WriteLine();
        output.WriteLine("Exit codes: 0=success, 1=usage error, 2=processing error");
    }

    private static void WriteInspectHelp(TextWriter output)
    {
        output.WriteLine("Usage: tunetag inspect <path...>");
        output.WriteLine("Print normalized tags for files or directories (recursive).\n");
        output.WriteLine("Examples:");
        output.WriteLine("  tunetag inspect ~/Music/Album");
        output.WriteLine("  tunetag inspect song.mp3 song.flac");
    }

    private static void WriteSetHelp(TextWriter output)
    {
        output.WriteLine("Usage: tunetag set [field options] <path...>");
        output.WriteLine("Applies only explicitly provided fields; unspecified fields are preserved.\n");
        output.WriteLine("Field options:");
        output.WriteLine("  --title <value>");
        output.WriteLine("  --artist <value>");
        output.WriteLine("  --album <value>");
        output.WriteLine("  --album-artist <value>");
        output.WriteLine("  --genre <value>");
        output.WriteLine("  --composer <value>");
        output.WriteLine("  --comment <value>");
        output.WriteLine("  --track <uint>");
        output.WriteLine("  --disc <uint>");
        output.WriteLine("  --year <uint>");
        output.WriteLine();
        output.WriteLine("Example:");
        output.WriteLine("  tunetag set --album \"X\" --year 1999 ~/Music/Album");
    }

    private static void WriteAuditHelp(TextWriter output)
    {
        output.WriteLine("Usage: tunetag audit <path...>");
        output.WriteLine("Reports missing core fields and inconsistent album-level values.\n");
        output.WriteLine("Example:");
        output.WriteLine("  tunetag audit ~/Music/Album");
    }

    private static void WriteCommandHelp(string commandName, TextWriter output)
    {
        switch (commandName)
        {
            case "inspect":
                WriteInspectHelp(output);
                break;
            case "set":
                WriteSetHelp(output);
                break;
            case "audit":
                WriteAuditHelp(output);
                break;
            default:
                WriteRootHelp(output);
                break;
        }
    }

    private readonly record struct ParseSetResult(bool Success, SetOptions? Options, IReadOnlyList<string>? Paths)
    {
        public static ParseSetResult Fail => new(false, null, null);
    }

    private sealed class SetOptions
    {
        public string? Title { get; set; }
        public string? Artist { get; set; }
        public string? Album { get; set; }
        public string? AlbumArtist { get; set; }
        public string? Genre { get; set; }
        public string? Composer { get; set; }
        public string? Comment { get; set; }
        public uint? Track { get; set; }
        public uint? Disc { get; set; }
        public uint? Year { get; set; }

        public bool HasTitle { get; set; }
        public bool HasArtist { get; set; }
        public bool HasAlbum { get; set; }
        public bool HasAlbumArtist { get; set; }
        public bool HasGenre { get; set; }
        public bool HasComposer { get; set; }
        public bool HasComment { get; set; }
        public bool HasTrack { get; set; }
        public bool HasDisc { get; set; }
        public bool HasYear { get; set; }

        public bool HasAnyField =>
            HasTitle || HasArtist || HasAlbum || HasAlbumArtist || HasGenre ||
            HasComposer || HasComment || HasTrack || HasDisc || HasYear;

        public void ApplyTo(TrackTags tags)
        {
            if (HasTitle)
            {
                tags.Title = Title;
            }

            if (HasArtist)
            {
                tags.Artist = Artist;
            }

            if (HasAlbum)
            {
                tags.Album = Album;
            }

            if (HasAlbumArtist)
            {
                tags.AlbumArtist = AlbumArtist;
            }

            if (HasGenre)
            {
                tags.Genre = Genre;
            }

            if (HasComposer)
            {
                tags.Composer = Composer;
            }

            if (HasComment)
            {
                tags.Comment = Comment;
            }

            if (HasTrack)
            {
                tags.TrackNumber = Track;
            }

            if (HasDisc)
            {
                tags.DiscNumber = Disc;
            }

            if (HasYear)
            {
                tags.Year = Year;
            }
        }
    }

    private readonly record struct AuditTrack(string Path, TrackTags Tags);
}
