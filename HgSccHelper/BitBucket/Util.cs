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
using System.Text;
using System.Web;
using RestSharp;

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
				var url = string.Format("https://bitbucket.org/{0}/{1}", HttpUtility.UrlEncode(username), repo_slug);
				return url;
			}
			catch (UriFormatException)
			{
				return "";
			}
		}

		//-----------------------------------------------------------------------------
		public static bool CheckUser(string username, string password)
		{
			if (String.IsNullOrEmpty(username))
				return false;

			var client = new RestClient(Api);
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

			var client = new RestClient(Api);
			client.Authenticator = new HttpBasicAuthenticator(username, password);

			var request = new RestRequest("users/{username}");
			request.AddParameter("username", username, ParameterType.UrlSegment);

			var response = client.Execute<BitBucketUser>(request);

			if (response.ResponseStatus != ResponseStatus.Completed)
				return repositories;

			if (response.Data == null)
				return repositories;

			return response.Data.Repositories;
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
	}

	//=============================================================================
	public class BitBucketUser
	{
		public List<BitBucketRepo> Repositories { get; set; }
		public string Username { get; set; }
		public string FirstName { get; set; }
		public string LastName { get; set; }
		public string Avatart { get; set; }
		public List<string> Email { get; set; }
		public string ResourceUri { get; set; }
	}
}
