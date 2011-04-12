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
using System.Web;
using RestSharp;

namespace HgSccHelper.Kiln
{
	//=============================================================================
	class Session
	{
		public string Token { get; private set; }
		public string Site { get; private set; }

		private static Session instance = new Session();

		//-----------------------------------------------------------------------------
		static Session()
		{
		}

		//-----------------------------------------------------------------------------
		private Session()
		{
			Token = "";
			Site = "";
		}

		//-----------------------------------------------------------------------------
		public static Session Instance
		{
			get { return instance; }
		}

		//-----------------------------------------------------------------------------
		private string Api
		{
			get { return Site + "Kiln/Api/1.0/"; }
		}

		//------------------------------------------------------------------
		private RestClient CreateRestClient()
		{
			var client = new RestClient(Api);
			client.Proxy = System.Net.WebRequest.DefaultWebProxy;
			client.Proxy.Credentials = System.Net.CredentialCache.DefaultNetworkCredentials;

			return client;
		}

		//-----------------------------------------------------------------------------
		public bool Login(string site, string username, string password)
		{
			Token = "";
			Site = site;
			
			if (String.IsNullOrEmpty(site))
				return false;

			if (!Site.EndsWith("/"))
				Site += "/";

			if (String.IsNullOrEmpty(username))
				return false;

			var client = CreateRestClient();
			
			var request = new RestRequest("Auth/Login");
			request.AddParameter("sUser", username);
			request.AddParameter("sPassword", password);

			var response = client.Execute(request);
			if (response.ResponseStatus != ResponseStatus.Completed)
				return false;

			if (response.Content == null || response.Content.Contains("errors"))
				return false;

			Token = response.Content.UnQuote();
			return IsValid;
		}

		//-----------------------------------------------------------------------------
		public List<KilnProject> GetProjects()
		{
			var projects = new List<KilnProject>();

			if (!IsValid)
				return projects;

			var client = CreateRestClient();
			
			var request = new RestRequest("Project");
			request.AddParameter("token", Token);

			var response = client.Execute<List<KilnProject>>(request);
			if (response.ResponseStatus != ResponseStatus.Completed)
				return projects;

			if (response.Data == null)
				return projects;

			return response.Data;
		}

		//-----------------------------------------------------------------------------
		public KilnRepo CreateRepository(int repo_group, string name)
		{
			if (!IsValid)
				return null;

			var client = CreateRestClient();

			var request = new RestRequest("Repo/Create", Method.POST);
			request.AddParameter("ixRepoGroup", repo_group);
			request.AddParameter("sName", name);
			request.AddParameter("token", Token);

			var response = client.Execute<KilnRepo>(request);
			if (response.ResponseStatus != ResponseStatus.Completed)
				return null;

			return response.Data;
		}

		//-----------------------------------------------------------------------------
		public KilnRepo GetRepository(int ix_repo)
		{
			if (!IsValid)
				return null;

			var client = CreateRestClient();

			var request = new RestRequest("Repo/{ixRepo}");
			request.AddParameter("ixRepo", ix_repo, ParameterType.UrlSegment);
			request.AddParameter("token", Token);

			var response = client.Execute<KilnRepo>(request);
			if (response.ResponseStatus != ResponseStatus.Completed)
				return null;

			return response.Data;
		}

		//-----------------------------------------------------------------------------
		public bool DeleteRepository(int ix_repo)
		{
			if (!IsValid)
				return false;

			var client = CreateRestClient();

			var request = new RestRequest("Repo/{ixRepo}/Delete", Method.POST);
			request.AddParameter("ixRepo", ix_repo, ParameterType.UrlSegment);
			request.AddParameter("token", Token);

			var response = client.Execute(request);
			if (response.ResponseStatus != ResponseStatus.Completed)
				return false;

			return true;
		}

		//-----------------------------------------------------------------------------
		public bool IsValid
		{
			get { return !String.IsNullOrEmpty(Token); }
		}

		//-----------------------------------------------------------------------------
		public string MakeRepoUrl(string project_slug, string group_slug, string repo_slug)
		{
			var url = string.Format("{0}Repo/{1}/{2}/{3}", Site, project_slug, group_slug, repo_slug);
			return url;
		}
	}

/*
	//-----------------------------------------------------------------------------
	internal class KilnToken
	{
		public List<KilnError> Errors { get; set; }
		public string Token { get; set; }
	}
*/

	//-----------------------------------------------------------------------------
	internal class KilnErrors
	{
		public List<KilnError> Errors { get; set; }
	}

	//-----------------------------------------------------------------------------
	internal class KilnError
	{
		public string codeError { get; set; }
		public string sError { get; set; }
	}

	//-----------------------------------------------------------------------------
	public class KilnProjectsArray : List<KilnProject>
	{
	}

	//-----------------------------------------------------------------------------
	public class KilnProject
	{
		public int ixProject { get; set; } // A unique project identifier
		public string sSlug { get; set; } // A unique project URL slug
		public string sName { get; set; } // the project name
		public string sDescription { get; set; } // the project description
		public string permissionDefault { get; set; } // the default project permission
		public List<KilnGroup> repoGroups { get; set; } // a list of repository group records belonging to the project
	}

	//-----------------------------------------------------------------------------
	public class KilnGroup
	{
		public int ixRepoGroup { get; set; } // a unique repository group identifier
		public int ixProject { get; set; } // the project the repository group belongs to
		public string sSlug { get; set; } // a unique repository group URL slug
		public string sName { get; set; } // the repository group name
		public List<KilnRepo> repos { get; set; } // a list of repository records belonging to the repository group

		//-----------------------------------------------------------------------------
		public string DisplayName
		{
			get
			{
				if (!string.IsNullOrEmpty(sName))
					return sName;

				return "Group";
			}
		}
	}

	//-----------------------------------------------------------------------------
	public class KilnRepo
	{
		public int ixRepo { get; set; } // a unique repository identifier
		public int ixRepoGroup { get; set; } // the repository group the repository belongs to
		public int? ixParent { get; set; } // the repository this repository was branched from; null if this repository was not branched from any repository
		public bool fCentral { get; set; } // returns true if the repository is central, false otherwise
		public string sSlug { get; set; } // a unique repository URL slug
		public string sGroupSlug { get; set; } // the repository's repository group unique URL slug
		public string sProjectSlug { get; set; } // the repository's project unique URL slug
		public string sName { get; set; } // the repository name
		public string sDescription { get; set; } // the repository description
		public string sStatus { get; set; } // the status of the repository in the backend (one of "error", "new", "good", "conflicted", or "deleted")
//		public bytesSize: the size of the repository in bytes, may be null if the Kiln backend has yet to tally this information
		public KilnPerson personCreator { get; set; } //: the creator's person record
		public string permissionDefault { get; set; } // the default repository permission
		public List<KilnRepo> repoBranches { get; set; } // a list of repository records that are branches of this repository; the list is empty if this repository is not a central repository (only central repositories may have branch repositories, same as the website user interface)
	}

	//-----------------------------------------------------------------------------
	public class KilnPerson
	{
		public int ixPerson { get; set; }
		public string sEmail { get; set; }
		public string sName { get; set; }
	}
}
