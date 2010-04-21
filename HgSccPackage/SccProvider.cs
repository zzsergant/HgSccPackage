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
using System.IO;
using System.Resources;
using System.Diagnostics;
using System.Globalization;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.ComponentModel.Design;
using Microsoft.Win32;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio;
using System.Runtime.Serialization.Formatters.Binary;
using MsVsShell = Microsoft.VisualStudio.Shell;
using ErrorHandler = Microsoft.VisualStudio.ErrorHandler;
using HgSccHelper;
using HgSccPackage.Vs;
using System.Linq;

namespace HgSccPackage
{
	/////////////////////////////////////////////////////////////////////////////
	// SccProvider
#if RELEASE_DEPLOY
	[MsVsShell.DefaultRegistryRoot("Software\\Microsoft\\VisualStudio\\9.0")]
#else
	[MsVsShell.DefaultRegistryRoot("Software\\Microsoft\\VisualStudio\\9.0Exp")]
#endif
	// Register the package to have information displayed in Help/About dialog box
	[MsVsShell.InstalledProductRegistration(false, "#100", "#101", "1.2.0.0",
		IconResourceID = CommandId.iiconProductIcon)]
	// Declare that resources for the package are to be found in the managed assembly
	// resources, and not in a satellite dll
	[MsVsShell.PackageRegistration(UseManagedResourcesOnly = true)]
	// Register the resource ID of the CTMENU section (generated from compiling the VSCT file),
	// so the IDE will know how to merge this package's menus with the rest of the IDE when
	// "devenv /setup" is run.
	// The menu resource ID needs to match the ResourceName number defined in the csproj project
	// file in the VSCTCompile section. Everytime the version number changes VS will automatically
	// update the menus on startup; if the version doesn't change, you will need to run manually
	// "devenv /setup /rootsuffix:Exp" to see VSCT changes reflected in IDE
	[MsVsShell.ProvideMenuResource(1000, 1)]
	// Register a sample options page visible as Tools/Options/SourceControl/SampleOptionsPage
	// when the provider is active
	[MsVsShell.ProvideOptionPageAttribute(typeof (SccProviderOptions),
		"Source Control", "Mercurial Options Page", 106, 107, false)]
	[ProvideToolsOptionsPageVisibility("Source Control", "Mercurial Options Page",
		"A7F26CA1-0000-4729-896E-0BBE9E380635")]
	// Register the source control provider's service (implementing IVsScciProvider interface)
	[MsVsShell.ProvideService(typeof (SccProviderService),
		ServiceName = "Mercurial Source Control Provider Service")]
	// Register the source control provider to be visible in Tools/Options/SourceControl/Plugin
	// dropdown selector
	[ProvideSourceControlProvider("Mercurial Source Control Package", "#100")]
	// Pre-load the package when the command UI context is asserted (the provider will be automatically loaded after restarting the shell if it was active last time the shell was shutdown)
	[MsVsShell.ProvideAutoLoad("A7F26CA1-0000-4729-896E-0BBE9E380635")]
	// Register the key used for persisting solution properties, so the IDE will know to load the source control package when opening a controlled solution containing properties written by this package
	[ProvideSolutionProps(_strSolutionPersistanceKey)]
	[MsVsShell.ProvideLoadKey("Standard", "1.0", "Mercurial Source Control Package", "Sergey Antonov", 104)]
	// Declare the package guid
	[Guid("A7F26CA1-2000-4729-896E-0BBE9E380635")]
	public sealed class SccProvider : MsVsShell.Package,
									  IOleCommandTarget,
									  IVsPersistSolutionProps
		// We'll write properties in the solution file to track when solution is controlled; the interface needs to be implemented by the package object
	{
		// The service provider implemented by the package
		private SccProviderService sccService = null;
		// The name of this provider (to be written in solution and project files)
		// As a best practice, to be sure the provider has an unique name, a guid like the provider guid can be used as a part of the name
		private const string _strProviderName =
			"Mercurial Source Control Package";

		// The name of the solution section used to persist provider options (should be unique)
		private const string _strSolutionPersistanceKey =
			"MercurialSourceControlSolutionProperties";

		// The name of the section in the solution user options file used to persist user-specific options (should be unique, shorter than 31 characters and without dots)
		private const string _strSolutionUserOptionsKey =
			"MercurialSourceControlProvider";

		// The names of the properties stored by the provider in the solution file
		private const string _strSolutionControlledProperty = "SolutionIsControlled";
		private const string _strSolutionBindingsProperty = "SolutionBindings";
		// Whether the solution was just added to source control and the provider needs to saved source control properties in the solution file when the solution is saved
		private bool _solutionHasDirtyProps = false;

		//------------------------------------------------------------------
		public SccProvider()
		{
			Logger.WriteLine("Entering constructor for: {0}", this.ToString());

			// Set the configuration name
			Cfg.Program = "HgSccPackage";

			// The provider implements the IVsPersistSolutionProps interface which is derived from IVsPersistSolutionOpts,
			// The base class MsVsShell.Package also implements IVsPersistSolutionOpts, so we're overriding its functionality
			// Therefore, to persist user options in the suo file we will not use the set of AddOptionKey/OnLoadOptions/OnSaveOptions 
			// set of functions, but instead we'll use the IVsPersistSolutionProps functions directly.
		}

		#region Package Members

		//------------------------------------------------------------------
		public T GetService<T>()
			where T : class
		{
			return (T)GetService(typeof(T));
		}

		//------------------------------------------------------------------
		public T GetServiceAs<T>()
			where T : class
		{
			return GetService(typeof(T)) as T;
		}

		//------------------------------------------------------------------
		public new Object GetService(Type serviceType)
		{
			return base.GetService(serviceType);
		}

		//------------------------------------------------------------------
		private static void AddMenuCommand(MsVsShell.OleMenuCommandService menu_service,
			int cmd_id, EventHandler exec_handler)
		{
			var cmd = new CommandID(GuidList.guidSccProviderCmdSet, cmd_id);
			var menuCmd = new MenuCommand(exec_handler, cmd);
			menu_service.AddCommand(menuCmd);
		}

		//------------------------------------------------------------------
		protected override void Initialize()
		{
			Logger.WriteLine("Entering Initialize() of: {0}", this.ToString());
			base.Initialize();

			// Proffer the source control service implemented by the provider
			sccService = new SccProviderService(this);
			((IServiceContainer) this).AddService(typeof (SccProviderService),
												  sccService, true);

			// Add our command handlers for menu (commands must exist in the .vsct file)
			MsVsShell.OleMenuCommandService mcs =
				GetService(typeof (IMenuCommandService)) as
				MsVsShell.OleMenuCommandService;

			if (mcs != null)
			{
				// Source control menu commmads
				AddMenuCommand(mcs, CommandId.icmdAddToSourceControl, Exec_icmdAddToSourceControl);
				AddMenuCommand(mcs, CommandId.icmdClone, Exec_icmdClone);
				AddMenuCommand(mcs, CommandId.icmdSynchronize, Exec_icmdSynchronize);
				AddMenuCommand(mcs, CommandId.icmdCommit, Exec_icmdCommit);
				AddMenuCommand(mcs, CommandId.icmdRevert, Exec_icmdRevert);
				AddMenuCommand(mcs, CommandId.icmdCompare, Exec_icmdCompare);
				AddMenuCommand(mcs, CommandId.icmdViewHistory, Exec_icmdViewHistory);
				AddMenuCommand(mcs, CommandId.icmdAnnotate, Exec_icmdAnnotate);
				AddMenuCommand(mcs, CommandId.icmdGrep, Exec_icmdGrep);
				AddMenuCommand(mcs, CommandId.icmdViewChangeLog, Exec_icmdViewChangeLog);
				AddMenuCommand(mcs, CommandId.icmdUpdate, Exec_icmdUpdate);
				AddMenuCommand(mcs, CommandId.icmdTags, Exec_icmdTags);
				AddMenuCommand(mcs, CommandId.icmdRefreshStatus, Exec_icmdRefreshStatus);
			}

			// Register the provider with the source control manager
			// If the package is to become active, this will also callback on OnActiveStateChange and the menu commands will be enabled
			IVsRegisterScciProvider rscp =
				(IVsRegisterScciProvider) GetService(typeof (IVsRegisterScciProvider));
			rscp.RegisterSourceControlProvider(GuidList.guidSccProvider);
		}

		//------------------------------------------------------------------
		protected override void Dispose(bool disposing)
		{
			Logger.WriteLine("Entering Dispose() of: {0}", this.ToString());

			sccService.Dispose();
			base.Dispose(disposing);
		}

		//------------------------------------------------------------------
		// This function is called by the IVsSccProvider service implementation when the active state of the provider changes
		// If the package needs to refresh UI or perform other tasks, this is a good place to add the code
		public void OnActiveStateChange()
		{
		}

		// Returns the name of the source control provider
		public string ProviderName
		{
			get { return _strProviderName; }
		}

		#endregion

		//--------------------------------------------------------------------------------
		// IVsPersistSolutionProps specific functions
		//--------------------------------------------------------------------------------

		#region IVsPersistSolutionProps interface functions

		public int OnProjectLoadFailure([InAttribute] IVsHierarchy pStubHierarchy,
										[InAttribute] string pszProjectName,
										[InAttribute] string pszProjectMk,
										[InAttribute] string pszKey)
		{
			return VSConstants.S_OK;
		}

		//------------------------------------------------------------------
		public int QuerySaveSolutionProps([InAttribute] IVsHierarchy pHierarchy,
										  [OutAttribute] VSQUERYSAVESLNPROPS[]
											pqsspSave)
		{
			if (!HgSccOptions.Options.UseSccBindings)
			{
				pqsspSave[0] = VSQUERYSAVESLNPROPS.QSP_HasNoProps;
				return VSConstants.S_OK;
			}

			// This function is called by the IDE to determine if something needs to be saved in the solution.
			// If the package returns that it has dirty properties, the shell will callback on SaveSolutionProps

			// We will write solution properties only for the solution
			// A provider may consider writing in the solution project-binding-properties for each controlled project
			// that could help it locating the projects in the store during operations like OpenFromSourceControl
			
			// Check if it is not a solution hierarchy
			if (pHierarchy != null)
			{
				// We have properties only for solution, so return
				// no props	for other hierarchies

				pqsspSave[0] = VSQUERYSAVESLNPROPS.QSP_HasNoProps;
				return VSConstants.S_OK;
			}
			
			if (!sccService.IsProjectControlled(pHierarchy))
			{
				pqsspSave[0] = VSQUERYSAVESLNPROPS.QSP_HasNoProps;
			}
			else
			{
				if (SolutionHasDirtyProps)
				{
					pqsspSave[0] = VSQUERYSAVESLNPROPS.QSP_HasDirtyProps;
				}
				else
				{
					pqsspSave[0] = VSQUERYSAVESLNPROPS.QSP_HasNoDirtyProps;
				}
			}

			return VSConstants.S_OK;
		}

		//------------------------------------------------------------------
		public int SaveSolutionProps([InAttribute] IVsHierarchy pHierarchy,
									 [InAttribute] IVsSolutionPersistence
										pPersistence)
		{
			// This function gets called by the shell after determining the package has dirty props.
			// The package will pass in the key under which it wants to save its properties, 
			// and the IDE will call back on WriteSolutionProps

			// The properties will be saved in the Pre-Load section
			// When the solution will be reopened, the IDE will call our package to load them back before the projects in the solution are actually open
			// This could help if the source control package needs to persist information like projects translation tables, that should be read from the suo file
			// and should be available by the time projects are opened and the shell start calling IVsSccEnlistmentPathTranslation functions.

			pPersistence.SavePackageSolutionProps(1, null, this,
												  _strSolutionPersistanceKey);

			// Once we saved our props, the solution is not dirty anymore
			SolutionHasDirtyProps = false;

			return VSConstants.S_OK;
		}

		//------------------------------------------------------------------
		public int WriteSolutionProps([InAttribute] IVsHierarchy pHierarchy,
									  [InAttribute] string pszKey,
									  [InAttribute] IPropertyBag pPropBag)
		{
			// The package will only save one property in the solution, to indicate that solution is controlled

			// A good provider may need to persist as solution properties the controlled status of projects and their locations, too.
			// If an operation like OpenFromSourceControl has sense for the provider, and the user has selected to open from 
			// source control the solution file, the bindings written as solution properties will help identifying where the 
			// project files are in the source control database. The source control provider can download the project files 
			// before they are needed by the IDE to be opened.
			string strControlled = true.ToString();
			object obj = strControlled;
			pPropBag.Write(_strSolutionControlledProperty, ref obj);
			string strSolutionLocation = "<Solution Location In Database>";
			obj = strSolutionLocation;
			pPropBag.Write(_strSolutionBindingsProperty, ref obj);

			return VSConstants.S_OK;
		}

		//------------------------------------------------------------------
		public int ReadSolutionProps([InAttribute] IVsHierarchy pHierarchy,
									 [InAttribute] string pszProjectName,
									 [InAttribute] string pszProjectMk,
									 [InAttribute] string pszKey,
									 [InAttribute] int fPreLoad,
									 [InAttribute] IPropertyBag pPropBag)
		{
			// This function gets called by the shell when a solution controlled by this provider is opened in IDE.
			// The shell encounters the _strSolutionPersistanceKey section in the solution, and based based on 
			// registration info written by ProvideSolutionProps identifies this package as the section owner, 
			// loads this package if necessary and call the package to read the persisted solution options.

			if (_strSolutionPersistanceKey.CompareTo(pszKey) == 0)
			{
				// We were called to read the key written by this source control provider
				// First thing to do: register the source control provider with the source control manager.
				// This allows the scc manager to switch the active source control provider if necessary,
				// and set this provider active; the provider will be later called to provide source control services for this solution.
				// (This is how automatic source control provider switching on solution opening is implemented)
				IVsRegisterScciProvider rscp =
					(IVsRegisterScciProvider) GetService(typeof (IVsRegisterScciProvider));
				rscp.RegisterSourceControlProvider(GuidList.guidSccProvider);

				// Now we can read all the data and store it in memory
				// The read data will be used when the solution has completed opening
				object pVar;
				pPropBag.Read(_strSolutionControlledProperty, out pVar, null, 0, null);
				if (pVar.ToString().CompareTo(true.ToString()) == 0)
				{
					pPropBag.Read(_strSolutionBindingsProperty, out pVar, null, 0, null);
					sccService.LoadingControlledSolutionLocation = pVar.ToString();
				}
			}
			return VSConstants.S_OK;
		}

		//------------------------------------------------------------------
		public int SaveUserOptions([InAttribute] IVsSolutionPersistence pPersistence)
		{
			// This function gets called by the shell when the SUO file is saved.
			// The provider calls the shell back to let it know which options keys it will use in the suo file.
			// The shell will create a stream for the section of interest, and will call back the provider on 
			// IVsPersistSolutionProps.WriteUserOptions() to save specific options under the specified key.
			int pfResult = 0;
			sccService.AnyItemsUnderSourceControl(out pfResult);
			if (pfResult > 0)
			{
				pPersistence.SavePackageUserOpts(this, _strSolutionUserOptionsKey);
			}
			return VSConstants.S_OK;
		}

		//------------------------------------------------------------------
		public int WriteUserOptions([InAttribute] IStream pOptionsStream,
									[InAttribute] string pszKey)
		{
			// This function gets called by the shell to let the package write user options under the specified key.
			// The key was declared in SaveUserOptions(), when the shell started saving the suo file.
			Debug.Assert(pszKey.CompareTo(_strSolutionUserOptionsKey) == 0,
						 "The shell called to read an key that doesn't belong to this package");

			Hashtable hashProjectsUserData = new Hashtable();
			var solution = (IVsSolution)GetService(typeof(SVsSolution));
			var scc_projects = solution.EnumHierarchies().Where(hier => hier is IVsSccProject2);

			var hier_map = new HashSet<IVsHierarchy>();
			foreach (var hier in scc_projects)
				hier_map.Add(hier);

			// add the solution to the controllable projects list
			IVsHierarchy solHier = (IVsHierarchy) GetService(typeof (SVsSolution));
			hier_map.Add(solHier);
			
			// collect all projects that are controlled and offline
			foreach (IVsHierarchy pHier in hier_map)
			{
				if (sccService.IsProjectControlled(pHier) &&
					sccService.IsProjectOffline(pHier))
				{
					// The information we'll persist in the suo file needs to be usable if the solution is moved in a diffrent location
					// therefore we'll store project names as known by the solution (mostly relativized to the solution's folder)
					string projUniqueName;
					if (solution.GetUniqueNameOfProject(pHier, out projUniqueName) ==
						VSConstants.S_OK)
					{
						hashProjectsUserData[projUniqueName] = true;
					}
				}
			}

			// The easiest way to read/write the data of interest is by using a binary formatter class
			// This way, we can write a map of information about projects with one call 
			// (each element in the map needs to be serializable though)
			// The alternative is to write binary data in any byte format you'd like using pOptionsStream.Write
			
			var pStream = new DataStreamFromComStream(pOptionsStream);
			var formatter = new BinaryFormatter();
			formatter.Serialize(pStream, hashProjectsUserData);

			return VSConstants.S_OK;
		}

		//------------------------------------------------------------------
		public int LoadUserOptions(
			[InAttribute] IVsSolutionPersistence pPersistence,
			[InAttribute] uint grfLoadOpts)
		{
			// This function gets called by the shell when a solution is opened and the SUO file is read.
			// Note this can be during opening a new solution, or may be during merging of 2 solutions.
			// The provider calls the shell back to let it know which options keys from the suo file were written by this provider.
			// If the shell will find in the suo file a section that belong to this package, it will create a stream, 
			// and will call back the provider on IVsPersistSolutionProps.ReadUserOptions() to read specific options 
			// under that option key.
			pPersistence.LoadPackageUserOpts(this, _strSolutionUserOptionsKey);
			return VSConstants.S_OK;
		}

		//------------------------------------------------------------------
		public int ReadUserOptions([InAttribute] IStream pOptionsStream,
								   [InAttribute] string pszKey)
		{
			// This function is called by the shell if the _strSolutionUserOptionsKey section declared
			// in LoadUserOptions() as being written by this package has been found in the suo file. 
			// Note this can be during opening a new solution, or may be during merging of 2 solutions.
			// A good source control provider may need to persist this data until OnAfterOpenSolution or OnAfterMergeSolution is called

			// The easiest way to read/write the data of interest is by using a binary formatter class
			var pStream = new DataStreamFromComStream(pOptionsStream);
			Hashtable hashProjectsUserData = new Hashtable();
			if (pStream.Length > 0)
			{
				var formatter = new BinaryFormatter();
				hashProjectsUserData = formatter.Deserialize(pStream) as Hashtable;
			}

			IVsSolution solution = (IVsSolution) GetService(typeof (SVsSolution));
			foreach (string projUniqueName in hashProjectsUserData.Keys)
			{
				// If this project is recognizable as part of the solution
				IVsHierarchy pHier = null;
				if (solution.GetProjectOfUniqueName(projUniqueName, out pHier) ==
					VSConstants.S_OK &&
					pHier != null)
				{
					sccService.ToggleOfflineStatus(pHier);
				}
			}

			return VSConstants.S_OK;
		}

		#endregion

		#region Source Control Command Enabling

		/// <summary>
		/// The shell call this function to know if a menu item should be visible and
		/// if it should be enabled/disabled.
		/// Note that this function will only be called when an instance of this editor
		/// is open.
		/// </summary>
		/// <param name="guidCmdGroup">Guid describing which set of command the current command(s) belong to</param>
		/// <param name="cCmds">Number of command which status are being asked for</param>
		/// <param name="prgCmds">Information for each command</param>
		/// <param name="pCmdText">Used to dynamically change the command text</param>
		/// <returns>HRESULT</returns>
		public int QueryStatus(ref Guid guidCmdGroup, uint cCmds, OLECMD[] prgCmds,
							   IntPtr pCmdText)
		{
			Debug.Assert(cCmds == 1, "Multiple commands");
			Debug.Assert(prgCmds != null, "NULL argument");

			if (prgCmds == null)
				return VSConstants.E_INVALIDARG;

			// Filter out commands that are not defined by this package
			if (guidCmdGroup != GuidList.guidSccProviderCmdSet)
			{
				return
					(int)
					(Microsoft.VisualStudio.OLE.Interop.Constants.OLECMDERR_E_NOTSUPPORTED);
			}

			OLECMDF cmdf = OLECMDF.OLECMDF_SUPPORTED;

			// All source control commands needs to be hidden and disabled when the provider is not active
			if (!sccService.Active)
			{
				cmdf = cmdf | OLECMDF.OLECMDF_INVISIBLE;
				cmdf = cmdf & ~(OLECMDF.OLECMDF_ENABLED);

				prgCmds[0].cmdf = (uint) cmdf;
				return VSConstants.S_OK;
			}

			// Process our Commands
			switch (prgCmds[0].cmdID)
			{
				case CommandId.icmdAddToSourceControl:
					cmdf |= QueryStatus_icmdAddToSourceControl();
					break;

				case CommandId.icmdClone:
					cmdf |= QueryStatus_icmdClone();
					break;

				case CommandId.icmdSynchronize:
					cmdf |= QueryStatus_icmdSynchronize();
					break;

				case CommandId.icmdCommit:
					cmdf |= QueryStatus_icmdCommit();
					break;

				case CommandId.icmdRevert:
					cmdf |= QueryStatus_icmdRevert();
					break;

				case CommandId.icmdCompare:
					cmdf |= QueryStatus_icmdCompare();
					break;

				case CommandId.icmdViewHistory:
					cmdf |= QueryStatus_icmdHistory();
					break;

				case CommandId.icmdAnnotate:
					cmdf |= QueryStatus_icmdAnnotate();
					break;

				case CommandId.icmdGrep:
					cmdf |= QueryStatus_icmdGrep();
					break;

				case CommandId.icmdViewChangeLog:
					cmdf |= QueryStatus_icmdViewChangeLog();
					break;

				case CommandId.icmdUpdate:
					cmdf |= QueryStatus_icmdUpdate();
					break;

				case CommandId.icmdTags:
					cmdf |= QueryStatus_icmdTags();
					break;

				case CommandId.icmdRefreshStatus:
					cmdf |= QueryStatus_icmdRefreshStatus();
					break;

				default:
					return
						(int)
						(Microsoft.VisualStudio.OLE.Interop.Constants.OLECMDERR_E_NOTSUPPORTED);
			}

			prgCmds[0].cmdf = (uint) cmdf;

			return VSConstants.S_OK;
		}

		//------------------------------------------------------------------
		private OLECMDF QueryStatus_icmdCommit()
		{
			if (!IsThereASolution())
				return OLECMDF.OLECMDF_INVISIBLE;

			var selected_hier = GetFirstSelectedHierarchy();

			if (	sccService.IsValidStorage(selected_hier)
				&&	sccService.IsAnyModifedFiles(selected_hier))
			{
				return OLECMDF.OLECMDF_ENABLED;
			}

			return OLECMDF.OLECMDF_SUPPORTED;
		}

		//------------------------------------------------------------------
		private IVsHierarchy GetFirstSelectedHierarchy()
		{
			IList<VSITEMSELECTION> sel = GetSelectedNodes();
			bool is_solution_selected;
			var selected_hiers = GetSelectedHierarchies(sel, out is_solution_selected);
			foreach (var hier in selected_hiers)
				return hier;

			return null;
		}

		//------------------------------------------------------------------
		private OLECMDF QueryStatus_icmdRevert()
		{
			if (!IsThereASolution())
				return OLECMDF.OLECMDF_INVISIBLE;

			var selected_hier = GetFirstSelectedHierarchy();

			if (	sccService.IsValidStorage(selected_hier)
				&&	sccService.IsAnyModifedFiles(selected_hier))
			{
				return OLECMDF.OLECMDF_ENABLED;
			}

			return OLECMDF.OLECMDF_SUPPORTED;
		}

		//------------------------------------------------------------------
		private OLECMDF QueryStatus_icmdHistory()
		{
			if (!IsThereASolution())
				return OLECMDF.OLECMDF_INVISIBLE;

			var selected_files = GetSelectedNodes();
			if (selected_files.Count != 1)
				return OLECMDF.OLECMDF_INVISIBLE;

			var files = GetFilesInControlledProjectsWithoutSpecial(selected_files);

			if (files.Count == 1
				&& sccService.GetFileStatus(files[0]) != SourceControlStatus.scsUncontrolled)
			{
				return OLECMDF.OLECMDF_ENABLED;
			}

			return OLECMDF.OLECMDF_SUPPORTED;
		}

		//------------------------------------------------------------------
		private OLECMDF QueryStatus_icmdAnnotate()
		{
			return QueryStatus_icmdHistory();
		}

		//------------------------------------------------------------------
		private OLECMDF QueryStatus_icmdGrep()
		{
			return QueryStatus_icmdViewChangeLog();
		}

		//------------------------------------------------------------------
		private OLECMDF QueryStatus_icmdViewChangeLog()
		{
			if (!IsThereASolution())
				return OLECMDF.OLECMDF_INVISIBLE;

			var selected_hier = GetFirstSelectedHierarchy();

			if (sccService.IsValidStorage(selected_hier))
				return OLECMDF.OLECMDF_ENABLED;

			return OLECMDF.OLECMDF_SUPPORTED;
		}

		//------------------------------------------------------------------
		private OLECMDF QueryStatus_icmdUpdate()
		{
			if (!IsThereASolution())
				return OLECMDF.OLECMDF_INVISIBLE;

			var selected_hier = GetFirstSelectedHierarchy();

			if (sccService.IsValidStorage(selected_hier))
				return OLECMDF.OLECMDF_ENABLED;

			return OLECMDF.OLECMDF_SUPPORTED;
		}

		//------------------------------------------------------------------
		private OLECMDF QueryStatus_icmdTags()
		{
			if (!IsThereASolution())
				return OLECMDF.OLECMDF_INVISIBLE;

			var selected_hier = GetFirstSelectedHierarchy();

			if (sccService.IsValidStorage(selected_hier))
				return OLECMDF.OLECMDF_ENABLED;

			return OLECMDF.OLECMDF_SUPPORTED;
		}

		//------------------------------------------------------------------
		private OLECMDF QueryStatus_icmdRefreshStatus()
		{
			if (!IsThereASolution())
				return OLECMDF.OLECMDF_INVISIBLE;

			int pfResult = 0;
			sccService.AnyItemsUnderSourceControl(out pfResult);
			if (pfResult > 0)
			{
				return OLECMDF.OLECMDF_ENABLED;
			}

			return OLECMDF.OLECMDF_SUPPORTED;
		}

		//------------------------------------------------------------------
		private OLECMDF QueryStatus_icmdAddToSourceControl()
		{
			if (!IsThereASolution())
				return OLECMDF.OLECMDF_INVISIBLE;

			IList<VSITEMSELECTION> sel = GetSelectedNodes();
			bool isSolutionSelected = false;
			var hash = GetSelectedHierarchies(sel, out isSolutionSelected);

			// The command is enabled when the solution is selected and is uncontrolled yet
			// or when an uncontrolled project is selected
			if (isSolutionSelected)
			{
				if (!sccService.IsProjectControlled(null))
				{
					return OLECMDF.OLECMDF_ENABLED;
				}
			}
			else
			{
				foreach (IVsHierarchy pHier in hash)
				{
					if (!sccService.IsProjectControlled(pHier))
					{
						return OLECMDF.OLECMDF_ENABLED;
					}
				}
			}

			return OLECMDF.OLECMDF_SUPPORTED;
		}

		//------------------------------------------------------------------
		private OLECMDF QueryStatus_icmdClone()
		{
			return OLECMDF.OLECMDF_ENABLED;
		}

		//------------------------------------------------------------------
		private OLECMDF QueryStatus_icmdSynchronize()
		{
			if (!IsThereASolution())
				return OLECMDF.OLECMDF_INVISIBLE;

			var selected_hier = GetFirstSelectedHierarchy();

			if (sccService.IsValidStorage(selected_hier))
				return OLECMDF.OLECMDF_ENABLED;

			return OLECMDF.OLECMDF_SUPPORTED;
		}

		//------------------------------------------------------------------
		private OLECMDF QueryStatus_icmdCompare()
		{
			if (!IsThereASolution())
				return OLECMDF.OLECMDF_INVISIBLE;

			var selected_files = GetSelectedNodes();
			if (selected_files.Count != 1)
				return OLECMDF.OLECMDF_INVISIBLE;

			var files = GetFilesInControlledProjectsWithoutSpecial(selected_files);

			if (files.Count == 1)
			{
				switch (sccService.GetFileStatus(files[0]))
				{
					case SourceControlStatus.scsModified:
					case SourceControlStatus.scsCopied:
					case SourceControlStatus.scsDeleted:
					case SourceControlStatus.scsRemoved:
						{
							return OLECMDF.OLECMDF_ENABLED;
						}
				}				
			}

			return OLECMDF.OLECMDF_SUPPORTED;
		}

		#endregion

		#region Source Control Commands Execution

		//------------------------------------------------------------------
		private void Exec_icmdCommit(object sender, EventArgs e)
		{
			if (!IsThereASolution())
			{
				return;
			}

			var selected_files = GetSelectedFilesInControlledProjects();
			var modified_files = new List<string>();

			foreach (string file in selected_files)
			{
				SourceControlStatus status = sccService.GetFileStatus(file);
				if (	status == SourceControlStatus.scsClean
					||	status == SourceControlStatus.scsUncontrolled
					||	status == SourceControlStatus.scsIgnored
					)
				{
					continue;
				}

				modified_files.Add(file);
			}

			sccService.CommitFiles(modified_files);
		}

		//------------------------------------------------------------------
		private void Exec_icmdRevert(object sender, EventArgs e)
		{
			if (!IsThereASolution())
				return;
			
			var selected_files = GetSelectedFilesInControlledProjects();
			var modified_files = new List<string>();

			foreach (string file in selected_files)
			{
				SourceControlStatus status = sccService.GetFileStatus(file);
				if (	status == SourceControlStatus.scsClean
					||	status == SourceControlStatus.scsUncontrolled
					||	status == SourceControlStatus.scsIgnored
					)
				{
					continue;
				}

				modified_files.Add(file);
			}

			sccService.RevertFiles(modified_files);
		}

		//------------------------------------------------------------------
		private void Exec_icmdViewHistory(object sender, EventArgs e)
		{
			if (!IsThereASolution())
				return;

			var selected_files = GetSelectedNodes();
			if (selected_files.Count != 1)
				return;

			var files = GetFilesInControlledProjectsWithoutSpecial(selected_files);

			if (files.Count == 1)
			{
				 sccService.ViewHistory(files[0]);
			}
		}

		//------------------------------------------------------------------
		private void Exec_icmdAnnotate(object sender, EventArgs e)
		{
			if (!IsThereASolution())
				return;

			var selected_files = GetSelectedNodes();
			if (selected_files.Count != 1)
				return;

			var files = GetFilesInControlledProjectsWithoutSpecial(selected_files);

			if (files.Count == 1)
			{
				sccService.Annotate(files[0]);
			}
		}

		//------------------------------------------------------------------
		private void Exec_icmdViewChangeLog(object sender, EventArgs e)
		{
			if (!IsThereASolution())
				return;

			var selected_hier = GetFirstSelectedHierarchy();
			sccService.ViewChangeLog(selected_hier);
		}

		//------------------------------------------------------------------
		private void Exec_icmdGrep(object sender, EventArgs e)
		{
			if (!IsThereASolution())
				return;

			var selected_hier = GetFirstSelectedHierarchy();
			sccService.Grep(selected_hier);
		}

		//------------------------------------------------------------------
		private void Exec_icmdUpdate(object sender, EventArgs e)
		{
			if (!IsThereASolution())
				return;

			var selected_hier = GetFirstSelectedHierarchy();
			sccService.Update(selected_hier);
		}

		//------------------------------------------------------------------
		private void Exec_icmdTags(object sender, EventArgs e)
		{
			if (!IsThereASolution())
				return;

			var selected_hier = GetFirstSelectedHierarchy();
			sccService.Tags(selected_hier);
		}

		//------------------------------------------------------------------
		private void Exec_icmdRefreshStatus(object sender, EventArgs e)
		{
			if (!IsThereASolution())
				return;

			sccService.RefreshStatus();
		}

		//------------------------------------------------------------------
		private void Exec_icmdAddToSourceControl(object sender, EventArgs e)
		{
			if (!IsThereASolution())
				return;

			IList<VSITEMSELECTION> sel = GetSelectedNodes();
			bool isSolutionSelected = false;
			var hash = GetSelectedHierarchies(sel, out isSolutionSelected);

			var hashUncontrolledProjects = new HashSet<IVsHierarchy>();
			if (isSolutionSelected)
			{
				// When the solution is selected, all the uncontrolled projects in the solution will be added to scc
				var solution = (IVsSolution)GetService(typeof(SVsSolution));
				var scc_projects = solution.EnumHierarchies().Where(hier => hier is IVsSccProject2);

				hash.Clear();
				foreach (var hier in scc_projects)
					hash.Add(hier);
			}

			foreach (IVsHierarchy pHier in hash)
			{
				if (!sccService.IsProjectControlled(pHier))
				{
					hashUncontrolledProjects.Add(pHier);
				}
			}

			sccService.AddProjectsToSourceControl(hashUncontrolledProjects,
												  isSolutionSelected);
		}

		//------------------------------------------------------------------
		private void Exec_icmdClone(object sender, EventArgs e)
		{
			var selected_hier = GetFirstSelectedHierarchy();
			sccService.Clone(selected_hier);
		}

		//------------------------------------------------------------------
		private void Exec_icmdSynchronize(object sender, EventArgs e)
		{
			if (!IsThereASolution())
				return;

			var selected_hier = GetFirstSelectedHierarchy();
			sccService.Synchronize(selected_hier);
		}

		//------------------------------------------------------------------
		private void Exec_icmdCompare(object sender, EventArgs e)
		{
			if (!IsThereASolution())
				return;

			var selected_files = GetSelectedNodes();
			if (selected_files.Count != 1)
				return;

			var files = GetFilesInControlledProjectsWithoutSpecial(selected_files);

			if (files.Count == 1)
			{
				sccService.Compare(files[0]);
			}
		}

		#endregion

		#region Source Control Utility Functions

		//------------------------------------------------------------------
		/// <summary>
		/// Returns whether suorce control properties must be saved in the solution file
		/// </summary>
		public bool SolutionHasDirtyProps
		{
			get { return _solutionHasDirtyProps; }
			set { _solutionHasDirtyProps = value; }
		}

		//------------------------------------------------------------------
		/// <summary>
		/// Checks whether a solution exist
		/// </summary>
		/// <returns>True if a solution was created.</returns>
		private bool IsThereASolution()
		{
			return (GetSolutionFileName() != null);
		}

		//------------------------------------------------------------------
		/// <summary>
		/// Gets the list of selected controllable project hierarchies
		/// </summary>
		/// <returns>True if a solution was created.</returns>
		private HashSet<IVsHierarchy> GetSelectedHierarchies(
			IList<VSITEMSELECTION> sel, out bool solutionSelected)
		{
			// Initialize output arguments
			solutionSelected = false;

			var mapHierarchies = new HashSet<IVsHierarchy>();
			foreach (VSITEMSELECTION vsItemSel in sel)
			{
				if (vsItemSel.pHier == null
					|| (vsItemSel.pHier as IVsSolution) != null
					)
				{
					solutionSelected = true;
				}

				// See if the selected hierarchy implements the IVsSccProject2 interface
				// Exclude from selection projects like FTP web projects that don't support SCC
				IVsSccProject2 sccProject2 = vsItemSel.pHier as IVsSccProject2;
				if (sccProject2 != null)
				{
					// FIXME: null
					mapHierarchies.Add(vsItemSel.pHier);
				}
			}

			return mapHierarchies;
		}

		//------------------------------------------------------------------
		/// <summary>
		/// Gets the list of directly selected VSITEMSELECTION objects
		/// </summary>
		/// <returns>A list of VSITEMSELECTION objects</returns>
		private IList<VSITEMSELECTION> GetSelectedNodes()
		{
			// Retrieve shell interface in order to get current selection
			IVsMonitorSelection monitorSelection =
				this.GetService(typeof (IVsMonitorSelection)) as IVsMonitorSelection;
			Debug.Assert(monitorSelection != null,
						 "Could not get the IVsMonitorSelection object from the services exposed by this project");
			if (monitorSelection == null)
			{
				throw new InvalidOperationException();
			}

			var selectedNodes = new List<VSITEMSELECTION>();
			IntPtr hierarchyPtr = IntPtr.Zero;
			IntPtr selectionContainer = IntPtr.Zero;
			try
			{
				// Get the current project hierarchy, project item, and selection container for the current selection
				// If the selection spans multiple hierachies, hierarchyPtr is Zero
				uint itemid;
				IVsMultiItemSelect multiItemSelect = null;
				ErrorHandler.ThrowOnFailure(
					monitorSelection.GetCurrentSelection(out hierarchyPtr, out itemid,
														 out multiItemSelect,
														 out selectionContainer));

				if (itemid != VSConstants.VSITEMID_SELECTION)
				{
					// We only care if there are nodes selected in the tree
					if (itemid != VSConstants.VSITEMID_NIL)
					{
						if (hierarchyPtr == IntPtr.Zero)
						{
							// Solution is selected
							VSITEMSELECTION vsItemSelection;
							vsItemSelection.pHier = null;
							vsItemSelection.itemid = itemid;
							selectedNodes.Add(vsItemSelection);
						}
						else
						{
							IVsHierarchy hierarchy =
								(IVsHierarchy) Marshal.GetObjectForIUnknown(hierarchyPtr);
							// Single item selection
							VSITEMSELECTION vsItemSelection;
							vsItemSelection.pHier = hierarchy;
							vsItemSelection.itemid = itemid;
							selectedNodes.Add(vsItemSelection);
						}
					}
				}
				else
				{
					if (multiItemSelect != null)
					{
						// This is a multiple item selection.

						//Get number of items selected and also determine if the items are located in more than one hierarchy
						uint numberOfSelectedItems;
						int isSingleHierarchyInt;
						ErrorHandler.ThrowOnFailure(
							multiItemSelect.GetSelectionInfo(out numberOfSelectedItems,
															 out isSingleHierarchyInt));
						bool isSingleHierarchy = (isSingleHierarchyInt != 0);

						// Now loop all selected items and add them to the list 
						Debug.Assert(numberOfSelectedItems > 0, "Bad number of selected itemd");
						if (numberOfSelectedItems > 0)
						{
							VSITEMSELECTION[] vsItemSelections =
								new VSITEMSELECTION[numberOfSelectedItems];
							ErrorHandler.ThrowOnFailure(multiItemSelect.GetSelectedItems(0,
																						 numberOfSelectedItems,
																						 vsItemSelections));
							foreach (VSITEMSELECTION vsItemSelection in vsItemSelections)
							{
								selectedNodes.Add(vsItemSelection);
							}
						}
					}
				}
			}
			finally
			{
				if (hierarchyPtr != IntPtr.Zero)
				{
					Marshal.Release(hierarchyPtr);
				}
				if (selectionContainer != IntPtr.Zero)
				{
					Marshal.Release(selectionContainer);
				}
			}

			return selectedNodes;
		}

		//------------------------------------------------------------------
		/// <summary>
		/// Returns a list of source controllable files in the selection (recursive)
		/// </summary>
		private IList<string> GetSelectedFilesInControlledProjects()
		{
			IList<VSITEMSELECTION> selectedNodes = null;
			return GetSelectedFilesInControlledProjects(out selectedNodes);
		}

		//------------------------------------------------------------------
		/// <summary>
		/// Returns a list of source controllable files in the selection (recursive)
		/// </summary>
		private IList<string> GetSelectedFilesInControlledProjects(
			out IList<VSITEMSELECTION> selectedNodes)
		{
			IList<string> sccFiles = new List<string>();

			selectedNodes = GetSelectedNodes();
			bool isSolutionSelected = false;
			var hash = GetSelectedHierarchies(selectedNodes, out isSolutionSelected);
			if (isSolutionSelected)
			{
				// Replace the selection with the root items of all controlled projects
				selectedNodes.Clear();

				var solution = (IVsSolution)GetService(typeof(SVsSolution));
				var scc_projects = solution.EnumHierarchies().Where(hier => hier is IVsSccProject2);
				
				foreach (IVsHierarchy pHier in scc_projects)
				{
					if (sccService.IsProjectControlled(pHier))
					{
						VSITEMSELECTION vsItemSelection;
						vsItemSelection.pHier = pHier;
						vsItemSelection.itemid = VSConstants.VSITEMID_ROOT;
						selectedNodes.Add(vsItemSelection);
					}
				}

				// Add the solution file to the list
				if (sccService.IsProjectControlled(null))
				{
					IVsHierarchy solHier = (IVsHierarchy) GetService(typeof (SVsSolution));
					VSITEMSELECTION vsItemSelection;
					vsItemSelection.pHier = solHier;
					vsItemSelection.itemid = VSConstants.VSITEMID_ROOT;
					selectedNodes.Add(vsItemSelection);
				}
			}

			// now look in the rest of selection and accumulate scc files
			foreach (VSITEMSELECTION vsItemSel in selectedNodes)
			{
				var pscp2 = vsItemSel.pHier as IVsSccProject2;
				if (pscp2 == null)
				{
					// solution case
					sccFiles.Add(GetSolutionFileName());
				}
				else
				{
					IList<string> nodefilesrec = GetProjectFiles(pscp2, vsItemSel.itemid);
					foreach (string file in nodefilesrec)
					{
						sccFiles.Add(file);
					}
				}
			}

			return sccFiles;
		}

		//------------------------------------------------------------------
		private IList<string> GetFilesInControlledProjectsWithoutSpecial(IList<VSITEMSELECTION> selectedNodes)
		{
			IList<string> sccFiles = new List<string>();

			foreach (VSITEMSELECTION vsItemSel in selectedNodes)
			{
				var pscp2 = vsItemSel.pHier as IVsSccProject2;

				if (vsItemSel.pHier == null)
				{
					// solution case
					if (sccService.IsProjectControlled(null))
						sccFiles.Add(GetSolutionFileName());
				}
				else if (pscp2 != null)
				{
					IList<string> nodefilesrec = GetNodeFilesWithoutSpecial(pscp2, vsItemSel.itemid);
					foreach (string file in nodefilesrec)
					{
						sccFiles.Add(file);
					}

					if (nodefilesrec.Count == 0)
					{
						// special or uncontrolled file
						var project = (IVsProject)pscp2;
						string bstrMKDocument;
						if (project.GetMkDocument(vsItemSel.itemid, out bstrMKDocument) == VSConstants.S_OK
							&& !string.IsNullOrEmpty(bstrMKDocument))
						{
							object prop;
							var res = vsItemSel.pHier.GetProperty(vsItemSel.itemid, (int)__VSHPROPID.VSHPROPID_Parent, out prop);
							if (res == VSConstants.S_OK)
							{
//								Logger.WriteLine("ParentId: {0}", prop);
							}

							uint parent_id = (uint)((int)prop);
							IList<string> files = GetNodeFiles(pscp2, parent_id);
							if (files.Count != 0)
							{
								var lower_path = bstrMKDocument;//.ToLower();

								foreach (var f in files)
								{
//									if (f.ToLower().EndsWith(lower_path))
									if (f == lower_path)
									{
										sccFiles.Add(f);
									}
								}
							}
						}
					}
				}
			}

			return sccFiles;
		}

		//------------------------------------------------------------------
		/// <summary>
		/// Returns a list of source controllable files associated with the specified node
		/// </summary>
		public IList<string> GetNodeFiles(IVsHierarchy hier, uint itemid)
		{
			var pscp2 = hier as IVsSccProject2;
			return GetNodeFiles(pscp2, itemid);
		}

		//------------------------------------------------------------------
		/// <summary>
		/// Returns a list of source controllable files associated with the specified node
		/// </summary>
		private IList<string> GetNodeFiles(IVsSccProject2 pscp2, uint itemid)
		{
			// NOTE: the function returns only a list of files, containing both regular files and special files
			// If you want to hide the special files (similar with solution explorer), you may need to return 
			// the special files in a hastable (key=master_file, values=special_file_list)

			// Initialize output parameters
			IList<string> sccFiles = new List<string>();
			if (pscp2 != null)
			{
				CALPOLESTR[] pathStr = new CALPOLESTR[1];
				CADWORD[] flags = new CADWORD[1];

				if (pscp2.GetSccFiles(itemid, pathStr, flags) == 0)
				{
					for (int elemIndex = 0; elemIndex < pathStr[0].cElems; elemIndex++)
					{
						IntPtr pathIntPtr = Marshal.ReadIntPtr(pathStr[0].pElems, elemIndex * IntPtr.Size);
						String path = Marshal.PtrToStringAuto(pathIntPtr);

						sccFiles.Add(path);

						// See if there are special files
						if (flags.Length > 0 && flags[0].cElems > 0)
						{
							int flag = Marshal.ReadInt32(flags[0].pElems, elemIndex);

							if (flag != 0)
							{
								// We have special files
								CALPOLESTR[] specialFiles = new CALPOLESTR[1];
								CADWORD[] specialFlags = new CADWORD[1];

								pscp2.GetSccSpecialFiles(itemid, path, specialFiles, specialFlags);
								for (int i = 0; i < specialFiles[0].cElems; i++)
								{
									IntPtr specialPathIntPtr = Marshal.ReadIntPtr(
										specialFiles[0].pElems, i * IntPtr.Size);
									String specialPath = Marshal.PtrToStringAuto(specialPathIntPtr);

									sccFiles.Add(specialPath);
									Marshal.FreeCoTaskMem(specialPathIntPtr);
								}

								if (specialFiles[0].cElems > 0)
								{
									Marshal.FreeCoTaskMem(specialFiles[0].pElems);
								}
							}
						}

						Marshal.FreeCoTaskMem(pathIntPtr);
					}
					if (pathStr[0].cElems > 0)
					{
						Marshal.FreeCoTaskMem(pathStr[0].pElems);
					}
				}
			}

			return sccFiles;
		}

		//------------------------------------------------------------------
		/// <summary>
		/// Returns a list of source controllable files associated with the specified node
		/// </summary>
		private IList<string> GetNodeFilesWithoutSpecial(IVsSccProject2 pscp2, uint itemid)
		{
			// Initialize output parameters
			IList<string> sccFiles = new List<string>();
			if (pscp2 != null)
			{
				CALPOLESTR[] pathStr = new CALPOLESTR[1];
				CADWORD[] flags = new CADWORD[1];

				if (pscp2.GetSccFiles(itemid, pathStr, flags) == 0)
				{
					for (int elemIndex = 0; elemIndex < pathStr[0].cElems; elemIndex++)
					{
						IntPtr pathIntPtr = Marshal.ReadIntPtr(pathStr[0].pElems, elemIndex * IntPtr.Size);
						String path = Marshal.PtrToStringAuto(pathIntPtr);

						sccFiles.Add(path);

						Marshal.FreeCoTaskMem(pathIntPtr);
					}
					if (pathStr[0].cElems > 0)
					{
						Marshal.FreeCoTaskMem(pathStr[0].pElems);
					}
				}
			}

			return sccFiles;
		}

		/*
				private string GetNodeFileFromItemId(IVsSccProject2 pscp2, uint itemid, uint itemid_to_find)
				{
					string node_file = null;

					if (pscp2 != null)
					{
						CALPOLESTR[] pathStr = new CALPOLESTR[1];
						CADWORD[] flags = new CADWORD[1];

						if (pscp2.GetSccFiles(itemid, pathStr, flags) == 0)
						{
							for (int elemIndex = 0; elemIndex < pathStr[0].cElems; elemIndex++)
							{
								IntPtr pathIntPtr = Marshal.ReadIntPtr(pathStr[0].pElems, elemIndex * IntPtr.Size);
								String path = Marshal.PtrToStringAuto(pathIntPtr);

								if (itemid == itemid_to_find)
									node_file = path;

								if (node_file == null)
								{
									// See if there are special files
									if (flags.Length > 0 && flags[0].cElems > 0)
									{
										int flag = Marshal.ReadInt32(flags[0].pElems, elemIndex);

										if (flag != 0)
										{
											// We have special files
											CALPOLESTR[] specialFiles = new CALPOLESTR[1];
											CADWORD[] specialFlags = new CADWORD[1];

											pscp2.GetSccSpecialFiles(itemid, path, specialFiles, specialFlags);
											for (int i = 0; i < specialFiles[0].cElems; i++)
											{
												IntPtr specialPathIntPtr = Marshal.ReadIntPtr(
													specialFiles[0].pElems, i*IntPtr.Size);
												String specialPath = Marshal.PtrToStringAuto(specialPathIntPtr);

												if (itemid)
												sccFiles.Add(specialPath);
												Marshal.FreeCoTaskMem(specialPathIntPtr);
											}

											if (specialFiles[0].cElems > 0)
											{
												Marshal.FreeCoTaskMem(specialFiles[0].pElems);
											}
										}
									}
								}

								Marshal.FreeCoTaskMem(pathIntPtr);
							}
							if (pathStr[0].cElems > 0)
							{
								Marshal.FreeCoTaskMem(pathStr[0].pElems);
							}
						}
					}

					return sccFiles;
				}
		*/

		//------------------------------------------------------------------
		/// <summary>
		/// Refreshes the glyphs of the specified hierarchy nodes
		/// </summary>
		public void RefreshNodesGlyphs(IList<VSITEMSELECTION> selectedNodes)
		{
			foreach (VSITEMSELECTION vsItemSel in selectedNodes)
			{
				var sccProject2 = vsItemSel.pHier as IVsSccProject2;
				if (vsItemSel.itemid == VSConstants.VSITEMID_ROOT)
				{
					if (sccProject2 == null)
					{
						// Note: The solution's hierarchy does not implement IVsSccProject2, IVsSccProject interfaces
						// It may be a pain to treat the solution as special case everywhere; a possible workaround is 
						// to implement a solution-wrapper class, that will implement IVsSccProject2, IVsSccProject and
						// IVsHierarhcy interfaces, and that could be used in provider's code wherever a solution is needed.
						// This approach could unify the treatment of solution and projects in the provider's code.

						// Until then, solution is treated as special case
						string[] rgpszFullPaths = new string[1];
						rgpszFullPaths[0] = GetSolutionFileName();
						VsStateIcon[] rgsiGlyphs = new VsStateIcon[1];
						uint[] rgdwSccStatus = new uint[1];
						sccService.GetSccGlyph(1, rgpszFullPaths, rgsiGlyphs, rgdwSccStatus);

						// Set the solution's glyph directly in the hierarchy
						IVsHierarchy solHier = (IVsHierarchy) GetService(typeof (SVsSolution));
						solHier.SetProperty(VSConstants.VSITEMID_ROOT,
											(int) __VSHPROPID.VSHPROPID_StateIconIndex,
											rgsiGlyphs[0]);
					}
					else
					{
						// Refresh all the glyphs in the project; the project will call back GetSccGlyphs() 
						// with the files for each node that will need new glyph
						sccProject2.SccGlyphChanged(0, null, null, null);
					}
				}
				else
				{
					// It may be easier/faster to simply refresh all the nodes in the project, 
					// and let the project call back on GetSccGlyphs, but just for the sake of the demo, 
					// let's refresh ourselves only one node at a time
					IList<string> sccFiles = GetNodeFiles(sccProject2, vsItemSel.itemid);

					if (sccFiles.Count > 0)
					{
						string[] rgpszFullPaths = new string[sccFiles.Count];
						for (int i = 0; i < sccFiles.Count; ++i)
							rgpszFullPaths[i] = sccFiles[i];

						VsStateIcon[] rgsiGlyphs = new VsStateIcon[sccFiles.Count];
						uint[] rgdwSccStatus = new uint[sccFiles.Count];
						sccService.GetSccGlyph(sccFiles.Count, rgpszFullPaths, rgsiGlyphs, rgdwSccStatus);

						uint[] rguiAffectedNodes = new uint[sccFiles.Count];
						IList<uint> subnodes = GetProjectItems(vsItemSel.pHier, vsItemSel.itemid);
						if (subnodes.Count != sccFiles.Count)
						{
							Logger.WriteLine("RefreshNodeGlyphs: subnodes.Count != sccFiles.Count");
							for(int i = 0; i < sccFiles.Count; ++i)
								Logger.WriteLine("[{0}]: {1}", i, sccFiles[i]);

							var dict = new Dictionary<string, uint>();
							var proj = vsItemSel.pHier as IVsProject2;

							foreach (var id in subnodes)
							{
								string docname;
								var res = proj.GetMkDocument(id, out docname);

								if (res == VSConstants.S_OK && !string.IsNullOrEmpty(docname))
									dict[docname] = id;
							}

							for(int i = 0; i < sccFiles.Count; ++i)
							{
								uint id;
								if (dict.TryGetValue(sccFiles[i], out id))
								{
									rguiAffectedNodes[i] = id;
								}
								else
								{
									Logger.WriteLine("Error: Unable to map id<->filename: {0}", sccFiles[i]);
								}
							}
						}
						else
						{
							for (int i = 0; i < sccFiles.Count; ++i)
							{
								rguiAffectedNodes[i] = subnodes[i];
							}
						}

						sccProject2.SccGlyphChanged(sccFiles.Count, rguiAffectedNodes, rgsiGlyphs,
													rgdwSccStatus);
					}
					// We'll use for the node glyph just the Master file's status (ignoring special files of the node)
/*
					if (sccFiles.Count > 0)
					{
						string[] rgpszFullPaths = new string[1];
						rgpszFullPaths[0] = sccFiles[0];
						VsStateIcon[] rgsiGlyphs = new VsStateIcon[1];
						uint[] rgdwSccStatus = new uint[1];
						sccService.GetSccGlyph(1, rgpszFullPaths, rgsiGlyphs, rgdwSccStatus);

						uint[] rguiAffectedNodes = new uint[1];
						rguiAffectedNodes[0] = vsItemSel.itemid;
						sccProject2.SccGlyphChanged(1, rguiAffectedNodes, rgsiGlyphs,
													rgdwSccStatus);
					}
*/
				}
			}
		}


		//------------------------------------------------------------------
		/// <summary>
		/// Returns the filename of the solution
		/// </summary>
		public string GetSolutionFileName()
		{
			var sol = (IVsSolution) GetService(typeof (SVsSolution));
			string solutionDirectory;
			string solutionFile;
			string solutionUserOptions;
			
			if (sol.GetSolutionInfo(out solutionDirectory, out solutionFile,
									out solutionUserOptions) == VSConstants.S_OK)
			{
				return solutionFile;
			}
			
			return null;
		}

		//------------------------------------------------------------------
		/// <summary>
		/// Returns the filename of the specified controllable project 
		/// </summary>
		public string GetProjectFileName(IVsSccProject2 pscp2Project)
		{
			// Note: Solution folders return currently a name like "NewFolder1{1DBFFC2F-6E27-465A-A16A-1AECEA0B2F7E}.storage"
			// Your provider may consider returning the solution file as the project name for the solution, if it has to persist some properties in the "project file"
			// UNDONE: What to return for web projects? They return a folder name, not a filename! Consider returning a pseudo-project filename instead of folder.

			var hierProject = (IVsHierarchy) pscp2Project;
			var project = (IVsProject) pscp2Project;

			// Attempt to get first the filename controlled by the root node 
			IList<string> sccFiles = GetNodeFiles(pscp2Project,
												  VSConstants.VSITEMID_ROOT);
			if (sccFiles.Count > 0 && !string.IsNullOrEmpty(sccFiles[0]))
			{
				return sccFiles[0];
			}

			return GetProjectFileName(project);
		}

		//------------------------------------------------------------------
		/// <summary>
		/// Returns the filename of the specified controllable project 
		/// </summary>
		public string GetProjectFileName(IVsProject project)
		{
			var hierProject = (IVsHierarchy)project;

			// Attempt to get a name from the IVsProject interface
			string bstrMKDocument;
			if (project.GetMkDocument(VSConstants.VSITEMID_ROOT, out bstrMKDocument) == VSConstants.S_OK
				&& !string.IsNullOrEmpty(bstrMKDocument))
			{
				return bstrMKDocument;
			}

			// If that failes, attempt to get the filename from the solution
			var sol = (IVsSolution)GetService(typeof(SVsSolution));
			string uniqueName;
			if (sol.GetUniqueNameOfProject(hierProject, out uniqueName) == VSConstants.S_OK
				&& !string.IsNullOrEmpty(uniqueName))
			{
				// uniqueName may be a full-path or may be relative to the solution's folder
				if (uniqueName.Length > 2 && uniqueName[1] == ':')
				{
					return uniqueName;
				}

				// try to get the solution's folder and relativize the project name to it
				string solutionDirectory, solutionFile, solutionUserOptions;
				if (
					sol.GetSolutionInfo(out solutionDirectory, out solutionFile,
										out solutionUserOptions) == VSConstants.S_OK)
				{
					uniqueName = solutionDirectory + "\\" + uniqueName;

					// UNDONE: eliminate possible "..\\.." from path
					return uniqueName;
				}
			}

			// If that failed, attempt to get the project name from 
			string bstrName;
			if (
				hierProject.GetCanonicalName(VSConstants.VSITEMID_ROOT, out bstrName) ==
				VSConstants.S_OK)
			{
				return bstrName;
			}

			// if everything we tried fail, return null string
			return null;
		}

		//------------------------------------------------------------------
		private void DebugWalkingNode(IVsHierarchy pHier, uint itemid)
		{
			object property = null;
			if (
				pHier.GetProperty(itemid, (int) __VSHPROPID.VSHPROPID_Name, out property) ==
				VSConstants.S_OK)
			{
				//Logger.WriteLine("Walking hierarchy node: {0}", (string) property);
			}
		}

		//------------------------------------------------------------------
		/// <summary>
		/// Gets the list of ItemIDs that are nodes in the specified project
		/// </summary>
		private IList<uint> GetProjectItems(IVsHierarchy pHier)
		{
			// Start with the project root and walk all expandable nodes in the project
			return GetProjectItems(pHier, VSConstants.VSITEMID_ROOT);
		}

		//------------------------------------------------------------------
		/// <summary>
		/// Gets the list of ItemIDs that are nodes in the specified project, starting with the specified item
		/// </summary>
		private IList<uint> GetProjectItems(IVsHierarchy pHier, uint startItemid)
		{
			var projectNodes = new List<uint>();

			// The method does a breadth-first traversal of the project's hierarchy tree
			var nodesToWalk = new Queue<uint>();
			nodesToWalk.Enqueue(startItemid);

			while (nodesToWalk.Count > 0)
			{
				uint node = nodesToWalk.Dequeue();
				projectNodes.Add(node);

				DebugWalkingNode(pHier, node);

				object property = null;
				if (
					pHier.GetProperty(node, (int) __VSHPROPID.VSHPROPID_FirstChild,
									  out property) == VSConstants.S_OK)
				{
					uint childnode = (uint) (int) property;
					if (childnode == VSConstants.VSITEMID_NIL)
					{
						continue;
					}

					DebugWalkingNode(pHier, childnode);

					if (
						(pHier.GetProperty(childnode, (int) __VSHPROPID.VSHPROPID_Expandable,
										   out property) == VSConstants.S_OK &&
						 (int) property != 0) ||
						(pHier.GetProperty(childnode, (int) __VSHPROPID2.VSHPROPID_Container,
										   out property) == VSConstants.S_OK && (bool) property))
					{
						nodesToWalk.Enqueue(childnode);
					}
					else
					{
						projectNodes.Add(childnode);
					}

					while (
						pHier.GetProperty(childnode, (int) __VSHPROPID.VSHPROPID_NextSibling,
										  out property) == VSConstants.S_OK)
					{
						childnode = (uint) (int) property;
						if (childnode == VSConstants.VSITEMID_NIL)
						{
							break;
						}

						DebugWalkingNode(pHier, childnode);

						if (
							(pHier.GetProperty(childnode, (int) __VSHPROPID.VSHPROPID_Expandable,
											   out property) == VSConstants.S_OK &&
							 (int) property != 0) ||
							(pHier.GetProperty(childnode, (int) __VSHPROPID2.VSHPROPID_Container,
											   out property) == VSConstants.S_OK && (bool) property))
						{
							nodesToWalk.Enqueue(childnode);
						}
						else
						{
							projectNodes.Add(childnode);
						}
					}
				}
			}

			return projectNodes;
		}

		//------------------------------------------------------------------
		/// <summary>
		/// Gets the list of source controllable files in the specified project
		/// </summary>
		public IList<string> GetProjectFiles(IVsSccProject2 pscp2Project)
		{
			return GetProjectFiles(pscp2Project, VSConstants.VSITEMID_ROOT);
		}

		//------------------------------------------------------------------
		/// <summary>
		/// Gets the list of source controllable files in the specified project
		/// </summary>
		public IList<string> GetProjectFiles(IVsSccProject2 pscp2Project,
											 uint startItemId)
		{
			var projectFiles = new List<string>();
			var hierProject = (IVsHierarchy) pscp2Project;
			var projectItems = GetProjectItems(hierProject, startItemId);

			foreach (uint itemid in projectItems)
			{
				IList<string> sccFiles = GetNodeFiles(pscp2Project, itemid);
				foreach (string file in sccFiles)
				{
					projectFiles.Add(file);
				}
			}

			return projectFiles;
		}

		//------------------------------------------------------------------
		/// <summary>
		/// Checks whether the provider is invoked in command line mode
		/// </summary>
		public bool InCommandLineMode()
		{
			var shell = (IVsShell) GetService(typeof (SVsShell));
			object pvar;
			if (
				shell.GetProperty((int) __VSSPROPID.VSSPROPID_IsInCommandLineMode,
								  out pvar) == VSConstants.S_OK &&
				(bool) pvar)
			{
				return true;
			}

			return false;
		}

		#endregion
	}
}
