// Dekereke Data Sources for Phonology Assistant
// Copyright (C) 2026 Seth Johnston
//
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// This program is free software: you can redistribute it and/or modify it
// under the terms of the GNU Affero General Public License as published by
// the Free Software Foundation, either version 3 of the License, or (at your
// option) any later version.
//
// This program is distributed in the hope that it will be useful, but WITHOUT
// ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or
// FITNESS FOR A PARTICULAR PURPOSE. See the GNU Affero General Public License
// for more details.
//
// You should have received a copy of the GNU Affero General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.
//
// COMPILE-ONLY STUB of the Pa.exe API surface the add-on touches, transcribed
// from docs/pa-internals/api-surface.md (itself transcribed from Phonology
// Assistant 4.1.1, (c) SIL International, MIT). Never ship this.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Xml.Serialization;
using SilTools;

namespace SIL.FieldWorks.Common.UIAdapters
{
	// Verified against PA source 2026-08-10: this namespace is compiled INTO
	// Pa.exe (src/Pa/UIAdapter/TMInterface.cs, HelperClasses.cs). Partial
	// transcriptions - only the members the add-on uses; the real interface is
	// much larger. Partial interface stubs are safe for CALLERS (member
	// references bind by name+signature at run time); never IMPLEMENT
	// ITMAdapter against this stub.

	/// <summary>Subset of src/Pa/UIAdapter/HelperClasses.cs:47.</summary>
	public class TMItemProperties
	{
		public string Name { get; set; }
		public string Text { get; set; }
		public string CommandId { get; set; }
		public bool Enabled { get; set; }
		public bool Visible { get; set; }
		public bool Update { get; set; }
	}

	/// <summary>Subset of src/Pa/UIAdapter/TMInterface.cs:22.</summary>
	public interface ITMAdapter
	{
		void AddCommandItem(string cmdId, string message, string text, string textAlt,
			string contextMenuText, string toolTipText, string category, string statusMsg,
			System.Windows.Forms.Keys shortcutKey, string imageLabel, System.Drawing.Image image);

		void AddMenuItem(TMItemProperties itemProps, string parentItemName, string insertBeforeItem);

		TMItemProperties GetItemProperties(string name);
	}
}

namespace SIL.Pa
{
	using SIL.FieldWorks.Common.UIAdapters;
	using SIL.Pa.Model;

	public static class App
	{
		public static Mediator MsgMediator { get; internal set; }

		public static void AddMediatorColleague(IxCoreColleague colleague)
		{
			throw new NotImplementedException();
		}

		public static void RemoveMediatorColleague(IxCoreColleague colleague)
		{
			throw new NotImplementedException();
		}

		public static void ReadAddOns()
		{
			throw new NotImplementedException();
		}

		public static void CloseSplashScreen()
		{
			throw new NotImplementedException();
		}

		public static string AssemblyPath
		{
			get { throw new NotImplementedException(); }
		}

		public static string ProjectFolder { get; set; }

		public static ITMAdapter TMAdapter { get; set; }

		// Verified against PA source 2026-08-10: App.cs:726 and App.cs:857.
		public static System.Windows.Forms.Form MainForm { get; set; }

		public static PaProject Project { get; set; }

		public static List<Assembly> AddOnAssemblys { get; private set; }

		public static List<object> AddOnManagers { get; private set; }
	}
}

namespace SIL.Pa.DataSource.FieldWorks
{
	/// <summary>
	/// Empty placeholder; the add-on never touches FieldWorks sources.
	/// Namespace verified against PA source 2026-08-10
	/// (src/Pa/DataSourceClasses/FieldWorks/FwDataSourceInfo.cs:19).
	/// </summary>
	public class FwDataSourceInfo
	{
	}
}

namespace SIL.Pa.DataSource
{
	using SIL.Pa.DataSource.FieldWorks;
	using SIL.Pa.Model;

	public enum DataSourceType
	{
		PAXML, FW, FW7, SA, SFM, Toolbox, XML, LIFT, Unknown
	}

	public enum DataSourceParseType
	{
		PhoneticOnly, None, OneToOne, Interlinear
	}

	[XmlType("DataSource")]
	public class PaDataSource
	{
		public const string kRecordMarker = "RecMrkr";
		public const string kShoeboxMarker = "\\_sh ";

		public PaDataSource()
		{
			throw new NotImplementedException();
		}

		public PaDataSource(IEnumerable<PaField> projectFields, FwDataSourceInfo fwDbItem)
		{
			throw new NotImplementedException();
		}

		public PaDataSource(IEnumerable<PaField> fields, string filename)
		{
			throw new NotImplementedException();
		}

		public PaDataSource Copy()
		{
			throw new NotImplementedException();
		}

