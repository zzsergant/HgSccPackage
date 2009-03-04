using System;

namespace HgSccPackage
{
	/// <summary>
	/// This class is used only to expose the list of Guids used by this package.
	/// This list of guids must match the set of Guids used inside the VSCT file.
	/// </summary>
    public static class GuidList
    {
	// Now define the list of guids as public static members.
   
        // Unique ID of the source control provider; this is also used as the command UI context to show/hide the pacakge UI
        public static readonly Guid guidSccProvider = new Guid("{B0BAC05D-0000-41D1-A6C3-704E6C1A3DE2}");
        // The guid of the source control provider service (implementing IVsSccProvider interface)
        public static readonly Guid guidSccProviderService = new Guid("{B0BAC05D-1000-41D1-A6C3-704E6C1A3DE2}");
        // The guid of the source control provider package (implementing IVsPackage interface)
        public static readonly Guid guidSccProviderPkg = new Guid("{B0BAC05D-2000-41D1-A6C3-704E6C1A3DE2}");
        // Other guids for menus and commands
        public static readonly Guid guidSccProviderCmdSet = new Guid("{B0BAC05D-5743-4FEB-A929-2938249CBA26}");
    };
}
