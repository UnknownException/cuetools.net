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

using System.Collections.ObjectModel;
using System.Linq;
using CUERipper.Avalonia.Events;
using CUERipper.Avalonia.Models;
using CUERipper.Avalonia.Services.Abstractions;
using Microsoft.Extensions.Localization;

namespace CUERipper.Avalonia.ViewModels.UserControls
{
    public partial class TrackGridViewModel : ViewModelBase
    {
        public ObservableCollection<TrackViewModel> Tracks { get; set; } = [];
        public string HeaderTitle { get => _localizer["TrackList:Title"]; }
        public string HeaderLength { get => _localizer["TrackList:Length"]; }
        public string HeaderProgress { get => _localizer["TrackList:Progress"]; }
        public string HeaderArtist { get => _localizer["TrackList:Artist"]; }

        private readonly ICUEMetaService _metaService;
        private readonly IStringLocalizer _localizer;
        public TrackGridViewModel(ICUEMetaService metaService
            , IStringLocalizer<Language> stringLocalizer)
        {
            _metaService = metaService;
            _localizer = stringLocalizer;

            _metaService.OnSelectedMetadataChanged += OnSelectedMetadataChanged;
        }

        public void Clear()
        {
            Tracks.Clear();
        }

        public void OnSelectedMetadataChanged(object? sender, SelectedMetadataChangedEventArgs e)
        {
            Clear();

            var meta = e.AlbumMetadata;
            if (meta == null) return;

            var tracksLength = _metaService.GetTracksLength();
            for (int i = 0; i < meta.Data.Tracks.Count; ++i)
            {
                var trackInfo = meta.Data.Tracks[i];
                Tracks.Add(new TrackViewModel
                {
                    Title = trackInfo?.Title ?? $"{Constants.UnknownTrack} {i + 1}"
                    , TrackNo = i + 1
                    , Artist = trackInfo?.Artist ?? meta.Data.Artist
                    , Length = tracksLength.ElementAtOrDefault(i) ?? Constants.TrackNullLength
                    , OnUpdate = (TrackViewModel model) =>
                    {
                        meta.Data.Tracks[model.TrackNo - 1].Title = model.Title;
                        meta.Data.Tracks[model.TrackNo - 1].Artist = model.Artist;
                    }
                });
            }
        }
    }
}
