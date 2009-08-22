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
using System.ComponentModel;
using System.Collections;
using System.Drawing;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using MsVsShell = Microsoft.VisualStudio.Shell;
using HgSccHelper;
using System.IO;

namespace HgSccPackage
{
    /// <summary>
    /// Summary description for SccProviderOptions.
    /// </summary>
    /// 
    [Guid("B0BAC05D-111E-4a5b-9834-076CB319ED59")]
    public class SccProviderOptions : MsVsShell.DialogPage
    {
        private SccProviderOptionsControl page = null;

        /// <include file='doc\DialogPage.uex' path='docs/doc[@for="DialogPage".Window]' />
        /// <devdoc>
        ///     The window this dialog page will use for its UI.
        ///     This window handle must be constant, so if you are
        ///     returning a Windows Forms control you must make sure
        ///     it does not recreate its handle.  If the window object
        ///     implements IComponent it will be sited by the 
        ///     dialog page so it can get access to global services.
        /// </devdoc>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        protected override IWin32Window Window
        {
            get
            {
                page = new SccProviderOptionsControl();
                page.Location = new Point(0, 0);
                page.OptionsPage = this;
                return page;
            }
        }

        /// <include file='doc\DialogPage.uex' path='docs/doc[@for="DialogPage.OnActivate"]' />
        /// <devdoc>
        ///     This method is called when VS wants to activate this
        ///     page.  If the Cancel property of the event is set to true, the page is not activated.
        /// </devdoc>
        protected override void OnActivate(CancelEventArgs e)
        {
            Trace.WriteLine(string.Format("In OnActivate"));
            base.OnActivate(e);

// 			if (page != null)
// 				page.Activate();
        }

        /// <include file='doc\DialogPage.uex' path='docs/doc[@for="DialogPage.OnClosed"]' />
        /// <devdoc>
        ///     This event is raised when the page is closed.   
        /// </devdoc>
        protected override void OnClosed(EventArgs e)
        {
            Trace.WriteLine(string.Format("In OnClosed"));
            base.OnClosed(e);
        }

        /// <include file='doc\DialogPage.uex' path='docs/doc[@for="DialogPage.OnDeactivate"]' />
        /// <devdoc>
        ///     This method is called when VS wants to deatviate this
        ///     page.  If true is set for the Cancel property of the event, 
        ///     the page is not deactivated.
        /// </devdoc>
        protected override void OnDeactivate(CancelEventArgs e)
        {
            Trace.WriteLine(string.Format("In OnDeactivate"));

            base.OnDeactivate(e);
        }

        /// <include file='doc\DialogPage.uex' path='docs/doc[@for="DialogPage.OnApply"]' />
        /// <devdoc>
        ///     This method is called when VS wants to save the user's 
        ///     changes then the dialog is dismissed.
        /// </devdoc>
		protected override void OnApply(PageApplyEventArgs e)
        {
            Trace.WriteLine(string.Format("In OnApply"));

			if (page != null)
			{
				string diff_tool = page.DiffToolPath;

				if (diff_tool.Length == 0)
				{
					MessageBox.Show("You should browse for Diff tool", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
					e.ApplyBehavior = ApplyKind.CancelNoNavigate;
				}
				else if (!File.Exists(diff_tool))
				{
					MessageBox.Show("File: " + diff_tool + " is not exist", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
					e.ApplyBehavior = ApplyKind.CancelNoNavigate;
				}
				else
				{
					HgSccOptions.Options.DiffTool = diff_tool;
					HgSccOptions.Save();
				}
			}
			else
			{
				e.ApplyBehavior = ApplyKind.Cancel;
			}
			
			base.OnApply(e);
/*
            string messageText = Resources.ResourceManager.GetString("ApplyProviderOptions");
            string messageCaption = Resources.ResourceManager.GetString("ProviderName");

            IVsUIShell uiShell = (IVsUIShell)GetService(typeof(SVsUIShell));
            Guid clsid = Guid.Empty;
            int result = VSConstants.S_OK;
            if (uiShell.ShowMessageBox(0, ref clsid,
                                messageCaption,
                                messageText,
                                string.Empty,
                                0,
                                OLEMSGBUTTON.OLEMSGBUTTON_OKCANCEL,
                                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST,
                                OLEMSGICON.OLEMSGICON_QUERY,
                                0,        // false = application modal; true would make it system modal
                                out result) != VSConstants.S_OK
                || result != (int)DialogResult.OK)
            {
                Trace.WriteLine(string.Format("Cancelled the OnApply event"));
                e.ApplyBehavior = ApplyKind.Cancel;
            }
            else
            {
                base.OnApply(e);
            }
*/
        }
    }
}
