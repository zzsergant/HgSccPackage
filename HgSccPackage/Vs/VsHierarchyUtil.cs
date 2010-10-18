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

// This code is based on LearnVSXNow articles

using System;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio;
using System.ComponentModel;
using System.Globalization;

//=============================================================================
namespace HgSccPackage.Vs
{
	//=============================================================================
	[TypeConverter(typeof(HierarchyIdTypeConverter))]
	public struct HierarchyId
	{
		private readonly uint id;

		public static HierarchyId Root = new HierarchyId(VSConstants.VSITEMID_ROOT);
		public static HierarchyId Nil = new HierarchyId(VSConstants.VSITEMID_NIL);
		public static HierarchyId Selection = new HierarchyId(VSConstants.VSITEMID_SELECTION);

		//-----------------------------------------------------------------------------
		public HierarchyId(uint id)
		{
			this.id = id;
		}

		//-----------------------------------------------------------------------------
		public HierarchyId(int id)
		{
			this.id = (uint)id;
		}

		//-----------------------------------------------------------------------------
		public uint Value
		{
			get { return id; }
		}

		//-----------------------------------------------------------------------------
		public bool IsNil
		{
			get { return id == Nil.id; }
		}

		//-----------------------------------------------------------------------------
		public bool IsRoot
		{
			get { return id == Root.id; }
		}

		//-----------------------------------------------------------------------------
		public bool IsSelection
		{
			get { return id == Selection.id; }
		}

		//-----------------------------------------------------------------------------
		public static explicit operator int(HierarchyId id)
		{
			return (int)id.id;
		}

		//-----------------------------------------------------------------------------
		public static explicit operator uint(HierarchyId id)
		{
			return id.id;
		}

		//-----------------------------------------------------------------------------
		public static implicit operator HierarchyId(int id)
		{
			return new HierarchyId(id);
		}

		//-----------------------------------------------------------------------------
		public static implicit operator HierarchyId(uint id)
		{
			return new HierarchyId(id);
		}

		//-----------------------------------------------------------------------------
		public override string ToString()
		{
			if (id == VSConstants.VSITEMID_ROOT) return "ROOT";
			if (id == VSConstants.VSITEMID_NIL) return "NIL";
			if (id == VSConstants.VSITEMID_NIL) return "SELECTION";
			return id.ToString();
		}

		//-----------------------------------------------------------------------------
		public static bool TryParse(string value, out HierarchyId id)
		{
			if (String.Compare("root", value, true) == 0)
			{
				id = Root;
				return true;
			}
			if (String.Compare("nil", value, true) == 0)
			{
				id = Nil;
				return true;
			}
			if (String.Compare("selection", value, true) == 0)
			{
				id = Selection;
				return true;
			}
			int int_id;
			if (Int32.TryParse(value, out int_id))
			{
				id = new HierarchyId(int_id);
				return true;
			}
			uint uint_id;
			if (UInt32.TryParse(value, out uint_id))
			{
				id = new HierarchyId(uint_id);
				return true;
			}
			id = Nil;
			return false;
		}

		//-----------------------------------------------------------------------------
		public static HierarchyId Parse(string value)
		{
			HierarchyId result;
			if (TryParse(value, out result)) return result;
			throw new InvalidCastException();
		}
	}

	//=============================================================================
	public class HierarchyIdTypeConverter : TypeConverter
	{
		//-----------------------------------------------------------------------------
		public override bool CanConvertFrom(ITypeDescriptorContext context, Type source_type)
		{
			if (source_type == typeof(string) || source_type == typeof(int) ||
			  source_type == typeof(uint)) return true;
			return base.CanConvertFrom(context, source_type);
		}

		//-----------------------------------------------------------------------------
		public override bool CanConvertTo(ITypeDescriptorContext context, Type destination_type)
		{
			if (destination_type == typeof(string) || destination_type == typeof(int) ||
			  destination_type == typeof(uint)) return true;
			return base.CanConvertTo(context, destination_type);
		}

		//-----------------------------------------------------------------------------
		public override object ConvertFrom(ITypeDescriptorContext context,
		  CultureInfo culture, object value)
		{
			if (value is int) return new HierarchyId((int)value);
			if (value is uint) return new HierarchyId((uint)value);
			var str_value = value as string;
			if (str_value != null) return HierarchyId.Parse(str_value);
			return base.ConvertFrom(context, culture, value);
		}

		//-----------------------------------------------------------------------------
		public override object ConvertTo(ITypeDescriptorContext context,
		  CultureInfo culture, object value, Type destination_type)
		{
			if (!(value is HierarchyId)) return null;
			var hier_value = (HierarchyId)value;
			if (destination_type == typeof(int)) return (int)hier_value;
			if (destination_type == typeof(uint)) return (uint)hier_value;
			if (destination_type == typeof(string)) return hier_value.ToString();
			return base.ConvertTo(context, culture, value, destination_type);
		}
	}

	//=============================================================================
	public class HierarchyItem
	{
		private readonly IVsHierarchy hierarchy;
		private readonly uint item_id;

		//-----------------------------------------------------------------------------
		public HierarchyItem(IVsHierarchy hierarchy, uint item_id)
		{
			this.hierarchy = hierarchy;
			this.item_id = item_id;
		}

		//-----------------------------------------------------------------------------
		[Browsable(false)]
		public IVsHierarchy Hierarchy
		{
			get { return hierarchy; }
		}

