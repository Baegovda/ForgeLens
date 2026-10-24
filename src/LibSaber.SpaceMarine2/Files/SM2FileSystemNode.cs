using LibSaber.FileSystem;
using LibSaber.SpaceMarine2.Structures.Resources;

namespace LibSaber.SpaceMarine2.Files;

public class SM2FileSystemNode : FileSystemNode
{

    #region Properties

    public fioZIP_CACHE_FILE.ENTRY Entry { get; }
    public long SizeInBytes { get; }

    /// <summary>
    /// Archive-relative path using '/' separators, as stored in the cache.
    /// </summary>
    public string RelativePath => (Entry.FileName ?? string.Empty).Replace('\\', '/');

    #endregion

    #region Constructor

    public SM2FileSystemNode( IFileSystemDevice device, fioZIP_CACHE_FILE.ENTRY entry, IFileSystemNode parent = null)
        : base( device, entry.FileName, parent )
    {
        Entry = entry;
        SizeInBytes = entry.Size;
    }

    #endregion

}
