// Copyright (c) Alexander Bogarsukov.
// Licensed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Threading;

namespace UnityAppTools
{
	/// <summary>
	/// A basic action scheduler.
	/// </summary>
	public interface IAsyncScheduler
	{
		/// <summary>
		/// 
		/// </summary>
		/// <param name="d"></param>
		/// <param name="state"></param>
		void Send(SendOrPostCallback d, object state);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="d"></param>
		/// <param name="state"></param>
		void Post(SendOrPostCallback d, object state);
	}
}
