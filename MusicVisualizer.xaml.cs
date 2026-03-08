using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using NAudio.Wave;
using System.Runtime.InteropServices;
using SONA.Services;

namespace SONA.Controls
{
    public partial class MusicVisualizer : UserControl
    {
        private WasapiLoopbackCapture? _capture;
        private readonly List<Rectangle> _bars = new();
        private const int BarCount = 40;
        private float[] _fftBuffer = new float[1024];
        private float[] _lastFrequencies = new float[BarCount];
        private readonly DispatcherTimer _renderTimer = new();
        private bool _isStarted = false;

        public MusicVisualizer()
        {
            try
            {
                InitializeComponent();
                SetupBars();
                
                _renderTimer.Interval = TimeSpan.FromMilliseconds(30); // ~33 FPS
                _renderTimer.Tick += (s, e) => UpdateBars();
                
                Loaded += (s, e) => Start();
                Unloaded += (s, e) => Stop();
            }
            catch (Exception ex)
            {
                LoggingService.Log($"MusicVisualizer init failed: {ex.Message}");
            }
        }

        private void SetupBars()
        {
            try
            {
                if (VisualizerCanvas == null) return;
                VisualizerCanvas.Children.Clear();
                _bars.Clear();

                var colorStr = AppConfig.GetString("visualizer_color", "#7c3aed");
                if (string.IsNullOrEmpty(colorStr)) colorStr = "#7c3aed";
                
                var color = (Color)ColorConverter.ConvertFromString(colorStr);
                var brush = new SolidColorBrush(color);
                var opacity = AppConfig.GetDouble("visualizer_opacity", 0.6);

                for (int i = 0; i < BarCount; i++)
                {
                    var bar = new Rectangle
                    {
                        Width = 4,
                        Height = 2,
                        Fill = brush,
                        Opacity = opacity,
                        RadiusX = 2,
                        RadiusY = 2,
                        VerticalAlignment = VerticalAlignment.Bottom
                    };
                    _bars.Add(bar);
                    VisualizerCanvas.Children.Add(bar);
                }

                SizeChanged += (s, e) => LayoutBars();
            }
            catch (Exception ex)
            {
                LoggingService.Log($"MusicVisualizer SetupBars failed: {ex.Message}");
            }
        }

        private void LayoutBars()
        {
            double totalWidth = ActualWidth;
            if (totalWidth <= 0) return;

            double barWidth = Math.Max(2, (totalWidth / BarCount) - 4);
            double spacing = (totalWidth - (barWidth * BarCount)) / (BarCount + 1);

            for (int i = 0; i < BarCount; i++)
            {
                _bars[i].Width = barWidth;
                Canvas.SetLeft(_bars[i], spacing + i * (barWidth + spacing));
                Canvas.SetBottom(_bars[i], 0);
            }
        }

        public void Start()
        {
            if (_isStarted) return;
            try
            {
                _capture = new WasapiLoopbackCapture();
                _capture.DataAvailable += OnDataAvailable;
                _capture.StartRecording();
                _renderTimer.Start();
                _isStarted = true;
            }
            catch { }
        }

        public void Stop()
        {
            if (!_isStarted) return;
            try
            {
                _renderTimer.Stop();
                _capture?.StopRecording();
                _capture?.Dispose();
                _capture = null;
                _isStarted = false;
                
                // Reset bars
                foreach (var bar in _bars) bar.Height = 2;
            }
            catch { }
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            // Simple peak detection/FFT-like behavior for visualization
            // In a real app, you'd use a proper FFT library. Here we do basic amplitude analysis.
            var buffer = new WaveBuffer(e.Buffer);
            int samplesRead = e.BytesRecorded / 4; // 32-bit float samples

            for (int i = 0; i < samplesRead; i++)
            {
                float sample = Math.Abs(buffer.FloatBuffer[i]);
                // Basic distribution across bars
                int barIndex = (i % BarCount);
                _lastFrequencies[barIndex] = Math.Max(_lastFrequencies[barIndex], sample);
            }
        }

        private void UpdateBars()
        {
            double maxHeight = ActualHeight;
            if (maxHeight <= 0) maxHeight = 40;

            for (int i = 0; i < BarCount; i++)
            {
                double targetHeight = 2 + (_lastFrequencies[i] * maxHeight * 2);
                targetHeight = Math.Min(targetHeight, maxHeight);
                
                // Smoothing
                double currentHeight = _bars[i].Height;
                _bars[i].Height = currentHeight + (targetHeight - currentHeight) * 0.3;
                
                // Decay
                _lastFrequencies[i] *= 0.8f;
            }
        }

        public void UpdateStyles()
        {
            try
            {
                var colorStr = AppConfig.GetString("visualizer_color", "#7c3aed");
                if (string.IsNullOrEmpty(colorStr)) colorStr = "#7c3aed";

                var color = (Color)ColorConverter.ConvertFromString(colorStr);
                var brush = new SolidColorBrush(color);
                var opacity = AppConfig.GetDouble("visualizer_opacity", 0.6);
                var height = AppConfig.GetDouble("visualizer_height", 40.0);

                Height = height;
                foreach (var bar in _bars)
                {
                    bar.Fill = brush;
                    bar.Opacity = opacity;
                }
            }
            catch (Exception ex)
            {
                LoggingService.Log($"MusicVisualizer UpdateStyles failed: {ex.Message}");
            }
        }
    }
}
