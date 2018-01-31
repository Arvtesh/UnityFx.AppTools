// Copyright (c) Alexander Bogarsukov.
// Licensed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Threading;

namespace UnityAppTools
{
	/// <summary>
	/// Implementation of <see cref="SynchronizationContext"/> for Unity. This class is a helper for <see cref="MainThreadScheduler"/>;
	/// do not use unless absolutely nessesary.
	/// </summary>
	/// <seealso cref="IAsyncScheduler"/>
	public sealed class UnitySynchronizationContext : SynchronizationContext
	{
		#region data

		private IAsyncScheduler _scheduler;

		#endregion

		#region interface

		/// <summary>
		/// Initializes a new instance of the <see cref="AsyncResult"/> class.
		/// </summary>
		public UnitySynchronizationContext(IAsyncScheduler scheduler)
		{
			if (scheduler == null)
			{
				throw new ArgumentNullException("scheduler");
			}

			_scheduler = scheduler;
		}

		#endregion

		#region SynchronizationContext

		/// <inheritdoc/>
		public override SynchronizationContext CreateCopy()
		{
			return new UnitySynchronizationContext(_scheduler);
		}

		/// <inheritdoc/>
		public override void Send(SendOrPostCallback d, object state)
		{
			_scheduler.Send(d, state);
		}

		/// <inheritdoc/>
		public override void Post(SendOrPostCallback d, object state)
		{
			_scheduler.Post(d, state);
		}

		#endregion

		#region implementation
		#endregion
	}
}
