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
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HgSccHelper.UI;

//==================================================================
namespace HgSccHelper
{
	//==================================================================
	public class RevLogLinesVisual : FrameworkElement
	{
		DrawingVisual visual;
		RevLogLinesPair lines;

		//------------------------------------------------------------------
		private Pen BlackPen
		{
			get { return ThemeManager.Instance.Current.RevLogLinePen; }
		}

		//------------------------------------------------------------------
		private Pen BluePen
		{
			get { return ThemeManager.Instance.Current.RevLogNodePen; }
		}

		//------------------------------------------------------------------
		public RevLogLinesVisual()
		{
			this.visual = new DrawingVisual();
			
			AddVisualChild(visual);
			AddLogicalChild(visual);
		}

		//------------------------------------------------------------------
		private void RenderWpf(double width, double height)
		{
			if (lines != null)
			{
				if (Double.IsNaN(height) || width == 0)
					return;

				var step = height;
				var offset_x = step / 2;

				using (DrawingContext context = visual.RenderOpen())
				{
					var rc = new Rect(0, 0, width, height);
					context.PushClip(new RectangleGeometry(rc));

					var center = new Point(offset_x + lines.Current.Pos * step, step / 2);

					if (lines.Prev != null)
					{
						foreach (var line in lines.Prev.Lines)
						{
							var p1 = new Point(offset_x + step * line.X1,
								step / 2 - step);

							var p2 = new Point(offset_x + step * line.X2,
								step + step / 2 - step);

							context.DrawLine(BlackPen, p1, p2);
						}
					}
					foreach (var line in lines.Current.Lines)
					{
						var p1 = new Point(offset_x + step * line.X1,
							step / 2);

						var p2 = new Point(offset_x + step * line.X2,
							step + step / 2);

						context.DrawLine(BlackPen, p1, p2);
					}

					context.DrawEllipse(BluePen.Brush, null, center, 4, 4);
					context.Pop();
					context.Close();
				}
			}
		}

		//------------------------------------------------------------------
		protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
		{
			base.OnRenderSizeChanged(sizeInfo);
			Render(sizeInfo.NewSize.Width, sizeInfo.NewSize.Height);
		}

		//------------------------------------------------------------------
		private void Render(double width, double height)
		{
			RenderWpf(width, height);
		}

		//------------------------------------------------------------------
		protected override Size ArrangeOverride(Size finalSize)
		{
			if (!Double.IsNaN(finalSize.Height))
			{
				if (lines != null)
				{
					var h = Math.Ceiling(finalSize.Height);
					var text_padding = 5;
					var size = new Size(h * lines.Current.Count + text_padding, h);
					var f = base.ArrangeOverride(size);

					if (Width == f.Width && Height == f.Height)
					{
						// If the ListView is recycling visual items
						// we need to redraw graph

						Render(Width, Height);
					}

					this.Width = f.Width;
					this.Height = f.Height;
					return f;
				}
			}
			return base.ArrangeOverride(finalSize);
		}

		#region Necessary Overrides -- Needed by WPF to maintain bookkeeping of our hosted visuals
		protected override int VisualChildrenCount
		{
			get { return 1; }
		}

		protected override Visual GetVisualChild(int index)
		{
			if (index < 0 || index >= 1)
			{
				throw new IndexOutOfRangeException("index");
			}

			return visual;
		}
		#endregion

		//-----------------------------------------------------------------------------
		/// <summary>
		/// </summary>
		public static readonly DependencyProperty RevLogLinesProperty =
			DependencyProperty.Register("RevLogLines", typeof(RevLogLinesPair),
			typeof(RevLogLinesVisual), new FrameworkPropertyMetadata(
				null,
				FrameworkPropertyMetadataOptions.AffectsArrange,
				new PropertyChangedCallback(RevLogLinesChanged)));

		//-----------------------------------------------------------------------------
		/// <summary>
		/// </summary>
		public RevLogLinesPair RevLogLines
		{
			get { return (RevLogLinesPair)this.GetValue(RevLogLinesProperty); }
			set { this.SetValue(RevLogLinesProperty, value); }
		}

		//-----------------------------------------------------------------------------
		/// <summary>
		/// </summary>
		/// <param name="d"></param>
		/// <param name="e"></param>
		private static void RevLogLinesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var visual = (RevLogLinesVisual)d;
			var rev_log_lines = (RevLogLinesPair)e.NewValue;

			visual.lines = rev_log_lines;
		}
	}
}
