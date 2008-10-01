using System;
namespace HgSccHelper
{
	//-----------------------------------------------------------------------------
	public enum SccErrors
	{
		Ok = 0,

		InitializeFailed = -1,
		UnknownProject = -2,
		CouldNotCreateProject = -3,
		NotCheckedOut = -4,
		AllreadyCheckedOut = -5,
		FileIsLocked = -6,
		FileOutExclusive = -7,
		AccessFailure = -8,
		CheckInConflict = -9,
		FileAllreadyExists = -10,
		FileNotControlled = -11,
		FileIsCheckedOut = -12,
		NoSpecifiedVersion = -13,
		OpNotSupported = -14,
		NonSpecificError = -15,
		OpNotPerformed = -16,
		TypeNotSupported = -17,
		VerifyMerge = -18,
		FixMerge = -19,
		ShellFailure = -20,
		InvalidUser = -21,
		ProjectAllreadyOpen = -22,
		ProjSyntaxErr = -23,
		InvalidFilePath = -24,
		ProjNotOpen = -25,
		NotAuthorized = -26,
		FileSyntaxErr = -27,
		FileNotExist = -28,
		ConnectionFailure = -29,
		UnknownError = -30,
		BackGroundGetInProgress = -31,

		I_ShareSubProjOk = 7,
		I_FileDiffers = 6,
		I_ReloadFile = 5,
		I_FileNotAffected = 4,
		I_ProjectCreated = 3,
		I_OperationCanceled = 2,
		I_AdvSupport = 1
	}

	//-----------------------------------------------------------------------------
	[Flags]
	public enum SccStatus : uint
	{
		None	= 0,
		Invalid = 0xFFFFFFFF,			// Status could not be obtained, don't rely on it
		NotControlled = 0x00000000,	// File is not under source control
		Controlled = 0x00000001,	// File is under source code control
		CheckedOut = 0x00000002,	// Checked out to current user at local path
		OutOther = 0x00000004,	// File is checked out to another user
		OutExclusive = 0x00000008,	// File is exclusively check out
		OutMultiple = 0x00000010,	// File is checked out to multiple people
		OutOfDate = 0x00000020,	// The file is not the most recent
		Deleted = 0x00000040,	// File has been deleted from the project
		Locked = 0x00000080,	// No more versions allowed
		Merged = 0x00000100,	// File has been merged but not yet fixed/verified
		Shared = 0x00000200,	// File is shared between projects
		Pinned = 0x00000400,	// File is shared to an explicit version
		Modified = 0x00000800,	// File has been modified/broken/violated
		OutByUser = 0x00001000,	// File is checked out by current user someplace
		NoMerge = 0x00002000,	// File is never mergeable and need not be saved before a GET
		Reserved1 = 0x00004000,	// Status bit reserved for internal use
		Reserved2 = 0x00008000,	// Status bit reserved for internal use
		Reserved3 = 0x00010000 	// Status bit reserved for internal use
	};

	[Flags]
	public enum SccCaps : uint
	{
		None			  = 0,
		Remove            = 0x00000001, // Supports the SCC_Remove command
		Rename            = 0x00000002, // Supports the SCC_Rename command
		Diff              = 0x00000004, // Supports the SCC_Diff command
		History           = 0x00000008, // Supports the SCC_History command
		Properties        = 0x00000010, // Supports the SCC_Properties command
		RunScc            = 0x00000020, // Supports the SCC_RunScc command
		GetCommandOptions = 0x00000040, // Supports the SCC_GetCommandOptions command
		QueryInfo         = 0x00000080, // Supports the SCC_QueryInfo command
		GetEvents         = 0x00000100, // Supports the SCC_GetEvents command
		GetProjPath       = 0x00000200, // Supports the SCC_GetProjPath command
		AddFromScc        = 0x00000400, // Supports the SCC_AddFromScc command
		CommentCheckOut   = 0x00000800, // Supports a comment on Checkout
		CommentCheckIn    = 0x00001000, // Supports a comment on Checkin
		CommentAdd        = 0x00002000, // Supports a comment on Add
		CommentRemove     = 0x00004000, // Supports a comment on Remove
		TextOut           = 0x00008000, // Writes text to an IDE-provided output function
		CreateSubProject  = 0x00010000, // Supports the SccCreateSubProject command
		GetParentProject  = 0x00020000, // Supports the SccGetParentProjectPath command
		Batch             = 0x00040000, // Supports the SccBeginBatch and SccEndBatch commands
		DirectoryStatus   = 0x00080000, // Supports the querying of directory status
		DirectoryDiff     = 0x00100000, // Supports differencing on directories
		AddStoreLatest    = 0x00200000, // Supports storing files without deltas
		HistoryMultFile  = 0x00400000, // Multiple file history is supported
		IgnoreCase        = 0x00800000, // Supports case insensitive file comparison
		IgnoreSpace       = 0x01000000, // Supports file comparison that ignores white space
		PopulateList      = 0x02000000, // Supports finding extra files
		CommentProject    = 0x04000000, // Supports comments on create project
		MultiCheckOut     = 0x08000000, // Supports multiple checkouts on a file
										//   (subject to administrator override)
		DiffAlways        = 0x10000000, // Supports diff in all states if under control
		GetNoUI           = 0x20000000, // Provider doesn't support a UI for SccGet,
										//   but IDE may still call SccGet function.
		Reentrant		  = 0x40000000, // Provider is reentrant and thread safe.
		SccFile           = 0x80000000  // Supports the MSSCCPRJ.SCC file
										//   (subject to user/administrator override)
	}

