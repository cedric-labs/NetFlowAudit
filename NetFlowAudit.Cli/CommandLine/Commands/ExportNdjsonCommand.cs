using System.CommandLine;
using NetFlowAudit.Cli.CommandLine.OptionSets;
using NetFlowAudit.Cli.Configuration;
using NetFlowAudit.Core.Export;

namespace NetFlowAudit.Cli.CommandLine.Commands;

/// <summary>
/// <c>netflowaudit export ndjson</c>: conversion of CSV audit logs into a single NDJSON (JSON Lines) file.
/// </summary>
public sealed class ExportNdjsonCommand : Command
{
    private static readonly string[] CsvExtension = [".csv"];

    private readonly AuditInputOptionSet _inputs = new();
    private readonly Option<string?> _output = OptionFactory.CreatePath("--output", "NDJSON file to write. Tokens {ServerName}, {HostName}, {Date}, {Timestamp} are expanded. Default: netflow_audit.ndjson.", "-o");
    private readonly Option<string?> _configFile;

    /// <summary>
    /// Creates the command.
    /// </summary>
    /// <param name="configFile">Global <c>--config</c> option.</param>
    public ExportNdjsonCommand(Option<string?> configFile)
        : base("ndjson", "Convert CSV audit logs into one NDJSON (JSON Lines) file.")
    {
        _configFile = configFile;
        _inputs.AddTo(this);
        Options.Add(_output);

        SetAction(async (parseResult, cancellationToken) =>
        {
            var config = AppConfigurationFile.Load(parseResult.GetValue(_configFile)).Configuration;
            var files = _inputs.ResolveFiles(parseResult, config.OutputCsvPath, CsvExtension);
            if (files.Count == 0)
            {
                await Console.Error.WriteLineAsync($"No audit log found for: {_inputs.Describe(parseResult, config.OutputCsvPath)}").ConfigureAwait(false);
                return 1;
            }

            var delimiter = !string.IsNullOrEmpty(config.CsvDelimiter) ? config.CsvDelimiter[0] : ',';
            var outputPath = OutputFilePathResolver.Resolve(parseResult.GetValue(_output) ?? "netflow_audit.ndjson");
            var count = await new AuditNdjsonExporter().ExportCsvFilesToNdjsonAsync(files, outputPath, delimiter, cancellationToken).ConfigureAwait(false);
            Console.WriteLine($"Exported {count} audit flow entries from {files.Count} file(s) to NDJSON: {Path.GetFullPath(outputPath)}");
            return 0;
        });
    }
}
