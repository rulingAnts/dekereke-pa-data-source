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
// COMPILE-ONLY STUB of the SilTools.dll API surface the add-on touches,
// transcribed from docs/pa-internals/api-surface.md (itself transcribed from
// Phonology Assistant 4.1.1, (c) SIL International, MIT). Never ship this.

using System;

namespace SilTools
{
	/// <summary>See api-surface.md. The one member the mediator requires.</summary>
	public interface IxCoreColleague
	{
		IxCoreColleague[] GetMessageTargets();
	}

	/// <summary>
	/// See api-surface.md. SendMessage("Foo", arg) invokes OnFoo(object arg) on
	/// each registered colleague (handlers may be protected; bool return, true
	/// stops propagation).
	/// </summary>
	public sealed class Mediator : IDisposable
	{
		public void AddColleague(IxCoreColleague colleague)
		{
			throw new NotImplementedException();
		}

		public void RemoveColleague(IxCoreColleague colleague)
		{
			throw new NotImplementedException();
		}

		public object SendMessage(string message, object parameter)
		{
			throw new NotImplementedException();
		}

		public void PostMessage(string message, object parameter)
		{
			throw new NotImplementedException();
		}

		public void Dispose()
		{
			throw new NotImplementedException();
		}
	}

}
