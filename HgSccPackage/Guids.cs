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
        public static readonly Guid guidSccProvider = new Guid("{A7F26CA1-0000-4729-896E-0BBE9E380635}");
        // The guid of the source control provider service (implementing IVsSccProvider interface)
        public static readonly Guid guidSccProviderService = new Guid("{A7F26CA1-1000-4729-896E-0BBE9E380635}");
        // The guid of the source control provider package (implementing IVsPackage interface)
        public static readonly Guid guidSccProviderPkg = new Guid("{A7F26CA1-2000-4729-896E-0BBE9E380635}");
        // Other guids for menus and commands
        public static readonly Guid guidSccProviderCmdSet = new Guid("{A7F26CA1-3000-4729-896E-0BBE9E380635}");
    };
}
