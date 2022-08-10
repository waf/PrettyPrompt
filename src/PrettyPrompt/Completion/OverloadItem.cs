#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using PrettyPrompt.Highlighting;

namespace PrettyPrompt.Completion;

/// <summary>
/// An overload item in the Overload Menu Pane.
/// </summary>
[DebuggerDisplay("{Signature}")]
public class OverloadItem
{
    public FormattedString Signature { get; }
    public FormattedString Summary { get; }
    public FormattedString Return { get; }
    public IReadOnlyList<Parameter> Parameters { get; }

    public OverloadItem(FormattedString signature, FormattedString summary, FormattedString returnDescription, IReadOnlyList<Parameter> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        Signature = signature;
        Summary = summary;
        Return = returnDescription;
        Parameters = parameters;
    }

    [DebuggerDisplay("{Name}: {Description}")]
    public readonly struct Parameter
    {
        public readonly string Name;
        public readonly FormattedString Description;

        public Parameter(string name, FormattedString description)
        {
            ArgumentNullException.ThrowIfNull(name);

            Name = name;
            Description = description;
        }
    }
}