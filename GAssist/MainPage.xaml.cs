﻿using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Tizen.Applications;
using Tizen.Multimedia;
using Tizen.System;
using Tizen.Wearable.CircularUI.Forms;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using Application = Tizen.Applications.Application;

namespace GAssist
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class MainPage
    {
        private static MainPage _mainpage;
        public static Preferences Pref;


        public static readonly Label Label = new Label
        {
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            VerticalOptions = LayoutOptions.FillAndExpand,
            HorizontalOptions = LayoutOptions.FillAndExpand,
            Margin = new Thickness(50, 50, 50, 80),
            Text = "GAssist.Net\n\nPress listen button to start"
        };

        private static WebView WebView2;

        public static InformationPopup TextPopUp;

        public static ProgressPopup ProgressPopup;

        private const string imageDir =
            "/opt/usr/apps/com.cybernetic87.GAssist.Tizen.Wearable/shared/res";

        private Player _player;
        private SapService _sapService;
        private readonly App _app;


        public MainPage(App app)
        {
            _mainpage = this;
            _app = app;
            InitializeComponent();
            Pref = new Preferences(app, this);
            Pref.LoadSettings();
            SettingsPage.Appearing += SettingsPage_Appearing;

            _mainpage.ScrollView.Content = Label;

            SetButtonImage("listen_disabled_allgreyedout.png");
            SetActionButtonIsEnabled(false);

            ImageButton.Clicked += ActionButton_ButtonClicked;
            ImageButton.Pressed += ImageButton_PressedAsync;

            TextPopUp = new InformationPopup();

            TextPopUp.BackButtonPressed += (s, e) =>
            {
                TextPopUp.Dismiss();
            };



            Task.Run(async () =>
            {
                _sapService = new SapService(OnConnectedCallback);
                await _sapService.Connect().ConfigureAwait(false);
            });

            Task.Run(() =>
            {
                _player = new Player();
                _player.SetSource(new MediaBufferSource(File.ReadAllBytes(Path.Combine(imageDir, "ding.mp3"))));
                _player.PrepareAsync();
                _player.PlaybackCompleted += delegate { _player.Stop(); };
            }
            );
        }


        private void SettingsPage_Appearing(object sender, EventArgs e)
        {
            if (MyScroller.ItemsSource == null)
            {
                MyScroller.ItemsSource = Pref.Settings;
            }
        }

        private async void ImageButton_PressedAsync(object sender, EventArgs e)
        {
            await ImageButton.FadeTo(0.5, 300).ConfigureAwait(false);
            await ImageButton.FadeTo(1, 300).ConfigureAwait(false);
        }

        public void App_ResumeEvent(object sender, EventArgs e)
        {
            var appid = Application.Current.ApplicationInfo.ApplicationId;
            var arc = new ApplicationRunningContext(appid);

            if (SapService.IsConnected && !AudioRecorder.IsRecording && arc.State == ApplicationRunningContext.AppState.Background)
            {
                if (AudioPlayer.IsPlaying) ResponseHandler.Player.Stop();
                StartListening();
            }
            //            else if (!SapService.IsConnected)
            //            {
            //#pragma warning disable 4014
            //                _sapService.Connect();
            //#pragma warning restore 4014
            //            }
        }

        private void Player_PlaybackStopped(object sender, EventArgs e)
        {
            SetButtonImage("listen_blue.png");
            SetActionButtonIsEnabled(true);
        }

        private void OnConnectedCallback()
        {
            var arc = new ApplicationRunningContext(Application.Current.ApplicationInfo.ApplicationId);
            if (Pref.GetRecordOnStart() && arc.State == ApplicationRunningContext.AppState.Foreground)
            {
                StartListening();
                return;
            }

            SetButtonImage("listen_blue.png");
            SetActionButtonIsEnabled(true);
        }

        internal static void SetLabelText(string text)
        {
            if (_mainpage.ScrollView.Content != Label)
            {
                _mainpage.ScrollView.Margin = new Thickness(50, 50, 50, 80);
                _mainpage.ScrollView.Content = Label;
                _mainpage.ScrollView.Orientation = ScrollOrientation.Vertical;
            }
            Label.Text = text.TrimEnd(Environment.NewLine.ToCharArray());
        }

        internal static void SetHtmlView(string html)
        {
            var htmlSource = new HtmlWebViewSource
            {
                Html = HtmlResponseParser.ParseHtmlResponse(html)
            };
            //htmlSource.Html = HtmlResponseParser.ParseHtmlResponse2(parsed);

            if (_mainpage.ScrollView.Content != WebView2)
            {
                WebView2 = new WebView
                {
                    BackgroundColor = Color.Black,
                    AnchorX = 0.5,
                    AnchorY = 0.5
                };

                _mainpage.ScrollView.Content = WebView2;
                _mainpage.ScrollView.Orientation = ScrollOrientation.Both;
                _mainpage.ScrollView.Margin = new Thickness(0, 0, 0, 0);
            }

            WebView2.Source = htmlSource;
        }

        internal static void SetButtonImage(string img)
        {
            _mainpage.ImageButton.Source = ImageSource.FromFile(Path.Combine(imageDir, img));
        }

        internal static void SetActionButtonIsEnabled(bool isEnable)
        {
            _mainpage.ImageButton.IsEnabled = isEnable;
        }

        private void ActionButton_ButtonClicked(object sender, EventArgs e)
        {
            if (AudioPlayer.IsPlaying)
            {
                ResponseHandler.Player.Stop();
            }
            else if (!AudioRecorder.IsRecording && SapService.IsConnected)
            {
                StartListening();
            }
        }

        private void StartListening()
        {
            Task.Run(() => Parallel.Invoke(
            () =>
            {
                if (Pref.GetSoundFeedback())
                {
                    _player.Start();
                }
                //WavPlayer.StartAsync(Path.Combine(imageDir, "ding.wav"), new AudioStreamPolicy(AudioStreamType.Media));
            },
            () =>
            {
                if (Pref.GetVibrateFeedback())
                {
                    Vibrator vibrator = Vibrator.Vibrators[0];
                    vibrator.Vibrate(200, 100);
                }
            }
            ));

            //NoResponseTimer.Start();
            AudioRecorder.StartRecording(Pref.GetHtmlResponse());
            (ProgressPopup ?? (ProgressPopup = new ProgressPopup())).Show();

            SetActionButtonIsEnabled(false);
            SetButtonImage("listen_disabled_allgreyedout.png");
            //SetActionButtonIsEnabled(false); button dissapears when listening
            SetLabelText(string.Empty);
        }


        public static void ShowMessage(string message, string debugLog = null)
        {
            Toast.DisplayText(message, 2000);
            Debug.WriteLine("[DEBUG] " + message);
        }
    }
}