//=========================================================================
// Copyright 2011 Sergey Antonov <sergant_@mail.ru>
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
using System.Linq;
using System.Text;
using Gajatko.IniFiles;

namespace HgSccHelper
{
	//=============================================================================
	class HgPaths
	{
		const string PathsSection = "paths";

		private string hgrc_path;
		private IniFile hgrc;

		//-----------------------------------------------------------------------------
		public HgPaths(string repository_root)
		{
			hgrc_path = System.IO.Path.Combine(repository_root, @".hg\hgrc");
			hgrc = IniFile.FromFile(hgrc_path);
		}

		//-----------------------------------------------------------------------------
		public IEnumerable<string> GetAliases()
		{
			return hgrc[PathsSection].GetKeys();
		}

		//-----------------------------------------------------------------------------
		public string GetPath(string alias)
		{
			return hgrc[PathsSection][alias];
		}

		//-----------------------------------------------------------------------------
		public bool IsExists(string alias)
		{
			return hgrc[PathsSection].GetKeys().Contains(alias);
		}

		//-----------------------------------------------------------------------------
		public void Set(string alias, string path)
		{
			hgrc[PathsSection][alias] = path;
		}

		//-----------------------------------------------------------------------------
		public void Delete(string alias)
		{
			hgrc[PathsSection].DeleteKey(alias);
		}

		//-----------------------------------------------------------------------------
		public void Save()
		{
			hgrc.Save(hgrc_path);
		}
	}
}
