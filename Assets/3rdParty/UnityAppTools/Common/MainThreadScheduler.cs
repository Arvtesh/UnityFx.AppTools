// Copyright (c) Alexander Bogarsukov.
// Licensed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using UnityEngine;

namespace UnityAppTools
{
	/// <summary>
	/// Main thread scheduler that can be used make sure the code is executed on the main thread.
	/// The implementation setups a <see cref="SynchronizationContext"/> and attaches it to the main thread.
	/// Do not use this on Unity 2017+ with .NET 4.6 profile (there is a native Unity <see cref="SynchronizationContext"/>
	/// attached to the main thread already).
	/// </summary>
	/// <seealso cref="UnitySynchronizationContext"/>
	public sealed class MainThreadScheduler : MonoBehaviour, IAsyncScheduler
	{
		#region data

		private struct ActionData
		{
			public SendOrPostCallback Action;
			public object State;
		}

		private UnitySynchronizationContext _context;
		private int _mainThreadId;
		private Queue<ActionData> _actionQueue = new Queue<ActionData>();

		#endregion

		#region interface

		/// <summary>
		/// 
		/// </summary>
		/// <param name="d"></param>
		/// <param name="asyncState"></param>
		/// <returns></returns>
		public IAsyncResult BeginInvoke(SendOrPostCallback d, object asyncState)
		{
			if (d == null)
			{
				throw new ArgumentNullException("d");
			}

			throw new NotImplementedException();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="asyncResult"></param>
		public void EndInvoke(IAsyncResult asyncResult)
		{
			if (asyncResult == null)
			{
				throw new ArgumentNullException("asyncResult");
			}

			throw new NotImplementedException();
		}

		#endregion

		#region MonoBehaviour

		private void Awake()
		{
			var currentContext = SynchronizationContext.Current;

			if (currentContext != null)
			{
				throw new InvalidOperationException("SynchronizationContext instance is already set.");
			}

			var context = new UnitySynchronizationContext(this);
			SynchronizationContext.SetSynchronizationContext(context);

			_context = context;
			_mainThreadId = Thread.CurrentThread.ManagedThreadId;
		}

		private void OnDestroy()
		{
			if (_context == SynchronizationContext.Current)
			{
				SynchronizationContext.SetSynchronizationContext(null);
			}

			_context = null;
		}

		private void Update()
		{
			if (_actionQueue.Count > 0)
			{
				lock (_actionQueue)
				{
					while (_actionQueue.Count > 0)
					{
						var data = _actionQueue.Dequeue();
						data.Action.Invoke(data.State);
					}
				}
			}
		}

		#endregion

		#region IAsyncScheduler

		/// <inheritdoc/>
		public void Send(SendOrPostCallback d, object state)
		{
			if (d == null)
			{
				throw new ArgumentNullException("d");
			}

			if (_mainThreadId == Thread.CurrentThread.ManagedThreadId)
			{
				d.Invoke(state);
			}
			else
			{
				var asyncResult = BeginInvoke(d, state);
				EndInvoke(asyncResult);
			}
		}

		/// <inheritdoc/>
		public void Post(SendOrPostCallback d, object state)
		{
			if (d == null)
			{
				throw new ArgumentNullException("d");
			}

			lock (_actionQueue)
			{
				_actionQueue.Enqueue(new ActionData() { Action = d, State = state });
			}
		}

		#endregion

		#region implementation
		#endregion
	}
}
