// Copyright (c) Alexander Bogarsukov.
// Licensed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
#if NET_4_6
using System.Runtime.ExceptionServices;
#endif
using System.Threading;

namespace UnityAppTools
{
	/// <summary>
	/// A yieldable asynchronous operation.
	/// </summary>
	/// <seealso href="https://blogs.msdn.microsoft.com/nikos/2011/03/14/how-to-implement-the-iasyncresult-design-pattern/"/>
	[DebuggerDisplay("{DebuggerDisplay,nq}")]
	public class AsyncResult<T> : IAsyncOperation<T>, IAsyncResult, IEnumerator, IDisposable
	{
		#region data

		private const int _statusDisposedFlag = 1;
		private const int _statusSynchronousFlag = 2;
		private const int _statusRunning = 0;
		private const int _statusCompleted = 4;
		private const int _statusFaulted = 8;
		private const int _statusCanceled = 16;
		private const int _typeMask = 0x3;

		private const string _strOperationCompleted = "The operation is already completed.";

		private AsyncCallback _asyncCallback;
		private object _asyncState;
		private EventWaitHandle _waitHandle;
		private Exception _exception;
		private int _status;
		private T _result;

		#endregion

		#region interface

		/// <summary>
		/// Initializes a new instance of the <see cref="AsyncResult"/> class.
		/// </summary>
		public AsyncResult()
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="AsyncResult"/> class.
		/// </summary>
		public AsyncResult(AsyncCallback asyncCallback, object asyncState)
		{
			_asyncCallback = asyncCallback;
			_asyncState = asyncState;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="AsyncResult"/> class.
		/// </summary>
		public AsyncResult(T result, bool completedSynchronously, AsyncCallback asyncCallback, object asyncState)
		{
			_result = result;
			_status = _statusCompleted;
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
		/// Initializes a new instance of the <see cref="AsyncResult"/> class.
		/// </summary>
		public AsyncResult(Exception e, bool completedSynchronously, AsyncCallback asyncCallback, object asyncState)
		{
			_exception = e;
			_status = _statusFaulted;
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
		/// Blocks until the operation is completed.
		/// </summary>
		/// <returns>Result of the operation.</returns>
		public T Join()
		{
			if (!IsCompleted)
			{
				AsyncWaitHandle.WaitOne();
			}

			if (_exception != null)
			{
#if NET_4_6
				ExceptionDispatchInfo.Capture(_exception).Throw();
#else
				throw _exception;
#endif
			}

			return _result;
		}

		/// <summary>
		/// Throws <see cref="InvalidOperationException"/> if the operation is not completed without errors.
		/// </summary>
		protected void ThrowIfNotCompletedSuccessfully()
		{
			if ((_status & _statusCompleted) == 0)
			{
				throw new InvalidOperationException("The operation result is not available.", _exception);
			}
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
		/// Returns the operation name.
		/// </summary>
		/// <returns>Name of the operation.</returns>
		protected virtual string GetOperationName()
		{
			return "AsyncResult";
		}

		/// <summary>
		/// Called on each <see cref="MoveNext"/> invokation to update the operation state if needed.
		/// </summary>
		protected virtual void OnUpdate()
		{
		}

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
				_asyncCallback = null;
			}
		}

		#endregion

		#region IAsyncOperationController

		/// <summary>
		/// 
		/// </summary>
		/// <param name="result"></param>
		/// <param name="completedSynchronously"></param>
		/// <seealso cref="TrySetResult(T, bool)"/>
		public void SetResult(T result, bool completedSynchronously = false)
		{
			if (!TrySetResult(result, completedSynchronously))
			{
				throw new InvalidOperationException(_strOperationCompleted);
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="result"></param>
		/// <param name="completedSynchronously"></param>
		/// <returns></returns>
		/// <seealso cref="SetResult(T, bool)"/>
		public bool TrySetResult(T result, bool completedSynchronously = false)
		{
			if (TrySetStatus(_statusCompleted, completedSynchronously))
			{
				_result = result;
				OnCompleted();
				return true;
			}

			return false;
		}

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
			var status = e is OperationCanceledException ? _statusCanceled : _statusFaulted;

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
			if (TrySetStatus(_statusCanceled, completedSynchronously))
			{
				OnCompleted();
				return true;
			}

			return false;
		}

		#endregion

		#region IAsyncOperation

		/// <inheritdoc/>
		public T Result
		{
			get
			{
				ThrowIfDisposed();
				ThrowIfNotCompletedSuccessfully();
				return _result;
			}
		}

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
				return (_status & _statusCompleted) != 0;
			}
		}

		/// <inheritdoc/>
		public bool IsFaulted
		{
			get
			{
				return _status >= _statusFaulted;
			}
		}

		/// <inheritdoc/>
		public bool IsCanceled
		{
			get
			{
				return (_status & _statusCanceled) != 0;
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

				if (_waitHandle == null)
				{
					var done = IsCompleted;
					var mre = new ManualResetEvent(done);

					if (Interlocked.CompareExchange(ref _waitHandle, mre, null) != null)
					{
						// Another thread created this object's event; dispose the event we just created.
						mre.Close();
					}
					else if (!done && IsCompleted)
					{
						// We published the event as unset, but the operation has subsequently completed;
						// set the event state properly so that callers do not deadlock.
						_waitHandle.Set();
					}
				}

				return _waitHandle;
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

		#region IEnumerator

		/// <inheritdoc/>
		public object Current
		{
			get
			{
				return null;
			}
		}

		/// <inheritdoc/>
		public bool MoveNext()
		{
			try
			{
				OnUpdate();
			}
			catch (Exception e)
			{
				TrySetException(e);
			}

			return _status == _statusRunning;
		}

		/// <inheritdoc/>
		public void Reset()
		{
			throw new NotSupportedException();
		}

		#endregion

		#region IDisposable

		/// <inheritdoc/>
		public void Dispose()
		{
			if ((_status & _statusDisposedFlag) == 0)
			{
				if (!IsCompleted)
				{
					throw new InvalidOperationException("Cannot dispose non-completed operation.");
				}

				_status |= _statusDisposedFlag;
				_asyncCallback = null;
				_asyncState = null;
				_exception = null;

				if (_waitHandle != null)
				{
					_waitHandle.Close();
					_waitHandle = null;
				}
			}
		}

		#endregion

		#region implementation

		private string DebuggerDisplay
		{
			get
			{
				var result = GetOperationName();
				var state = "Running";

				if ((_status & _statusCompleted) != 0)
				{
					state = "Completed";
				}
				else if ((_status & _statusFaulted) != 0)
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
				else if ((_status & _statusCanceled) != 0)
				{
					state = "Canceled";
				}

				result += ", State = ";
				result += state;

				if ((_status & _statusDisposedFlag) != 0)
				{
					result += ", Disposed";
				}

				return result;
			}
		}

		private bool TrySetStatus(int newStatus, bool completedSynchronously)
		{
			if (_status < _statusCompleted)
			{
				if (completedSynchronously)
				{
					newStatus |= _statusSynchronousFlag;
				}

				return Interlocked.CompareExchange(ref _status, newStatus, _statusRunning) == _statusRunning;
			}

			return false;
		}

		#endregion
	}
}
