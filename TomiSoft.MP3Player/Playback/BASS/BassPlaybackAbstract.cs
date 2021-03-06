﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Threading;
using TomiSoft.MP3Player.Common.Playback;
using TomiSoft.MP3Player.MediaInformation;
using Un4seen.Bass;

namespace TomiSoft.MP3Player.Playback.BASS {
	/// <summary>
	/// Provides basic functionality for playback handlers that uses
	/// BASS to play the media.
	/// </summary>
	internal abstract class BassPlaybackAbstract : IPlaybackManager, IAudioPeakMeter {
        private readonly IEnumerable<string> Properties = typeof(BassPlaybackAbstract).GetProperties().Select(x => x.Name);
        private bool playing;
		private bool paused;
		private int channelID;
		private DispatcherTimer PlaybackTimer;
        private long PositionInBytes;
        private long LengthInBytes;

        protected ISongInfo songInfo;
        
		/// <summary>
		/// Occures when a property is changed.
		/// </summary>
		public event PropertyChangedEventHandler PropertyChanged;

		/// <summary>
		/// Occures when the song is ended.
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

				if (this.playing)
					this.PlaybackTimer.Start();
				else
					this.PlaybackTimer.Stop();
			}
		}

		/// <summary>
		/// Gets if the playback is paused.
		/// </summary>
		public bool IsPaused {
			get {
				return this.paused;
			}
			private set {
				this.paused = value;
				this.NotifyPropertyChanged("IsPaused");
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
				#region Error checking
				if (value < 0 || value > 100)
					throw new ArgumentOutOfRangeException("A hangerő 0 és 100 közti érték lehet.");
				#endregion

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
            protected set {
                if (value == 0)
                    return;

                Bass.BASS_StreamFree(this.channelID);
                this.channelID = value;
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
					this.PositionInBytes
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

				Bass.BASS_ChannelSetPosition(
                    this.ChannelID,
					value
				);

                this.PositionInBytes = Bass.BASS_ChannelGetPosition(this.channelID);

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
					this.LengthInBytes
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
		/// Gets the maximum possible value for the peak meter.
		/// </summary>
		public int Maximum {
			get {
				return 32768;
			}
		}

		/// <summary>
		/// Initializes a new instance of <see cref="BassPlaybackAbstract"/> using the given channel ID.
		/// </summary>
		/// <param name="ChannelID">The channel ID provided by BASS.</param>
		/// <exception cref="IOException">when BASS can't open the file</exception>
		public BassPlaybackAbstract(int ChannelID) {
			#region Error checking
			if (ChannelID == 0) {
                Trace.TraceWarning($"[BASS] Could not open file (ErrorCode={Bass.BASS_ErrorGetCode()})");
				throw new IOException("Nem sikerült megnyitni a fájlt: " + Bass.BASS_ErrorGetCode());
			}
			#endregion

			this.channelID = ChannelID;

			this.PlaybackTimer = new DispatcherTimer() {
				Interval = TimeSpan.FromSeconds(0.02)
			};
            this.PlaybackTimer.Tick += TimerTick;

			this.IsPlaying = false;

			this.songInfo = new BassSongInfo(this.ChannelID);
		}

		/// <summary>
		/// This method is executed when the <see cref="PlaybackTimer"/> ticks.
		/// </summary>
		/// <param name="sender">The <see cref="DispatcherTimer"/> instance</param>
		/// <param name="e">Event parameters</param>
		private void TimerTick(object sender, EventArgs e) {
            this.PositionInBytes = Bass.BASS_ChannelGetPosition(this.channelID);
            this.LengthInBytes = Bass.BASS_ChannelGetLength(this.channelID);

            if (this.Length - this.Position < 0.15) {
                this.Stop();
                this.SongEnded?.Invoke();
            }
            
            this.NotifyAll();
        }
        
        /// <summary>
        /// Plays the stream.
        /// </summary>
        public void Play() {
			if (Bass.BASS_ChannelPlay(this.ChannelID, false)) {
				this.IsPlaying = true;
				this.IsPaused = false;
			}
			else {
				Trace.TraceWarning($"[BASS] Could not start playback (BassError = {Bass.BASS_ErrorGetCode()})");
			}
		}

		/// <summary>
		/// Stops the playback and sets the playback position to 0.
		/// </summary>
		public void Stop() {
			if (Bass.BASS_ChannelStop(this.ChannelID)) {
				this.IsPlaying = false;
				this.IsPaused = false;
                this.Position = 0;
				this.NotifyAll();
			}
			else {
				Trace.TraceWarning($"[BASS] Could not stop playback (BassError = {Bass.BASS_ErrorGetCode()})");
			}
		}

		/// <summary>
		/// Stops the playback but does not rewind the playback position to 0.
		/// </summary>
		public void Pause() {
			if (Bass.BASS_ChannelPause(this.ChannelID)) {
				this.IsPlaying = false;
				this.IsPaused = true;
				this.NotifyAll();
			}
			else {
				Trace.TraceWarning($"[BASS] Could not pause playback (BassError = {Bass.BASS_ErrorGetCode()})");
			}
		}

		/// <summary>
		/// Closes the BASS channel.
		/// </summary>
		public virtual void Dispose() {
			if (this.IsPlaying) {
				this.Stop();
			}

            if (!Bass.BASS_StreamFree(this.ChannelID))
                Trace.TraceWarning($"[BASS] Could not release stream: (BassError = {Bass.BASS_ErrorGetCode()})");

            this.PlaybackTimer.Tick -= TimerTick;
		}

		/// <summary>
		/// Fires the <see cref="PropertyChanged"/> event for the given property.
		/// </summary>
		/// <param name="PropertyName">The property's name that changed.</param>
		private void NotifyPropertyChanged(string PropertyName) {
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));
		}

		/// <summary>
		/// Fires <see cref="PropertyChanged"/> event for all properties.
		/// </summary>
		private void NotifyAll() {
			#region Error checking
			if (this.PropertyChanged == null)
				return;
			#endregion

			foreach (var PropertyName in this.Properties) {
				PropertyChanged(this, new PropertyChangedEventArgs(PropertyName));
			}
		}
	}
}
