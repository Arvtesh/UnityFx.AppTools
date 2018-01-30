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

		private sealed class InvokeResult : IAsyncResult
		{
			private readonly AsyncCallback _asyncCallback;
			private readonly object _asyncState;
			private EventWaitHandle _event;

			public object AsyncState { get { return _asyncState; } }
			public WaitHandle AsyncWaitHandle { get { return Utilities.TryCreateAsyncWaitHandle(ref _event, this); } }
			public bool CompletedSynchronously { get { return false; } }
			public bool IsCompleted { get; private set; }
			public Exception Exception { get; set; }

			public InvokeResult(AsyncCallback asyncCallback, object asyncState)
			{
				_asyncCallback = asyncCallback;
				_asyncState = asyncState;
				_event = new ManualResetEvent(false);
			}

			public void Join()
			{
				Utilities.TryCreateAsyncWaitHandle(ref _event, this);

				_event.WaitOne();
				_event.Close();
				_event = null;
			}

			public void SetCompleted()
			{
				IsCompleted = true;

				if (_event != null)
				{
					_event.Set();
				}

				if (_asyncCallback != null)
				{
					_asyncCallback(this);
				}
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

			return BeginInvokeInternal(d, asyncState);
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

			if (asyncResult is InvokeResult)
			{
				EndInvokeInternal(asyncResult as InvokeResult);
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
						var asyncResult = data.State as InvokeResult;

						if (asyncResult != null)
						{
							try
							{
								data.Action.Invoke(asyncResult.AsyncState);
							}
							catch (Exception e)
							{
								asyncResult.Exception = e;
							}

							asyncResult.SetCompleted();
						}
						else
						{
							try
							{
								data.Action.Invoke(data.State);
							}
							catch (Exception e)
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

			if (_mainThreadId == Thread.CurrentThread.ManagedThreadId)
			{
				d.Invoke(state);
			}
			else
			{
				var asyncResult = BeginInvokeInternal(d, state);
				EndInvokeInternal(asyncResult);
			}
		}

		/// <inheritdoc/>
		public void Post(SendOrPostCallback d, object state)
		{
			if (d == null)
			{
				throw new ArgumentNullException("d");
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

		private InvokeResult BeginInvokeInternal(SendOrPostCallback d, object asyncState)
		{
			var result = new InvokeResult(null, asyncState);
			PostInternal(d, result);
			return result;
		}

		private void EndInvokeInternal(InvokeResult asyncResult)
		{
			if (!asyncResult.IsCompleted)
			{
				asyncResult.Join();
			}

			if (asyncResult.Exception != null)
			{
				throw asyncResult.Exception;
			}
		}

		#endregion
	}
}
