//=========================================================================
// Copyright 2009 Sergey Antonov <sergant_@mail.ru>
// 
// This software may be used and distributed according to the terms of the
// GNU General Public License version 2 as published by the Free Software
// Foundation.
// 
// See the file COPYING.TXT for the full text of the license, or see
// http://www.gnu.org/licenses/gpl-2.0.txt
// 
//=========================================================================

//=============================================================================
using HgSccHelper;

namespace HgSccPackage
{
	//=============================================================================
	public sealed class HgSccOptions
	{
		public bool UseSccBindings { get; set; }
		public bool CheckProjectsForMercurialRepository { get; set; }

		static readonly HgSccOptions instance = new HgSccOptions();

		// Explicit static constructor to tell C# compiler
		// not to mark type as beforefieldinit
		static HgSccOptions()
		{
		}

		//-----------------------------------------------------------------------------
		private HgSccOptions()
		{
			UseSccBindings = true;
			CheckProjectsForMercurialRepository = true;

			Load();
		}

		//-----------------------------------------------------------------------------
		public static HgSccOptions Instance
		{
			get
			{
				return instance;
			}
		}
		
		//-----------------------------------------------------------------------------
		public static void Save()
		{
			Cfg.Set("", "UseSccBindings", Instance.UseSccBindings);
			Cfg.Set("", "CheckProjectsForMercurialRepository", Instance.CheckProjectsForMercurialRepository);
		}

		//-----------------------------------------------------------------------------
		private void Load()
		{
			bool use_scc_bindings;
			bool check_projects_for_repo;

			if (Cfg.Get("", "UseSccBindings", out use_scc_bindings, UseSccBindings))
				UseSccBindings = use_scc_bindings;

			if (Cfg.Get("", "CheckProjectsForMercurialRepository", out check_projects_for_repo, CheckProjectsForMercurialRepository))
				CheckProjectsForMercurialRepository = check_projects_for_repo;
		}
	}
}