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
using Avalonia.Interactivity;
using CUERipper.Avalonia.Exceptions;
using CUERipper.Avalonia.Models;
using CUERipper.Avalonia.Services.Abstractions;
using CUERipper.Avalonia.ViewModels.Bindings;
using CUERipper.Avalonia.ViewModels.UserControls;
using CUERipper.Avalonia.Views.UserControls.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace CUERipper.Avalonia.Views.UserControls;

public sealed partial class MetaGrid : CUEGrid<MetaGridColumnKey
        , GridColumnDefinition<MetaGridColumnKey>
        , EditableFieldProxy
    >, ICUEUserControl
{
    public MetaGridViewModel ViewModel => DataContext as MetaGridViewModel
        ?? throw new ViewModelMismatchException(typeof(MetaGridViewModel), DataContext?.GetType());

    public MetaGrid()
    {
        InitializeComponent();
    }

    public void Init(IServiceProvider serviceProvider)
    {
        var metaService = serviceProvider.GetRequiredService<ICUEMetaService>();
        var localizer = serviceProvider.GetRequiredService<IStringLocalizer<Language>>();

        DataContext = new MetaGridViewModel(metaService, localizer);

        DefineColumns();
        InitGrid(metaGrid);
    }

    public void DefineColumns()
    {
        Columns.Add(MetaGridColumnKey.Field, new GridColumnDefinition<MetaGridColumnKey> {
            Header = nameof(EditableFieldProxy.Field)
            , HeaderBinding = false
            , Binding = nameof(EditableFieldProxy.Field)
            , ReadOnly = true
            , Clipboard = true
            , Create = CreateTextColumn
        });

        Columns.Add(MetaGridColumnKey.Value, new GridColumnDefinition<MetaGridColumnKey> {
            Header = nameof(EditableFieldProxy.Value)
            , HeaderBinding = false
            , Binding = nameof(EditableFieldProxy.Value)
            , ReadOnly = false
            , Clipboard = true
            , Create = CreateTextColumn
        });        
    }

    public void Clear() => ViewModel.Clear();
}