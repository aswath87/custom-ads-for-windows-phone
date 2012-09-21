using System;
using System.Net;
using System.Windows;
using System.IO;
using Newtonsoft.Json;
using System.IO.IsolatedStorage;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using System.Windows.Media.Imaging;

namespace ProjectBrowser
{
    /// <summary>
    /// CustomAdClient helps you push ads to your apps. 
    /// You control what ads are displayed and how much through a config file that you host. 
    /// </summary>
    public class CustomAdClient
    {
        #region Members 

        private Uri _adConfigUri; // uri to the ad config file 
        private int _adConfigRefreshFrequencyInHours = 12; // interval to refresh cached ad config file          
        public List<AdImageUnit> _adImageUnits; // list of ad units 
        private string _adConfigId; // config id 
        private bool _forceAdConfigUpdate = false;
        string _currentAppName;
        
        // Names for isolated storage members 
        //
        private const string AdSelectorLastUpdateTimeVariableName = "AdSelectorLastUpdateTime";
        private const string AdConfigIdVariableName = "AdConfigId";
        private const string AdConfigFileName = "AdConfigFile.txt";

        #endregion 

        #region Constructor

        public CustomAdClient(string currentAppName, Uri adConfigUri, bool updateAdConfig)
        {
            _currentAppName = currentAppName;
            _adConfigUri = adConfigUri;
            _adImageUnits = new List<AdImageUnit>();
            _adConfigId = String.Empty;

            // Check for cached ad config file
            //
            if (FileHelpers.FileExists(AdConfigFileName))
            {
                // initialize config values from the cached file (json)
                //
                String jsonAdConfig = FileHelpers.ReadFile(AdConfigFileName).Trim();

                if (!String.IsNullOrEmpty(jsonAdConfig))
                {
                    try
                    {
                        JObject adConfigJObject = JsonConvert.DeserializeObject<JObject>(jsonAdConfig);
                        String adUnitsJson = adConfigJObject["adunits"].ToString();
                        _adConfigId = adConfigJObject["id"].ToString();
                        _adConfigRefreshFrequencyInHours = Convert.ToInt32(adConfigJObject["update_frequency_in_hours"].ToString());
                        _adImageUnits = JsonConvert.DeserializeObject<List<AdImageUnit>>(adUnitsJson);
                    }

                    catch (Exception e)
                    {
                        _forceAdConfigUpdate = true; 
                    }
                }
            }

            // If no cached ad config file exists
            //
            else
            {
                _forceAdConfigUpdate = true;
            }

            // Refresh ad config from server 
            //
            if (updateAdConfig)
            {
                UpdateAdConfig(); 
            }        
        }        

        #endregion

        #region Methods

        /// <summary>
        /// Randomly selects ad based on weightage
        /// </summary>
        /// <param name="forceSelect">if true, definitely returns a AdImageUnit</param>
        /// <returns></returns>
        public AdImageUnit SelectAd(bool forceSelect)
        {
            int totalPercentage;

            // If we have to select an ad from the list of adImageUnits, totalPercentage should be sum of percentages 
            //
            if (forceSelect)
            {
                totalPercentage = 0;
                _adImageUnits.ForEach(ad => totalPercentage += ad.display_percentage);
            }
            else
            {
                totalPercentage = 100;
            }

            Random random = new Random();
            int rand = random.Next(0, totalPercentage);
            int percentageCount = 0;
            foreach (AdImageUnit adImageUnit in _adImageUnits)
            {
                percentageCount += adImageUnit.display_percentage;

                // if we want to force an ad, and all ad units have 0 percent weightage - just display the first one
                //
                if (rand < percentageCount || (forceSelect && totalPercentage == 0))
                {
                    // if it's an ad for the current app, don't return it
                    //
                    if (!adImageUnit.name.Equals(_currentAppName, StringComparison.OrdinalIgnoreCase))
                    {
                        return adImageUnit;
                    }
                }
            }

            return null;
        }        

        /// <summary>
        /// Updates Ad config from server if needed.
        /// </summary>
        public void UpdateAdConfig()
        {
            bool isUpdateRquired = true;

            // Check if last updated time is recent enough. If it is, we don't have to update the ad config file 
            // We store the Last updated time in the IsolatedStorage app settings. 
            //
            if (IsolatedStorageSettings.ApplicationSettings.Contains(AdSelectorLastUpdateTimeVariableName))
            {
                DateTime lastUpdateTime = DateTime.Parse(IsolatedStorageSettings.ApplicationSettings[AdSelectorLastUpdateTimeVariableName].ToString());

                // If the last update time is less than the refresh frequency, we don't have to check the server for updates. 
                //
                if ((DateTime.Now - lastUpdateTime).TotalHours < _adConfigRefreshFrequencyInHours)
                {
                    isUpdateRquired = false;
                }
            }

            // Read ad config from if update is required 
            //
            if (isUpdateRquired || _forceAdConfigUpdate)
            {
                WebClient web = new WebClient();
                web.OpenReadCompleted += new System.Net.OpenReadCompletedEventHandler(AdConfigReadCompleted);
                web.OpenReadAsync(_adConfigUri);
            }
        }

