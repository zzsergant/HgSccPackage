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
using Gajatko.IniFiles;

namespace HgSccHelper
{
	/// <summary>
	/// Interaction logic for SynchronizeSettingsWindow.xaml
	/// </summary>
	public partial class SynchronizeSettingsWindow : Window
	{
		//-----------------------------------------------------------------------------
		public static RoutedUICommand AddCommand = new RoutedUICommand("Add",
			"Add", typeof(SynchronizeSettingsWindow));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand EditCommand = new RoutedUICommand("Edit",
			"Edit", typeof(SynchronizeSettingsWindow));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand RemoveCommand = new RoutedUICommand("Remove",
			"Remove", typeof(SynchronizeSettingsWindow));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand SetAsDefaultCommand = new RoutedUICommand("Set as default",
			"SetAsDefault", typeof(SynchronizeSettingsWindow));

		const string paths_section = "paths";

		//-----------------------------------------------------------------------------
		public string WorkingDir { get; set; }

		string HgrcPath { get; set; }
		ObservableCollection<PathItem> paths;
		IniFile hgrc;

		public const string CfgPath = @"GUI\SynchronizeSettingsWindow";
		CfgWindowPosition wnd_cfg;

		//-----------------------------------------------------------------------------
		public SynchronizeSettingsWindow()
		{
			wnd_cfg = new CfgWindowPosition(CfgPath, this);

			InitializeComponent();

			hgrc = new IniFile();
			paths = new ObservableCollection<PathItem>();

			listPaths.ItemsSource = paths;
			WorkingDir = "";

			listPaths.ItemContainerGenerator.StatusChanged += ListItemContainerGenerator_StatusChanged;
		}

		//-----------------------------------------------------------------------------
		void ListItemContainerGenerator_StatusChanged(object sender, EventArgs e)
		{
			var generator = (ItemContainerGenerator)sender;
			if (generator.Status == System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
			{
				if (listPaths.SelectedIndex != -1)
				{
					var item = (ListViewItem)listPaths.ItemContainerGenerator.ContainerFromIndex(listPaths.SelectedIndex);
					if (item != null)
						item.Focus();
				}
			}
		}

		//-----------------------------------------------------------------------------
		void OnPathAdded(PathItem path)
		{
			paths.Add(path);
			hgrc[paths_section][path.Alias] = path.Path;

			SelectAndFocusItem(listPaths.Items.Count - 1);
		}

		//-----------------------------------------------------------------------------
		void OnPathChanged(PathItem path)
		{
			int idx = GetPathIndex(path.Alias);
			paths[idx] = path;

			hgrc[paths_section][path.Alias] = path.Path;

			SelectAndFocusItem(idx);
		}

		//-----------------------------------------------------------------------------
		void OnPathRemoved(PathItem path)
		{
			var idx = GetPathIndex(path.Alias);
			paths.RemoveAt(idx);
			hgrc[paths_section].DeleteKey(path.Alias);

			SelectAndFocusItem(idx);
		}

		//-----------------------------------------------------------------------------
		void OnPathRenamed(string old_alias, PathItem path)
		{
			int new_path_idx = GetPathIndex(path.Alias);
			int idx = GetPathIndex(old_alias);

			if (new_path_idx != -1)
			{
				// Replacing path

				var replaced_path = paths[new_path_idx];
				paths[new_path_idx] = path;

				SelectAndFocusItem(new_path_idx);
				paths.RemoveAt(idx);
			}
			else
			{
				// Just renaming
				paths[idx] = path;

				SelectAndFocusItem(idx);
			}

			hgrc[paths_section].DeleteKey(old_alias);
			hgrc[paths_section][path.Alias] = path.Path;
		}

		//-----------------------------------------------------------------------------
		void SelectAndFocusItem(int idx)
		{
			if (idx < (listPaths.Items.Count - 1))
				listPaths.SelectedIndex = idx;
			else
			{
				if (listPaths.Items.Count > 0)
				    listPaths.SelectedIndex = listPaths.Items.Count - 1;
			}

			if (listPaths.SelectedIndex != -1)
			{
				var list_item = listPaths.ItemContainerGenerator.ContainerFromIndex(listPaths.SelectedIndex) as ListViewItem;
				if (list_item != null)
					list_item.Focus();
			}
		}

		//-----------------------------------------------------------------------------
		int GetPathIndex(string alias)
		{
			int found_idx = -1;
			for (int i = 0; i < paths.Count; ++i)
			{
				if (paths[i].Alias == alias)
				{
					found_idx = i;
					break;
				}
			}

			return found_idx;
		}

		//-----------------------------------------------------------------------------
		void AddOrEditPath(PathItem new_path)
		{
			bool quit = false;
			var prev_alias = new_path.Alias;

			while (!quit)
			{
				var wnd = new PathEditWindow();
				wnd.Alias = new_path.Alias;
				wnd.Path = new_path.Path;
				wnd.WorkingDir = WorkingDir;

				if (wnd.ShowDialog() != true)
					return;

				new_path.Alias = wnd.Alias;
				new_path.Path = wnd.Path;

				int found_idx = GetPathIndex(new_path.Alias);
				if (found_idx == -1)
				{
					if (String.IsNullOrEmpty(prev_alias))
						OnPathAdded(new_path);
					else
						OnPathRenamed(prev_alias, new_path);
					return;
				}
				else
				{
					if (prev_alias == new_path.Alias)
					{
						// It is edited alias, no need to prompt for override
						OnPathChanged(new_path);
						return;
					}
					else
					{
						var msg = String.Format("The path with alias = '{0}' is allready exists.\nAre you sure to override it ?",
							new_path.Alias);

						var result = MessageBox.Show(msg, "Warning", MessageBoxButton.OKCancel, MessageBoxImage.Question);
						if (result == MessageBoxResult.OK)
						{
							if (String.IsNullOrEmpty(prev_alias))
								OnPathChanged(new_path);
							else
								OnPathRenamed(prev_alias, new_path);
							return;
						}
					}
				}
			}
		}

		//-----------------------------------------------------------------------------
		private void root_Loaded(object sender, RoutedEventArgs e)
		{
			Title = string.Format("Syncrhonize Settings: '{0}'", WorkingDir);

			HgrcPath = System.IO.Path.Combine(WorkingDir, @".hg\hgrc");
			hgrc = IniFile.FromFile(HgrcPath);

			var section = hgrc[paths_section];
			foreach (var key in section.GetKeys())
			{
				paths.Add(new PathItem { Alias = key, Path = section[key] });
			}

			listPaths.Focus();
			SelectAndFocusItem(0);
		}

		//-----------------------------------------------------------------------------
		private void Add_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = true;
			e.Handled = true;
		}

