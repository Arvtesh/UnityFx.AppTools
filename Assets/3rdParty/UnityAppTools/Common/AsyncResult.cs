// Copyright (c) Alexander Bogarsukov.
// Licensed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Diagnostics;
using System.Threading;

namespace UnityAppTools
{
	/// <summary>
	/// Defines <see cref="IAsyncResult"/> related helpers.
	/// </summary>
	/// <seealso href="https://blogs.msdn.microsoft.com/nikos/2011/03/14/how-to-implement-the-iasyncresult-design-pattern/"/>
	[DebuggerDisplay("{DebuggerDisplay,nq}")]
	public abstract class AsyncResult : IAsyncResult, IDisposable
	{
		#region data

		private const int _statusRunning = 0;
		private const int _statusDisposedFlag = 1;
		private const int _statusSynchronousFlag = 2;
		private const int _statusCompletedFlag = 4;
		private const int _statusFaultedFlag = 8;
		private const int _statusCanceledFlag = 16;
		private const int _completedMask = _statusCompletedFlag | _statusFaultedFlag | _statusCanceledFlag;

		private readonly string _name;
		private readonly AsyncCallback _asyncCallback;
		private readonly object _asyncState;

		private Exception _exception;
		private EventWaitHandle _waitHandle;
		private int _status;

		#endregion

		#region interface

