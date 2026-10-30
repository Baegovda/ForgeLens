// Copied from LibGGPK3 (https://github.com/aianlinb/LibGGPK3, commit e5872fddd932b4fd00f332f0d872a95fa9b96939) by aianlinb.
// Licensed under the GNU Affero General Public License v3.0 (see ../LICENSE). Unmodified except for this notice.
// ForgeLens private copy - must be removed before any distribution.
using System;

namespace LibGGPK3;
public class GGPKBrokenException : Exception {
	public GGPK? Ggpk { get; }
	public GGPKBrokenException() : base() { }
	public GGPKBrokenException(GGPK ggpk) : base() {
		Ggpk = ggpk;
	}
	public GGPKBrokenException(string message) : base(message) { }
	public GGPKBrokenException(GGPK ggpk, string message) : base(message) {
		Ggpk = ggpk;
	}
	public GGPKBrokenException(string message, Exception innerException) : base(message, innerException) { }
	public GGPKBrokenException(GGPK ggpk, string message, Exception innerException) : base(message, innerException) {
		Ggpk = ggpk;
	}
}