		public IEnumerable<string> GetSfMarkers(bool showMsgOnError)
		{
			throw new NotImplementedException();
		}

		public bool VerifyMappings()
		{
			throw new NotImplementedException();
		}

		public bool UpdateLastModifiedTime()
		{
			throw new NotImplementedException();
		}

		public static DataSourceType GetPaXmlType(string filename, out string fwServer, out string fwDBname)
		{
			throw new NotImplementedException();
		}

		[XmlElement("DataSourceFile")]
		public string SourceFile { get; set; }

		public DataSourceType Type { get; set; }
		public DataSourceParseType ParseType { get; set; }
		public string SfmRecordMarker { get; set; }
		public string XSLTFile { get; set; }
		public string FirstInterlinearField { get; set; }
		public int TotalLinesInFile { get; set; }
		public string ToolboxSortField { get; set; }
		public string Editor { get; set; }
		public bool SkipLoading { get; set; }
		public List<FieldMapping> FieldMappings { get; set; }
		public FwDataSourceInfo FwDataSourceInfo { get; set; }

		[XmlIgnore]
		public bool SkipLoadingBecauseOfProblem { get; set; }

		[XmlIgnore]
		public DateTime LastModification { get; set; }

		[XmlIgnore]
		public bool IsSfmType
		{
			get { throw new NotImplementedException(); }
		}

		[XmlIgnore]
		public string TypeAsString
		{
			get { throw new NotImplementedException(); }
		}

		[XmlIgnore]
		public bool FwSourceDirectFromDB
		{
			get { throw new NotImplementedException(); }
		}

		public string DisplayTextWhenReading
		{
			get { throw new NotImplementedException(); }
		}
	}
}

namespace SIL.Pa.Model
{
	using SIL.Pa.DataSource;

	public enum FieldType
	{
		GeneralText, GeneralNumeric, GeneralFilePath, Date,
		Reference, Phonetic, AudioFilePath
	}

	[XmlType("field")]
	public class PaField
	{
		public const string kPhoneticFieldName = "Phonetic";
		public const string kCVPatternFieldName = "CVPattern";
		public const string kDataSourceFieldName = "DataSource";
		public const string kDataSourcePathFieldName = "DataSourcePath";
		public const string kAudioFileFieldName = "AudioFile";
		public const string kPhoneticSourceFieldName = "Phonetic Source";

		public PaField()
		{
		}

		public PaField(string name)
		{
			throw new NotImplementedException();
		}

		public PaField(string name, FieldType type)
		{
			throw new NotImplementedException();
		}

		public string Name { get; set; }
		public FieldType Type { get; set; }

		public bool IsCollection
		{
			get { throw new NotImplementedException(); }
		}

		public string[] GetPossibleDataSourceFieldNames()
		{
			throw new NotImplementedException();
		}

		public PaField Copy()
		{
			throw new NotImplementedException();
		}
	}

	[XmlType("mapping")]
	public class FieldMapping
	{
		public FieldMapping()
		{
		}

		public FieldMapping(PaField field, bool isParsed)
		{
			throw new NotImplementedException();
		}

		public FieldMapping(PaField field, string parsedFields)
		{
			throw new NotImplementedException();
		}

		public FieldMapping(PaField field, IEnumerable<string> parsedFields)
		{
			throw new NotImplementedException();
		}

		public FieldMapping(string nameInSource, PaField field, bool isParsed)
		{
			throw new NotImplementedException();
		}

		[XmlAttribute("nameInSource")]
		public string NameInDataSource { get; set; }

		[XmlElement("paFieldName")]
		public string PaFieldName { get; set; }

		[XmlElement("isParsed")]
		public bool IsParsed { get; set; }

		[XmlElement("isInterlinear")]
		public bool IsInterlinear { get; set; }

		[XmlElement("fwWritingSystem")]
		public string FwWsId { get; set; }

		[XmlIgnore]
		public PaField Field { get; set; }

		public FieldMapping Copy()
		{
			throw new NotImplementedException();
		}
	}

	public class PaProject : IDisposable
	{
		public List<PaDataSource> DataSources { get; set; }

		public IEnumerable<PaField> Fields { get; private set; }

		public string Folder
		{
			get { throw new NotImplementedException(); }
		}

		[XmlElement("name")]
		public string Name { get; set; }

		public string ProjectPathFilePrefix
		{
			get { throw new NotImplementedException(); }
		}

		public void Save()
		{
			throw new NotImplementedException();
		}

		public void ReloadDataSources()
		{
			throw new NotImplementedException();
		}

		public void CheckForModifiedDataSources()
		{
			throw new NotImplementedException();
		}

		public void Dispose()
		{
			throw new NotImplementedException();
		}
	}
}
