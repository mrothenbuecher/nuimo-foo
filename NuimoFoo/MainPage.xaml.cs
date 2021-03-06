﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuimoSDK;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Search;
using Windows.System;
using Windows.UI;
using Windows.UI.Notifications;
using Windows.UI.Popups;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

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
        private Windows.Storage.StorageFolder _localFolder = Windows.Storage.ApplicationData.Current.LocalFolder;

        private ToastNotifier _toastNotifier = ToastNotificationManager.CreateToastNotifier();
        private ToastNotification _lastToast = null;

        private ProcessRequester _processRequester;
        private Profile _profile;
        private Settings _settings;

        public MainPage()
        {
            InitializeComponent();
            InitSettings();
            InitProfiles();

            _processRequester = new ProcessRequester();

            ListPairedNuimos();
            AddLedCheckBoxes();
            OutputTextBox.TextWrapping = TextWrapping.NoWrap;
            ProfileTextBox.TextWrapping = TextWrapping.NoWrap;
            DisplayIntervalTextBox.Text = "5.0";

            Windows.UI.Xaml.Application.Current.DebugSettings.EnableFrameRateCounter = false;
            ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.Auto;

        }

        private async void InitSettings()
        {
            StorageFile settingsFile;
            string json;
            try
            {
                settingsFile = await _localFolder.GetFileAsync("settings.json"); //Getting Text files
            }
            catch (Exception e)
            {
                ProfileTextBox.Text = new StringBuilder(ProfileTextBox.Text)
                    .Append("settings exception: " + e.Message + "\n")
                    .ToString();

                // there is no settings file
                // create one
                _settings = new Settings();

                settingsFile = await UpdateSettingsFileAsync();
            }

            json = await FileIO.ReadTextAsync(settingsFile);

            ProfileTextBox.Text = new StringBuilder(ProfileTextBox.Text)
                .Append("read settings:" + json + "\n")
                .ToString();

            _settings = JsonConvert.DeserializeObject<Settings>(json);

            valueThreshold.Text = "" + _settings.rotateThreshold;
            automaticSwitch.IsChecked = _settings.automaticSwitchBetweenProfiles;

        }

        private async void InitProfiles()
        {
            var profileFolders = await _localFolder.CreateFolderAsync("Profiles", CreationCollisionOption.OpenIfExists);

            var Files = await profileFolders.GetFilesAsync(CommonFileQuery.OrderByName); //Getting Text files
            if (Files.Count == 0)
            {
                // there are noe profile files
                // create one
                _profile = new Profile();
                string json = JsonConvert.SerializeObject(_profile, Formatting.Indented);

                ProfileTextBox.Text = new StringBuilder(ProfileTextBox.Text)
                        .Append(json + "\n")
                        .ToString();
                var textFile = await profileFolders.CreateFileAsync("default.json");
                await FileIO.WriteTextAsync(textFile, json);

                Files = await profileFolders.GetFilesAsync(CommonFileQuery.OrderByName);

            }
            ProfilesComboBox.Items?.Clear();
            // there are profiles
            string str = "";
            foreach (StorageFile file in Files)
            {
                str = str + ", " + file.Name;
                ProfilesComboBox.Items?.Add(file.Name);
            }

            ProfileTextBox.Text = new StringBuilder(ProfileTextBox.Text)
            .Append("Profiles found:" + str + "\n")
            .ToString();

            //TODO may be save
            if (ProfilesComboBox.Items?.Count > 0) ProfilesComboBox.SelectedIndex = 0;

            SwitchProfile(true);

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
            if (PairedNuimosComboBox.Items?.Count == 1)
            {
                // automatic connect to nuimo
                ConnectButton_OnClick(null, null);

                OutputTextBox.Text = new StringBuilder(OutputTextBox.Text)
                .Append("autoconnect" + "\n")
                .ToString();
            }
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
            string foo = LedGrid.Children
                .Select(element => element as Windows.UI.Xaml.Controls.CheckBox)
                .Select(checkBox => (checkBox.IsChecked ?? false) ? "*" : " ")
                .Aggregate((matrix, led) => matrix + led);

            ProfileTextBox.Text = new StringBuilder(ProfileTextBox.Text)
                            .Append(foo + "\n")
                            .ToString();

            return foo;
        }

        private int GetLedMatrixOptions()
        {
            return
                ((FadeTransitionCheckBox.IsChecked ?? false) ? (int)NuimoLedMatrixWriteOption.WithFadeTransition : 0) +
                ((WithoutWriteResponseCheckBox.IsChecked ?? false) ? (int)NuimoLedMatrixWriteOption.WithoutWriteResponse : 0);
        }

        private async void TriggerApp(String appUri)
        {
            //Process.Start("C:\\");
            // The URI to launch
            if (!String.IsNullOrEmpty(appUri))
            {
                // change profile with nuimo
                if (appUri.ToLower().Equals("profile.next") || appUri.ToLower().Equals("profile.prev"))
                {
                    int i = 0;
                    if (appUri.ToLower().Equals("profile.next"))
                    {
                        // next profile in list
                        i = (ProfilesComboBox.SelectedIndex + 1) % ProfilesComboBox.Items.Count;
                    }
                    else
                    {
                        // previous profile in list
                        i = ProfilesComboBox.SelectedIndex - 1;
                        if (i < 0)
                        {
                            i = ProfilesComboBox.Items.Count - 1;
                        }
                    }
                    ProfilesComboBox.SelectedIndex = i;
                    SwitchProfile(false);
                }
                else
                {

                    string proc = _processRequester.GetProcesses();

                    ProfileTextBox.Text = new StringBuilder(ProfileTextBox.Text)
                   .Append("received proc:" + proc + "\n")
                   .ToString();

                    var uriBing = new Uri(appUri);

                    ProfileTextBox.Text = new StringBuilder(ProfileTextBox.Text)
                   .Append("trigger App with uri: " + appUri + "\n")
                   .ToString();

                    // Launch the URI
                    var success = await Launcher.LaunchUriAsync(uriBing);

                    if (success)
                    {
                        // URI launched
                        ProfileTextBox.Text = new StringBuilder(ProfileTextBox.Text)
                            .Append("Success" + "\n")
                            .ToString();
                    }
                    else
                    {
                        // URI launch failed
                        ProfileTextBox.Text = new StringBuilder(ProfileTextBox.Text)
                            .Append("error " + "\n")
                            .ToString();
                    }
                }
            }
            else
            {
                // no command found
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

            try
            {

                if (nuimoGestureEvent.Gesture == NuimoGesture.ButtonPress)
                {
                    TriggerApp(_profile.ButtonPress);
                }
                if (nuimoGestureEvent.Gesture == NuimoGesture.ButtonRelease)
                {
                    TriggerApp(_profile.ButtonRelease);
                }
                if (nuimoGestureEvent.Gesture == NuimoGesture.SwipeUp)
                {
                    TriggerApp(_profile.SwipeUp);
                }
                if (nuimoGestureEvent.Gesture == NuimoGesture.SwipeDown)
                {
                    TriggerApp(_profile.SwipeDown);
                }
                if (nuimoGestureEvent.Gesture == NuimoGesture.SwipeLeft)
                {
                    TriggerApp(_profile.SwipeLeft);
                }
                if (nuimoGestureEvent.Gesture == NuimoGesture.SwipeRight)
                {
                    TriggerApp(_profile.SwipeRight);
                }

                if (nuimoGestureEvent.Gesture == NuimoGesture.Rotate && nuimoGestureEvent.Value > _settings.rotateThreshold)
                {
                    TriggerApp(_profile.RotateRight);
                }
                if (nuimoGestureEvent.Gesture == NuimoGesture.Rotate && nuimoGestureEvent.Value < (-1* _settings.rotateThreshold))
                {
                    TriggerApp(_profile.RotateLeft);
                }

                if (nuimoGestureEvent.Gesture == NuimoGesture.FlyUpDown && nuimoGestureEvent.Value >= 135)
                {
                    TriggerApp(_profile.FlyUp);
                }
                if (nuimoGestureEvent.Gesture == NuimoGesture.FlyUpDown && nuimoGestureEvent.Value <= 115 && nuimoGestureEvent.Value > 1)
                {
                    TriggerApp(_profile.FlyDown);
                }

                if (nuimoGestureEvent.Gesture == NuimoGesture.FlyLeft)
                {
                    TriggerApp(_profile.FlyLeft);
                }
                if (nuimoGestureEvent.Gesture == NuimoGesture.FlyRight)
                {
                    TriggerApp(_profile.FlyRight);
                }

                if(ProfileTextBox != null)
                    ProfileTextBox.ScrollToBottom();
                if(OutputTextBox != null)
                    OutputTextBox.ScrollToBottom();
            }
            catch (Exception ex)
            {
                OutputTextBox.Text = new StringBuilder(OutputTextBox.Text)
                .Append("Exception : ")
                .Append(ex.Message)
                .Append("\n")
                .Append(ex.StackTrace)
                .Append("\n")
                .ToString();
            }
        }

        private void OnFirmwareVersion(string firmwareVersion)
        {
            OutputTextBox.Text = "Firmware version: " + firmwareVersion + "\n";
        }

        private void OnConnectionState(NuimoConnectionState nuimoConnectionState)
        {
            string buttonTitle;
            switch (nuimoConnectionState)
            {
                case NuimoConnectionState.Disconnected: buttonTitle = "Connect"; break;
                case NuimoConnectionState.Connecting: buttonTitle = "Connecting..."; break;
                case NuimoConnectionState.Connected:
                    buttonTitle = "Disconnect";
                    var matrixString = " *     * ***   *** *     *             ***     *   *    *   *    *   *     ***   ";
                    _nuimoController?.DisplayLedMatrixAsync(new NuimoLedMatrix(matrixString));
                    break;
                case NuimoConnectionState.Disconnecting: buttonTitle = "Disconnecting..."; break;
                default: buttonTitle = ""; break;
            }

            ShowToastNotification("Connection State changed", nuimoConnectionState.ToString());
                


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
             SwitchProfile(false);
        }

        private async void SwitchProfile(bool silent)
        {
            var profileFolders = await _localFolder.CreateFolderAsync("Profiles", CreationCollisionOption.OpenIfExists);

            if (ProfilesComboBox.SelectedValue != null && !String.IsNullOrEmpty(ProfilesComboBox.SelectedValue.ToString()))
            {
                try
                {

                    var textFile = await profileFolders.GetFileAsync(ProfilesComboBox.SelectedValue.ToString());

                    String json = await FileIO.ReadTextAsync(textFile);

                    ProfileTextBox.Text = new StringBuilder(ProfileTextBox.Text)
                        .Append("read json:" + json + "\n")
                        .ToString();

                    _profile = JsonConvert.DeserializeObject<Profile>(json);


                    if (!silent) ShowToastNotification("changed profile", ProfilesComboBox.SelectedValue.ToString());

                    ProfileTextBox.Text = new StringBuilder(ProfileTextBox.Text)
                       .Append("loaded profile:" + ProfilesComboBox.SelectedValue.ToString() + "\n")
                       .ToString();
                }
                catch (Exception ex)
                {
                    ProfileTextBox.Text = new StringBuilder(ProfileTextBox.Text)
                       .Append("Exception:" + ex.Message + "\n")
                       .ToString();
                }
            }
        }

        private void ShowToastNotification(string title, string stringContent)
        {
            if (_lastToast != null)
            {
                _toastNotifier.Hide(_lastToast);
            }
            Windows.Data.Xml.Dom.XmlDocument toastXml = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastText02);
            Windows.Data.Xml.Dom.XmlNodeList toastNodeList = toastXml.GetElementsByTagName("text");
            toastNodeList.Item(0).AppendChild(toastXml.CreateTextNode(title));
            toastNodeList.Item(1).AppendChild(toastXml.CreateTextNode(stringContent));
            Windows.Data.Xml.Dom.IXmlNode toastNode = toastXml.SelectSingleNode("/toast");
            //Windows.Data.Xml.Dom.XmlElement audio = toastXml.CreateElement("audio");
            //audio.SetAttribute("src", "ms-winsoundevent:Notification.SMS");

            ToastNotification toast = new ToastNotification(toastXml);
            toast.ExpirationTime = DateTime.Now.AddSeconds(5);
            _toastNotifier.Show(toast);
            _lastToast = toast;
        }

        private async void Open_Profile_DirAsync(object sender, RoutedEventArgs e)
        {
            var profileFolders = await _localFolder.CreateFolderAsync("Profiles", CreationCollisionOption.OpenIfExists);
            await Launcher.LaunchFolderAsync(profileFolders);
        }

        private void AutomaticSwitch_Checked(object sender, RoutedEventArgs e)
        {
            UpdateSettings();
        }

        private void ValueThreshold_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateSettings();
        }

        private async void UpdateSettings()
        {
            _settings.automaticSwitchBetweenProfiles =  automaticSwitch.IsChecked.Value;
            var value = valueThreshold.Text.Replace(',', '.');
            int val = value.Length > 0 ? int.Parse(value, new CultureInfo("us")) : 2;
            _settings.rotateThreshold = val;

            await UpdateSettingsFileAsync();
        }

        private async Task<StorageFile> UpdateSettingsFileAsync()
        {
            string json = JsonConvert.SerializeObject(_settings, Formatting.Indented);

            ProfileTextBox.Text = new StringBuilder(ProfileTextBox.Text)
                    .Append("updated settings: " + json + "\n")
                    .ToString();
            var textFile = await _localFolder.CreateFileAsync("settings.json");
            var result = FileIO.WriteTextAsync(textFile, json);

            return await _localFolder.GetFileAsync("settings.json");
        }
    }
}

internal static class DependencyObjectExtension
{
    public static void ScrollToBottom(this DependencyObject dependencyObject)
    {
        if (dependencyObject != null)
        {
            try
            {
                var grid = (Grid)VisualTreeHelper.GetChild(dependencyObject, 0);
                for (var i = 0; i < VisualTreeHelper.GetChildrenCount(grid); i++)
                {
                    object obj = VisualTreeHelper.GetChild(grid, i);
                    if (!(obj is ScrollViewer)) continue;
                    ((ScrollViewer)obj).ChangeView(0.0f, ((ScrollViewer)obj).ExtentHeight, 1.0f, true);
                    break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to scroll: " + ex.Message);
            }
        }
    }
}
