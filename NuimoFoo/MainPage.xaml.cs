﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Windows.UI;
using Windows.UI.Popups;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using NuimoSDK;
using System.Diagnostics;
using Newtonsoft.Json.Linq;
using System.IO;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace NuimoFoo
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage
    {
        private readonly PairedNuimoManager _pairedNuimoManager = new PairedNuimoManager();
        private IEnumerable<INuimoController> _nuimoControllers = new List<INuimoController>();
        private INuimoController _nuimoController;

        private Profile _profile = new Profile();

        public MainPage()
        {
            InitializeComponent();
            ListPairedNuimos();
            AddLedCheckBoxes();
            OutputTextBox.TextWrapping = TextWrapping.NoWrap;
            DisplayIntervalTextBox.Text = "5.0";

            //string json = JsonConvert.SerializeObject(_profile);
            //await Windows.Storage.FileIO.WriteTextAsync(sampleFile, "Swift as a shadow");

            initProfiles();
            Windows.UI.Xaml.Application.Current.DebugSettings.EnableFrameRateCounter = false;
            ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.PreferredLaunchViewSize;
        }

        private async void initProfiles()
        {
            Windows.Storage.StorageFolder localFolder = Windows.Storage.ApplicationData.Current.LocalFolder;


            JObject o = (JObject)JToken.FromObject(_profile);

            JSONTextBox.Text = new StringBuilder(JSONTextBox.Text)
                        .Append(o.ToString() + "\n")
                        .ToString();

            JSONTextBox.Text = new StringBuilder(JSONTextBox.Text)
                        .Append(localFolder.Path + "\n")
                        .ToString();

            DirectoryInfo d = new DirectoryInfo(@""+localFolder.Path);//Assuming Test is your Folder
            FileInfo[] Files = d.GetFiles("*.json"); //Getting Text files
            if (Files.Length >0)
            {
                // there are profiles
                string str = "";
                foreach (FileInfo file in Files)
                {
                    str = str + ", " + file.Name;
                }
                JSONTextBox.Text = new StringBuilder(JSONTextBox.Text)
                        .Append("Profiles:"+str+ "\n")
                        .ToString();
            }
            else
            {

            }

        }

        private async void ListPairedNuimos()
        {
            PairedNuimosComboBox.Items?.Clear();
            _nuimoControllers = await _pairedNuimoManager.ListPairedNuimosAsync();
            foreach (var nuimoController in _nuimoControllers)
            {
                PairedNuimosComboBox.Items?.Add("Nuimo: " + nuimoController.Identifier);
            }
            ReloadButton.Content = "Reload";
            if (PairedNuimosComboBox.Items?.Count > 0) PairedNuimosComboBox.SelectedIndex = 0;
        }

        private void AddLedCheckBoxes()
        {
            for (var row = 0; row < 9; row++)
            {
                for (var col = 0; col < 9; col++)
                {
                    var checkBox = new Windows.UI.Xaml.Controls.CheckBox
                    {
                        Background = new SolidColorBrush(new Color { A = 255, B = 255, G = 255, R = 255 })
                    };
                    Grid.SetRow(checkBox, row);
                    Grid.SetColumn(checkBox, col);
                    LedGrid.Children.Add(checkBox);
                }
            }
        }

        public async void Close()
        {
            var task = _nuimoController?.DisconnectAsync();
            if (task != null) await task;
        }

        private async void ConnectButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (_nuimoControllers == null) return;
            var oldNuimoController = _nuimoController;
            if (oldNuimoController != null) RemoveDelegates(oldNuimoController);
            _nuimoController = _nuimoControllers.ElementAt(PairedNuimosComboBox.SelectedIndex);

            AddDelegates(_nuimoController);

            switch (_nuimoController.ConnectionState)
            {
                case NuimoConnectionState.Disconnected: await _nuimoController.ConnectAsync(); break;
                case NuimoConnectionState.Connected: await _nuimoController.DisconnectAsync(); break;
            }
        }

        private void AddDelegates(INuimoController nuimoController)
        {
            nuimoController.GestureEventOccurred += OnNuimoGestureEventAsync;
            nuimoController.FirmwareVersionRead += OnFirmwareVersion;
            nuimoController.ConnectionStateChanged += OnConnectionState;
            nuimoController.BatteryPercentageChanged += OnBatteryPercentage;
            nuimoController.LedMatrixDisplayed += OnLedMatrixDisplayed;
        }

        private void RemoveDelegates(INuimoController nuimoController)
        {
            nuimoController.GestureEventOccurred -= OnNuimoGestureEventAsync;
            nuimoController.FirmwareVersionRead -= OnFirmwareVersion;
            nuimoController.ConnectionStateChanged -= OnConnectionState;
            nuimoController.BatteryPercentageChanged -= OnBatteryPercentage;
            nuimoController.LedMatrixDisplayed -= OnLedMatrixDisplayed;
        }

        private async void ReloadButton_OnClick(object sender, RoutedEventArgs e)
        {
            var task = _nuimoController?.DisconnectAsync();
            if (task != null) await task;
            ListPairedNuimos();
        }

        private void PairedNuimosComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ConnectButton.IsEnabled = e.AddedItems != null;
        }

        private async void DisplayMatrix_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var displayInterval = GetLedMatrixDisplayInterval();
                var matrixString = GetLedMatrixString();
                var options = GetLedMatrixOptions();
                _nuimoController?.DisplayLedMatrixAsync(new NuimoLedMatrix(matrixString), displayInterval, options);
            }
            catch (FormatException) { await new MessageDialog("Display interval: Please enter a number between 0.00 and 25.5").ShowAsync(); }
        }

        private double GetLedMatrixDisplayInterval()
        {
            var displayIntervalText = DisplayIntervalTextBox.Text.Replace(',', '.');
            return displayIntervalText.Length > 0 ? double.Parse(displayIntervalText, new CultureInfo("us")) : 2;
        }

        private string GetLedMatrixString()
        {
            return LedGrid.Children
                .Select(element => element as Windows.UI.Xaml.Controls.CheckBox)
                .Select(checkBox => (checkBox.IsChecked ?? false) ? "*" : " ")
                .Aggregate((matrix, led) => matrix + led);
        }

        private int GetLedMatrixOptions()
        {
            return
                ((FadeTransitionCheckBox.IsChecked ?? false) ? (int)NuimoLedMatrixWriteOption.WithFadeTransition : 0) +
                ((WithoutWriteResponseCheckBox.IsChecked ?? false) ? (int)NuimoLedMatrixWriteOption.WithoutWriteResponse : 0);
        }

        private async void triggerKey(String key)
        {
            //Process.Start("C:\\");
            // The URI to launch
            if (!(key == null || key == String.Empty))
            {
                var uriBing = new Uri(@"nuimokeytrigger:" + key);

                OutputTextBox.Text = new StringBuilder(OutputTextBox.Text)
               .Append("trigger keys" + key + "\n")
               .ToString();

                // Launch the URI
                var success = await Windows.System.Launcher.LaunchUriAsync(uriBing);

                if (success)
                {
                    // URI launched
                    OutputTextBox.Text = new StringBuilder(OutputTextBox.Text)
                        .Append("Success" + "\n")
                        .ToString();
                }
                else
                {
                    // URI launch failed
                    OutputTextBox.Text = new StringBuilder(OutputTextBox.Text)
                        .Append("nope" + "\n")
                        .ToString();
                }
            }
            else
            {

            }
        }

        private void OnNuimoGestureEventAsync(NuimoGestureEvent nuimoGestureEvent)
        {
            OutputTextBox.Text = new StringBuilder(OutputTextBox.Text)
                .Append("NuimoGesture: ")
                .Append(nuimoGestureEvent.Gesture)
                .Append(" value: ")
                .Append(nuimoGestureEvent.Value + "\n")
                .ToString();

            if (nuimoGestureEvent.Gesture == NuimoGesture.ButtonPress)
            {
                triggerKey(_profile.ButtonPress);
            }
            if (nuimoGestureEvent.Gesture == NuimoGesture.ButtonRelease)
            {
                triggerKey(_profile.ButtonRelease);
            }
            if (nuimoGestureEvent.Gesture == NuimoGesture.SwipeUp)
            {
                triggerKey(_profile.SwipeUp);
            }
            if (nuimoGestureEvent.Gesture == NuimoGesture.SwipeDown)
            {
                triggerKey(_profile.SwipeDown);
            }
            if (nuimoGestureEvent.Gesture == NuimoGesture.SwipeLeft)
            {
                triggerKey(_profile.SwipeLeft);
            }
            if (nuimoGestureEvent.Gesture == NuimoGesture.SwipeRight)
            {
                triggerKey(_profile.SwipeRight);
            }

            if (nuimoGestureEvent.Gesture == NuimoGesture.Rotate && nuimoGestureEvent.Value > 10)
            {
                triggerKey(_profile.RotateRight);
            }
            if (nuimoGestureEvent.Gesture == NuimoGesture.Rotate && nuimoGestureEvent.Value < -10)
            {
                triggerKey(_profile.RotateLeft);
            }

            if (nuimoGestureEvent.Gesture == NuimoGesture.FlyUpDown && nuimoGestureEvent.Value > 125)
            {
                triggerKey(_profile.FlyUp);
            }
            if (nuimoGestureEvent.Gesture == NuimoGesture.FlyUpDown && nuimoGestureEvent.Value <= 125)
            {
                triggerKey(_profile.FlyDown);
            }

            if (nuimoGestureEvent.Gesture == NuimoGesture.FlyLeft)
            {
                triggerKey(_profile.FlyLeft);
            }
            if (nuimoGestureEvent.Gesture == NuimoGesture.FlyRight)
            {
                triggerKey(_profile.FlyRight);
            }

            OutputTextBox.ScrollToBottom();
        }

        private void OnFirmwareVersion(string firmwareVersion)
        {
            OutputTextBox.Text = "-------------------------------\nFirmware version: " + firmwareVersion + "\n-------------------------------\n";
        }

        private void OnConnectionState(NuimoConnectionState nuimoConnectionState)
        {
            string buttonTitle;
            switch (nuimoConnectionState)
            {
                case NuimoConnectionState.Disconnected: buttonTitle = "Connect"; break;
                case NuimoConnectionState.Connecting: buttonTitle = "Connecting..."; break;
                case NuimoConnectionState.Connected: buttonTitle = "Disconnect"; break;
                case NuimoConnectionState.Disconnecting: buttonTitle = "Disconnecting..."; break;
                default: buttonTitle = ""; break;
            }
            ConnectButton.Content = buttonTitle;
            ConnectionStateTextBlock.Text = nuimoConnectionState.ToString();
        }

        private void OnBatteryPercentage(int batteryPercentage)
        {
            BatteryPercentageTextBlock.Text = batteryPercentage + "%";
        }

        private void OnLedMatrixDisplayed()
        {
            OutputTextBox.Text = new StringBuilder(OutputTextBox.Text)
                .Append("The matrix you have sent has been displayed." + "\n")
                .ToString();
            OutputTextBox.ScrollToBottom();
        }

        private void TextBlock_SelectionChanged(object sender, RoutedEventArgs e)
        {

        }

        private void Select_Profile(object sender, RoutedEventArgs e)
        {

        }
    }
}

internal static class DependencyObjectExtension
{
    public static void ScrollToBottom(this DependencyObject dependencyObject)
    {
        var grid = (Grid)VisualTreeHelper.GetChild(dependencyObject, 0);
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(grid); i++)
        {
            object obj = VisualTreeHelper.GetChild(grid, i);
            if (!(obj is ScrollViewer)) continue;
            //((ScrollViewer)obj).ChangeView
            ((ScrollViewer)obj).ScrollToVerticalOffset(((ScrollViewer)obj).ExtentHeight);
            break;
        }
    }
}