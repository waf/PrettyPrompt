#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

// Intentionally trivial: the value of this project is the native AOT publish, which analyzes all of
// PrettyPrompt (rooted via TrimmerRootAssembly in the csproj) and its dependencies. See the csproj.
System.Console.WriteLine(typeof(PrettyPrompt.Prompt).Assembly.GetName().Name + " published with native AOT.");
