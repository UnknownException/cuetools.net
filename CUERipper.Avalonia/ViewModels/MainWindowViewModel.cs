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
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CUERipper.Avalonia.Configuration.Abstractions;
using CUERipper.Avalonia.Extensions;
using CUERipper.Avalonia.Models;
using CUERipper.Avalonia.Services.Abstractions;
using CUERipper.Avalonia.ViewModels.UserControls;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace CUERipper.Avalonia.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        public ObservableCollection<string> DiscDrives { get; set; } = [];

        [ObservableProperty]
        private string selectedDrive = string.Empty;

        public bool CDDriveAvailable => string.Compare(DiscDrives[0], Constants.NoCDDriveFound) != 0;

        partial void OnSelectedDriveChanged(string? oldValue, string newValue)
        {
            if (string.IsNullOrWhiteSpace(newValue)) return;
            if (string.Compare(oldValue, newValue) == 0) return;
            if (string.Compare(newValue, Constants.NoCDDriveFound) == 0) return;

            _ripperService.SelectedDrive = newValue[0];
            _config.DefaultDrive = newValue;
        }

        public ObservableCollection<AlbumRelease> AlbumReleases { get; set; } = [];

        [ObservableProperty]
        private AlbumRelease? selectedAlbum;

        partial void OnSelectedAlbumChanged(AlbumRelease? oldValue, AlbumRelease? newValue)
        {
            if (string.IsNullOrWhiteSpace(newValue?.Name)) return;
            if (oldValue == newValue) return;

            var meta = GetSelectedAlbumMeta();
            _metaService.SelectedMetadata = meta;

            OutputPath = meta?.PathStringFromFormat(_config.PathFormat, _config) ?? "Output Path";
        }

        [ObservableProperty]
        private Bitmap? albumCoverImage;

        [ObservableProperty]
        private string albumTitle = string.Empty;

        [ObservableProperty]
        private string albumArtist = string.Empty;

        [ObservableProperty]
        private string albumYear = string.Empty;

        [ObservableProperty]
        private string albumDisc = string.Empty;

        [ObservableProperty]
        private string outputPath = "Output Path";

        [ObservableProperty]
        private int readingProgress;

        [ObservableProperty]
        private int totalProgress;

        [ObservableProperty]
        private int errorProgress;

        [ObservableProperty]
        private bool splitPaneOpen;
        partial void OnSplitPaneOpenChanged(bool oldValue, bool newValue)
        {
            _config.DetailPaneOpened = newValue;
        }

        public string HeaderTracks { get => _localizer["Main:Tracks"]; }
        public string HeaderMetadata { get => _localizer["Main:Metadata"]; }

        public DriveSettingSectionViewModel DriveSettings { get; }

        private readonly ICUEConfigFacade _config;
        private readonly ICUERipperService _ripperService;
        private readonly ICUEMetaService _metaService;
        private readonly IStringLocalizer _localizer;
        private readonly IIconService _iconService;
        public MainWindowViewModel(ICUEConfigFacade config
            , ICUERipperService ripperService
            , ICUEMetaService metaService
            , IStringLocalizer<Language> stringLocalizer
            , IIconService iconService
            , DriveSettingSectionViewModel driveSettings)
        {
            _config = config;
            _ripperService = ripperService;
            _metaService = metaService;
            _localizer = stringLocalizer;
            _iconService = iconService;

            DriveSettings = driveSettings;
        }

        public void RefreshAlbums()
        {
            AlbumReleases.Clear();

            var metaInfo = _metaService.GetAlbumMetaInformation(false);
            new ObservableCollection<AlbumRelease>(
                metaInfo.Select((meta, index) =>
                {
                    const string YEAR_SEPERATOR = ": ";

                    string year = meta.Data.Year;
                    string artist = meta.Data.Artist ?? Constants.UnknownArtist;
                    string title = meta.Data.Title ?? Constants.UnknownTitle;
                    string country = meta.Data.Country ?? string.Empty;
                    string labelName = meta.Data.Label ?? string.Empty;
                    string barcode = meta.Data.Barcode ?? string.Empty;
                    string releaseDate = meta.Data.ReleaseDate ?? string.Empty;

                    if (string.IsNullOrWhiteSpace(country)
                        && string.IsNullOrWhiteSpace(labelName)
                        && string.IsNullOrWhiteSpace(barcode)
                        && string.IsNullOrWhiteSpace(releaseDate))
                    {
                        return new AlbumRelease($"{(string.IsNullOrWhiteSpace(year) ? string.Empty : year + YEAR_SEPERATOR)}{artist} - {title}"
                            , Icon: _iconService.GetIcon(meta.Source)
                            , Index: index);
                    }

                return new AlbumRelease($"{(string.IsNullOrWhiteSpace(year) ? string.Empty : year + YEAR_SEPERATOR)}{artist} - {title} ({country} - {labelName} {barcode} - {releaseDate})"
                    , Icon: _iconService.GetIcon(meta.Source)
                    , Index: index);
                })
            ).MoveAll(AlbumReleases);

            SelectedAlbum = AlbumReleases.Any() ? AlbumReleases[0] : null;
        }

        private AlbumMetadata? GetSelectedAlbumMeta()
        {
            if (!CDDriveAvailable || SelectedAlbum == null) return null;

            var index = Math.Min(Math.Max(0, SelectedAlbum.Index), AlbumReleases.Count - 1);
            var albumMetaInformation = _metaService.GetAlbumMetaInformation(false);
            return index < albumMetaInformation.Count ? albumMetaInformation.ElementAt(index) : null;
        }

        private void Clear()
        {
            AlbumReleases.Clear();
            DiscDrives.Clear();

            ReadingProgress = 0;
            ErrorProgress = 0;
            TotalProgress = 0;
        }

        internal bool SetInitState(Bitmap? albumCover)
        {
            Clear();

            foreach (var driveName in _ripperService.QueryAvailableDriveInformation())
            {
                DiscDrives.Add(driveName.Value.Name);
            }

            if (DiscDrives.Count == 0)
            {
                DiscDrives.Add(Constants.NoCDDriveFound);
            }

            SplitPaneOpen = _config.DetailPaneOpened;
            AlbumCoverImage = albumCover;

            SelectedDrive = !string.IsNullOrWhiteSpace(_config.DefaultDrive)
                    && DiscDrives.Contains(_config.DefaultDrive)
                ? _config.DefaultDrive
                : DiscDrives[0];

            if (DiscDrives[0] != Constants.NoCDDriveFound)
            {
                RefreshAlbums();
            }

            return true;
        }
    }
}
