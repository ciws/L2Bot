using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Client.Infrastructure.Service
{
    /// <summary>
    /// Downloads missing Interlude geodata files (.l2j) from the public
    /// LGK-Games/Geodata repository into Assets/geodata.
    /// </summary>
    public class GeodataDownloader
    {
        private const string RepoTreeUrl = "https://api.github.com/repos/LGK-Games/Geodata/git/trees/main?recursive=1";
        private const string RawBaseUrl = "https://raw.githubusercontent.com/LGK-Games/Geodata/main/";
        private const string RemoteFolder = "INTERLUDE/Geodata2/geodata/";
        private const int ParallelDownloads = 6;

        private readonly string geodataPath;

        public GeodataDownloader(string geodataDirectory)
        {
            geodataPath = Path.Combine(Directory.GetCurrentDirectory(), "Assets", geodataDirectory);
        }

        /// <summary>
        /// Returns the list of geodata files that are missing locally or have a wrong size.
        /// Empty list means geodata is complete and no download is needed.
        /// </summary>
        public async Task<List<GeodataFile>> GetMissingFilesAsync(CancellationToken cancellationToken = default)
        {
            using var http = CreateClient();
            var json = await http.GetStringAsync(RepoTreeUrl, cancellationToken);

            using var doc = JsonDocument.Parse(json);
            var missing = new List<GeodataFile>();

            foreach (var entry in doc.RootElement.GetProperty("tree").EnumerateArray())
            {
                var path = entry.GetProperty("path").GetString() ?? "";
                if (!path.StartsWith(RemoteFolder))
                {
                    continue;
                }

                var fileName = path.Substring(RemoteFolder.Length);
                if (!fileName.EndsWith(".l2j") && fileName != "geo_index.txt")
                {
                    continue;
                }

                var size = entry.GetProperty("size").GetInt64();
                var localPath = Path.Combine(geodataPath, fileName);

                if (!File.Exists(localPath) || new FileInfo(localPath).Length != size)
                {
                    missing.Add(new GeodataFile
                    {
                        Name = fileName,
                        Url = RawBaseUrl + path,
                        Size = size
                    });
                }
            }

            return missing;
        }

        public async Task DownloadAsync(
            IReadOnlyList<GeodataFile> files,
            IProgress<GeodataDownloadProgress> progress,
            CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(geodataPath);

            var totalBytes = files.Sum(f => f.Size);
            long downloadedBytes = 0;
            var downloadedFiles = 0;

            using var http = CreateClient();
            using var throttler = new SemaphoreSlim(ParallelDownloads);

            var tasks = files.Select(async file =>
            {
                await throttler.WaitAsync(cancellationToken);
                try
                {
                    var tempPath = Path.Combine(geodataPath, file.Name + ".part");
                    var finalPath = Path.Combine(geodataPath, file.Name);

                    await using (var input = await http.GetStreamAsync(file.Url, cancellationToken))
                    await using (var output = File.Create(tempPath))
                    {
                        var buffer = new byte[81920];
                        int read;
                        while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
                        {
                            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                            var bytes = Interlocked.Add(ref downloadedBytes, read);
                            progress.Report(new GeodataDownloadProgress
                            {
                                FilesDone = downloadedFiles,
                                TotalFiles = files.Count,
                                BytesDone = bytes,
                                TotalBytes = totalBytes,
                                CurrentFile = file.Name
                            });
                        }
                    }

                    File.Move(tempPath, finalPath, true);
                    Interlocked.Increment(ref downloadedFiles);
                }
                finally
                {
                    throttler.Release();
                }
            });

            await Task.WhenAll(tasks);

            progress.Report(new GeodataDownloadProgress
            {
                FilesDone = files.Count,
                TotalFiles = files.Count,
                BytesDone = totalBytes,
                TotalBytes = totalBytes,
                CurrentFile = ""
            });
        }

        private static HttpClient CreateClient()
        {
            var client = new HttpClient();
            // GitHub API rejects requests without a User-Agent
            client.DefaultRequestHeaders.UserAgent.ParseAdd("L2Bot2.0-Client");
            return client;
        }
    }

    public class GeodataFile
    {
        public string Name { get; set; } = "";
        public string Url { get; set; } = "";
        public long Size { get; set; }
    }

    public class GeodataDownloadProgress
    {
        public int FilesDone { get; set; }
        public int TotalFiles { get; set; }
        public long BytesDone { get; set; }
        public long TotalBytes { get; set; }
        public string CurrentFile { get; set; } = "";
    }
}
