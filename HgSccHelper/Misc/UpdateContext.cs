using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HgSccHelper
{
	//==================================================================
	public class UpdateContext
	{
		public bool IsParentChanged { get; set; }
		public bool IsTagsChanged { get; set; }
		public bool IsBranchChanged { get; set; }
		public bool IsCommited { get; set; }
		public bool IsBookmarksChanged { get; set; }

		//------------------------------------------------------------------
		public void MergeWith(UpdateContext context)
		{
			IsParentChanged |= context.IsParentChanged;
			IsTagsChanged |= context.IsTagsChanged;
			IsBranchChanged |= context.IsBranchChanged;
			IsCommited |= context.IsCommited;
			IsBookmarksChanged |= context.IsBookmarksChanged;
		}
	}
}
