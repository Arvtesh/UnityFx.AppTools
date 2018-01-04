// Copyright (c) Alexander Bogarsukov.
// Licensed under the MIT license. See the LICENSE.md file in the project root for more information.

namespace UnityAppTools
{
	/// <summary>
	/// Defines constants used by the UnityAppTools classes.
	/// </summary>
	public static class Constants
	{
		#region common

		public const string KeyPrefix = "_UnityAppTools";

		#endregion

		#region PlayerPrefs keys

		public const string KeyDeviceId = KeyPrefix + ".App.DeviceId";
		public const string KeyFirstLaunch = KeyPrefix + "App.FirstLaunch";

		#endregion
	}
}
