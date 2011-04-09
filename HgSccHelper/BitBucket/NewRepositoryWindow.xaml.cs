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
using System.Windows;

namespace HgSccHelper.BitBucket
{
	/// <summary>
	/// Interaction logic for NewRepositoryWindow.xaml
	/// </summary>
	public partial class NewRepositoryWindow : Window
	{
		//-----------------------------------------------------------------------------
		public static readonly DependencyProperty RepositoryNameProperty =
			DependencyProperty.Register("RepositoryName", typeof(string),
			typeof(NewRepositoryWindow));

		//-----------------------------------------------------------------------------
		public string RepositoryName
		{
			get { return (string)this.GetValue(RepositoryNameProperty); }
			set { this.SetValue(RepositoryNameProperty, value); }
		}

		//-----------------------------------------------------------------------------
		public static readonly DependencyProperty IsPrivateProperty =
			DependencyProperty.Register("IsPrivate", typeof(bool),
			typeof(NewRepositoryWindow));

		//-----------------------------------------------------------------------------
		public bool IsPrivate
		{
			get { return (bool)this.GetValue(IsPrivateProperty); }
			set { this.SetValue(IsPrivateProperty, value); }
		}

		//-----------------------------------------------------------------------------
		public NewRepositoryWindow()
		{
			InitializeComponent();

			this.DataContext = this;
		}

		//-----------------------------------------------------------------------------
		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			textRepositoryName.SelectAll();
			textRepositoryName.Focus();
		}

		//-----------------------------------------------------------------------------
		private void Button_Click(object sender, RoutedEventArgs e)
		{
			if (String.IsNullOrEmpty(RepositoryName))
			{
				MessageBox.Show("Repository name must not be empty", "Error",
				                MessageBoxButton.OK, MessageBoxImage.Exclamation);
				return;
			}
			
			DialogResult = true;
		}
	}
}
