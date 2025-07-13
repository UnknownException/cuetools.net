#region Copyright (C) 2025 Max Visser
/*
    Copyright (C) 2025 Max Visser

    This program is free software; you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation; either version 2 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License along
    with this program; if not, see <https://www.gnu.org/licenses/>.
*/
#endregion
using System;

namespace CUERipper.Avalonia.Views.UserControls.Abstractions
{
    public interface ICUEUserControl
    {
        /// <summary>
        /// Initialize component
        /// </summary>
        /// <param name="serviceProvider"></param>
        // Allow the component to resolve its own depenencies beside the constructor.
        // Avalonia controls must have parameterless constructors.
        void Init(IServiceProvider serviceProvider);
    }
}