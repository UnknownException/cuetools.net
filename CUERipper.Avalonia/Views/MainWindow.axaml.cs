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
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using CUERipper.Avalonia.Compatibility;
using CUERipper.Avalonia.Configuration;
using CUERipper.Avalonia.Configuration.Abstractions;
using CUERipper.Avalonia.Events;
using CUERipper.Avalonia.Exceptions;
using CUERipper.Avalonia.Extensions;
using CUERipper.Avalonia.Models;
using CUERipper.Avalonia.Services;
using CUERipper.Avalonia.Services.Abstractions;
using CUERipper.Avalonia.ViewModels;
using CUETools.Processor;
using CUETools.Ripper;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CUERipper.Avalonia.Views
{
    public sealed partial class MainWindow : Window
    {
        public MainWindowViewModel ViewModel => DataContext as MainWindowViewModel
            ?? throw new ViewModelMismatchException(typeof(MainWindowViewModel), DataContext?.GetType());

        private readonly IServiceProvider _serviceProvider;
        private readonly ICUERipperService _ripperService;
        private readonly ICUEMetaService _metaService;
        private readonly ICUEConfigFacade _config;
        private readonly IStringLocalizer<Language> _localizer;
        private readonly IUpdateService _updateService;
        private readonly ILogger _logger;


// TODO not clean, refactor
#if DEBUG
#pragma warning disable 8618
        /// <summary>
        /// This constructor should only be used by Avalonia in design mode.
        /// </summary>
        /// <exception cref="Exception"></exception>
        public MainWindow()
        {
            if (!Design.IsDesignMode) throw new NotInAvaloniaDesignModeException();

            _config = CUEConfigFacade.Create();

            InitializeComponent();
        }
#pragma warning restore 8618
#endif

        public MainWindow(IServiceProvider serviceProvider
            , ICUERipperService ripperService
            , ICUEMetaService metaService
            , IDriveNotificationService driveNotificationService
            , ICUEConfigFacade config
            , IStringLocalizer<Language> localizer
            , IUpdateService updateService
            , MainWindowViewModel viewModel
            , ILogger<MainWindow> logger)
        {
            _serviceProvider = serviceProvider;
            _ripperService = ripperService;
            _metaService = metaService;
            _config = config;
            _localizer = localizer;
            _updateService = updateService;
            _logger = logger;

            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
            Closing += OnWindowClosing;

            buttonRefreshDrives.Click += OnRefreshDrivesClicked;

            driveNotificationService.SetCallbacks(OnDriveListRefreshRequestedCallback
                , OnDriveUnmountedCallback
                , OnDriveMountedCallback);

            _ripperService.OnSecondaryProgress += RepairStatusCallback;
            _ripperService.OnRepairSelection += RepairSelectionCallback;
            _ripperService.OnRippingProgress += RipperStatusCallback;
            _ripperService.OnFinish += RipperFinishedCallback;
            _ripperService.OnDirectoryConflict += DirectoryConflictCallback;
            _ripperService.OnSelectedDriveChanged += (object? _, DriveChangedEventArgs e)
                => Dispatcher.UIThread.Post(async () => { 
                    // Prevent double initializing and only re-initialize when a new drive has been selected
                    if (e.PreviousDrive != Constants.NullDrive && e.PreviousDrive != e.NextDrive) { 
                        await InitializeApplicationAsync();
                    }
                });

            DataContext = viewModel;
        }

        private async Task InitializeApplicationAsync()
        {
            ViewModel.EncodingTabs.InitializeTabs();

            ViewModel.CoverViewer.Clear();
            ViewModel.TrackGrid.Clear();
            ViewModel.MetaGrid.Clear();

            ViewModel.SetInitState();

            if (ViewModel.CDDriveAvailable && ViewModel.AlbumReleases.Any())
            {
                ViewModel.CoverViewer.Feed();

                ViewModel.RipSession.Mode = SessionState.Ready;

                if (_config.AutomaticRip && _metaService.SelectedMetadata != null)
                {
                    await ViewModel.RipSession.StartCommand.ExecuteAsync(null);
                }
            }
            else
            {
                ViewModel.RipSession.Mode = SessionState.Init;
                _logger.LogInformation(Constants.NoCDDriveFound);
            }

            var fetched = await _updateService.FetchAsync();
            ViewModel.UpdateAvailable = fetched && _updateService.UpdateMetadata.UpdateAvailable();
        }

        private async void OnDataContextChanged(object? sender, EventArgs e)
            => await InitializeApplicationAsync();

        private async void OnRefreshDrivesClicked(object? sender, EventArgs e)
            => await InitializeApplicationAsync();

        private void RepairSelectionCallback(object? sender, CUEToolsSelectionEventArgs args)
        {
            if (args.choices is CUEToolsSourceFile[] sourceFiles)
            {
                // TODO figure out how to NOT do it like this
                // https://github.com/davidfowl/AspNetCoreDiagnosticScenarios/blob/master/AsyncGuidance.md#avoid-using-taskresult-and-taskwait
                var result = Task.Run(() => Dispatcher.UIThread.InvokeAsync(
                    () => RepairSelectionDialog.CreateAsync(this, _serviceProvider, sourceFiles)
                )).GetAwaiter().GetResult();

                args.selection = result;
            }
        }

        private void RepairStatusCallback(object? sender, CUEToolsProgressEventArgs args)
        {
            string status = args.status;
            Dispatcher.UIThread.Post(() =>
            {
                ViewModel.RipSession.ReportStatus(status);
            });
        }

        private void RipperStatusCallback(object? sender, ReadProgressArgs args)
        {
            if (sender is not ICDRipper audioSource) return;

            int audioLength = (int)audioSource.TOC.AudioLength;
            int correctionQuality = audioSource.CorrectionQuality;
            int audioTrackCount = audioSource.TOC.TrackCount;
            var trackLength = new List<int>();
            for(int i = 0; i < audioTrackCount; ++i)
            {
                trackLength.Add((int)audioSource.TOC[i + 1].Length);
            }

            int processed = args.Position - args.PassStart;
            TimeSpan elapsed = DateTime.Now - args.PassTime;
            double speed = elapsed.TotalSeconds > 0 ? processed / elapsed.TotalSeconds / 75 : 1.0;

            double trackPercentage = (double)(args.Position - args.PassStart) / (args.PassEnd - args.PassStart);
            string retry = args.Pass > 0 ? $" ({_localizer["Status:Retry"]} {args.Pass})" : "";
            string status = (elapsed.TotalSeconds > 0 && args.Pass >= 0) ?
                string.Format("{0} @{1:00.00}x{2}...", args.Action, speed, retry) :
                string.Format("{0}{1}...", args.Action, retry);

            Dispatcher.UIThread.Post(() =>
            {
                int passTotalLength = args.PassEnd - args.PassStart;
                double correctionLength = (double)passTotalLength / (correctionQuality + 1);
                double correctionProcessed = (double)processed / (correctionQuality + 1) + correctionLength * Math.Min(args.Pass, correctionQuality);
                double currentProgress = args.PassStart + correctionProcessed;

                double errorRatio = Math.Log(args.ErrorsCount / 10.0 + 1);
                double passRatio = Math.Log((args.PassEnd - args.PassStart) / 10.0 + 1);
                double errorPercentage = (errorRatio / passRatio) * 100;

                if (DataContext is MainWindowViewModel viewModel)
                {
                    viewModel.RipSession.ReportStatus(currentProgress >= audioLength
                        ? _localizer["Status:Finalizing"]
                        : status);

                    viewModel.RipSession.ReportProgress(
                        reading: MathClamp.Clamp((int)(trackPercentage * 100), 0, 100)
                        , total: (int)Math.Round((MathClamp.Clamp(currentProgress, 0, audioLength) / audioLength * 100))
                        , error: MathClamp.Clamp((int)errorPercentage, 0, 100));

                    for (int i = 0; i < audioTrackCount && i < viewModel.TrackGrid.Tracks.Count; ++i)
                    {
                        var progressFraction = Math.Min(currentProgress / trackLength[i], 1f);
                        viewModel.TrackGrid.Tracks[i].Progress = Convert.ToInt32(Math.Round(progressFraction * 100f));

                        if (trackLength[i] >= currentProgress) break;
                        else currentProgress -= trackLength[i];
                    }
                }
            });
        }

        private void RipperFinishedCallback(object? sender, RipperFinishedEventArgs e)
        {
            Dispatcher.UIThread.Post(async () =>
            {
                ViewModel.RipSession.ReportStatus(e.Status);

                if (!string.IsNullOrWhiteSpace(e.PopupContent))
                {
                    var messageBox = new MessageBoxDefinition(e.Status, e.PopupContent, MessageBoxType.Ok);
                    await MessageBox.CreateAsync(this, _serviceProvider, messageBox);
                }

                ViewModel.RipSession.Mode = SessionState.Done;
            });
        }

        private void DirectoryConflictCallback(object? sender, DirectoryConflictEventArgs e)
        {            
            // TODO figure out how to NOT do it like this
            // https://github.com/davidfowl/AspNetCoreDiagnosticScenarios/blob/master/AsyncGuidance.md#avoid-using-taskresult-and-taskwait            
            var result = Task.Run(() => Dispatcher.UIThread.InvokeAsync(
                () =>
                {
                    var messageBox = new MessageBoxDefinition(_localizer["Warning:DirectoryExists"]
                        , _localizer["Warning:QuestionOverwriteDestination"]
                        , MessageBoxType.YesNo
                    );

                    return MessageBox.CreateAsync(this, _serviceProvider, messageBox);
                }
            )).GetAwaiter().GetResult();

            e.CanModifyContent = result;
        }

        private void OnDriveListRefreshRequestedCallback()
        {
            Dispatcher.UIThread.Post(async () =>
            {
                if (ViewModel.RipSession.IsBusy)
                {
                    var drives = _ripperService.QueryAvailableDriveInformation().Select(d => d.Key);
                    if (drives.Contains(_ripperService.SelectedDrive)) return;

                    ViewModel.RipSession.Cancel();
                    await WaitWithBusyCursorAsync();
                }

                await InitializeApplicationAsync();
            });
        }

        private void OnDriveUnmountedCallback(char driveLetter)
        {
            Dispatcher.UIThread.Post(async () =>
            {
                if (driveLetter == _ripperService.SelectedDrive)
                {
                    if (ViewModel.RipSession.IsBusy)
                    {
                        ViewModel.RipSession.Cancel();
                        ViewModel.RipSession.ReportStatus(_localizer["Status:DiscUnexpectedRemove"]);
                    }
                    else
                    {
                        ViewModel.RipSession.ReportStatus(_localizer["Status:DiscRemoved"]);
                    }

                    await WaitWithBusyCursorAsync();

                    ViewModel.RipSession.Mode = SessionState.Init;
                }
            });
        }

        private void OnDriveMountedCallback(char driveLetter)
        {
            Dispatcher.UIThread.Post(async() =>
            {
                if (driveLetter == _ripperService.SelectedDrive) await InitializeApplicationAsync();
            });
        }

        private async Task WaitWithBusyCursorAsync()
        {
            var previousCursor = Cursor;
            using (Cursor = new Cursor(StandardCursorType.Wait))
            {
                await ViewModel.RipSession.WaitForCompletionAsync();
            }

            Cursor = previousCursor;
        }

        private void OnSplitViewPaneClosing(object? sender, CancelRoutedEventArgs args)
        {
            if (ViewModel.SplitPaneOpen) args.Cancel = true;
        }

        private async void OnWindowClosing(object? sender, WindowClosingEventArgs e)
        {
            if (ViewModel.RipSession.IsBusy)
            {
                e.Cancel = true;

                var messageBox = new MessageBoxDefinition(_localizer["Warning:CantClose"]
                    , _localizer["Warning:RipInProgress"]
                    , MessageBoxType.YesNo
                );

                var result = await MessageBox.CreateAsync(this, _serviceProvider, messageBox);
                if (result)
                {
                    ViewModel.RipSession.Cancel();
                    await WaitWithBusyCursorAsync();

                    // Try again
                    Close();
                }
            }
            else
            {
                ViewModel.EncodingTabs.PersistTabs();
            }
        }
    }
}