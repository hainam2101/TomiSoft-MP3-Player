﻿using System;
using System.ComponentModel;
using System.Windows.Threading;
using Un4seen.Bass;

namespace TomiSoft.MP3Player.Playback {
	/// <summary>
	/// Provides basic functionality for playback handlers that uses
	/// BASS to play the media.
	/// </summary>
	public abstract class BassPlaybackAbstract : IPlaybackManager {
		private bool playing;
		private int channelID;
		private DispatcherTimer PlaybackTimer;
		protected ISongInfo songInfo;

		/// <summary>
		/// Occurs when a property is changed.
		/// </summary>
		public event PropertyChangedEventHandler PropertyChanged;

		/// <summary>
		/// Occurs when the song is ended.
		/// </summary>
		public event Action SongEnded;

		/// <summary>
		/// Gets if playback is running.
		/// </summary>
		public bool IsPlaying {
			get {
				return this.playing;
			}
			private set {
				this.playing = value;
				this.NotifyPropertyChanged("IsPlaying");

				if (this.playing) {
					this.PlaybackTimer.Start();
				}
				else {
					this.PlaybackTimer.Stop();
				}
			}
		}

		/// <summary>
		/// Gets or sets the playback volume (min. 0, max. 100).
		/// </summary>
		public int Volume {
			get {
				float vol = 1;
				Bass.BASS_ChannelGetAttribute(this.channelID, BASSAttribute.BASS_ATTRIB_VOL, ref vol);

				return (int)(vol * 100);
			}
			set {
				if (value < 0 || value > 100)
					throw new Exception("A hangerő 0 és 100 közti érték lehet.");

				Bass.BASS_ChannelSetAttribute(this.channelID, BASSAttribute.BASS_ATTRIB_VOL, (float)value / 100);
			}
		}

		/// <summary>
		/// Gets the left peak level (min. 0, max. 32768).
		/// </summary>
		public int LeftPeak {
			get {
				if (!IsPlaying)
					return 0;

				return Un4seen.Bass.Utils.LowWord32(Bass.BASS_ChannelGetLevel(this.channelID));
			}
		}

		/// <summary>
		/// Gets the right peak level (min. 0, max. 32768).
		/// </summary>
		public int RightPeak {
			get {
				if (!IsPlaying)
					return 0;

				return Un4seen.Bass.Utils.HighWord32(Bass.BASS_ChannelGetLevel(this.channelID));
			}
		}

		/// <summary>
		/// Gets the BASS playback channel's ID.
		/// </summary>
		public int ChannelID {
			get {
				return this.channelID;
			}
		}

		/// <summary>
		/// Gets or sets the playback position in seconds (min. 0, max. Length).
		/// </summary>
		/// <exception cref="ArgumentOutOfRangeException">when value is not between 0 and the value given by Length property</exception>
		public double Position {
			get {
				return Bass.BASS_ChannelBytes2Seconds(
					this.ChannelID,
					Bass.BASS_ChannelGetPosition(this.ChannelID)
				);
			}
			set {
				#region Error checking
				if (value < 0 || value > this.Length)
					throw new ArgumentOutOfRangeException(String.Format(
						"A lejátszási pozíciónak {0} és {1} közt kell lennie.",
						0,
						this.Length
					));
				#endregion

				Bass.BASS_ChannelSetPosition(this.ChannelID,
					Bass.BASS_ChannelSeconds2Bytes(this.ChannelID, value)
				);
				this.NotifyPropertyChanged("Position");
			}
		}

		/// <summary>
		/// Gets the song's length in seconds.
		/// </summary>
		public double Length {
			get {
				return Bass.BASS_ChannelBytes2Seconds(
					this.ChannelID,
					Bass.BASS_ChannelGetLength(this.ChannelID)
				);
			}
		}

		/// <summary>
		/// Gets informations about the song.
		/// </summary>
		public ISongInfo SongInfo {
			get {
				return this.songInfo;
			}
		}

		/// <summary>
		/// Initializes a new instance of BassPlaybackAbstract using the given channel ID.
		/// </summary>
		/// <param name="ChannelID">The channel ID provided by BASS.</param>
		public BassPlaybackAbstract(int ChannelID) {
			#region Error checking
			if (ChannelID == 0) {
				throw new Exception("Nem sikerült megnyitni a fájlt: " + Bass.BASS_ErrorGetCode());
			}
			#endregion

			this.channelID = ChannelID;

			this.PlaybackTimer = new DispatcherTimer() {
				Interval = TimeSpan.FromSeconds(0.02)
			};
			this.PlaybackTimer.Tick += (o, e) => {
				if (Position >= Length) {
					this.Stop();

					this.SongEnded?.Invoke();
				}

				this.NotifyAll();
			};

			this.IsPlaying = false;
		}

		/// <summary>
		/// Plays the stream.
		/// </summary>
		public void Play() {
			if (Bass.BASS_ChannelPlay(this.ChannelID, false)) {
				this.IsPlaying = true;
			}
		}

		/// <summary>
		/// Stops the playback and sets the playback position to 0.
		/// </summary>
		public void Stop() {
			if (Bass.BASS_ChannelStop(this.ChannelID)) {
				this.IsPlaying = false;
				this.Position = 0;
				this.NotifyAll();
			}
		}

		/// <summary>
		/// Stops the playback but does not rewind the playback position to 0.
		/// </summary>
		public void Pause() {
			if (Bass.BASS_ChannelPause(this.ChannelID)) {
				this.IsPlaying = false;
				this.NotifyAll();
			}
		}

		/// <summary>
		/// Closes the BASS channel.
		/// </summary>
		public void Dispose() {
			if (this.IsPlaying) {
				this.Stop();
			}

			Bass.BASS_StreamFree(this.ChannelID);
		}

		/// <summary>
		/// Fires the PropertyChanged event for the given property.
		/// </summary>
		/// <param name="info">The property's name that changed.</param>
		private void NotifyPropertyChanged(String info) {
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(info));
		}

		/// <summary>
		/// Fires PropertyChanged event for all properties.
		/// </summary>
		private void NotifyAll() {
			#region Error checking
			if (this.PropertyChanged == null)
				return;
			#endregion

			foreach (var Property in this.GetType().GetProperties()) {
				PropertyChanged(this, new PropertyChangedEventArgs(Property.Name));
			}
		}
	}
}
