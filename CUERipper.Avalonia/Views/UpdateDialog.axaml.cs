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
using Avalonia.Controls;
using System.Threading.Tasks;
using CUERipper.Avalonia.Extensions;
using CUERipper.Avalonia.Services.Abstractions;
using System;
using Avalonia.Interactivity;
using CUERipper.Avalonia.Events;
using Avalonia.Threading;
using CUERipper.Avalonia.Views;
using Microsoft.Extensions.Localization;
using CUERipper.Avalonia.Views.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using CUERipper.Avalonia.Models;

namespace CUERipper.Avalonia;

public partial class UpdateDialog : Window, ICUEDialog
{
    public required IServiceProvider ServiceProvider { get; init; }
    public required IUpdateService UpdateService { get; init; }
    public required IStringLocalizer Localizer { get; init; }
    public UpdateDialog()
    {
        InitializeComponent();

        buttonInstall.Click += OnInstallClicked;
        buttonCancel.Click += OnCancelClicked;
    }

    public void Init()
    {
        var data = UpdateService.UpdateMetadata
            ?? throw new ArgumentNullException(nameof(UpdateService.UpdateMetadata));

        textVersion.Text = $"Version: {data.CurrentVersion} -> {data.Version}";
        textSize.Text = $"Size: {(double)data.Size / (1024 * 1024):F2} MiB";
        textAuthor.Text = $"Author: {data.Author}";
        textDate.Text = $"Date: {data.Date:yyyy-MM-dd HH:mm}";

        textDescription.Text = data.Description;

#if !NET47
        if (!OperatingSystem.IsWindows())
        {
            buttonInstall.IsEnabled = false;
        }
#endif
    }

    private async void OnInstallClicked(object? sender, RoutedEventArgs e)
    {
        buttonInstall.IsEnabled = false;
        buttonCancel.IsEnabled = false;

        progressBarDownload.IsVisible = true;

        var success = await UpdateService.DownloadAsync((object? sender, GenericProgressEventArgs e) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                progressBarDownload.Value = Math.Min(Math.Ceiling(e.Progress), 100);
            });
        });

        if (success)
        {
            var messageBox = new MessageBoxDefinition("Update downloaded"
                , "CUERipper must be closed before applying the update."
                , MessageBoxType.OkCancel
            );

            var agreedToUpdate = await MessageBox.CreateAsync(
                Owner as Window ?? throw new InvalidCastException("Failed to cast property Owner to type Window")
                , ServiceProvider
                , messageBox
            );

            if (agreedToUpdate)
            {
                UpdateService.Install();
                Environment.Exit(0);
            }
        }
        else
        {
            var messageBox = new MessageBoxDefinition("Update failed"
                , "Failed to download update, check the error log."
                , MessageBoxType.Ok
            );
            
            await MessageBox.CreateAsync(
                Owner as Window ?? throw new InvalidCastException("Failed to cast property Owner to type Window")
                , ServiceProvider
                , messageBox
            );
        }

        Close();
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    public static async Task CreateAsync(Window owner, IServiceProvider serviceProvider)
    {
        var updateService = serviceProvider.GetRequiredService<IUpdateService>();
        var localizer = serviceProvider.GetRequiredService<IStringLocalizer<Language>>();

        var updateWindow = new UpdateDialog()
        {
            Owner = owner
            , ServiceProvider = serviceProvider
            , UpdateService = updateService
            , Localizer = localizer
        };

        updateWindow.Init();
        await updateWindow.ShowDialog(owner, lockParent: true);
    }
}