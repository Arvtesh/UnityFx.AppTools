// Copyright (c) Alexander Bogarsukov.
// Licensed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Diagnostics;
using System.Threading;

namespace UnityAppTools
{
	/// <summary>
	/// 
	/// </summary>
	public static class Utilities
	{
		/// <summary>
		/// 
		/// </summary>
		/// <param name="waitHandle"></param>
		/// <param name="asyncResult"></param>
		/// <returns></returns>
		public static EventWaitHandle TryCreateAsyncWaitHandle(ref EventWaitHandle waitHandle, IAsyncResult asyncResult)
		{
			if (waitHandle == null)
			{
				var done = asyncResult.IsCompleted;
				var mre = new ManualResetEvent(done);

				if (Interlocked.CompareExchange(ref waitHandle, mre, null) != null)
				{
					// Another thread created this object's event; dispose the event we just created.
					mre.Close();
				}
				else if (!done && asyncResult.IsCompleted)
				{
					// We published the event as unset, but the operation has subsequently completed;
					// set the event state properly so that callers do not deadlock.
					waitHandle.Set();
				}
			}

			return waitHandle;
		}
	}
}
