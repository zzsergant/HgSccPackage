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
using RestSharp.Extensions;
using System.Linq;

//=============================================================================
namespace HgSccHelper.BitBucket
{
	//=============================================================================
	class Util
	{
		public const string Api = "https://api.bitbucket.org/1.0";

		//-----------------------------------------------------------------------------
		public static string MakeRepoUrl(string username, string repo_slug)
		{
			try
			{
				var url = string.Format("https://bitbucket.org/{0}/{1}", username.UrlEncode(), repo_slug);
				return url;
			}
			catch (UriFormatException)
			{
				return "";
			}
		}

		//------------------------------------------------------------------
		private static RestClient CreateRestClient()
		{
			var client = new RestClient(Api);
			client.Proxy = System.Net.WebRequest.DefaultWebProxy;
			client.Proxy.Credentials = System.Net.CredentialCache.DefaultNetworkCredentials;

			return client;
		}

		//-----------------------------------------------------------------------------
		public static bool CheckUser(string username, string password)
		{
			if (String.IsNullOrEmpty(username))
				return false;

			var client = CreateRestClient();
			client.Authenticator = new HttpBasicAuthenticator(username, password);

			var request = new RestRequest("emails");

			var response = client.Execute<List<BitBucketEmail>>(request);
			if (response.ResponseStatus != ResponseStatus.Completed)
				return false;

			return response.Data.Count > 0;
		}

		//-----------------------------------------------------------------------------
		public static List<BitBucketRepo> GetRepositories(string username, string password)
		{
			var repositories = new List<BitBucketRepo>();

			if (String.IsNullOrEmpty(username))
				return repositories;

			var client = CreateRestClient();
			client.Authenticator = new HttpBasicAuthenticator(username, password);

			var request = new RestRequest("users/{username}");
			request.AddParameter("username", username, ParameterType.UrlSegment);

			var response = client.Execute<BitBucketUser>(request);

			if (response.ResponseStatus != ResponseStatus.Completed)
				return repositories;

			if (response.Data == null)
				return repositories;

			// Bitbucket supports Hg and Git repositories, but we need only Hg

			var hg_repos =
				response.Data.Repositories.Where(
					repo => StringComparer.InvariantCultureIgnoreCase.Compare(repo.Scm, "hg") == 0);

			return hg_repos.ToList();
		}

		//-----------------------------------------------------------------------------
		public static BitBucketRepo NewRepository(string username, string password, string repo_name, bool is_private)
		{
			if (String.IsNullOrEmpty(username))
				return null;

			var client = CreateRestClient();
			client.Authenticator = new HttpBasicAuthenticator(username, password);

			var request = new RestRequest("repositories/", Method.POST);
			request.AddParameter("name", repo_name);
			if (is_private)
				request.AddParameter("is_private", is_private);
			request.AddParameter("scm", "hg");

			var response = client.Execute<BitBucketRepo>(request);

			if (response.ResponseStatus != ResponseStatus.Completed)
				return null;

			if (response.Data == null)
				return null;

			return response.Data;
		}

		//-----------------------------------------------------------------------------
		public static bool DeleteRepository(string username, string password, string repo_slug)
		{
			if (String.IsNullOrEmpty(username))
				return false;

			var client = CreateRestClient();
			client.Authenticator = new HttpBasicAuthenticator(username, password);

			var request = new RestRequest("repositories/{user}/{repo_slug}", Method.DELETE);
			request.AddParameter("user", username, ParameterType.UrlSegment);
			request.AddParameter("repo_slug", repo_slug, ParameterType.UrlSegment);

			var response = client.Execute(request);

			return response.ResponseStatus == ResponseStatus.Completed;
		}
	}

	//-----------------------------------------------------------------------------
	public class BitBucketEmailInfo
	{
		private List<BitBucketEmail> Emails { get; set; }
	}

	//-----------------------------------------------------------------------------
	public class BitBucketEmail
	{
		public bool Active { get; set; }
		public string Email { get; set; }
		public bool Primary { get; set; }
	}

	//==================================================================
	public class BitBucketRepoList
	{
		public List<BitBucketRepo> Repositories { get; set; }
	}

	//==================================================================
	public class BitBucketRepo
	{
		public string Website { get; set; }
		public string Slug { get; set; }
		public string Name { get; set; }
		public string Owner { get; set; }
		public int FollowersCount { get; set; }
		public string Description { get; set; }
		public string Scm { get; set; }
	}

	//=============================================================================
	public class BitBucketUser
	{
		public List<BitBucketRepo> Repositories { get; set; }
		public string Username { get; set; }
		public string FirstName { get; set; }
		public string LastName { get; set; }
		public string Avatar { get; set; }
		public List<string> Email { get; set; }
		public string ResourceUri { get; set; }
	}
}
