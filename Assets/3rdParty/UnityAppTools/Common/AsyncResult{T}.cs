// Copyright (c) Alexander Bogarsukov.
// Licensed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections;
using System.Diagnostics;
#if NET_4_6
using System.Runtime.ExceptionServices;
#endif
using System.Threading;

namespace UnityAppTools
{
	/// <summary>
	/// A yieldable asynchronous operation.
	/// </summary>
	[DebuggerDisplay("{DebuggerDisplay,nq}")]
	public class AsyncResult<T> : AsyncResult, IAsyncOperation<T>, IEnumerator
	{
		#region data

		private const string _strOperationCompleted = "The operation is already completed.";

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
			: base(name)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="AsyncResult"/> class.
		/// </summary>
		public AsyncResult(AsyncCallback asyncCallback, object asyncState)
			: base(asyncCallback, asyncState)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="AsyncResult"/> class.
		/// </summary>
		public AsyncResult(string name, AsyncCallback asyncCallback, object asyncState)
			: base(name, asyncCallback, asyncState)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="AsyncResult"/> class.
		/// </summary>
		public AsyncResult(T result, bool completedSynchronously, AsyncCallback asyncCallback, object asyncState)
			: base(null, _statusCompletedSuccessfullyFlag, completedSynchronously, asyncCallback, asyncState)
		{
			_result = result;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="AsyncResult"/> class.
		/// </summary>
		public AsyncResult(string name, T result, bool completedSynchronously, AsyncCallback asyncCallback, object asyncState)
			: base(name, _statusCompletedSuccessfullyFlag, completedSynchronously, asyncCallback, asyncState)
		{
			_result = result;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="AsyncResult"/> class.
		/// </summary>
		public AsyncResult(Exception e, bool completedSynchronously, AsyncCallback asyncCallback, object asyncState)
			: base(null, _statusFaultedFlag, completedSynchronously, asyncCallback, asyncState)
		{
			_exception = e;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="AsyncResult"/> class.
		/// </summary>
		public AsyncResult(string name, Exception e, bool completedSynchronously, AsyncCallback asyncCallback, object asyncState)
			: base(name, _statusFaultedFlag, completedSynchronously, asyncCallback, asyncState)
		{
			_exception = e;
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

			ThrowIfFaulted();
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

			ThrowIfFaulted();
			return _result;
		}

		/// <summary>
		/// Throws an exception stored in the operatino (if any).
		/// </summary>
		public void ThrowIfFaulted()
		{
			if (_exception != null)
			{
#if NET_4_6
				ExceptionDispatchInfo.Capture(_exception).Throw();
#else
				throw _exception;
#endif
			}
		}

		/// <summary>
		/// Called on each <see cref="MoveNext"/> invokation to update the operation state if needed.
		/// </summary>
		/// <seealso cref="OnCompleted"/>
		protected virtual void OnUpdate()
		{
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
			if (TrySetStatus(_statusCompletedSuccessfullyFlag, completedSynchronously))
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
		public T Result
		{
			get
			{
				if ((GetOperationStatus() & _statusCompletedSuccessfullyFlag) == 0)
				{
					throw new InvalidOperationException("The operation result is not available.", _exception);
				}

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
				return (GetOperationStatus() & _statusCompletedSuccessfullyFlag) != 0;
			}
		}

		/// <inheritdoc/>
		public bool IsFaulted
		{
			get
			{
				return GetOperationStatus() >= _statusFaultedFlag;
			}
		}

		/// <inheritdoc/>
		public bool IsCanceled
		{
			get
			{
				return (GetOperationStatus() & _statusCanceledFlag) != 0;
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
			if (IsCompleted)
			{
				return false;
			}
			else
			{
				try
				{
					OnUpdate();
				}
				catch (Exception e)
				{
					TrySetException(e);
				}

				return !IsCompleted;
			}
		}

		/// <inheritdoc/>
		public void Reset()
		{
			throw new NotSupportedException();
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

				if ((status & _statusCompletedSuccessfullyFlag) != 0)
				{
					state = "Completed";
				}
				else if ((status & _statusFaultedFlag) != 0)
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
				else if ((status & _statusCanceledFlag) != 0)
				{
					state = "Canceled";
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
