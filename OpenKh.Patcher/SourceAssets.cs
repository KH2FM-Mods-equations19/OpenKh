using OpenKh.Common;
using OpenKh.Egs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xe.IO;

namespace OpenKh.Patcher
{
    public interface ISourceAssets : IDisposable
    {
        bool Exists(string path);
        Stream OpenRead(string path);
    }

    public class PlainSourceAssets : ISourceAssets
    {
        private string _basePath;

        public PlainSourceAssets(string baseDirectory)
        {
            _basePath = baseDirectory;
        }

        private string GetFullPath(string path) => Path.Combine(_basePath, path);
        public bool Exists(string path) => File.Exists(GetFullPath(path));
        public Stream OpenRead(string path) => File.OpenRead(GetFullPath(path));
        public void Dispose() { }
    }

    public class EpicGamesSourceAssets : ISourceAssets
    {
        internal record HedEntry(string FileName, Hed.Entry Entry, string PkgFilePath);

        internal record FoundHedAsset(HedEntry Entry, string RemasteredSubAssetName);

        private static readonly string[] Kh2Pkgs = new string[]
        {
            "kh2_first",
            "kh2_second",
            "kh2_third",
            "kh2_fourth",
            "kh2_fifth",
            "kh2_sixth",
        };
        private string _basePath;
        private readonly Dictionary<string, HedEntry> _entries = new();
        private readonly Dictionary<string, FileStream> _pkgStreams = new();

        public EpicGamesSourceAssets(string baseDirectory)
        {
            _basePath = Path.Combine(baseDirectory, "Image", "en");
            Kh2Pkgs.AsParallel().ForAll(baseFileName =>
            {
                var hedFilePath = Path.Combine(_basePath, $"{baseFileName}.hed");
                if (!File.Exists(hedFilePath))
                    return;

                using var hedStream = new FileStream(hedFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var foundEntries = Hed.Read(hedStream)
                    .Select(entry =>
                    {
                        var hash = Egs.Helpers.ToString(entry.MD5);
                        if (EgsTools.Names.TryGetValue(hash, out var fileName))
                            return (fileName, entry);
                        return (null, null);
                    })
                    .ToList();

                lock (_entries)
                {
                    foreach (var entry in foundEntries)
                        _entries[entry.fileName] = new HedEntry(
                            entry.fileName,
                            entry.entry,
                            baseFileName
                        );
                }
            });

            _pkgStreams = Kh2Pkgs.ToDictionary(x => x, x => File.OpenRead(Path.Combine(_basePath, $"{x}.pkg")));
        }

        private static string NormalizePath(string path)
        {
            // Existing mods used slashes and backslashes interchangeably, so support both by normalizing
            return path.Replace("\\", "/");
        }

        private FoundHedAsset FindHedAsset(string path)
        {
            path = NormalizePath(path);
            if (_entries.ContainsKey(path))
                return new FoundHedAsset(_entries[path], null);

            // remastered/ isn't actually a path in the game packages
            if (!path.StartsWith("remastered/"))
                return null;
            path = path.Substring("remastered/".Length);
            if (_entries.ContainsKey(path))
                return new FoundHedAsset(_entries[path], null);

            // If it's something like remastered/menu/us/title.2ld/US_title_2ld0.png, there should be an entry for
            // menu/us/title.2ld with individual HD sub-assets
            var parentPath = NormalizePath(Path.GetDirectoryName(path));
            if (!_entries.ContainsKey(parentPath))
                return null;

            var fileName = Path.GetFileName(path);
            var entry = _entries[parentPath];
            var pkgStream = _pkgStreams[entry.PkgFilePath];
            lock (pkgStream)
            {
                var hdAsset = new EgsHdAsset(pkgStream.SetPosition(entry.Entry.Offset));
                if (hdAsset.Assets.Contains(fileName))
                    return new FoundHedAsset(entry, fileName);
            }

            return null;
        }

        public bool Exists(string path)
        {
            return FindHedAsset(path) != null;
        }

        public Stream OpenRead(string path)
        {
            var hedAsset = FindHedAsset(path);
            if (hedAsset == null)
                throw new FileNotFoundException("Couldn't find an entry for path " + path);

            var entry = hedAsset.Entry;
            var pkgStream = _pkgStreams[entry.PkgFilePath];
            lock (pkgStream)
            {
                var remasteredSubAssetName = hedAsset.RemasteredSubAssetName;
                var hdAsset = new EgsHdAsset(pkgStream.SetPosition(entry.Entry.Offset));
                if (remasteredSubAssetName == null)
                    return new MemoryStream(hdAsset.OriginalData);
                else
                {
                    var decompressedData = hdAsset.RemasteredAssetsDecompressedData;
                    if (decompressedData.ContainsKey(remasteredSubAssetName))
                        return new MemoryStream(decompressedData[remasteredSubAssetName]);
                }
            }

            throw new FileNotFoundException("Couldn't find an entry for path " + path);
        }
        public void Dispose()
        {
            foreach (var stream in _pkgStreams)
                stream.Value.Dispose();
        }
    }
}
