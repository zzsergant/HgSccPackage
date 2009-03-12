using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Xml.Serialization;

//=============================================================================
namespace HgSccHelper
{
	//-----------------------------------------------------------------------------
	[Serializable]
	public class HgOptions
	{
		public string DiffTool { get; set; }

		public HgOptions()
		{
			DiffTool = "";
		}
	}

	//-----------------------------------------------------------------------------
	public sealed class HgSccOptions
	{
		static readonly HgSccOptions instance = new HgSccOptions();
		HgOptions options;

		// Explicit static constructor to tell C# compiler
		// not to mark type as beforefieldinit
		static HgSccOptions()
		{
		}

		//-----------------------------------------------------------------------------
		private HgSccOptions()
		{
			options = Load();
			if (options == null)
				options = new HgOptions();
		}

		//-----------------------------------------------------------------------------
		public static HgOptions Options
		{
			get
			{
				return instance.options;
			}
		}

		//-----------------------------------------------------------------------------
		private static string CfgPath
		{
			get
			{
				string appdata = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
				string hgdata = Path.Combine(appdata, "HgScc");
				if (!Directory.Exists(hgdata))
					Directory.CreateDirectory(hgdata);

				return Path.Combine(hgdata, "hgscc.xml");
			}
		}

		//-----------------------------------------------------------------------------
		public static void Save()
		{
			try
			{
				string cfg = CfgPath;

				// Create an instance of the XmlSerializer class;
				// specify the type of object to serialize.
				XmlSerializer serializer = new XmlSerializer(typeof(HgOptions));
				using (var writer = new StreamWriter(cfg))
				{
					// Serialize the purchase order, and close the TextWriter.
					serializer.Serialize(writer, Options);
				}
			}
			catch (System.Exception e)
			{
				System.Windows.Forms.MessageBox.Show(e.Message);
			}
		}

		//-----------------------------------------------------------------------------
		private static HgOptions Load()
		{
			try
			{
				string cfg = CfgPath;
				if (File.Exists(cfg))
				{
					// Create an instance of the XmlSerializer class;
					// specify the type of object to be deserialized.
					XmlSerializer serializer = new XmlSerializer(typeof(HgOptions));

					/* If the XML document has been altered with unknown 
					nodes or attributes, handle them with the 
					UnknownNode and UnknownAttribute events.*/

					serializer.UnknownNode += new XmlNodeEventHandler(serializer_UnknownNode);
					serializer.UnknownAttribute += new XmlAttributeEventHandler(serializer_UnknownAttribute);

					using (var fs = new FileStream(cfg, FileMode.Open))
					{
						// Declare an object variable of the type to be deserialized.
						var o = (HgOptions)serializer.Deserialize(fs);
						if (o != null)
							return o;
					}
				}
			}
			catch (System.Exception e)
			{
				System.Windows.Forms.MessageBox.Show(e.Message);
			}

			return new HgOptions();
		}

		//-----------------------------------------------------------------------------
		private static void serializer_UnknownNode(object sender, XmlNodeEventArgs e)
		{
//			Console.WriteLine("Unknown Node:" + e.Name + "\t" + e.Text);
		}

		//-----------------------------------------------------------------------------
		private static void serializer_UnknownAttribute(object sender, XmlAttributeEventArgs e)
		{
// 			System.Xml.XmlAttribute attr = e.Attr;
// 			Console.WriteLine("Unknown attribute " +
// 			attr.Name + "='" + attr.Value + "'");
		}
	}
}
