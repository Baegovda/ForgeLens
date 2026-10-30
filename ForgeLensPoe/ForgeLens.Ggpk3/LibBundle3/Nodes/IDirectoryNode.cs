// Copied from LibGGPK3 (https://github.com/aianlinb/LibGGPK3, commit e5872fddd932b4fd00f332f0d872a95fa9b96939) by aianlinb.
// Licensed under the GNU Affero General Public License v3.0 (see ../LICENSE). Unmodified except for this notice.
// ForgeLens private copy - must be removed before any distribution.
using System.Collections.Generic;

namespace LibBundle3.Nodes;

public interface IDirectoryNode : ITreeNode {
	/// <summary>
	/// The content will be filled by <see cref="Index.BuildTree(Index.CreateDirectoryInstance, Index.CreateFileInstance, bool)"/> and ordered by <see cref="ITreeNode.Name"/>.
	/// Do not modify them!
	/// </summary>
	public List<ITreeNode> Children { get; }
}