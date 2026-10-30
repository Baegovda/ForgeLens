// Copied from LibGGPK3 (https://github.com/aianlinb/LibGGPK3, commit e5872fddd932b4fd00f332f0d872a95fa9b96939) by aianlinb.
// Licensed under the GNU Affero General Public License v3.0 (see ../LICENSE). Unmodified except for this notice.
// ForgeLens private copy - must be removed before any distribution.
using SystemExtensions.Streams;

namespace LibGGPK3.Records;
/// <summary>
/// GGPK record is the very first record and exists at the very beginning of the GGPK file.
/// </summary>
public class GGPKRecord : BaseRecord {
	/// <summary>GGPK</summary>
	public const int Tag = 0x4B504747;

	/// <summary>
	/// 3 for PC, 4 for Mac, 2 for gmae-version before 3.11.2 which has no bundle in ggpk.
	/// </summary>
	public uint GGPKVersion { get; }

	public long RootDirectoryOffset { get; protected internal set; }
	public long FirstFreeRecordOffset { get; protected internal set; }

	protected internal GGPKRecord(uint length, GGPK ggpk) : base(length, ggpk) {
		Offset = ggpk.baseStream.Position - 8;
		GGPKVersion = ggpk.baseStream.Read<uint>(); // 3 for PC, 4 for Mac
		RootDirectoryOffset = ggpk.baseStream.Read<long>();
		FirstFreeRecordOffset = ggpk.baseStream.Read<long>();
	}

	protected internal override void WriteRecordData() {
		var s = Ggpk.baseStream;
		Offset = s.Position;
		s.Write(Length); // 28
		s.Write(Tag);
		s.Write(GGPKVersion); // 3 for PC, 4 for Mac
		s.Write(RootDirectoryOffset);
		s.Write(FirstFreeRecordOffset);
	}
}