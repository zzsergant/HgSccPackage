//=========================================================================
// Copyright 2010 Sergey Antonov <sergant_@mail.ru>
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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Collections.ObjectModel;
using HgSccHelper;
using Microsoft.VisualStudio.Shell.Interop;

namespace HgSccPackage.UI
{
	/// <summary>
	/// Interaction logic for ChangeSccBindingsWindow.xaml
	/// </summary>
	public partial class ChangeSccBindingsWindow : Window
	{
		//-----------------------------------------------------------------------------
		public static RoutedUICommand SccBindCommand = new RoutedUICommand("SccBind",
			"SccBind", typeof(ChangeSccBindingsWindow));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand SccUnbindCommand = new RoutedUICommand("SccUnbind",
			"SccUnbind", typeof(ChangeSccBindingsWindow));


		//------------------------------------------------------------------
		internal List<SccBindItem> SccBindItems { get; set; }

		//-----------------------------------------------------------------------------
		public ChangeSccBindingsWindow()
		{
			InitializeComponent();

			SccBindItems = new List<SccBindItem>();
		}

		//-----------------------------------------------------------------------------
		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			Title = string.Format("Change Scc Bindings");

			listProjects.ItemsSource = SccBindItems;
			if (listProjects.Items.Count > 0)
				listProjects.SelectedIndex = 0;
		}

		//-----------------------------------------------------------------------------
		private void Window_Closed(object sender, EventArgs e)
		{
		}

		//------------------------------------------------------------------
		private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
				Close();
		}

		//------------------------------------------------------------------
		private void SccBind_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			foreach (SccBindItem item in listProjects.SelectedItems)
			{
				if (item.SccBindStatus == SccBindStatus.NotBound)
				{
					e.CanExecute = true;
				}
			}
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void SccBind_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			foreach (SccBindItem item in listProjects.SelectedItems)
			{
				if (item.SccBindStatus == SccBindStatus.NotBound)
				{
					if (item.Bind != null && item.Bind(item.Hierarchy))
					{
						item.SccBindStatus = SccBindStatus.Bound;
					}
				}
			}
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void SccUnbind_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			foreach (SccBindItem item in listProjects.SelectedItems)
			{
				if (item.SccBindStatus == SccBindStatus.Bound)
				{
					e.CanExecute = true;
				}
			}
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void SccUnbind_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			foreach (SccBindItem item in listProjects.SelectedItems)
			{
				if (item.SccBindStatus == SccBindStatus.Bound)
				{
					if (item.Unbind != null && item.Unbind(item.Hierarchy))
					{
						item.SccBindStatus = SccBindStatus.NotBound;
					}
				}
			}
			e.Handled = true;
		}

		//-----------------------------------------------------------------------------
		private void AllButton_Click(object sender, RoutedEventArgs e)
		{
			if (listProjects.SelectedItems.Count == 0)
				listProjects.SelectAll();
			else if (listProjects.SelectedItems.Count == listProjects.Items.Count)
				listProjects.UnselectAll();
			else
				listProjects.SelectAll();
		}
	}

	//==================================================================
	enum SccBindStatus
	{
		Unknown,
		Bound,
		NotBound
	}

	//==================================================================
	enum SccProjectType
	{
		Unknown,
		Project,
		Solution
	}

	//==================================================================
	class SccBindItem : DependencyObject
	{
		public delegate bool BindFn(IVsHierarchy hier);
		public delegate bool UnbindFn(IVsHierarchy hier);

		//-----------------------------------------------------------------------------
		public SccBindStatus SccBindStatus
		{
			get { return (SccBindStatus)this.GetValue(SccBindStatusProperty); }
			set { this.SetValue(SccBindStatusProperty, value); }
		}

		//-----------------------------------------------------------------------------
		public static readonly DependencyProperty SccBindStatusProperty =
			DependencyProperty.Register("SccBindStatus", typeof(SccBindStatus),
			typeof(SccBindItem));

		//-----------------------------------------------------------------------------
		public SccProjectType SccProjectType
		{
			get { return (SccProjectType)this.GetValue(SccProjectTypeProperty); }
			set { this.SetValue(SccProjectTypeProperty, value); }
		}

		//-----------------------------------------------------------------------------
		public static readonly DependencyProperty SccProjectTypeProperty =
			DependencyProperty.Register("SccProjectType", typeof(SccProjectType),
			typeof(SccBindItem));

		//-----------------------------------------------------------------------------
		public string Name
		{
			get { return (string)this.GetValue(NameProperty); }
			set { this.SetValue(NameProperty, value); }
		}

		//-----------------------------------------------------------------------------
		public static readonly DependencyProperty NameProperty =
			DependencyProperty.Register("Name", typeof(string),
			typeof(SccBindItem));

		//-----------------------------------------------------------------------------
		public string Path
		{
			get { return (string)this.GetValue(PathProperty); }
			set { this.SetValue(PathProperty, value); }
		}

		//-----------------------------------------------------------------------------
		public static readonly DependencyProperty PathProperty =
			DependencyProperty.Register("Path", typeof(string),
			typeof(SccBindItem));

		public IVsHierarchy Hierarchy { get; set; }
		public BindFn Bind { get; set; }
		public UnbindFn Unbind { get; set; }
	}
}
