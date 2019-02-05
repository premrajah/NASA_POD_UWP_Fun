using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Media.Imaging;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace APOD_UWP
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        const string EndpointURL = "https://api.nasa.gov/planetary/apod";
        const string API_KEY = "pEhH1LvgQqA4RhhZNQDmEZeAfMhzxIeMhD6iLqeh";

        const string SettingDateToday = "date today";
        const string SettingShowOnStartup = "show on start up";
        const string SettingImageCountToday = "image count today";
        const string SettingLimitRange = "limit range";

        // declaire a container for the global settigns 
        Windows.Storage.ApplicationDataContainer localSettings;

        DateTime launchDate = new DateTime(1995, 6, 16); // APOD launch date

        int imageCountToday;

        /// <summary>
        /// Constructor
        /// </summary>
        public MainPage()
        {
            this.InitializeComponent();
            localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;

            MonthCalender.MinDate = launchDate;
            MonthCalender.MaxDate = DateTime.Today;

            ReadSettings();
        }

        /// <summary>
        /// Reading Local Settings
        /// </summary>
        private void ReadSettings()
        {
            bool isToday = false;
            Object todayObject = localSettings.Values[SettingDateToday];

            if(todayObject != null)
            {
                //first chekc if this is the same day as the previous run of th app
                DateTime dt = DateTime.Parse((string)todayObject);
                if (dt.Equals(DateTime.Today))
                {
                    isToday = true;
                }
            }

            // default for images downloaded today
            imageCountToday = 0;

            if(isToday)
            {
                Object value = localSettings.Values[SettingImageCountToday];
                if(value != null)
                {
                    imageCountToday = int.Parse((string)value);
                }
            }
            ImagesTodayTextBox.Text = imageCountToday.ToString();

            // set the ui check boxes depending on the stored settings or defaults if there are no settings
            Object showTodayObject = localSettings.Values[SettingShowOnStartup];
            if(showTodayObject != null)
            {
                ShowTodaysImageCheckBox.IsChecked = bool.Parse((string)showTodayObject);
            } else
            {
                // set the default
                ShowTodaysImageCheckBox.IsChecked = true;
            }

            Object limitRangeObject = localSettings.Values[SettingLimitRange];
            if(limitRangeObject != null)
            {
                LimitRangeCheckBox.IsChecked = bool.Parse((string)limitRangeObject);
            } else
            {
                // set default
                LimitRangeCheckBox.IsChecked = false;
            }


            // show todays image if the checkbox requires it
            if(ShowTodaysImageCheckBox.IsChecked == true)
            {
                MonthCalender.Date = DateTime.Today;
            }
        }

        private void LaunchButton_Click(object sender, RoutedEventArgs e)
        {
            LimitRangeCheckBox.IsChecked = false;
            MonthCalender.Date = launchDate;
        }


      

        private void LimitRangeCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            var firstDayOfThisYear = new DateTime(DateTime.Today.Year, 1, 1);
            MonthCalender.MinDate = firstDayOfThisYear;
        }
        private void LimitRangeCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            MonthCalender.MinDate = launchDate;
        }

        private async void MonthCalender_DateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
        {
            await RetrievePhoto();
        }

        /// <summary>
        /// Supported image formats
        /// </summary>
        /// <param name="photoURL"></param>
        /// <returns></returns>
        private bool IsSupportedFormat(string photoURL)
        {
            // extract extension
            string ext = Path.GetExtension(photoURL).ToLower();

            // Check the extension against supported UWP formats.
            return (
                        ext == ".jpg"   || 
                        ext == ".jpeg"  || 
                        ext == ".png"   || 
                        ext == ".gif"   ||
                        ext == ".tif"   || 
                        ext == ".bmp"   || 
                        ext == ".ico"   || 
                        ext == ".svg"
                    );
        }

        /// <summary>
        /// Get images and information from APOD
        /// </summary>
        /// <returns></returns>
        private async Task RetrievePhoto()
        {
            var client = new HttpClient();
            JObject jResult = null;

            string responseContent = null;
            string description = null;
            string photoUrl = null;
            string copyright = null;

            // sets ui elements to defaults
            ImageCopyrightTextBox.Text = "NASA";
            DescriptionTextBox.Text = "";

            // build the date parameter string for the date selected or the last date of the range is specified 
            DateTimeOffset dt = (DateTimeOffset)MonthCalender.Date;

            string dateSelected = $"{dt.Year.ToString()}-{dt.Month.ToString("00")}-{dt.Day.ToString("00")}";
            string URLParams = $"?date={dateSelected}&api_key={API_KEY}";

            //populate the http client appropriately
            client.BaseAddress = new Uri(EndpointURL);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            //the critical call: sends a GET resquest witht he appropriate parameers
            HttpResponseMessage response = client.GetAsync(URLParams).Result;

            

            if (response.IsSuccessStatusCode)
            {   
                // catch errors
                try
                {
                    // check remaining rate limit
                    var xRateLimitRemaining = response.Headers.GetValues("X-RateLimit-Remaining");
                    if (xRateLimitRemaining != null)
                    {
                        foreach (var item in xRateLimitRemaining)
                        {
                            ImagesTodayTextBox.Text = item;
                        }
                    }

                    

                    // parse response using newtonsoft API's
                    responseContent = await response.Content.ReadAsStringAsync();

                    //parse the response string for details needed
                    jResult = JObject.Parse(responseContent);

                    

                    // get the image
                    photoUrl = (string)jResult["url"];
                    var photoURI = new Uri(photoUrl);
                    var bmi = new BitmapImage(photoURI);

                    ImagePictureBox.Source = bmi; // assign to the image element

                    if (IsSupportedFormat(photoUrl))
                    {
                        // get the copyright message
                        copyright = (string)jResult["copyright"];

                        if(copyright != null && copyright.Length > 0)
                        {
                            ImageCopyrightTextBox.Text = copyright;
                        }

                        // populate the description box
                        description = (string)jResult["explanation"];
                        DescriptionTextBox.Text = description;
                        ImageURLDisplay.Text = photoUrl;
                    }
                    else
                    {
                        DescriptionTextBox.Text = $"Image type not supported. URL is {photoUrl}";
                    }
                    
                }
                catch (Exception ex)
                {
                    DescriptionTextBox.Text = $"Image data is not supported: {ex.Message}";
                }
            }
            else
            {
                DescriptionTextBox.Text = $"We were unable to retrieve the NASA picture for the day: {response.StatusCode.ToString()} {response.ReasonPhrase}";
            }















        }

        private void Grid_LostFocus(object sender, RoutedEventArgs e)
        {
            WriteSettings();
        }

        /// <summary>
        /// write to local settings 
        /// </summary>
        public void WriteSettings()
        {
            localSettings.Values[SettingDateToday] = DateTime.Today.ToString();
            localSettings.Values[SettingShowOnStartup] = ShowTodaysImageCheckBox.IsChecked.ToString();
            localSettings.Values[SettingLimitRange] = LimitRangeCheckBox.IsChecked.ToString();
            localSettings.Values[SettingImageCountToday] = imageCountToday.ToString();
        }
    }
}
