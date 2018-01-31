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
	/// The implementation setups a <see cref="SynchronizationContext"/> (if there is none) and
	/// attaches it to the main thread.
	/// </summary>
	/// <seealso cref="UnitySynchronizationContext"/>
	public sealed class MainThreadScheduler : MonoBehaviour, IAsyncScheduler
	{
		#region data

		private sealed class InvokeResult : AsyncResult
		{
			public InvokeResult(AsyncCallback asyncCallback, object asyncState)
				: base(asyncCallback, asyncState)
			{
			}
		}

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
		/// Initiates execution of a delegate on the main thread.
		/// </summary>
		/// <param name="d">The delegate to execute.</param>
		/// <param name="asyncState">The delegate arguments.</param>
		/// <exception cref="ArgumentNullException">Thrown if <paramref name="d"/> is <see langword="null"/>.</exception>
		/// <seealso cref="EndInvoke(IAsyncResult)"/>
		public IAsyncResult BeginInvoke(SendOrPostCallback d, object asyncState)
		{
			if (d == null)
			{
				throw new ArgumentNullException("d");
			}

			if (!this)
			{
				throw new InvalidOperationException();
			}

			var asyncResult = new InvokeResult(null, asyncState);
			PostInternal(d, asyncResult);
			return asyncResult;
		}

		/// <summary>
		/// Blocks calling thread until the specified operation is completed.
		/// </summary>
		/// <param name="asyncResult">The asynchronous operation to wait for.</param>
		/// <exception cref="ArgumentNullException">Thrown if <paramref name="asyncResult"/> is <see langword="null"/>.</exception>
		/// <exception cref="InvalidOperationException">Thrown if <paramref name="asyncResult"/> is not created via <see cref="BeginInvoke(SendOrPostCallback, object)"/> call.</exception>
		/// <seealso cref="BeginInvoke(SendOrPostCallback, object)"/>
		public void EndInvoke(IAsyncResult asyncResult)
		{
			if (asyncResult == null)
			{
				throw new ArgumentNullException("asyncResult");
			}

			if (!this)
			{
				throw new InvalidOperationException();
			}

			if (_mainThreadId == Thread.CurrentThread.ManagedThreadId)
			{
				throw new InvalidOperationException("Calling EndInvoke() from the main thread will likely cause a deadlock.");
			}

			if (asyncResult is InvokeResult)
			{
				using (var op = asyncResult as InvokeResult)
				{
					op.Join();
				}
			}
			else
			{
				throw new InvalidOperationException("Invalid operation instance. Only operations created by BeginInvoke() can be used.");
			}
		}

		#endregion

		#region MonoBehaviour

		private void Awake()
		{
			var currentContext = SynchronizationContext.Current;

			if (currentContext == null)
			{
				var context = new UnitySynchronizationContext(this);
				SynchronizationContext.SetSynchronizationContext(context);
				_context = context;
			}

			_mainThreadId = Thread.CurrentThread.ManagedThreadId;
		}

		private void OnDestroy()
		{
			if (_context != null && _context == SynchronizationContext.Current)
			{
				SynchronizationContext.SetSynchronizationContext(null);
			}

			lock (_actionQueue)
			{
				_actionQueue.Clear();
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
						var asyncResult = data.State as InvokeResult;

						if (asyncResult != null)
						{
							try
							{
								data.Action.Invoke(asyncResult.AsyncState);
								asyncResult.SetCompleted();
							}
							catch (Exception e)
							{
								asyncResult.TrySetException(e);
							}
						}
						else
						{
							try
							{
								data.Action.Invoke(data.State);
							}
							catch (Exception)
							{
								// TODO
							}
						}
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

			if (!this)
			{
				throw new InvalidOperationException();
			}

			if (_mainThreadId == Thread.CurrentThread.ManagedThreadId)
			{
				d.Invoke(state);
			}
			else
			{
				var asyncResult = new InvokeResult(null, state);
				PostInternal(d, asyncResult);
				asyncResult.JoinSleep(0);
			}
		}

		/// <inheritdoc/>
		public void Post(SendOrPostCallback d, object state)
		{
			if (d == null)
			{
				throw new ArgumentNullException("d");
			}

			if (!this)
			{
				throw new InvalidOperationException();
			}

			PostInternal(d, state);
		}

		#endregion

		#region implementation

		private void PostInternal(SendOrPostCallback d, object asyncState)
		{
			lock (_actionQueue)
			{
				_actionQueue.Enqueue(new ActionData() { Action = d, State = asyncState });
			}
		}

		#endregion
	}
}
