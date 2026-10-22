using System.Text;
using NetFlowAudit.Core.Models;

namespace NetFlowAudit.Core.Export;

/// <summary>
/// Gestionnaire d'écriture thread-safe et d'hydratation persistant du journal d'audit CSV.
/// Permet l'ouverture non exclusive pour la consultation concurrente sans bloquer le scanner.
/// </summary>
public sealed class PersistentCsvAuditWriter : IAsyncDisposable
{
    private readonly string _filePath;
    private readonly AuditCsvFormatter _formatter;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private FileStream? _fileStream;
    private StreamWriter? _streamWriter;

    /// <summary>
    /// Initialise une nouvelle instance du gestionnaire de persistance CSV.
    /// </summary>
    /// <param name="filePath">Chemin d'accès au fichier CSV cible.</param>
    /// <param name="formatter">Formateur de lignes CSV.</param>
    public PersistentCsvAuditWriter(string filePath, AuditCsvFormatter formatter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(formatter);

        _filePath = filePath;
        _formatter = formatter;
    }

    /// <summary>
    /// Initialise le fichier, écrit l'en-tête si le fichier est nouvellement créé, ou positionne le curseur en fin de flux.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var fileExisted = File.Exists(_filePath) && new FileInfo(_filePath).Length > 0;

            // Ouverture avec FileShare.ReadWrite pour permettre la lecture externe par un script ou un utilisateur
            _fileStream = new FileStream(
                _filePath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.ReadWrite,
                4096,
                useAsync: true);

            _streamWriter = new StreamWriter(_fileStream, Encoding.UTF8, bufferSize: 4096, leaveOpen: true)
            {
                AutoFlush = true
            };

            if (!fileExisted)
            {
                await _streamWriter.WriteLineAsync(_formatter.BuildHeaderLine().AsMemory(), cancellationToken).ConfigureAwait(false);
            }
            else
            {
                _fileStream.Seek(0, SeekOrigin.End);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Charge les clés de dédoublonnage existantes à partir du fichier CSV actuel pour éviter les doublons au redémarrage.
    /// </summary>
    public async Task<HashSet<string>> LoadExistingSignaturesAsync(CancellationToken cancellationToken = default)
    {
        var signatures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!File.Exists(_filePath))
        {
            return signatures;
        }

        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var readStream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(readStream, Encoding.UTF8);

            var isHeader = true;
            while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (isHeader)
                {
                    isHeader = false;
                    continue;
                }

                signatures.Add(line.Trim());
            }
        }
        finally
        {
            _semaphore.Release();
        }

        return signatures;
    }

    /// <summary>
    /// Ajoute de manière concurrente une nouvelle connexion auditée dans le flux persistant.
    /// </summary>
    public async Task AppendRecordAsync(NetworkConnectionRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (_streamWriter is null)
        {
            throw new InvalidOperationException("Le fichier CSV d'audit doit être initialisé avant d'y ajouter des enregistrements.");
        }

        var line = _formatter.FormatRecordLine(record);

        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _streamWriter.WriteLineAsync(line.AsMemory(), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_streamWriter is not null)
        {
            await _streamWriter.FlushAsync().ConfigureAwait(false);
            await _streamWriter.DisposeAsync().ConfigureAwait(false);
            _streamWriter = null;
        }

        if (_fileStream is not null)
        {
            await _fileStream.DisposeAsync().ConfigureAwait(false);
            _fileStream = null;
        }

        _semaphore.Dispose();
    }
}
