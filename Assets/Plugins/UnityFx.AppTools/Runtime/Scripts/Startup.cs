// Copyright (c) Alexander Bogarsukov.
// Licensed under the MIT license. See the LICENSE.md file in the project root for more information.

#if !UNITY_2017_2_OR_NEWER
#error UnityFx.AppTools requires Unity 2017.2 or newer.
#endif

#if NET_LEGACY || NET_2_0 || NET_2_0_SUBSET
#error UnityFx.AppTools does not support .NET 3.5. Please set Scripting Runtime Version to .NET 4.x Equivalent in Unity Player Settings.
#endif
