Custom Ads For Windows Phone Apps
=================================

Do you want to push your own display ads to your Windows Phone Apps and dynamically control which ads are displayed? 

Custom Ads will help you do that. You control what ads are displayed and how much through a config file that you host.

Battle tested with over 12 apps, with 200,000 downloads and 2000 daily users. 

Use cases: 
===========
1. You have multiple apps in the Windows Phone marketplace and want to cross promote your apps.
2. You want to promote some other product/app/non-profit/anything
3. You sourced your own ads and you want to push them 
4. ...

Features:
==========
1. You don't need to host a server! : You just need to host a config file somewhere. I use a dropbox public file to host my config file. 
2. Supports multiple ads, weightage for every ad (how often an ad should be displayed) etc. 
3. Offline mode: Once ads are loaded, this works offline too. 
4. Plays well with other AdControls - so you get to push your own ads or display ads from a different ad provider or mix them up. 

How To/ Get Started: 
=====================
1. Check out SampleAdConfigFile and make one of your own 
2. Check out SampleImplementationOfCustomAdClient to get an idea of how it's used. 
This isn't complete, but just has the relevant snippets 

To get started:

Initialization: 
===============
You need the app name, the uri to wherever you are hosting your config file. 
CustomAdClient _adClient = new CustomAdClient(AppName, new Uri(@"https://dl-web.dropbox.com/get/Public/adServer2.txt?w=9fd5ede2", false));

Update: 
========
This checks to see if the cached ad config (if it exists) needs to be updated and updates it (Usually you should do it at app start, on a seperate thread).
Dispatcher.BeginInvoke(() =>
{
    _adClient.UpdateAdConfig();
}

Get an ad: 
==========
This will select an ad for you based on it's weightage or return null if nothing is selected.
AdImageUnit adImageUnit = _adClient.SelectAd(false);
You can set this to UI control (image)

              
