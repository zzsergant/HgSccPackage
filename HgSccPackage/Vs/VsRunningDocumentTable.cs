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
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio;

namespace HgSccPackage.Vs
{
	//==================================================================
	class VsRunningDocumentTable : IDisposable, IVsRunningDocTableEvents
	{
		private uint advise_cookie;

		//------------------------------------------------------------------
		public IVsRunningDocumentTable Interface { get; private set; }

		//------------------------------------------------------------------
		public VsRunningDocumentTable(IVsRunningDocumentTable rdt)
		{
			if (rdt == null)
				throw new ArgumentNullException("rdt");

			if (VSConstants.S_OK != rdt.AdviseRunningDocTableEvents(this, out advise_cookie))
				throw new ArgumentException("Invalid rdt");

			this.Interface = rdt;
		}

		//------------------------------------------------------------------
		public RdtDocumentInfo FindAndLockDocument(string mk_document, _VSRDTFLAGS flags)
		{
			IVsHierarchy hier;
			uint cookie;
			uint itemid;
			IntPtr doc_data;
			var err = Interface.FindAndLockDocument((uint)flags, mk_document, out hier, out itemid, out doc_data, out cookie);
			if (err != VSConstants.S_OK)
				return null;

			// FIXME: Double look up. But without this doc info will not have read/edit locks
			return GetDocumentInfo(cookie);
		}

		//------------------------------------------------------------------
		public RdtDocumentInfo GetDocumentInfo(uint doc_cookie)
		{
			uint pgrfRDTFlags;
			uint pdwReadLocks;
			uint pdwEditLocks;
			string pbstrMkDocument;
			IVsHierarchy ppHier;
			uint pitemid;
			IntPtr ppunkDocData;

			var err = Interface.GetDocumentInfo(
				doc_cookie, out pgrfRDTFlags, out pdwReadLocks, out pdwEditLocks,
				out pbstrMkDocument, out ppHier, out pitemid, out ppunkDocData);

			if (err != VSConstants.S_OK)
				return null;

			var doc_info = new RdtDocumentInfo();
			doc_info.DocData = ppunkDocData;
			doc_info.EditLocks = pdwEditLocks;
			doc_info.ReadLocks = pdwReadLocks;
			doc_info.Flags = (_VSRDTFLAGS)pgrfRDTFlags;
			doc_info.Hierarchy = ppHier;
			doc_info.ItemId = pitemid;
			doc_info.MkDocument = pbstrMkDocument;
			doc_info.DocCookie = doc_cookie;
			return doc_info;
		}

		//------------------------------------------------------------------
		public IEnumerable<RdtDocumentInfo> EnumDocuments()
		{
			IEnumRunningDocuments rdt_enum;

			var err = Interface.GetRunningDocumentsEnum(out rdt_enum);
			if (err != VSConstants.S_OK)
				yield break;

			var doc_cookie_array = new uint[1];
			uint elements_fetched = 0;

			while (VSConstants.S_OK == rdt_enum.Next(1, doc_cookie_array, out elements_fetched))
			{
				var doc_info = GetDocumentInfo(doc_cookie_array[0]);
				if (doc_info != null)
					yield return doc_info;
			}
		}


		#region IDisposable Members

		//------------------------------------------------------------------
		public void Dispose()
		{
			if (Interface != null)
			{
				Interface.UnadviseRunningDocTableEvents(advise_cookie);
				advise_cookie = 0;
				Interface = null;
			}
		}

		#endregion

		#region IVsRunningDocTableEvents Members

		//------------------------------------------------------------------
		public event EventHandler<RdtAfterAttributeChangeEventArgs> AfterAttributeChangeEvent;

		//------------------------------------------------------------------
		public int OnAfterAttributeChange(uint docCookie, uint grfAttribs)
		{
			var e = AfterAttributeChangeEvent;
			if (e != null)
			{
				var args = new RdtAfterAttributeChangeEventArgs();
				args.DocInfo = GetDocumentInfo(docCookie);
				args.Attributes = (__VSRDTATTRIB)grfAttribs;
				e(this, args);
			}

			return VSConstants.S_OK;
		}

		//------------------------------------------------------------------
		public int OnAfterDocumentWindowHide(uint docCookie, IVsWindowFrame pFrame)
		{
			return VSConstants.E_NOTIMPL;
		}

		//------------------------------------------------------------------
		public int OnAfterFirstDocumentLock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining)
		{
			return VSConstants.E_NOTIMPL;
		}

		//------------------------------------------------------------------
		public event EventHandler<RdtAfterSaveEventArgs> AfterSaveEvent;
		
		//------------------------------------------------------------------
		public int OnAfterSave(uint docCookie)
		{
			var e = AfterSaveEvent;
			if (e != null)
			{
				var args = new RdtAfterSaveEventArgs();
				args.DocInfo = GetDocumentInfo(docCookie);
				e(this, args);
			}

			return VSConstants.S_OK;
		}

		//------------------------------------------------------------------
		public int OnBeforeDocumentWindowShow(uint docCookie, int fFirstShow, IVsWindowFrame pFrame)
		{
			return VSConstants.E_NOTIMPL;
		}

		//------------------------------------------------------------------
		public int OnBeforeLastDocumentUnlock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining)
		{
			return VSConstants.E_NOTIMPL;
		}

		#endregion
	}

	//==================================================================
	class RdtDocumentInfo
	{
		public uint DocCookie { get; set; }
		public string MkDocument { get; set; }
		public _VSRDTFLAGS Flags { get; set; }
		public uint ReadLocks { get; set; }
		public uint EditLocks { get; set; }
		public IVsHierarchy Hierarchy { get; set; }
		public uint ItemId { get; set; }
		public IntPtr DocData { get; set; }
	}

	//==================================================================
	class RdtAfterAttributeChangeEventArgs : EventArgs
	{
		public RdtDocumentInfo DocInfo { get; set; }
		public __VSRDTATTRIB Attributes { get; set; }
	}

	//==================================================================
	class RdtAfterSaveEventArgs : EventArgs
	{
		public RdtDocumentInfo DocInfo { get; set; }
	}
}
