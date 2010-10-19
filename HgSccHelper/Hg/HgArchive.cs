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
using System.Linq;
using System.Text;

//=============================================================================
namespace HgSccHelper
{
	//------------------------------------------------------------------
	public enum HgArchiveOptions
	{
		None,
		NoDecode
	}

	//-----------------------------------------------------------------------------
	public enum HgArchiveTypes
	{
		Files,		// a directory full of files (default)
		Tar,		// tar archive, uncompressed
		TarBzip2,	// tar archive, compressed using bzip2
		Gzip,		// tar archive, compressed using gzip
		Uzip,		// zip archive, uncompressed
		Zip			// zip archive, compressed using deflate
	}

	//=============================================================================
	public class HgArchive
	{
		//------------------------------------------------------------------
		public bool Archive(string work_dir, string revision, HgArchiveOptions options,
			HgArchiveTypes archive_type, string destination)
		{
			var args = new HgArgsBuilder();
			args.Append("archive");

			if (options == HgArchiveOptions.NoDecode)
				args.Append("--no-decode");

			var type_str = archive_type.HgTypeString();
			if (!String.IsNullOrEmpty(type_str))
			{
				args.Append("--type");
				args.Append(type_str);
			}

			if (revision.Length > 0)
			{
				// FIXME: Why revision with quote ?
				args.Append("--rev");
				args.Append(revision.Quote());
			}

			args.AppendPath(destination);

			if (args.Length >= Hg.MaxCmdLength)
				throw new HgCommandLineException("Archive");

			var hg = new Hg();
			return hg.RunHg(work_dir, args.ToString());
		}
	}

	//=============================================================================
	static public class HgArchiveUtil
	{
		//-----------------------------------------------------------------------------
		public static string HgTypeString(this HgArchiveTypes archive_type)
		{
			switch (archive_type)
			{
				case HgArchiveTypes.Files:		return "files";
				case HgArchiveTypes.Tar:		return "tar";
				case HgArchiveTypes.TarBzip2:	return "tbz2";
				case HgArchiveTypes.Gzip:		return "tgz";
				case HgArchiveTypes.Uzip:		return "uzip";
				case HgArchiveTypes.Zip:		return "zip";
			}

			throw new ArgumentException("Invalid archive type");
		}

		//-----------------------------------------------------------------------------
		public static string Description(this HgArchiveTypes archive_type)
		{
			switch (archive_type)
			{
				case HgArchiveTypes.Files:		return "A directory full of files (default)";
				case HgArchiveTypes.Tar:		return "Tar archive, uncompressed";
				case HgArchiveTypes.TarBzip2:	return "Tar archive, compressed using bzip2";
				case HgArchiveTypes.Gzip:		return "Tar archive, compressed using gzip";
				case HgArchiveTypes.Uzip:		return "Zip archive, uncompressed";
				case HgArchiveTypes.Zip:		return "Zip archive, compressed using deflate";
			}

			throw new ArgumentException("Invalid archive type");
		}

		//-----------------------------------------------------------------------------
		public static string FileExtension(this HgArchiveTypes archive_type)
		{
			switch (archive_type)
			{
				case HgArchiveTypes.Files:		return "";
				case HgArchiveTypes.Tar:		return ".tar";
				case HgArchiveTypes.TarBzip2:	return ".tar.bz2";
				case HgArchiveTypes.Gzip:		return ".tar.gz";
				case HgArchiveTypes.Uzip:		return ".zip";
				case HgArchiveTypes.Zip:		return ".zip";
			}

			throw new ArgumentException("Invalid archive type");
		}
	}
}