		/// <summary>
		/// Initializes a new instance of the <see cref="AsyncResult"/> class.
		/// </summary>
		protected AsyncResult()
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="AsyncResult"/> class.
		/// </summary>
		protected AsyncResult(string name)
		{
			_name = name;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="AsyncResult"/> class.
		/// </summary>
		protected AsyncResult(AsyncCallback asyncCallback, object asyncState)
		{
			_asyncCallback = asyncCallback;
			_asyncState = asyncState;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="AsyncResult"/> class.
		/// </summary>
		protected AsyncResult(string name, AsyncCallback asyncCallback, object asyncState)
		{
			_name = name;
			_asyncCallback = asyncCallback;
			_asyncState = asyncState;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="AsyncResult"/> class.
		/// </summary>
		protected AsyncResult(string name, int status, bool completedSynchronously, AsyncCallback asyncCallback, object asyncState)
		{
			Debug.Assert(status > _statusRunning);

			_name = name;
			_asyncCallback = asyncCallback;
			_asyncState = asyncState;
			_status = status | _statusCompletedFlag;
			_asyncState = asyncState;

			if (completedSynchronously)
			{
				_status |= _statusSynchronousFlag;
			}

			if (asyncCallback != null)
			{
				asyncCallback.Invoke(this);
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="newStatus"></param>
		/// <param name="completedSynchronously"></param>
		/// <returns></returns>
		protected bool TrySetStatus(int newStatus, bool completedSynchronously)
		{
			if (_status == _statusRunning)
			{
				if (completedSynchronously)
				{
					newStatus |= _statusCompletedFlag | _statusSynchronousFlag;
				}
				else
				{
					newStatus |= _statusCompletedFlag;
				}

				return Interlocked.CompareExchange(ref _status, newStatus, _statusRunning) == _statusRunning;
			}

			return false;
		}

		/// <summary>
		/// Throws <see cref="ObjectDisposedException"/> if the operation has been disposed.
		/// </summary>
		protected void ThrowIfDisposed()
		{
			if ((_status & _statusDisposedFlag) != 0)
			{
				throw new ObjectDisposedException(GetOperationName());
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		protected int GetOperationStatus()
		{
			return _status;
		}

		/// <summary>
		/// Returns the operation name.
		/// </summary>
		/// <returns>Name of the operation.</returns>
		protected string GetOperationName()
		{
			return string.IsNullOrEmpty(_name) ? GetType().Name : _name;
		}

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

		#endregion

		#region virtual interface

		/// <summary>
		/// Called when the operation is completed.
		/// </summary>
		protected virtual void OnCompleted()
		{
			if (_waitHandle != null)
			{
				_waitHandle.Set();
			}

			if (_asyncCallback != null)
			{
				_asyncCallback.Invoke(this);
			}
		}

		/// <summary>
		/// Releases unmanaged resources used by the object.
		/// </summary>
		/// <param name="disposing">Should be <see langword="true"/> if the method is called from <see cref="Dispose()"/>; <see langword="false"/> otherwise.</param>
		/// <seealso cref="Dispose()"/>
		/// <seealso cref="ThrowIfDisposed"/>
		protected virtual void Dispose(bool disposing)
		{
			if (disposing)
			{
				_status |= _statusDisposedFlag;

				if (_waitHandle != null)
				{
					_waitHandle.Close();
					_waitHandle = null;
				}
			}
		}

		#endregion

		#region IAsyncOperationController

		/// <summary>
		/// 
		/// </summary>
		/// <param name="e"></param>
		/// <param name="completedSynchronously"></param>
		/// <seealso cref="TrySetException(Exception, bool)"/>
		public void SetException(Exception e, bool completedSynchronously = false)
		{
			if (!TrySetException(e, completedSynchronously))
			{
				throw new InvalidOperationException(_strOperationCompleted);
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="e"></param>
		/// <param name="completedSynchronously"></param>
		/// <returns></returns>
		/// <seealso cref="SetException(Exception, bool)"/>
		public bool TrySetException(Exception e, bool completedSynchronously = false)
		{
			var status = e is OperationCanceledException ? _statusCanceledFlag : _statusFaultedFlag;

			if (TrySetStatus(status, completedSynchronously))
			{
				_exception = e;
				OnCompleted();
				return true;
			}

			return false;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="completedSynchronously"></param>
		/// <seealso cref="TrySetCanceled(bool)"/>
		public void SetCanceled(bool completedSynchronously = false)
		{
			if (!TrySetCanceled(completedSynchronously))
			{
				throw new InvalidOperationException(_strOperationCompleted);
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="completedSynchronously"></param>
		/// <returns></returns>
		/// <seealso cref="SetCanceled(bool)"/>
		public bool TrySetCanceled(bool completedSynchronously = false)
		{
			if (TrySetStatus(_statusCanceledFlag, completedSynchronously))
			{
				OnCompleted();
				return true;
			}

			return false;
		}

		#endregion

		#region IAsyncOperation

		/// <inheritdoc/>
		public Exception Exception
		{
			get
			{
				return _exception;
			}
		}

		/// <inheritdoc/>
		public bool IsCompletedSuccessfully
		{
			get
			{
				return _status < _statusFaultedFlag;
			}
		}

		/// <inheritdoc/>
		public bool IsFaulted
		{
			get
			{
				return _status >= _statusFaultedFlag;
			}
		}

		/// <inheritdoc/>
		public bool IsCanceled
		{
			get
			{
				return (_status & _statusCanceledFlag) != 0;
			}
		}

		#endregion

		#region IAsyncResult

		/// <inheritdoc/>
		public WaitHandle AsyncWaitHandle
		{
			get
			{
				ThrowIfDisposed();
				return TryCreateAsyncWaitHandle(ref _waitHandle, this);
			}
		}

		/// <inheritdoc/>
		public object AsyncState
		{
			get
			{
				return _asyncState;
			}
		}

		/// <inheritdoc/>
		public bool CompletedSynchronously
		{
			get
			{
				return (_status & _statusSynchronousFlag) != 0;
			}
		}

		/// <inheritdoc/>
		public bool IsCompleted
		{
			get
			{
				return _status > _statusRunning;
			}
		}

		#endregion

		#region IDisposable

		/// <inheritdoc/>
		public void Dispose()
		{
			if (_status == _statusRunning)
			{
				throw new InvalidOperationException("Cannot dispose non-completed operation.");
			}

			Dispose(true);
			GC.SuppressFinalize(this);
		}

		#endregion

		#region implementation

		private string DebuggerDisplay
		{
			get
			{
				var result = GetOperationName();
				var state = "Running";
				var status = GetOperationStatus();

				if (IsCompletedSuccessfully)
				{
					state = "Completed";
				}
				else if (IsCanceled)
				{
					state = "Canceled";
				}
				else if (IsFaulted)
				{
					if (_exception != null)
					{
						state = "Faulted (" + _exception.GetType().Name + ')';
					}
					else
					{
						state = "Faulted";
					}
				}

				result += ", State = ";
				result += state;

				if ((status & _statusDisposedFlag) != 0)
				{
					result += ", Disposed";
				}

				return result;
			}
		}

		#endregion
	}
}
