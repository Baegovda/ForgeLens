// Copied from LibGGPK3 (https://github.com/aianlinb/LibGGPK3, commit e5872fddd932b4fd00f332f0d872a95fa9b96939) by aianlinb.
// Licensed under the GNU Affero General Public License v3.0 (see ../LICENSE). Unmodified except for this notice.
// ForgeLens private copy - must be removed before any distribution.
using LibBundle3.Records;

namespace LibBundle3.Nodes;
public interface IFileNode : ITreeNode {
	public abstract FileRecord Record { get; }
}