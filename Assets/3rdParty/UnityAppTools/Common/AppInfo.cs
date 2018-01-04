// Copyright (c) Alexander Bogarsukov.
// Licensed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Threading.Tasks;
using UnityEngine;

namespace UnityAppTools
{
	/// <summary>
	/// 
	/// </summary>
	public sealed class AppInfo
	{
		#region data
		#endregion

		#region interface

		/// <summary>
		/// Returns unique device identifier for the application. Read only.
		/// </summary>
		/// <seealso cref="VendorId"/>
		/// <seealso cref="AdvertisingId"/>
		public string DeviceId { get; private set; }

		/// <summary>
		/// Returns vendor identifier for the application. Read only.
		/// </summary>
		/// <seealso cref="AdvertisingId"/>
		/// <seealso cref="DeviceId"/>
		public string VendorId { get; private set; }

		/// <summary>
		/// Returns advertising identifier for the application. Read only.
		/// </summary>
		/// <seealso cref="IsAdvertisingTrackingEnabled"/>
		/// <seealso cref="VendorId"/>
		/// <seealso cref="DeviceId"/>
		public string AdvertisingId { get; private set; }

		/// <summary>
		/// Returns <see langword="true"/> if <see cref="AdvertisingId"/> is available; <see langword="false"/> otherwise. Read only.
		/// </summary>
		/// <seealso cref="AdvertisingId"/>
		public bool IsAdvertisingTrackingEnabled { get; private set; }

		/// <summary>
		/// Returns <see langword="true"/> if it is the application is started for the first time; <see langword="false"/> otherwise. Read only.
		/// </summary>
		public bool IsFirstLaunch { get; private set; }

#if NET_4_6

		/// <summary>
		/// 
		/// </summary>
		public static Task<AppInfo> InitializeAsync()
		{
			var tcs = new TaskCompletionSource<AppInfo>();
			var result = new AppInfo();

			if (!Application.RequestAdvertisingIdentifierAsync((advertisingId, trackingEnabled, errorMsg) =>
			{
				if (string.IsNullOrEmpty(errorMsg))
				{
					result.AdvertisingId = advertisingId;
					result.IsAdvertisingTrackingEnabled = trackingEnabled;
					result.InitializeInternal(tcs);
				}
				else
				{
					tcs.SetException(new Exception(errorMsg));
				}
			}))
			{
				result.InitializeInternal(tcs);
			}

			return tcs.Task;
		}

#endif

		/// <summary>
		/// 
		/// </summary>
		public static AppInfo Initialize()
		{
			var result = new AppInfo();
			var advertisingId = string.Empty;

			if (TryGetAdvertisingId(out advertisingId))
			{
				result.AdvertisingId = advertisingId;
				result.IsAdvertisingTrackingEnabled = true;
			}

			result.VendorId = GetVendorId();
			result.DeviceId = GetDeviceId(advertisingId, result.VendorId);
			result.IsFirstLaunch = GetFirstLaunch();

			return result;
		}

		#endregion

		#region implementation

#if UNITY_IOS

		[System.Runtime.InteropServices.DllImport("__Internal")]
		private static extern string _GetKeychainValue(string key);

		[System.Runtime.InteropServices.DllImport("__Internal")]
		private static extern void _SetKeychainValue(string key, string value);

#endif

#if NET_4_6

			private void InitializeInternal(TaskCompletionSource<AppInfo> tcs)
		{
			try
			{
				VendorId = GetVendorId();
				DeviceId = GetDeviceId(AdvertisingId, VendorId);
				IsFirstLaunch = GetFirstLaunch();

				tcs.SetResult(this);
			}
			catch (Exception e)
			{
				tcs.SetException(e);
			}
		}

#endif

		private static bool GetFirstLaunch()
		{
			if (PlayerPrefs.HasKey(Constants.KeyFirstLaunch))
			{
				return false;
			}
			else
			{
				PlayerPrefs.SetInt(Constants.KeyFirstLaunch, 0);
				return true;
			}
		}

		private static string GetDeviceId(string advertisingId, string vendorId)
		{
			var result = GetKeychainValue(Constants.KeyDeviceId);

			if (string.IsNullOrEmpty(result))
			{
				if (PlayerPrefs.HasKey(Constants.KeyDeviceId))
				{
					result = PlayerPrefs.GetString(Constants.KeyDeviceId);

					if (string.IsNullOrEmpty(result))
					{
						result = GenerateDeviceId(advertisingId, vendorId);
					}
					else
					{
						SetKeychainValue(Constants.KeyDeviceId, result);
					}
				}
				else
				{
					result = GenerateDeviceId(advertisingId, vendorId);
				}
			}

			return result;
		}

		private static string GenerateDeviceId(string advertisingId, string vendorId)
		{
			string result = null;

			if (string.IsNullOrEmpty(advertisingId))
			{
				result = vendorId;
			}
			else
			{
				result = advertisingId;
			}

			if (string.IsNullOrEmpty(result))
			{
				result = Guid.NewGuid().ToString().Replace("-", string.Empty);
			}

			PlayerPrefs.SetString(Constants.KeyDeviceId, result);
			PlayerPrefs.Save();

			SetKeychainValue(Constants.KeyDeviceId, result);

			return result;
		}

		private static string GetKeychainValue(string key)
		{
#if UNITY_IOS && !UNITY_EDITOR
			return _GetKeychainValue(key);
#endif

			return null;
		}

		private static void SetKeychainValue(string key, string value)
		{
#if UNITY_IOS && !UNITY_EDITOR
			_SetKeychainValue(key, value);
#endif
		}

		private static string GetVendorId()
		{
#if UNITY_ANDROID && !UNITY_EDITOR

			// NOTE: getting the SystemInfo.deviceUniqueIdentifier on Android involves accessing device IMEI information,
			// which undesirably forces checking of the READ_PHONE_STATE permission in the Android manifest
			// http://forum.unity3d.com/threads/unique-identifier-details.353256/
			var up = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
			var currentActivity = up.GetStatic<AndroidJavaObject>("currentActivity");
			var contentResolver = currentActivity.Call<AndroidJavaObject>("getContentResolver");
			var secure = new AndroidJavaClass("android.provider.Settings$Secure");
			var androidId = secure.CallStatic<string>("getString", contentResolver, "android_id");

			return androidId;

#elif UNITY_IOS && !UNITY_EDITOR

			return UnityEngine.iOS.Device.vendorIdentifier;

#else

			return SystemInfo.deviceUniqueIdentifier;

#endif
		}

		private static bool TryGetAdvertisingId(out string advertisingId)
		{
#if UNITY_ANDROID && !UNITY_EDITOR

			// NOTE: getting advertising identifier on Android is a bit tricky
			// http://stackoverflow.com/questions/28179150/getting-the-google-advertising-id-and-limit-advertising
			var up = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
			var currentActivity = up.GetStatic<AndroidJavaObject>("currentActivity");
			var client = new AndroidJavaClass("com.google.android.gms.ads.identifier.AdvertisingIdClient");
			var adInfo = client.CallStatic<AndroidJavaObject>("getAdvertisingIdInfo", currentActivity);

			advertisingId = adInfo.Call<string>("getId").ToString();
			return adInfo.Call<bool>("isLimitAdTrackingEnabled");

#elif UNITY_IOS && !UNITY_EDITOR

			advertisingId = UnityEngine.iOS.Device.advertisingIdentifier;
			return UnityEngine.iOS.Device.advertisingTrackingEnabled;

#endif

			advertisingId = string.Empty;
			return false;
		}

		#endregion
	}
}
