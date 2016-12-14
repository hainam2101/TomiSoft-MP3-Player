﻿using System.ComponentModel;

namespace TomiSoft.MP3Player.Playback {
	/// <summary>
	/// This interface provides a way of managing audio peak meter.
	/// </summary>
	public interface IAudioPeakMeter : INotifyPropertyChanged {
		/// <summary>
		/// Gets the current left peak level.
		/// </summary>
		int LeftPeak { get; }

		/// <summary>
		/// Gets the current right peak level.
		/// </summary>
		int RightPeak { get; }
	}
}