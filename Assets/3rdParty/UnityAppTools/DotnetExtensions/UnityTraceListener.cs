// Copyright (c) Alexander Bogarsukov.
// Licensed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections;
using System.Diagnostics;
using System.Threading;

namespace UnityAppTools
{
	/// <summary>
	/// Implementation of <see cref="TraceListener"/> for Unity.
	/// </summary>
	public sealed class UnityTraceListener : TraceListener
	{
		#region interface
		#endregion

		#region TraceListener

		/// <inheritdoc/>
		public override void Write(string message)
		{
			throw new NotImplementedException();
		}

		/// <inheritdoc/>
		public override void WriteLine(string message)
		{
			throw new NotImplementedException();
		}

		#endregion
	}
}
