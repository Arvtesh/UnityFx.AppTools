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
	public class AsyncResult<T> : AsyncResult, IAsyncOperation<T>, IEnumerator
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

		private readonly string _name;

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
		public AsyncResult(string name)
		{
			_name = name;
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
		public AsyncResult(string name, AsyncCallback asyncCallback, object asyncState)
		{
			_name = name;
			_asyncCallback = asyncCallback;
			_asyncState = asyncState;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="AsyncResult"/> class.
		/// </summary>
		public AsyncResult(T result, bool completedSynchronously, AsyncCallback asyncCallback, object asyncState)
			: this(null, result, completedSynchronously, asyncCallback, asyncState)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="AsyncResult"/> class.
		/// </summary>
		public AsyncResult(string name, T result, bool completedSynchronously, AsyncCallback asyncCallback, object asyncState)
		{
			_name = name;
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
			: this(null, e, completedSynchronously, asyncCallback, asyncState)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="AsyncResult"/> class.
		/// </summary>
		public AsyncResult(string name, Exception e, bool completedSynchronously, AsyncCallback asyncCallback, object asyncState)
		{
			_name = name;
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
			ThrowIfDisposed();

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
		/// Blocks until the operation is completed. Uses <see cref="Thread.Sleep(int)"/> instead of <see cref="AsyncWaitHandle"/> for waiting.
		/// </summary>
		/// <returns>Result of the operation.</returns>
		public T JoinSleep(int millisecondsSleepTimeout)
		{
			ThrowIfDisposed();

			while (!IsCompleted)
			{
				Thread.Sleep(millisecondsSleepTimeout);
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
		/// Returns the operation name.
		/// </summary>
		/// <returns>Name of the operation.</returns>
		protected string GetOperationName()
		{
			return string.IsNullOrEmpty(_name) ? GetType().Name : _name;
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
		/// Called on each <see cref="MoveNext"/> invokation to update the operation state if needed.
		/// </summary>
		/// <seealso cref="OnCompleted"/>
		protected virtual void OnUpdate()
		{
		}

		/// <summary>
		/// Called when the operation is completed.
		/// </summary>
		/// <seealso cref="OnUpdate"/>
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

		/// <summary>
		/// Releases unmanaged resources used by the object.
		/// </summary>
		/// <param name="disposing">Should be <see langword="true"/> if the method is called from <see cref="Dispose()"/>; <see langword="false"/> otherwise.</param>
		/// <seealso cref="Dispose()"/>
		/// <seealso cref="ThrowIfDisposed"/>
		protected virtual void Dispose(bool disposing)
		{
			if (disposing && (_status & _statusDisposedFlag) == 0)
			{
				_status |= _statusDisposedFlag;
				_asyncCallback = null;

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
			if (!IsCompleted)
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