		//-----------------------------------------------------------------------------
		[DisplayName("ID")]
		[Category("Identification")]
		[Description("Item ID within the hierarchy.")]
		public HierarchyId Id
		{
			get { return item_id; }
		}

		//-----------------------------------------------------------------------------
		[DisplayName("Parent Node ID")]
		[Category("Hierarchy info")]
		[Description("Item ID of the parent node; VSITEMID_NIL, if no parent.")]
		public HierarchyId ParentId
		{
			get { return GetProperty<int>(__VSHPROPID.VSHPROPID_Parent); }
		}

		//-----------------------------------------------------------------------------
		[DisplayName("Name")]
		[Category("Names and Paths")]
		[Description("Name of the item.")]
		public string Name
		{
			get { return GetProperty<string>(__VSHPROPID.VSHPROPID_Name); }
		}

		//-----------------------------------------------------------------------------
		[Browsable(false)]
		public HierarchyItem ParentHierarchy
		{
			get
			{
				var parent_hierarchy = GetProperty(__VSHPROPID.VSHPROPID_ParentHierarchy)
				  as IVsHierarchy;
				return parent_hierarchy == null
				  ? null
				  : new HierarchyItem(parent_hierarchy, VSConstants.VSITEMID_ROOT);
			}
		}

		//-----------------------------------------------------------------------------
		[DisplayName("Item ID in Parent Hierarchy")]
		[Category("Identification")]
		[Description("If the current hierachy is nested into a parent hierarchy, " +
		  "this value tells the ID used in the parent hierarchy.")]
		public HierarchyId ParentHierarchyItemId
		{
			get
			{
				object id = GetProperty(__VSHPROPID.VSHPROPID_ParentHierarchyItemid);
				if (id is int) return (uint)(int)id;
				if (id is uint) return (uint)id;
				return HierarchyId.Nil;
			}
		}

		//-----------------------------------------------------------------------------
		[DisplayName("Is Nested Hierarchy?")]
		[Category("Hierarchy info")]
		[Description("This flag tells if this hierarchy item is nested into an " +
		  "owner hierarchy or not.")]
		public bool IsNestedHierachy
		{
			get { return Id.IsRoot && !ParentHierarchyItemId.IsNil; }
		}

		//-----------------------------------------------------------------------------
		[DisplayName("First Child ID")]
		[Category("Hierarchy info")]
		[Description("Item ID of the first child node; VSITEMID_NIL, if this item" +
		  " has no children.")]
		public HierarchyId FirstChild
		{
			get { return GetProperty<int>(__VSHPROPID.VSHPROPID_FirstChild); }
		}

		//-----------------------------------------------------------------------------
		[DisplayName("First Visible Child ID")]
		[Category("Hierarchy info")]
		[Description("Item ID of the first visible child node; VSITEMID_NIL, " +
		  "if this item has no visible children.")]
		public HierarchyId FirstVisibleChild
		{
			get { return GetProperty<int>(__VSHPROPID.VSHPROPID_FirstVisibleChild); }
		}

		//-----------------------------------------------------------------------------
		[DisplayName("Next Sibling ID")]
		[Category("Hierarchy info")]
		[Description("Item ID of the subsquent sibling of this item; " +
		  "VSITEMID_NIL, if this item has no more siblings.")]
		public HierarchyId NextSibling
		{
			get { return GetProperty<int>(__VSHPROPID.VSHPROPID_NextSibling); }
		}

		//-----------------------------------------------------------------------------
		[DisplayName("Next Visible Sibling ID")]
		[Category("Hierarchy info")]
		[Description("Item ID of the subsquent visible sibling of this item; " +
		  "VSITEMID_NIL, if this item has no more siblings.")]
		public HierarchyId NextVisibleSibling
		{
			get { return GetProperty<int>(__VSHPROPID.VSHPROPID_NextVisibleSibling); }
		}

		//-----------------------------------------------------------------------------
		protected T GetProperty<T>(__VSHPROPID prop_id)
		{
			return (T)GetProperty(prop_id);
		}

		//-----------------------------------------------------------------------------
		protected T GetProperty<T>(int prop_id)
		{
			return (T)GetProperty(prop_id);
		}

		//-----------------------------------------------------------------------------
		protected object GetProperty(__VSHPROPID prop_id)
		{
			return GetProperty((int)prop_id);
		}

		//-----------------------------------------------------------------------------
		protected object GetProperty(int prop_id)
		{
			if (prop_id == (int)__VSHPROPID.VSHPROPID_NIL) return null;
			object prop_value;
			hierarchy.GetProperty(item_id, prop_id, out prop_value);
			return prop_value;
		}
	}

	//=============================================================================
	public class HierarchyTraversalInfo
	{
		public HierarchyItem HierarchyNode { get; private set; }
		public int Depth { get; private set; }

		//-----------------------------------------------------------------------------
		public HierarchyTraversalInfo(HierarchyItem hierarchy_node, int depth)
		{
			HierarchyNode = hierarchy_node;
			Depth = depth;
		}

		//-----------------------------------------------------------------------------
		public HierarchyTraversalInfo(IVsHierarchy hierarchy_node, uint item_id,
		  int depth)
		{
			HierarchyNode = new HierarchyItem(hierarchy_node, item_id);
			Depth = depth;
		}
	}
}