		//-----------------------------------------------------------------------------
		private void Add_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			AddOrEditPath(new PathItem());
		}

		//-----------------------------------------------------------------------------
		private void SelectedItem_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			if (listPaths != null)
				e.CanExecute = listPaths.SelectedItem != null;

			e.Handled = true;
		}

		//-----------------------------------------------------------------------------
		private void Edit_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var path_item = listPaths.SelectedItem as PathItem;
			if (path_item != null)
				AddOrEditPath(new PathItem { Alias = path_item.Alias, Path = path_item.Path });
		}

		//-----------------------------------------------------------------------------
		private void Remove_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var path_item = listPaths.SelectedItem as PathItem;
			if (path_item != null)
				OnPathRemoved(new PathItem { Alias = path_item.Alias, Path = path_item.Path });
		}

		//-----------------------------------------------------------------------------
		private void SetAsDefault_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var default_alias = "default";

			var selected_item = listPaths.SelectedItem as PathItem;
			if (selected_item == null)
				return;

			if (selected_item.Alias == default_alias)
				return;

			var old_alias = selected_item.Alias;

			var path_item = new PathItem { Alias = default_alias, Path = selected_item.Path };

			var default_idx = GetPathIndex(default_alias);
			if (default_idx != -1)
			{
				var msg = String.Format("The path with alias = '{0}' is allready exists.\nAre you sure to override it ?",
					default_alias);

				var result = MessageBox.Show(msg, "Warning", MessageBoxButton.OKCancel, MessageBoxImage.Question);
				if (result != MessageBoxResult.OK)
					return;
			}

			OnPathRenamed(old_alias, path_item);
		}

		//-----------------------------------------------------------------------------
		private void btnSave_Click(object sender, RoutedEventArgs e)
		{
			hgrc.Save(HgrcPath);
			DialogResult = true;
			Close();
		}

		//-----------------------------------------------------------------------------
		private void btnCancel_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}

		//-----------------------------------------------------------------------------
		private void root_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			listPaths.ItemContainerGenerator.StatusChanged -= ListItemContainerGenerator_StatusChanged;
		}

		//------------------------------------------------------------------
		private void ListPaths_MouseDoubleClick(object sender, MouseEventArgs e)
		{
			if (EditCommand != null)
			{
				if (EditCommand.CanExecute(sender, e.Source as IInputElement))
					EditCommand.Execute(sender, e.Source as IInputElement);
			}
		}
	}

	//=============================================================================
	class PathItem : DependencyObject
	{
		//-----------------------------------------------------------------------------
		public string Alias
		{
			get { return (string)GetValue(AliasProperty); }
			set { SetValue(AliasProperty, value); }
		}

		//-----------------------------------------------------------------------------
		public static readonly DependencyProperty AliasProperty =
			DependencyProperty.Register("Alias", typeof(string), typeof(PathItem));

		//-----------------------------------------------------------------------------
		public string Path
		{
			get { return (string)GetValue(PathProperty); }
			set { SetValue(PathProperty, value); }
		}

		//-----------------------------------------------------------------------------
		public static readonly DependencyProperty PathProperty =
			DependencyProperty.Register("Path", typeof(string), typeof(PathItem),
			new PropertyMetadata(new PropertyChangedCallback(OnPathChanged)));

		//-----------------------------------------------------------------------------
		public string PathView
		{
			get { return (string)GetValue(PathViewProperty); }
			private set { SetValue(PathViewProperty, value); }
		}

		//-----------------------------------------------------------------------------
		public static readonly DependencyProperty PathViewProperty =
			DependencyProperty.Register("PathView", typeof(string), typeof(PathItem));		

		//-----------------------------------------------------------------------------
		static void OnPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs args)
		{
			var PathItem = d as PathItem;
			PathItem.PathView = Util.HideUrlPassword(args.NewValue as string);
		}
	}
}
