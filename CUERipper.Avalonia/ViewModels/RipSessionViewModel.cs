#region Copyright (C) 2026 Max Visser
/*
    Copyright (C) 2026 Max Visser

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
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CUERipper.Avalonia.Compatibility;
using CUERipper.Avalonia.Exceptions;
using CUERipper.Avalonia.Models;
using CUERipper.Avalonia.Services.Abstractions;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CUERipper.Avalonia.ViewModels
{
    public sealed partial class RipSessionViewModel : ViewModelBase, IDisposable
    {
        public delegate Task<RipSettings> RipSettingsFactory(CancellationToken ct);

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsRipping))]
        [NotifyPropertyChangedFor(nameof(IsDiscIdle))]
        [NotifyCanExecuteChangedFor(nameof(StartCommand))]
        [NotifyCanExecuteChangedFor(nameof(AbortCommand))]
        private SessionState mode = SessionState.Init;

        public bool IsRipping { get => Mode == SessionState.Ripping; }
        public bool IsDiscIdle { get => Mode == SessionState.Ready || Mode == SessionState.Done; }

        [ObservableProperty]
        private string status = string.Empty;

        [ObservableProperty]
        private int readingProgress;

        [ObservableProperty]
        private int totalProgress;

        [ObservableProperty]
        private int errorProgress;

        public bool IsBusy { get => _rippingTask != null && !_rippingTask.IsCompleted; }

        private RipSettingsFactory? _buildSettings;
        private Task? _rippingTask;
        private CancellationTokenSource _rippingCts = new();

        private readonly ICUERipperService _ripperService;
        private readonly ICUEMetaService _metaService;
        private readonly IStringLocalizer _localizer;
        private readonly ILogger _logger;

        public RipSessionViewModel(ICUERipperService ripperService
            , ICUEMetaService metaService
            , IStringLocalizer<Language> localizer
            , ILogger<RipSessionViewModel> logger)
        {
            _ripperService = ripperService;
            _metaService = metaService;
            _localizer = localizer;
            _logger = logger;
        }

        public void UseSettingsFactory(RipSettingsFactory factory)
            => _buildSettings = factory;

        private bool CanStart => IsDiscIdle;
        private bool CanAbort => IsRipping;

        [RelayCommand(CanExecute = nameof(CanStart))]
        private async Task StartAsync()
        {
            if (_buildSettings == null) throw new NotInitializedException(nameof(RipSettingsFactory));

            if (IsBusy)
            {
                _logger.LogError("Ripping already in progress, start shouldn't be reachable.");
                return;
            }

            _metaService.FinalizeMetadata();

            if (!_rippingCts.TryReset())
            {
                _rippingCts.Dispose();
                _rippingCts = new CancellationTokenSource();
            }

            Status = _localizer["Status:DownloadingAlbumCover"];

            RipSettings settings;
            try
            {
                settings = await _buildSettings(_rippingCts.Token);
            }
            catch (OperationCanceledException)
            {
                Mode = SessionState.Done;
                return;
            }

            Mode = SessionState.Ripping;

            _rippingTask = _ripperService.StartRipProcess(settings, _rippingCts.Token);

            // Don't let the error be swallowed
            _ = _rippingTask.ContinueWith(task
                    => _logger.LogError(task.Exception, "Ripping task faulted.")
                , CancellationToken.None
                , TaskContinuationOptions.OnlyOnFaulted
                , TaskScheduler.Default);
        }

        [RelayCommand(CanExecute = nameof(CanAbort))]
        private async Task AbortAsync()
        {
            if (!IsBusy)
            {
                _logger.LogError("No rip in progress, abort shouldn't be reachable.");
                return;
            }

            _rippingCts.Cancel();
            Status = _localizer["Status:RipperStop"];

            await WaitForCompletionAsync();

            Status = _localizer["Status:RipperStopped"];
            Mode = SessionState.Done;
        }

        public void Cancel()
        {
            if (!IsBusy) return;

            _rippingCts.Cancel();
        }

        public async Task WaitForCompletionAsync()
        {
            if (_rippingTask == null) return;

            try
            {
                await _rippingTask;
            }
            catch (OperationCanceledException)
            {
                // Ok
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ripping task faulted while waiting for it to finish.");
            }
        }

        public void ReportStatus(string status) => Status = status;

        public void ReportProgress(int reading, int total, int error)
        {
            ReadingProgress = reading;
            TotalProgress = total;
            ErrorProgress = error;
        }

        public void ResetProgress() => ReportProgress(0, 0, 0);

        private bool _disposed = false;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (!_rippingCts.IsCancellationRequested) _rippingCts.Cancel();

            if (IsBusy)
            {
                try
                {
                    _rippingTask!.Wait();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ripping task threw an exception while waiting for disposal.");
                }
            }

            _rippingCts.Dispose();

            GC.SuppressFinalize(this);
        }
    }
}
