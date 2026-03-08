using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;

namespace SONA.Services
{
    public static class SoundService
    {
        private static readonly Dictionary<string, MediaPlayer> _players = new();
        private static readonly string _soundsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Sounds");

        static SoundService()
        {
            AppConfig.VolumeChanged += (v) =>
            {
                foreach (var player in _players.Values)
                {
                    player.Volume = v;
                }
            };
        }

        public static void Play(string soundName)
        {
            try
            {
                if (!_players.TryGetValue(soundName, out var player))
                {
                    var path = Path.Combine(_soundsDir, soundName);
                    if (!File.Exists(path)) return;

                    player = new MediaPlayer();
                    player.Open(new Uri(path));
                    player.Volume = AppConfig.GetDouble("volume", 0.7);
                    _players[soundName] = player;
                }

                player.Stop();
                player.Play();
            }
            catch { }
        }

        // Common sounds
        public static void PlayButton() => Play("button.wav");
        public static void PlaySelect() => Play("select.wav");
        public static void PlaySwipe() => Play("swipe_03.wav");
        public static void PlayHover() => Play("tap_01.wav");
        public static void PlayToggleOn() => Play("toggle_on.wav");
        public static void PlayToggleOff() => Play("toggle_off.wav");
        public static void PlayNotification() => Play("notification.wav");
    }
}
