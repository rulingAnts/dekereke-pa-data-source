// Dekereke Data Sources for Phonology Assistant
// Copyright (C) 2026 Seth Johnston
//
// SPDX-License-Identifier: AGPL-3.0-or-later OR MIT
//
// This file is part of the DekerekeToPa core library, which is DUAL-LICENSED:
// use it under the GNU Affero General Public License v3.0 or later (LICENSE),
// OR under the MIT License (LICENSE-MIT), at your option. The dual licence
// exists so this library can be contributed to MIT-licensed upstream projects
// such as Phonology Assistant. Everything else in this repository remains
// AGPL-3.0-or-later only. See LICENSING.md for the reasoning.
//
// This program is distributed in the hope that it will be useful, but WITHOUT
// ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or
// FITNESS FOR A PARTICULAR PURPOSE.

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace DekerekeToPa
{
	/// <summary>
	/// Where generated SFM files live: %LOCALAPPDATA%\PaDekereke\&lt;hash&gt;\&lt;original name&gt;.
	///
	/// Not the user's project folder (they never asked for this file), not Program
	/// Files (no write access), not %TEMP% (PA holds the path across reloads and
	/// temp cleaners are aggressive). The hash directory keeps two databases with
	/// the same file name apart.
	///
	/// The cache file deliberately KEEPS the original file name, extension and all
	/// (e.g. "Fayu_stable.xml" containing SFM text): PA's per-record DataSource
	/// column shows Path.GetFileName of the file it read, so this way the UI shows
	/// the name the user recognizes. PA's type detection is content-based, not
	/// extension-based (XML parse fails on SFM text, then the SFM sniff sees the
	/// backslash lines), so the .xml name does not confuse it.
	/// </summary>
	public static class ConversionCache
	{
		public static string RootFolder
		{
			get
			{
				return Path.Combine(
					Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
					"PaDekereke");
			}
		}

		public static string GetCachePathFor(string sourcePath)
		{
			var full = Path.GetFullPath(sourcePath);
			string hash;

			using (var sha = SHA1.Create())
			{
				var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(full.ToLowerInvariant()));
				hash = BitConverter.ToString(bytes, 0, 4).Replace("-", string.Empty).ToLowerInvariant();
			}

			return Path.Combine(RootFolder, hash, Path.GetFileName(full));
		}
	}
}
