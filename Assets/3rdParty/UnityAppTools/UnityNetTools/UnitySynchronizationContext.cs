// Copyright (c) Alexander Bogarsukov.
// Licensed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections;
using System.Diagnostics;
using System.Threading;

namespace UnityAppTools
{
	/// <summary>
	/// Implementation of <see cref="SynchronizationContext"/> for Unity.
	/// </summary>
	public sealed class UnitySynchronizationContext : SynchronizationContext
	{
		#region interface

		/// <summary>
		/// Initializes a new instance of the <see cref="AsyncResult"/> class.
		/// </summary>
		public UnitySynchronizationContext()
		{
		}

		#endregion

		#region TraceListener

		/// <inheritdoc/>
		public override void Post(SendOrPostCallback d, object state)
		{
			base.Post(d, state);
		}

		#endregion
	}
}