	//-----------------------------------------------------------------------------
	public enum SccExCaps
	{
		CheckOutLocalVer		= 1,   // Supports the Checkout local version
		BackgroundGet			= 2,   // Supports the SccBackgroundGet operation
		EnumChangedFiles		= 3,   // Supports the SccEnumChangedFiles operation
		PopulateListDir			= 4,   // Supports finding extra directories
		QueryChanges			= 5,   // Supports enumerating file changes
		AddFilesFromScc			= 6,   // Supports the SccAddFilesFromSCC operation
		GetUserOptions			= 7,   // Supports the SccGetUserOption function
		ThreadSafeQueryInfo		= 8,   // Supports calling SccQueryInfo on multiple threads
		RemoveDir				= 9,   // Supports the SccRemoveDir function
		DeleteCheckedOut		= 10,  // Can delete checked out files
		RenameCheckedOut		= 11   // Can rename checked out files
	}

	//-----------------------------------------------------------------------------
	[Flags]
	public enum SccOpenProjectFlags : uint
	{
		None = 0,
		CreateIfNew = 0x00000001,
		SilentOpen = 0x00000002
	}

	//-----------------------------------------------------------------------------
	public class SccFileInfo
	{
		public string File { get; set; }
		public SccStatus Status { get; set; }
	}

	[Flags]
	public enum SccAddFlags
	{
		AddStoreLatest			= 0x04,	// Store only the latest version of the file(s).
		FileTypeAuto			= 0x00,	// Auto-detect type of the file(s).

		// The following flags are mutually exculsive.
		FileTypeTex				= 0x01,	// Obsolete. Use SCC_FILETYPE_TEXT_ANSI instead.
		FileTypeBinary			= 0x02,	// Treat the file(s) as binary.
		FileTypeTextAnsi		= 0x08,	// Treat the file(s) as ANSI.
		FileTypeUTF8			= 0x10,	// Treat the file(s) as Unicode in UTF8 format.
		FileTypeUTF16LE			= 0x20,	// Treat the file(s) as Unicode in UTF16 Little Endian format.
		FileTypeUTF16BE			= 0x40,	// Treat the file(s) as Unicode in UTF16 Big Endian format.
	}

	//-----------------------------------------------------------------------------
	public class SccAddFile
	{
		public string File { get; set; }
		public SccAddFlags Flags { get; set; }
	}

	//-----------------------------------------------------------------------------
	[Flags]
	public enum SccDiffFlags
	{
		None					= 0x0000,
		IgnoreCase				= 0x0002,
		IgnoreSpace				= 0x0004,
		QdContents				= 0x0010,
		QdCheckSum				= 0x0020,
		QdTime					= 0x0040,
		QuickDiff				= 0x0070		/* Any QD means no display     */
	}

	//-----------------------------------------------------------------------------
	public enum SccCommand
	{
		Get,
		CheckOut,
		CheckIn,
		UnCheckOut,
		Add,
		Remove,
		Diff,
		History,
		Rename,
		Properties,
		Options
	};

}