        /// <summary>
        /// Callback when adConfig read completes to local buffer
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AdConfigReadCompleted(Object sender, OpenReadCompletedEventArgs e)
        {
            String jsonAdConfig = String.Empty;
            using (System.IO.StreamReader reader = new System.IO.StreamReader(e.Result))
            {
                jsonAdConfig = reader.ReadToEnd();
            }

            // Parse json to get values for ad config 
            if (!String.IsNullOrEmpty(jsonAdConfig))
            {
                JObject adConfigJObject = JsonConvert.DeserializeObject<JObject>(jsonAdConfig);
                String adUnitsJson = adConfigJObject["adunits"].ToString();
                String adConfigId = adConfigJObject["id"].ToString();
                if ((adConfigId != _adConfigId && !_forceAdConfigUpdate))
                {
                    List<AdImageUnit> adImageUnits = JsonConvert.DeserializeObject<List<AdImageUnit>>(adUnitsJson);
                    adImageUnits.ForEach(adImageUnit => adImageUnit.DownloadImage());
                    FileHelpers.WriteFile(jsonAdConfig, AdConfigFileName);
                }
            }

            IsolatedStorageSettings.ApplicationSettings[AdSelectorLastUpdateTimeVariableName] = DateTime.Now;
            _forceAdConfigUpdate = false;
        }

        #endregion
    }

    /// <summary>
    /// This represents an AdImageUnit which basically bundles the ad image uri, ad link uri and other ad unit metadata
    /// </summary>
    public class AdImageUnit
    {
        #region Members 
        private String _name; 
        private String _uri;              
        private String _imageUri;        
        private WebClient _webClient = new WebClient();
        private String _imageFileName;
        private int _displayPercentage;

        #endregion

        #region Properties         
        // property names are lower case to reflect json as that's the only way to get the Newtonsoft json parser to work. also, we need to define these properties for the parser to works

        /// <summary>
        /// AdImageUnit name 
        /// </summary>
        public string name 
        {
            get
            {
                return _name; 
            }
            set
            {
                _name = value.Trim(); 
            }
        }

        /// <summary>
        /// Uri that the ad unit links to 
        /// </summary>
        public string uri 
        { 
            get 
            { 
                return _uri; 
            } 
            set 
            { 
                _uri = value.Trim(); 
            } 
        }
        
        /// <summary>
        /// Uri of the ad image
        /// </summary>
        public string image_uri 
        { 
            get 
            { 
                return _imageUri; 
            } 
            set 
            { 
                _imageUri = value.Trim(); 
            } 
        } 

        /// <summary>
        /// Returns file name for ad image on Isolated storage 
        /// </summary>
        public String ImageFileName
        {
            get
            {
                if (_imageFileName == null)
                {
                    string ext = String.Empty;

                    // sanitize image name and append file extension 
                    //
                    if (image_uri.Contains("."))
                    {
                        ext = image_uri.Substring(image_uri.LastIndexOf('.')).Trim();
                    }

                    _imageFileName = name.Replace(' ', '_').Trim() + ext;
                }

                return _imageFileName; 
                
            }
        }        

        #endregion 

        #region Methods 

        /// <summary>
        /// Downloads the ad image from the image_uri and saves to isolated storage
        /// </summary>
        public void DownloadImage()
        {
            _webClient.OpenReadCompleted += new OpenReadCompletedEventHandler(ImageUriReadCompleted);
            _webClient.OpenReadAsync(new Uri(image_uri, UriKind.Absolute));
        }

        /// <summary>
        /// Callback method when image read to local buffer completes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ImageUriReadCompleted(object sender, OpenReadCompletedEventArgs e)
        {
            byte[] buffer = new byte[1024];

            // Create (or replace) file and write image to it
            //
            Stream stream = e.Result;
            using (IsolatedStorageFile isf = IsolatedStorageFile.GetUserStoreForApplication())
            {
                if (!isf.FileExists(ImageFileName))
                {
                    isf.DeleteFile(ImageFileName);
                }

                // Saves image file to isolated storage
                //
                using (System.IO.IsolatedStorage.IsolatedStorageFileStream isfs = new IsolatedStorageFileStream(ImageFileName, FileMode.Create, isf))
                {
                    int count = 0;
                    while (0 < (count = stream.Read(buffer, 0, buffer.Length)))
                    {
                        isfs.Write(buffer, 0, count);
                    }

                    stream.Close();
                    isfs.Close();
                }
            }
        }

        
        // Reads the adimage from isolated storage, converts to BitmapImage and returns it
        // 
        public BitmapImage AdImage()
        {
            byte[] data;

            using (IsolatedStorageFile isf = IsolatedStorageFile.GetUserStoreForApplication())
            {
                // If image file doesn't exist, try to download it (async), but return null this time
                //
                if (!isf.FileExists(ImageFileName))
                {
                    this.DownloadImage();
                    return null;
                }

                // Open the file
                //
                using (IsolatedStorageFileStream isfs = isf.OpenFile(ImageFileName, FileMode.Open, FileAccess.Read))
                {
                    data = new byte[isfs.Length];
                    isfs.Read(data, 0, data.Length);
                    isfs.Close();
                }
            }

            // Create memory stream and bitmap
            //
            MemoryStream ms = new MemoryStream(data);
            BitmapImage bitmapImage = new BitmapImage();

            // Set bitmap source to memory stream
            //
            bitmapImage.SetSource(ms);
            return bitmapImage;
        }

        #endregion
    }
}
