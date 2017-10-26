using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuimoSDK;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
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
        private Windows.Storage.StorageFolder localFolder = Windows.Storage.ApplicationData.Current.LocalFolder;

        private ToastNotifier ToastNotifier = ToastNotificationManager.CreateToastNotifier();
        private ToastNotification lastToast = null;
        private Profile _profile;

        public MainPage()
        {
            InitializeComponent();

            ListPairedNuimos();
            AddLedCheckBoxes();
            OutputTextBox.TextWrapping = TextWrapping.NoWrap;
            ProfileTextBox.TextWrapping = TextWrapping.NoWrap;
            DisplayIntervalTextBox.Text = "5.0";

            initProfiles();
            Windows.UI.Xaml.Application.Current.DebugSettings.EnableFrameRateCounter = false;
            ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.Auto;
        }

        private async void initProfiles()
        {
            var profileFolders = await localFolder.CreateFolderAsync("Profiles", CreationCollisionOption.OpenIfExists);

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

            Select_ProfileAsync(null, null);

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

        private async void triggerApp(String appUri)
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
                    Select_ProfileAsync(null,null);
                }
                else
                {
                    var uriBing = new Uri(appUri);

                    ProfileTextBox.Text = new StringBuilder(ProfileTextBox.Text)
                   .Append("trigger App with uri" + appUri + "\n")
                   .ToString();

                    // Launch the URI
                    var success = await Windows.System.Launcher.LaunchUriAsync(uriBing);

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
                            .Append("nope" + "\n")
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
                    triggerApp(_profile.ButtonPress);
                }
                if (nuimoGestureEvent.Gesture == NuimoGesture.ButtonRelease)
                {
                    triggerApp(_profile.ButtonRelease);
                }
                if (nuimoGestureEvent.Gesture == NuimoGesture.SwipeUp)
                {
                    triggerApp(_profile.SwipeUp);
                }
                if (nuimoGestureEvent.Gesture == NuimoGesture.SwipeDown)
                {
                    triggerApp(_profile.SwipeDown);
                }
                if (nuimoGestureEvent.Gesture == NuimoGesture.SwipeLeft)
                {
                    triggerApp(_profile.SwipeLeft);
                }
                if (nuimoGestureEvent.Gesture == NuimoGesture.SwipeRight)
                {
                    triggerApp(_profile.SwipeRight);
                }

                if (nuimoGestureEvent.Gesture == NuimoGesture.Rotate && nuimoGestureEvent.Value > 10)
                {
                    triggerApp(_profile.RotateRight);
                }
                if (nuimoGestureEvent.Gesture == NuimoGesture.Rotate && nuimoGestureEvent.Value < -10)
                {
                    triggerApp(_profile.RotateLeft);
                }

                if (nuimoGestureEvent.Gesture == NuimoGesture.FlyUpDown && nuimoGestureEvent.Value >= 135)
                {
                    triggerApp(_profile.FlyUp);
                }
                if (nuimoGestureEvent.Gesture == NuimoGesture.FlyUpDown && nuimoGestureEvent.Value <= 115 && nuimoGestureEvent.Value > 1)
                {
                    triggerApp(_profile.FlyDown);
                }

                if (nuimoGestureEvent.Gesture == NuimoGesture.FlyLeft)
                {
                    triggerApp(_profile.FlyLeft);
                }
                if (nuimoGestureEvent.Gesture == NuimoGesture.FlyRight)
                {
                    triggerApp(_profile.FlyRight);
                }

                ProfileTextBox.ScrollToBottom();
                OutputTextBox.ScrollToBottom();
            }catch(Exception ex)
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
                case NuimoConnectionState.Connected: buttonTitle = "Disconnect"; break;
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

        private async void Select_ProfileAsync(object sender, RoutedEventArgs e)
        {
            var profileFolders = await localFolder.CreateFolderAsync("Profiles", CreationCollisionOption.OpenIfExists);

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

                    ShowToastNotification("changed profile", ProfilesComboBox.SelectedValue.ToString());

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
            if (lastToast != null)
            {
                ToastNotifier.Hide(lastToast);
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
            ToastNotifier.Show(toast);
            lastToast = toast;
        }

        private async void Open_Profile_DirAsync(object sender, RoutedEventArgs e)
        {
            var profileFolders = await localFolder.CreateFolderAsync("Profiles", CreationCollisionOption.OpenIfExists);
            await Launcher.LaunchFolderAsync(profileFolders);
        }
    }
}

internal static class DependencyObjectExtension
{
    public static void ScrollToBottom(this DependencyObject dependencyObject)
    {
        if (dependencyObject != null) {
            var grid = (Grid)VisualTreeHelper.GetChild(dependencyObject, 0);
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(grid); i++)
            {
                object obj = VisualTreeHelper.GetChild(grid, i);
                if (!(obj is ScrollViewer)) continue;
                ((ScrollViewer)obj).ChangeView(0.0f, ((ScrollViewer)obj).ExtentHeight, 1.0f, true);
                break;
            }
        }
    }
}
