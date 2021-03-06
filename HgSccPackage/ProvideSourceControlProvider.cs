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
using System.Globalization;
using MsVsShell = Microsoft.VisualStudio.Shell;

namespace HgSccPackage
{
	/// <summary>
	/// This attribute registers the source control provider.
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    public sealed class ProvideSourceControlProvider : MsVsShell.RegistrationAttribute
	{
        private string _regName = null;
        private string _uiName = null;
        
        /// <summary>
		/// </summary>
        public ProvideSourceControlProvider(string regName, string uiName)
		{
            _regName = regName;
            _uiName = uiName;
    	}

        /// <summary>
        /// Get the friendly name of the provider (written in registry)
        /// </summary>
        public string RegName
        {
            get { return _regName; }
        }

        /// <summary>
        /// Get the unique guid identifying the provider
        /// </summary>
        public Guid RegGuid
        {
            get { return GuidList.guidSccProvider; }
        }

        /// <summary>
        /// Get the UI name of the provider (string resource ID)
        /// </summary>
        public string UIName
        {
            get { return _uiName; }
        }

        /// <summary>
        /// Get the package containing the UI name of the provider
        /// </summary>
        public Guid UINamePkg
        {
            get { return GuidList.guidSccProviderPkg; }
        }

        /// <summary>
        /// Get the guid of the provider's service
        /// </summary>
        public Guid SccProviderService
        {
            get { return GuidList.guidSccProviderService; }
        }

		/// <summary>
		///     Called to register this attribute with the given context.  The context
		///     contains the location where the registration inforomation should be placed.
		///     It also contains other information such as the type being registered and path information.
		/// </summary>
        public override void Register(RegistrationContext context)
		{
            // Write to the context's log what we are about to do
            context.Log.WriteLine(String.Format(CultureInfo.CurrentCulture, "SccProvider:\t\t{0}\n", RegName));

            // Declare the source control provider, its name, the provider's service 
            // and aditionally the packages implementing this provider
            using (Key sccProviders = context.CreateKey("SourceControlProviders"))
            {
                using (Key sccProviderKey = sccProviders.CreateSubkey(RegGuid.ToString("B")))
                {
                    sccProviderKey.SetValue("", RegName);
                    sccProviderKey.SetValue("Service", SccProviderService.ToString("B"));

                    using (Key sccProviderNameKey = sccProviderKey.CreateSubkey("Name"))
                    {
                        sccProviderNameKey.SetValue("", UIName);
                        sccProviderNameKey.SetValue("Package", UINamePkg.ToString("B"));

                        sccProviderNameKey.Close();
                    }

                    // Additionally, you can create a "Packages" subkey where you can enumerate the dll
                    // that are used by the source control provider, something like "Package1"="SccProvider.dll"
                    // but this is not a requirement.
                    sccProviderKey.Close();
                }

                sccProviders.Close();
            }
		}

		/// <summary>
		/// Unregister the source control provider
		/// </summary>
		/// <param name="context"></param>
        public override void Unregister(RegistrationContext context)
		{
            context.RemoveKey("SourceControlProviders\\" + GuidList.guidSccProviderPkg.ToString("B"));
		}
	}
}
