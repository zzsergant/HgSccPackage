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

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

//=============================================================================
namespace HgSccHelper
{
	public class HgPkgOptions
	{
		public bool UseSccBindings { get; set; }
		public bool CheckProjectsForMercurialRepository { get; set; }

		public HgPkgOptions()
		{
			UseSccBindings = true;
			CheckProjectsForMercurialRepository = true;
		}
	}

	public sealed class HgSccOptions
	{
		static readonly HgSccOptions instance = new HgSccOptions();

		HgPkgOptions options;

		// Explicit static constructor to tell C# compiler
		// not to mark type as beforefieldinit
		static HgSccOptions()
		{
// 			instance.options = new HgPkgOptions();
		}

		//-----------------------------------------------------------------------------
		private HgSccOptions()
		{
			var new_options = Load();
			if (new_options != null)
				options = new_options;
			else
				options = new HgPkgOptions();
		}

		//-----------------------------------------------------------------------------
		public static HgPkgOptions Options
		{
			get
			{
				return instance.options;
			}
		}
		
		//-----------------------------------------------------------------------------
		public static void Save()
		{
			Cfg.Set("", "UseSccBindings", Options.UseSccBindings);
			Cfg.Set("", "CheckProjectsForMercurialRepository", Options.CheckProjectsForMercurialRepository);
		}

		//-----------------------------------------------------------------------------
		private static HgPkgOptions Load()
		{
			var options = new HgPkgOptions();

			bool use_scc_bindings = options.UseSccBindings;
			bool check_projects_for_repo = options.CheckProjectsForMercurialRepository;

			if (Cfg.Get("", "UseSccBindings", out use_scc_bindings, use_scc_bindings))
				options.UseSccBindings = use_scc_bindings;

			if (Cfg.Get("", "CheckProjectsForMercurialRepository", out check_projects_for_repo, check_projects_for_repo))
				options.CheckProjectsForMercurialRepository = check_projects_for_repo;

			return options;
		}
	}
}