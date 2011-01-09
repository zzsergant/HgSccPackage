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

		public UpdateContextCache Cache { get; set; }

		//------------------------------------------------------------------
		public UpdateContext()
		{
			Cache = new UpdateContextCache();
		}

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

	//------------------------------------------------------------------
	public class UpdateContextCache
	{
		public IdentifyInfo CurrentRevision { get; set; }
		public RevLogChangeDesc TargetRevision { get; set; }
		public List<RevLogChangeDesc> Parents { get; set; }
		public List<BranchInfo> Branches { get; set; }
		public List<TagInfo> Tags { get; set; }
		public List<BookmarkInfo> Bookmarks { get; set; }
	}
}
