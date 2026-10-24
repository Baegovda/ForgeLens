using LibSaber.IO;
using LibSaber.Serialization;
using LibSaber.SpaceMarine2.Structures.Resources;

namespace LibSaber.SpaceMarine2.Serialization.Resources;

public class fioZIP_CACHE_FILESerializer : SM2SerializerBase<fioZIP_CACHE_FILE>
{

  public override fioZIP_CACHE_FILE Deserialize(NativeReader reader, ISerializationContext context)
  {
    var cache = new fioZIP_CACHE_FILE();

    if (reader.Length == 0)
      return cache;

    if (reader.Length < sizeof(int))
      throw new EndOfStreamException("Cache file is truncated.");

    // Update 7: Cache files now have 0x20 bytes at the top. Hash/Checksum?
    // TODO: Detect this in a better way

    bool isVersion7Plus = false;
    var peek = reader.ReadInt32();
    reader.Position -= 4;
    if (peek > 5 || peek < 1)
    {
      isVersion7Plus = true;
      reader.Position += 0x20;
    }

    // Real caches have a short trailer that is not an entry. Entries are
    // always larger than this, so a shorter tail is leftover padding.
    const int SIZE_OF_CACHE_ITEM = 30;
    const int ENTRY_SUFFIX = sizeof(long) + sizeof(long) + sizeof(long) + sizeof(short);

    while (reader.Position + SIZE_OF_CACHE_ITEM < reader.Length)
    {
      var version = reader.ReadInt32();
      _ = reader.ReadByte();

      if (reader.Length - reader.Position < sizeof(int))
        break;

      var nameLength = reader.ReadInt32();
      var extra = isVersion7Plus ? sizeof(long) : version > 5 ? sizeof(int) : 0;
      var suffix = ENTRY_SUFFIX + extra;
      if (nameLength < 0 || nameLength > reader.Length - reader.Position - suffix)
        break;

      var fileName = nameLength == 0
        ? string.Empty
        : reader.ReadFixedLengthString(nameLength);

      var entry = new fioZIP_CACHE_FILE.ENTRY
      {
        FileName = fileName,
        Offset = reader.ReadInt64(),
        Size = reader.ReadInt64(),
        CompressedSize = reader.ReadInt64(),
        CompressMethod = (fioZIP_CACHE_FILE.COMPRESS_METHOD)reader.ReadInt16(),
      };

      if (isVersion7Plus)
        _ = reader.ReadInt64();
      else if (version > 5)
        _ = reader.ReadInt32(); //CRC?

      // Keep the first copy. A repeated name used to abort the whole cache.
      cache.AddEntry(entry);
    }

    return cache;
  }

}
